# 06 — Status transitions

**What to build:** the claim status lifecycle, enforced server-side against the reference transition table, with a UI that only ever offers valid next moves.

**Blocked by:** 05

**Status:** done

- [x] `PUT /api/claims/{id}/status` validates the requested transition against the allowed-transitions reference data and rejects anything not listed, returning the valid next statuses in the rejection.
- [x] Transitioning to Open is blocked unless there's at least one Claimant party and no unresolved Critical validation issues.
- [x] Transitioning to Closed is blocked unless every closure condition is satisfied, with the API returning the specific unmet conditions.
- [x] Transitioning to Reopened requires Supervisor role and a non-empty reason, and the claim lands in Open immediately afterward.
- [x] The claim detail header offers a transition control showing only the current claim's valid next statuses, with a confirmation dialog before committing, and a pre-flight checklist specifically when attempting to close.
- [x] Every successful transition writes an audit log entry, visible on the Audit Log tab.

## Comments

**Backend**: `PUT /api/claims/{id}/status` (`ClaimsController.TransitionStatus`) → `TransitionClaimStatusCommand`/
`Handler` (ADR-0001's repository path — `IClaimRepository.GetByIdAsync` now exists alongside `Add`, eagerly
including `Parties` since the transition conditions need them; no explicit `IUnitOfWork` transaction, unlike
`CreateClaimCommandHandler`, since there's exactly one independently-committing operation here). The actual
state-machine branching — valid/invalid from/to pairs, role authority, and the Open/Closed/Reopened/Withdrawn
conditions — lives on `Claim` itself as two pure methods, `ValidateTransition` and `ApplyTransition`, plus
`GetUnmetConditions`/`GetUnmetClosureConditions`: exactly the combinatorial branching spec.md's Testing
Decisions justify the narrow `Claim`-only in-memory unit-test seam for (16 tests in
`ClaimsModule.Domain.UnitTests/Entities/ClaimStatusTransitionTests.cs`). The endpoint/authorization/audit
wiring is covered by 12 new HTTP-seam tests (`ClaimStatusTransitionTests.cs` in `Api.IntegrationTests`)
against the real SQL Server test container, per the same Testing Decisions.

A new `BusinessRuleViolationException` (→ 422 `ProblemDetails`, `GlobalExceptionHandler`) is the first use of
spec.md's "business-rule violations map to 422" convention — nothing before this ticket needed it (every
prior validation failure was a FluentValidation/400 case). Its `Extensions` dictionary carries whatever
structured detail the specific rejection needs: `validNextStatuses` (invalid from/to pair), `requiredRole`
(insufficient role), or `unmetConditions` (Open/Closed/Reopen/Withdrawn conditions not met).

