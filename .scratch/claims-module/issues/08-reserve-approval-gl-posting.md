# 08 — Reserve approval + GL posting

**What to build:** the approval workflow for pending reserves and the simulated general-ledger posting that follows approval — including the authority, self-approval, and aggregate-cap rules, and the idempotent background job.

**Blocked by:** 07

**Status:** done

- [x] Approving a reserve requires Supervisor role for the $10k–$100k tier and Manager role above $100k, enforced server-side.
- [x] A user cannot approve a reserve they submitted themselves — the attempt is rejected with a clear error.
- [x] Rejecting a reserve requires a reason, and leaves the component available for resubmission per Ticket 07.
- [x] If approving a reserve would push the claim's total approved reserves over $10,000,000, approval is blocked unless a Manager has set an explicit override flag on the claim.
- [x] Approval enqueues a GL-posting background job carrying a deterministic idempotency key derived from the reserve and its change sequence.
- [x] Running the GL-posting job twice for the same key produces exactly one audit log entry and one posting-status update — verified by a test that deliberately invokes it twice.
- [x] The Reserves tab shows approve/reject actions only to users with Supervisor or Manager role, and reflects posting status once the job completes.

## Comments

**Backend**: `POST /api/claims/{id}/reserves/{reserveHistoryId}/approve` and `.../reject`
(`ClaimsController.ApproveReserve`/`RejectReserve`) → `ApproveReserveTransactionCommand`/`RejectReserveTransactionCommand`
and their handlers — ADR-0001's repository path, same 404-vs-422 split as `RetractReserveTransactionCommandHandler`.
Role enforcement is two-layered per ADR-0004: `[Authorize(Roles = "Supervisor,Manager")]` on both endpoints blocks
Handlers outright (403); the amount-dependent Supervisor-vs-Manager split (BR-R-02) and the aggregate-cap override
(BR-R-05) are both enforced inside `Claim.ApproveReserveTransaction` instead, since neither can be expressed as a
fixed role set on the endpoint — the same reasoning Ticket 06 used for status-transition role checks. The
authority-tier/self-approval/aggregate-cap branching is covered by 13 new tests in the narrow `Claim`-only seam
(`ClaimReserveTests.cs`); HTTP-seam tests (`ReservesTests.cs`) cover the endpoint/role/audit/GL-posting wiring.

**GL posting** (`PostGlReserveChangeJob`, FRS §12.1): Hangfire-agnostic by design, same rationale as
`SlaBreachScanJob` — only `IApplicationDbContext`/`IDateTime`, so `PostGlReserveChangeJobTests.cs` invokes
`ExecuteAsync` directly for its re-entrancy assertions rather than waiting on Hangfire's scheduler. Re-entrancy
checks `ReserveHistory.PostingStatus` before writing anything (a row already `Posted` is a no-op); the
`IdempotencyKey`'s own unique index (ADR-0007, already in place from Ticket 07) remains the safety net against
two *different* history rows ever colliding on the same key, a distinct concern from this job's own re-run safety.

New seam: `IBackgroundJobScheduler` (`ClaimsModule.Application/Common/Interfaces`) wraps Hangfire's
`BackgroundJob.Enqueue` behind an Application-owned interface, implemented against the real
`IBackgroundJobClient` in `ClaimsModule.Infrastructure` (`HangfireBackgroundJobScheduler`) — kept out of
Application so command handlers (and their tests) don't take a direct Hangfire dependency. Both
`SubmitReserveTransactionCommandHandler` (auto-approval) and `ApproveReserveTransactionCommandHandler` (manual
approval) enqueue through it immediately on their respective approvals, then stamp the returned job id onto
`ReserveHistory.PostingJobId` in a second small `SaveChangesAsync` — the one place that actually has Hangfire's
assigned id, without `PostGlReserveChangeJob` itself needing a Hangfire-typed `PerformContext` parameter just to
read its own id back (which would also break its direct-invocation testability).

