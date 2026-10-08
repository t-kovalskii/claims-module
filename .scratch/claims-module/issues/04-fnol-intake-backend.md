# 04 — FNOL intake: backend

**What to build:** the complete backend contract for creating, listing, and viewing claims — the `Claim` aggregate and its child entities, atomic claim numbering, full creation validation, and the audit trail — verifiable end-to-end via Swagger before any UI is built against it.

**Blocked by:** 01, 03

**Status:** done

- [x] Creating a claim atomically creates its loss event and the submitted parties and risk objects, all in one transaction, and returns the generated claim number. (No reserve yet — see Ticket 07.)
- [x] Claim numbers are generated via an atomic, per-organisation, per-year counter with no gaps and no reuse, in the `CLM-{YYYY}-{7-digit}` format.
- [x] Creation enforces every FNOL validation rule: loss date not in the future, loss date vs. policy period as a non-blocking warning, loss description minimum length, active cause-of-loss code required, and a non-blocking warning when no policy is linked.
- [x] A `ClaimCreated` entry is written to the claim's audit log as part of the same transaction, and `GET /api/claims/{id}/audit` returns it.
- [x] `GET /api/claims` supports filtering by status, date range, assigned handler, and cause of loss, plus a free-text search on claim number or client name, with server-side pagination.
- [x] `GET /api/claims/{id}` returns full claim detail including parties, risk objects, and loss event (reserves and documents return empty collections until their tickets land).
- [x] All endpoints are covered by HTTP-seam integration tests against a real SQL Server test container, including at least one case per validation rule.

## Comments

Implemented the full backend contract: `Claim` aggregate root (`ClaimsModule.Domain/Entities/Claim.cs`)
with a `Create` factory enforcing the one non-trivial invariant (always starts `Draft`, always
raises `ClaimCreated`) plus child entities `LossEvent`, `ClaimParty`, `ClaimRiskObject`, and the
append-only `ClaimAuditLog` (deliberately not an `AuditableEntity` — no soft-delete/concurrency
token, since BR-A-01 forbids update/delete on it entirely) and `ClaimNumberCounter` (ADR-0008).

**ADR-0001 repository + UoW path**: new `IClaimRepository` (`Add` only — reads never go through
it) and `IUnitOfWork.ExecuteInTransactionAsync`, implemented in Persistence wrapping an explicit
`Database.BeginTransactionAsync()`. `CreateClaimCommandHandler` runs the ADR-0008 atomic counter
increment (`ClaimNumberGenerator`, raw `UPDATE ... OUTPUT INSERTED` with an insert-on-first-use
fallback) and the aggregate insert inside that one transaction, so a failed creation rolls the
counter back too — verified by a genuine-concurrency test (8 parallel `POST`s, asserts all 8
claim numbers are distinct).

**ADR-0003 implemented for the first time**: Tickets 01-03 defined `DomainEventNotification<T>`
but nothing ever dispatched one. Added `DispatchDomainEventsInterceptor`, a `SaveChangesInterceptor`
that publishes a tracked entity's domain events from inside `SavingChanges` (before EF builds the
SQL batch), so `ClaimCreatedEventHandler` adding a `ClaimAuditLog` row lands in the very same
`SaveChanges` call/transaction as the `Claim` insert that raised the event. Registered before
`AuditableEntitySaveChangesInterceptor` in `DependencyInjection.cs` so newly-added entities still
get audit-stamped (moot for `ClaimAuditLog` itself, which isn't an `AuditableEntity` and sets its
own `CreatedAt`/`CreatedByUserId` directly in the handler).

**ADR-0002 reads**: three query handlers (`GetClaimsQuery`, `GetClaimByIdQuery`,
`GetClaimAuditLogQuery`), all `AsNoTracking()` + direct `.Select()` projection off
`IApplicationDbContext.Claims`, navigating straight to `LossEvent`/`Parties`/`RiskObjects`/
`CauseOfLossCode` — no repository involvement. `ClaimDetailDto.Reserves`/`Documents` are typed
`List<object>` (always `[]`) as placeholders; Tickets 07/10 should replace these with real typed
DTOs once those shapes exist, not just leave them as `object`.

