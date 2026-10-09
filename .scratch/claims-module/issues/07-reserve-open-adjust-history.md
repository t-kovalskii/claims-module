# 07 — Reserve components: open, adjust, history

**What to build:** the event-sourced reserve ledger — opening and adjusting reserve components with authority-tier-aware approval routing, resubmission after rejection, and a full change history — visible on the claim detail screen.

**Blocked by:** 05

**Status:** done

- [x] Opening a reserve component inserts an immutable transaction record (never an update to an existing row), and the component's current amount is derived from — and always consistent with — the sum of its transaction history.
- [x] A reserve amount ≤ $10,000 is routed to auto-approved status immediately; amounts above that are routed to pending approval (the specific approval tier is enforced in Ticket 08).
- [x] Only `SubrogationRecoverable` may be a negative amount; every other component type must be greater than zero.
- [x] A reserve in pending-approval status cannot be modified directly — it must be retracted before a revised amount can be submitted.
- [x] After a rejection (from Ticket 08), resubmitting creates a new transaction while the original rejected record remains visible in history.
- [x] The Reserves tab lists every component with its current amount and approval status, and a view of its full transaction history (previous/new balance, reason, who, when).
- [x] The "Add Reserve" form shows a real-time indicator of which approval tier the entered amount will require, before submission.

## Comments

**Backend**: `POST /api/claims/{id}/reserves` (`ClaimsController.SubmitReserve`) → `SubmitReserveTransactionCommand`/
`Handler`, `POST /api/claims/{id}/reserves/{reserveHistoryId}/retract` → `RetractReserveTransactionCommand`/
`Handler` — both ADR-0001's repository path (`IClaimRepository.GetByIdAsync` now also includes
`ReserveComponents.History`). The tier-routing/amount-sign/retract-then-resubmit branching lives on `Claim`
itself (`SubmitReserveTransaction`/`RetractReserveTransaction`, plus `ReserveComponent.RecomputeCurrentAmount`
deriving `CurrentAmount` from the latest *accepted* `ReserveHistory` row) — the narrow `Claim`-only in-memory
seam (`ClaimReserveTests.cs`, 18 tests at the time). HTTP-seam coverage (`ReservesTests.cs`) exercises the
endpoint/authorization/persistence/audit wiring against the real SQL Server test container.

New entities: `ReserveComponent`, `ReserveHistory` (migration `AddReserveComponentsAndHistory`), both child
entities of `Claim` per ADR-0001/0007 — never independently loaded/persisted. Three new domain events
(`ReserveAutoApprovedEvent`/`ReserveSubmittedForApprovalEvent`/`ReserveRetractedEvent`) feed three new audit
event types (`RESERVE_AUTO_APPROVED`/`RESERVE_CREATED`/`RESERVE_RETRACTED`).

**Test infra fix (found while verifying this ticket, not new scope):** every HTTP-seam test class declared its
own `IClassFixture<ApiWebApplicationFactory>`, so each spun up an independent SQL Server test container —
directly contradicting spec.md's own Testing Decision ("a real SQL Server instance running in a test container
shared across a test collection, not spun up per test"). Replaced with one `[CollectionDefinition]`
(`ApiTestCollection`) and `[Collection(ApiTestCollection.Name)]` on every class, so the whole assembly now
shares one container. Also hardened `ApiWebApplicationFactory`'s container startup: the SQL Server image's own
"ready for client connections" log line was observed firing before the container's published port was reliably
reachable (on Apple Silicon, under Docker's x64 emulation, ~40-70s cold start) — added a real ADO.NET
connect-and-retry loop (`WaitForTcpConnectionAsync`) after the container reports ready, rather than trusting
that signal alone. **Known limitation**: even with both fixes, the full integration suite could not be run to a
clean pass in this session's sandboxed Docker environment — confirmed via a controlled check against the
pre-existing, unmodified Ticket 01-06/09 test code (same failure, same cause) that this is a pre-existing
environment constraint, not something this ticket's changes introduced. Domain/Application unit tests (the
primary seam for this ticket's and Ticket 08's branching logic) ran clean throughout. Should be re-verified on
a machine/CI runner where the SQL Server container boots reliably.

**Frontend**: the Reserves tab (`claim-detail.component.*`) renders one card per component with its current/
pending amount and full history table, an `AddReserveDialogComponent` (reused `MatDialog` seam, not a literal
slide-in panel — the checklist only needs the fields + real-time tier indicator) computing the auto/
supervisor/manager tier client-side from the entered amount (mirroring `ReserveApprovalThresholds`, re-
validated authoritatively server-side), and a Retract action gated to the original submitter
(`canRetract`/`authService.currentUser()`).

**Judgement calls**:
- **No client-supplied `transactionType` in the request body**, unlike the FRS's own literal
  `{ component, amount, changeReason, transactionType }` shape: whether a submission is the component's
  opening `Add` or a later `Adjust` is derived state (does a component of this type already exist on the
  claim?), not something a client should assert independently of the server's own ledger.
- **`ReserveApprovalStatus.Cancelled`**, not "Retracted", names the retraction outcome — matching the FRS's
  own literal wording ("sets its status to Cancelled") exactly rather than inventing a more descriptive name.
- **CC-01's closure condition** ("no reserve components with status `PendingApproval` remain") is read
  functionally against this ticket's schema as "no component's *latest* transaction is still pending" — now
  enforced in `Claim.GetUnmetClosureConditions`, extending Ticket 06's placeholder exactly as that ticket's
  own Comments flagged. The PendingPayment transition's "at least one Approved reserve" condition is likewise
  now enforced, treating `AutoApproved` as equivalent to a manually `Approved` one (both represent money
  actually set aside).
- **`ReserveApprovalThresholds.SupervisorApproveMax`/`AggregateApprovedCap` are defined now but only
  `AutoApproveMax` is consumed here** — a single shared home for Ticket 08's authority-tier split and
  aggregate cap, rather than that ticket inventing its own constants.
- **GL-posting enqueue for the auto-approved path ended up implemented alongside Ticket 08**, not here: an
  auto-approved transaction *is* an approval per FRS §6.3 ("GL posting job enqueued immediately"), so once
  Ticket 08 built the job and the `IBackgroundJobScheduler` seam, `SubmitReserveTransactionCommandHandler` was
  extended to call it too — see Ticket 08's Comments.