Two new domain events/audit types: `ReserveApprovedEvent`/`RESERVE_APPROVED`, `ReserveRejectedEvent`/`RESERVE_REJECTED`,
plus `GL_POSTING_SIMULATED` (written by the job itself, not a domain event — it's not a `Claim`-aggregate mutation).
New `Claim.AggregateCapOverrideApproved` (migration `AddAggregateCapOverrideToClaim`) persists a Manager's override
once set — BR-R-05 reads as a standing flag on the claim, not a one-shot parameter on a single approval call.

**Frontend**: Approve/Reject actions on each `PendingApproval` history row, gated by `hasRoleAuthority(role,
'Supervisor')` (`canReject`) and additionally excluding the original submitter for Approve (`canApprove`) — both
also server-enforced, so this is UX-only, never the actual authorization boundary. Reject reuses
`ConfirmDialogComponent`'s `requireReason` mode (its own doc comment already anticipated this). Approve tries
without the override flag first; if the server reports the aggregate-cap message and the caller is a Manager, a
second confirm dialog offers to retry with `overrideAggregateCap: true` — so a Manager only sees that prompt on
the specific approval that actually needs it, rather than always being asked up front. A new "Posting" column on
the history table shows `ReserveHistory.PostingStatus` once the GL job completes.

## Judgement calls

- **The aggregate cap (BR-R-05) is enforced on manual approval only, not on auto-approval.** The FRS frames it
  under "approving a reserve" specifically, and every individual auto-approved transaction is ≤ $10,000 by
  definition — reaching $10M purely through auto-approvals would take 1,000+ consecutive maxed-out
  transactions on one claim, an edge case `SubmitReserveTransaction`'s already-tested auto-approval path
  deliberately doesn't special-case. If a future ticket needs the cap enforced there too, `SubmitReserveTransaction`
  is the method to extend, mirroring `ApproveReserveTransaction`'s `projectedTotal` calculation.
- **`AggregateCapOverrideApproved` is a one-way, sticky flag on the claim**, not a per-approval parameter that
  must be re-asserted every time. The FRS describes a Manager "setting an override flag on the claim" (singular,
  persisted state), not re-confirming it per transaction — once a Manager has explicitly accepted the aggregate
  exposure once, a later approval that again exceeds the cap doesn't re-prompt. There's no "un-set" action,
  since the FRS describes only the one-way set. **Confirmed as intended** (code-review flagged this as worth a
  conscious call rather than a silent discovery) — kept as-is.

## Known limitations / follow-ups

- The frontend's aggregate-cap-override retry (`claim-detail.component.ts`'s `offerAggregateCapOverrideIfApplicable`)
  decides whether to offer a Manager the override retry by substring-matching the backend's free-text RFC 7807
  `detail` message (`.includes('$10,000,000')`). Nothing machine-readable ties the two together, so a future
  wording change to `Claim.ApproveReserveTransaction`'s rejection message would silently break this UX with no
  compile-time signal. A typed error code (e.g. a `code` field alongside `detail` in the 422 body, or a
  structured `BusinessRuleViolationException.Extensions` entry like Ticket 06's `requiredRole`/`unmetConditions`)
  would remove the coupling — flagged here rather than fixed now, since spec.md doesn't require structured error
  codes beyond the RFC 7807 shape and this is the only place in the app doing this kind of matching.
- **Self-approval is compared by `SubmittedByUserId == actorUserId`, never by role** — a Manager who happens to
  be the original submitter is blocked exactly like a Handler would be, per BR-R-03's literal wording ("a user
  may not approve their own reserve").
- **Rejection has no self-rejection restriction.** BR-R-03 only names self-*approval*; nothing in the FRS blocks
  a Supervisor/Manager from rejecting their own submission (unlikely in practice, since a submitter rejecting
  their own pending transaction has no advantage over just retracting it — but the FRS doesn't forbid it, so
  this doesn't either).
- **A rejected transaction needs no retraction step before resubmission**, unlike a pending one: BR-R-04 only
  says the submitter "may create a new reserve transaction"; `SubmitReserveTransaction`'s existing pending-
  approval guard already only blocks on a component whose *latest* transaction is PendingApproval, and
  `Rejected` isn't that — so this fell out of Ticket 07's logic unchanged, verified by a dedicated test rather
  than assumed.