**FluentValidation non-blocking warnings**: the ticket requires BR-C-02/BR-C-06 warnings to run
*through the FluentValidation pipeline* without blocking. `ValidationBehaviour` now splits
`Severity.Error` (blocking, thrown as before) from `Severity.Warning` (never blocking); when the
handler's response implements the new `IHasValidationWarnings` marker, warning messages are
appended to it after `next()` returns. `CreateClaimResult.Warnings` carries these for FNOL intake.
Covered by two new `ValidationBehaviourTests`.

**Judgement calls** (frontend/Ticket 05 and later tickets should know these):
- `ClaimParty` roles are the FRS's full five (`Claimant`/`Insured`/`ThirdParty`/`Witness`/`Attorney`,
  BR-P-02), not a 3-value simplification — FRS governs per spec.md's own conflict-resolution rule.
- `CreateClaimCommand.LossEvent` is a nested `CreateClaimLossEventRequest` bundle (mirroring how
  `Parties`/`RiskObjects` already travel as their own request DTOs), not flat top-level fields —
  the frontend's create-claim payload needs `{ policyId, lossEvent: { lossDate, lossDescription,
  lossLocation, causeOfLossCodeId, estimatedLossAmount, policeReportNumber }, assignedHandlerUserId,
  parties: [...], riskObjects: [...] }`.
- The create command takes `causeOfLossCodeId` (the `Guid` from `GET /api/reference/cause-of-loss-codes`'s
  `Id` field), not the `Code` string.
- When no policy is linked, `Claim.ClientName` is derived from the first `Claimant` party's name
  (or `"Unknown"` if none given) rather than left blank, since the list/detail screens always
  display it.
- Added a defensive Critical rule ("Linked policy was not found.") for a `PolicyId` that doesn't
  exist — not in the FRS's validation table, but `PolicyId` is a real FK and this avoids a raw
  500 from a constraint violation reaching the client.
- A global `JsonStringEnumConverter` was registered in `Program.cs` so `CreateClaimCommand`'s
  enum-valued fields (party role/type, risk-object asset type) bind from JSON strings (e.g.
  `"Claimant"`, `"Vehicle"`) rather than requiring clients to know C#'s underlying ordinals —
  consistent with every other enum-shaped field this API already returns as a string.
- `GET /api/claims` does not support a `policyId` filter even though FRS §10.1 lists one — the
  ticket's own checklist (the primary authority here) only lists status/date-range/handler/cause/
  search, so it was left out. Flag this if Ticket 05's frontend needs it; adding it later is a
  small, additive change to `GetClaimsQuery`/`GetClaimsQueryHandler`.
- `ClaimNumberCounter` implements `IHasOrganization` (gets the global org query filter) purely
  for consistency with spec.md's "every table" tenant-isolation principle, even though its only
  real access path (`ClaimNumberGenerator`'s raw SQL) bypasses EF query filters entirely — it's
  the one table besides ADR-0009's four reference/policy tables that could plausibly have been
  treated as exempt, and this avoids creating a second, undocumented exception.

Covered by 21 new HTTP-seam integration tests (`ClaimsTests.cs`, including a real-concurrency
claim-number test) against the real SQL Server test container, plus 2 new `ValidationBehaviour`
unit tests, per spec.md's Testing Decisions. Full backend suite passes: 41 integration tests
(20 pre-existing + 21 new) plus 8 unit tests across the Application/Infrastructure/Domain unit
test projects. Reviewed via `/code-review` (Standards + Spec axes, parallel sub-agents) before
committing; real findings were addressed (consolidated the validator's duplicate Policy lookup
into one query, bundled the loss-event fields into their own request DTO to match
`Parties`/`RiskObjects`, deduplicated the controllers' paging-default logic, made
`ClaimNumberCounter` implement `IHasOrganization` for tenant-isolation consistency, and added the
genuine-concurrency claim-number test) except two accepted judgement calls noted above (`policyId`
filter omission, `ClaimDetailDto`'s placeholder `List<object>` for Reserves/Documents).