Three new domain events replace a single generic one: `ClaimStatusChangedEvent` (routine transitions, audit
`STATUS_CHANGED`), `ClaimClosedEvent` (audit `CLAIM_CLOSED`, `NewValue` = closure reason per FRS §14.1's own
event table), and `ClaimReopenedEvent` (audit `CLAIM_REOPENED`, `NewValue` = reopen reason). Added
`Claim.ClosedAt`/`ClosureReason` (FRS §9.1 columns Ticket 04 hadn't needed yet) via migration
`AddClaimClosureFields`.

**FRS role-table verification** (done as instructed, against `Claims_Module_Candidate_Specification.docx`
§4.2 and §3, not just trusting Ticket 03's own documented inference): confirmed Ticket 03's seeded
`RequiredPermission` values are correct. The FRS's §4.2 transition table names an explicit role for exactly
3 of the 12 rows — `Open→UnderInvestigation` ("Handler or supervisor"), `UnderInvestigation→Open` ("Handler
manually reverts"), `Closed→Reopened` ("supervisor role required", BR-ST-04) — and §3's role-capability table
lists "manage claim status transitions" only from Supervisor upward, while *not* listing general status-
transition authority under Handler's own capabilities at all (Handler's row only lists create/parties/
documents/reserves-within-authority/view). Where the per-transition table (§4.2) and the capability summary
(§3) could be read as being in tension for the 9 transitions without an explicit named role, the per-transition
table — being the one BR-ST-01..04 actually point at for transition mechanics — governs, and Ticket 03's own
inference rule (Handler for routine progress, Supervisor for anything landing on Closed, plus Reopened per
BR-ST-04) is exactly what that reading produces. No seed-data correction was needed.

**Judgement calls**:
- **"No unresolved Critical validation issues" (BR-ST-02a / CC-02) is vacuously satisfied.** Ticket 04 never
  introduced a persisted "validation issue" entity beyond FluentValidation's own request-time Critical/Warning
  split, and every Critical rule (BR-C-01/05/07) already blocks claim creation itself — no `Claim` can exist
  today with an outstanding Critical issue. `Claim.GetUnmetOpenConditions()`/`GetUnmetClosureConditions()`
  document this explicitly inline rather than silently omitting the check. If a future ticket adds editable
  claim fields (e.g. letting a handler revise the loss description after creation) or a real `ValidationIssue`
  entity, this is the method to extend.
- **CC-01 (no `PendingApproval` reserves) is vacuously satisfied for the same reason reserves don't exist
  yet** — `Claim` has no `ReserveComponents` until Ticket 07/08. `GetUnmetClosureConditions()` has an inline
  comment flagging that Ticket 07/08 **must** extend it once reserves exist, or claims become closable with
  reserves still pending approval. CC-04 (non-blocking "open reserves at closure" warning) is also moot today
  and was not implemented as a warning-return, for the same reason — nothing to warn about yet.
- **Reopened never persists as a literal status.** BR-ST-04 says the claim "lands in Open immediately" after
  Reopened — `Claim.ApplyTransition` sets `Status = ClaimStatus.Open` directly for a Reopened target, never
  writing `Reopened` to the database, while still raising `ClaimReopenedEvent` (audit `CLAIM_REOPENED`,
  recording the Closed→Reopened transition and reason) so the audit trail reads correctly. One consequence:
  `TransitionClaimStatusCommandHandler`'s lookup (`ClaimStatusTransitions WHERE FromStatus == claim.Status`)
  only ever queries `FromStatus == Closed` for this flow — it never queries `FromStatus == Reopened`. Ticket
  03's seeded `('Reopened', 'Open', 'Supervisor')` row is therefore never read by this code path. **This is
  intentional, not stale data to clean up**: `GET /api/reference/claim-statuses` still needs to describe the
  FRS's full documented state machine accurately (story 51 — "see which status transitions are valid") even
  though this implementation collapses the last hop internally; removing the row would make the reference
  endpoint misrepresent the FRS's own table.
- **Insufficient role on a transition is mapped to 422, not 403.** Reserve-approval endpoints (Ticket 08) will
  likely use a static `[Authorize(Roles = "...")]` per ADR-0004, which ASP.NET maps to 403. Here the required
  role is data-driven (varies per `ClaimStatusTransition` row, discovered only after loading the claim and
  matching the from/to pair), so it can't be a static attribute — it's enforced inside
  `Claim.ValidateTransition` instead. Folding it into the same `BusinessRuleViolationException`/422 shape as
  "invalid pair" and "unmet conditions" keeps one rejection shape for every way this one endpoint can refuse a
  transition, rather than introducing a second status code for what's still, functionally, "you can't make
  this specific transition request succeed." Flagging this explicitly since it's a defensible but debatable
  call — Ticket 11/12's architecture docs should mention it if reviewers ask why role failures aren't 403 here
  but might be 403 on reserve approval.
- **Withdrawn also requires a non-empty reason**, beyond this ticket's own minimum checklist (which only names
  Reopen's reason requirement) — the FRS's own §4.2 table lists "Withdrawal reason code provided" for both
  `Open→Withdrawn` and `UnderInvestigation→Withdrawn`, and it's a one-line, no-missing-dependency check, so it
  was included rather than left as a documented gap.
- **PendingPayment's FRS condition ("at least one Approved reserve exists") is not enforced** — same reason as
  CC-01: no `ReserveComponents` exist yet. The transition is currently unconditional once role/pair checks
  pass. Ticket 07/08 should add this check once reserves exist; flagging here so it isn't silently forgotten.
- **Reason-required-for-Reopened/Withdrawn is a 422 business-rule condition, not a 400 FluentValidation rule**,
  even though "is this field empty" sounds like ordinary request-shape validation. It's classified as
  business-rule because whether a reason is required at all depends on the *target status* chosen, which is
  itself only meaningful once compared against the claim's current state — there's no
  `TransitionClaimStatusCommandValidator` at all; everything for this command goes through `Claim`'s own
  methods. Noted as a reasonable but debatable classification call.
- **Frontend pre-flight closure checklist is computed client-side** from the already-loaded `ClaimDetail`
  (checking `parties` for an active Claimant; the other two items always render "satisfied" per the backend
  judgement calls above) — there's no dedicated "preview closure conditions" endpoint in the FRS's API table,
  so this is a best-effort preview, re-validated authoritatively by the server on actual submission (same
  pattern Ticket 05's wizard already uses for its own warnings preview).
- **`ConfirmDialogComponent`** (`shared/components/confirm-dialog/`) is deliberately generic — title/message/
  optional checklist/optional required-reason — rather than a one-off "status transition dialog", so Tickets
  07/08/10 (reserve approve/reject, document deletion, etc.) can reuse it instead of growing near-duplicate
  dialogs. Its checklist-unmet copy was kept domain-neutral ("All conditions above must be satisfied before
  continuing.") after review flagged an earlier draft's claim-closure-specific wording as inconsistent with
  that stated genericity.
- **`shared/auth/role-authority.ts`** hand-mirrors the backend `UserRole` enum's declared order
  (Handler < Supervisor < Manager) as a `ROLE_RANK` lookup, since the frontend has no endpoint exposing role
  hierarchy. Documented inline with a pointer to the exact backend file; a backend role reorder/addition would
  need this file updated too — flagging for whoever touches `UserRole.cs` next.

**For Ticket 11/12** (CI/CD, architecture docs): the 422-vs-403 judgement call above and the PendingPayment/
CC-01/CC-04 reserve-dependent gaps are worth a line in `ARCHITECTURE.md`'s known-limitations section.

Reviewed via `/code-review` (Standards + Spec axes, parallel sub-agents) before committing. Standards axis:
no hard violations (ADR-0001/0002/0003/0004 and spec.md's Testing/error-handling conventions all honoured);
judgement-call smells noted were the confirm-dialog's now-fixed hardcoded "closed" copy (fixed), the
`role-authority.ts` duplication noted above (accepted, documented), and the audit-event-handlers' boilerplate
repetition (matches the pre-existing `ClaimCreatedEventHandler` pattern, not a new issue). Spec axis: no
missing or wrongly-implemented checklist items; the two flagged items (422-vs-403 for role, and the seeded
`Reopened→Open` row's reachability) are both addressed above as accepted, documented judgement calls rather
than defects.

Backend: 16 new `Claim` unit tests + 12 new HTTP-seam integration tests, on top of the pre-existing 83
backend tests (all re-verified passing after this ticket's changes — run per-test-class rather than as one
`dotnet test` invocation across the whole solution, since this host's Docker daemon is memory/CPU-constrained
by many unrelated long-running containers and starting 6 Testcontainers SQL Server instances fully in parallel
causes resource-contention failures unrelated to this change; each class passes cleanly in isolation).
Frontend: 45 tests total (7 new `ConfirmDialogComponent` tests, 1 new `ClaimsService.transitionStatus` test,
and 7 new `ClaimDetailComponent` transition-control/dialog/checklist tests added to the existing suite), `ng
build` clean, initial bundle unchanged at ~543kB (well under the 600kB budget).
