Status: ready-for-agent

# Claims Module — FNOL Intake & Reserve Management

## Problem Statement

A claims handler at an insurance carrier needs to record a loss the moment it's reported (First Notice of Loss), link it to the right policy, capture everyone and everything involved, and set aside financial reserves against the expected cost — all while supervisors and managers retain approval authority over larger financial exposures, and every action is auditable. Today none of this exists: there is no system to intake a claim, no way to track or approve reserves, and no record of what happened to a claim and when.

This is being built as a self-contained, greenfield technical assessment: a bounded vertical slice (FNOL intake + reserve management) of a much larger Policy Administration System's Claims module, implemented end-to-end — database through API through Angular frontend — and deployed to Azure.

## Solution

A Clean Architecture .NET 9 backend (CQRS via MediatR) and an Angular 18+ frontend, covering:

- FNOL intake: a multi-step form that creates a `Claim` linked to a (simulated) policy, captures the loss event, parties, and risk objects, validates business rules synchronously, and optionally opens an initial reserve.
- A claims dashboard: paginated, filterable list with status, reserve totals, and quick navigation into detail.
- Claim detail: full view across loss event, parties, risk objects, reserves, documents, and an immutable audit log, with status-transition controls gated by the valid-transition state machine.
- Reserve management: opening and adjusting reserve components, authority-tiered approval (auto / supervisor / manager), rejection and resubmission, and a $10M aggregate cap requiring manager override.
- Two Hangfire background jobs: a GL posting simulation (idempotent) triggered on reserve approval, and a recurring SLA breach scan.
- Role-gated access (handler / supervisor / manager) enforced by a real, validated JWT — only the identity-proofing step is mocked.
- A deployed, publicly accessible instance on Azure with a basic CI/CD pipeline, plus architecture and AI-workflow documentation.

## User Stories

### FNOL Intake

1. As a claims handler, I want to search for a policy by number or client name during FNOL intake, so that I can link the claim to the correct policy.
2. As a claims handler, I want to view the coverages available on a linked policy, so that I know which coverage types apply to the loss.
3. As a claims handler, I want to create a claim with no policy linked when the policy is unknown at intake time, so that I can still capture the loss immediately and link the policy later.
4. As a claims handler, I want the system to warn me — not block me — when the loss date falls outside the policy's effective period, so that I can proceed with the FNOL while the issue is flagged for review.
5. As a claims handler, I want to record one or more claimants, witnesses, and third parties against the claim, so that everyone involved is captured from first notice.
6. As a claims handler, I want to record one or more risk objects (vehicle, property, equipment, etc.) affected by the loss, so that the claim reflects what was actually damaged.
7. As a claims handler, I want to select a cause-of-loss code from an active reference list, so that the claim is classified consistently.
8. As a claims handler, I want the system to reject loss dates in the future, so that obviously invalid claims can't be created.
9. As a claims handler, I want the system to require a loss description of at least 20 characters, so that claims carry enough detail for triage.
10. As a claims handler, I want the system to generate a unique, sequential claim number automatically on creation, so that I never assign one manually or risk colliding with another handler's concurrent submission.
11. As a claims handler, I want to optionally open an initial reserve component as part of FNOL intake, so that straightforward claims don't need a separate step afterward.
12. As a claims handler, I want a real-time indicator of whether my entered reserve amount will be auto-approved or require supervisor/manager approval, so that I understand the approval implications before I submit.
13. As a claims handler, I want to review a summary of everything I've entered before final submission, so that I can catch mistakes before the claim is created.
14. As a claims handler, I want to see the generated claim number and a link to the claim detail screen immediately after successful submission, so that I can continue working on the claim right away.

### Claims Dashboard

15. As a claims handler, I want a paginated list of all claims, so that I can work through my caseload without loading everything at once.
16. As a claims handler, I want to filter claims by status, loss date range, assigned handler, and cause of loss, so that I can quickly find the subset relevant to me.
17. As a claims handler, I want to search claims by partial claim number or client name, so that I can locate a specific claim quickly.
18. As a claims handler, I want each claim's status shown as a colour-coded badge, so that I can scan caseload state visually at a glance.
19. As a claims handler, I want to see the total reserve amount for each claim in the list, so that I can gauge severity without opening it.
20. As a claims handler, I want to click a claim row to navigate to its detail screen, so that drilling in doesn't need extra navigation steps.
21. As a claims handler, I want a prominent "Log New Claim" action on the dashboard, so that starting FNOL intake is always one click away.

### Claim Detail

22. As a claims handler, I want a claim's core details (number, status, policy number, client name, loss date, cause of loss, assigned handler) in a persistent header, so that I have context regardless of which tab I'm viewing.
23. As a claims handler, I want to view the loss event, parties, and risk objects for a claim, so that I have full context on what happened and who's involved.
24. As a claims handler, I want to view all reserve components with their current amounts, approval status, and change history, so that I understand the claim's financial position.
25. As a claims handler, I want to view and download documents attached to a claim, so that I can review supporting evidence.
26. As a claims handler, I want to upload a new document to a claim, so that I can attach supporting evidence as it becomes available.
27. As a claims handler, I want a reverse-chronological, immutable audit log of every significant action on the claim, so that I have a full accountability trail.
28. As a claims handler, I want to transition a claim to a valid next status, so that I can progress it through its lifecycle.
29. As a claims handler, I want the system to show only the statuses a claim can validly move to from its current state, so that I can't attempt an invalid transition.
30. As a claims handler, I want a confirmation dialog before any status transition, so that I don't change claim state by accident.
31. As a claims handler trying to close a claim, I want to see a pre-flight checklist of closure conditions, so that I understand what's blocking closure before I try.
32. As a claims handler, I want to be blocked from transitioning a claim to Open if it has no Claimant party or has unresolved Critical validation issues, so that incomplete claims can't proceed.
33. As a supervisor, I want to reopen a closed claim by providing a reason, so that claims with new developments can resume active handling.

### Reserve Management

34. As a claims handler, I want to open a new reserve component on a claim (Indemnity, Expense, ALAE, or SubrogationRecoverable), so that I can set aside funds against the claim's expected cost.
35. As a claims handler, I want reserve amounts ≤ $10,000 to be auto-approved immediately, so that routine small reserves don't create unnecessary friction.
36. As a claims handler, I want reserve amounts between $10,000 and $100,000 to require supervisor or manager approval, and amounts over $100,000 to require manager approval, so that larger financial exposures get appropriate oversight.
37. As a supervisor or manager, I want to see which reserves are pending my approval, so that I can act on them without hunting through every claim.
38. As a supervisor or manager, I want to approve a pending reserve, so that the claim's financial position is finalised and GL posting can proceed.
39. As a supervisor or manager, I want to reject a pending reserve with a reason, so that the submitter knows why it wasn't approved and can correct it.
40. As a supervisor or manager, I want to be prevented from approving a reserve I submitted myself, so that self-approval of financial transactions is impossible.
41. As a claims handler whose reserve was rejected, I want to resubmit a revised amount, so that I can correct and retry without losing the original rejected record from history.
42. As a claims handler, I want to see the full change history of a reserve component (amount, reason, who, when), so that I can audit how the current balance was reached.
43. As a manager, I want to be required to set an explicit override flag before approving a reserve that would push a claim's total approved reserves over $10,000,000, so that unusually large aggregate exposures get deliberate sign-off rather than slipping through.
44. As a claims handler, I want a reserve in PendingApproval status to require retraction (not direct editing) before I can change its amount, so there's never ambiguity about what's actually awaiting approval.
45. As the system, I want every approved reserve change to automatically enqueue a GL posting simulation job, so that financial posting happens without manual intervention.
46. As the system, I want the GL posting job to never create duplicate audit entries even if it runs twice for the same change, so that retries from transient failures can't corrupt the financial audit trail.

### Background Processing

47. As the system, I want a recurring job to scan for claims stuck in Draft or Open for more than 48 hours without an update, so that stale claims surface for handler attention.
48. As the system, I want SLA breach detection recorded only in the audit log, never changing the claim's actual status, so the status lifecycle stays governed strictly by its explicit transition rules.
49. As the system, I want SLA breach detection to never re-flag the same claim more than once per 24 hours, so the audit log isn't flooded with repeated entries for one ongoing breach.

### Reference Data

50. As a claims handler, I want to see only active cause-of-loss codes when filing a claim, so that I can't select a retired or invalid code.
51. As a claims handler, I want to see which status transitions are valid and what permission each requires, so that I understand the workflow rules governing my claims.

### Roles & Access

52. As a claims handler, I want to log in with a role-appropriate account, so that the system knows what I'm permitted to do.
53. As a claims handler without supervisor/manager privileges, I want reserve-approval actions hidden from my UI, so that I'm not shown controls I can't use.
54. As any user, I want every API call authenticated via a Bearer token attached automatically, so that I never have to manage credentials per request.
55. As a supervisor or manager, I want the server — not just the UI — to enforce my role on approval endpoints, so that authorization is real, not cosmetic.

### Frontend Quality

56. As a claims handler, I want inline validation messages on form fields and user-friendly snackbar notifications for API errors, so that I understand what went wrong without reading raw error payloads.
57. As a claims handler, I want loading indicators on every async action, so that I know the system is working rather than appearing frozen.

### Delivery & Operations

58. As a DICEUS reviewer, I want a publicly accessible, deployed frontend and backend on Azure with Swagger available, so that I can evaluate the running system without local setup.
59. As a DICEUS reviewer, I want at least two test accounts (handler, and supervisor or manager), so that I can exercise both the standard and approval-gated workflows.
60. As a DICEUS reviewer, I want a basic CI/CD pipeline that builds both backend and frontend, runs EF Core migrations, and deploys to Azure, so that I can see the delivery mechanism, not just the artifact.
61. As a DICEUS reviewer, I want `ARCHITECTURE.md` and `AI-WORKFLOW.md` documents describing the design decisions and AI-assisted workflow, so that I can assess engineering judgment and AI orchestration maturity, not just the code.
62. As a candidate, I want a README with complete local setup, environment-variable, migration, and deployment-reproduction instructions, so anyone — including a reviewer — can stand the system up from scratch.

## Implementation Decisions

- **Repository shape**: single monorepo with `backend/` (the Clean Architecture .NET 9 solution) and `frontend/` (Angular 18+), plus root-level `CONTEXT.md` / `docs/adr/` / `docs/agents/`.
- **Clean Architecture layering** (fixed by the assessment, not a project decision): Domain, Application, Infrastructure, Persistence, API.
- **CQRS via MediatR**: every state change is a Command, every read is a Query. FluentValidation runs at the MediatR pipeline level (a `ValidationBehavior`) for both commands and queries — not only at the controller.
- **Aggregate boundary** (see ADR-0001): `Claim` is the sole aggregate root, owning `LossEvent`, the `ClaimParty` list, the `ClaimRiskObject` list, and the `ReserveComponent` list as child entities — loaded and persisted together through one `ClaimRepository` + Unit of Work. No other aggregate roots exist in this slice.
- **Reserves are event-sourced** (see ADR-0007, which supersedes ADR-0006): `ReserveComponent.CurrentAmount` is a maintained projection; every change is an inserted, immutable `ReserveHistory` row (`TransactionType`: Add/Adjust/Reverse, signed delta, `PreviousBalance`→`NewBalance`). `IdempotencyKey`, `PostingStatus`, `PostingJobId`, and `ChangeSequence` live on `ReserveHistory`, not on the audit log.
- **Reads bypass the repository entirely** (see ADR-0002): all nine read endpoints are query handlers projecting directly from the EF Core context (`AsNoTracking`, conditional `.Where()`, `.Select()` to DTOs), enabling server-side filtering and pagination. The repository/UoW path exists only for commands mutating the `Claim` aggregate.
- **Domain events dispatched in-process** via MediatR notifications, within the same transaction as the triggering command — no outbox (see ADR-0003), since every consumer (audit logging, Hangfire enqueueing) lives in the same process and database, and Hangfire's own durable job storage already provides the delivery guarantee an outbox would add.
- **Policy simulation**: seeded `Policies`/`PolicyCoverages` tables in the same SQL Server database, populated by an independently re-runnable SQL seed script (not EF Core migration seed data) — including the FRS's own expired-policy example used to exercise the loss-date warning.
- **Authentication** (see ADR-0004): a minimal login endpoint issues a real, self-signed JWT (role + organisation claims) for a seeded handler/supervisor/manager user, with no real password verification. Standard JWT bearer middleware validates it and role-based authorization attributes enforce access server-side, not only in the Angular UI.
- **Frontend state management** (see ADR-0005): Angular signals plus plain injectable services, scoped per feature module; no NgRx. The FNOL multi-step form's internal state uses Reactive Forms, independent of this decision.
- **Claim numbering** (see ADR-0008): a counter table keyed by organisation and year, incremented via an atomic `UPDATE ... OUTPUT INSERTED` statement, resetting to 1 each new year per organisation. Format: `CLM-{YYYY}-{7-digit-zero-padded}`.
- **GL posting idempotency** (see ADR-0007): the Hangfire job checks for an existing `ReserveHistory` row with the computed idempotency key (unique-indexed) before simulating the posting. The unique index — not just the pre-check — is the actual safety net against two workers racing the same key.
- **SLA monitoring job**: a recurring job (every 15 minutes) scans claims in Draft/Open with no update in 48+ hours and writes an `SLA_BREACH_DETECTED` audit entry; it never changes `Claims.Status`, and throttles to at most one new entry per claim per 24 hours.
- **Document storage**: an `IStorageService` abstraction with Azure Blob and local-filesystem implementations, selected by configuration; uploads path to a per-organisation, per-claim blob prefix with a sanitised filename; retrieval returns a short-lived SAS URL rather than streaming bytes through the API.
- **Error handling**: RFC 7807 `ProblemDetails` via a global exception handler. Validation failures map to 400, business-rule violations to 422 with the specific blocking condition(s), not-found to 404, unhandled exceptions to 500 with no leaked internals.
- **Logging**: structured logging (Serilog), console locally and Application Insights when deployed, plus a MediatR logging behaviour recording every command/query with a correlation ID, duration, and outcome.
- **Local database**: SQL Server 2022 via `docker-compose.yml`, matching the production target exactly.
- **Source control & CI/CD**: GitHub + GitHub Actions; the pipeline builds the backend, runs EF Core migrations, builds the frontend, and deploys both to Azure (manual trigger is acceptable).
- **Azure provisioning**: a Bicep template provisioning the App Service(s)/Static Web App, Azure SQL, Blob Storage, and optionally Key Vault — serving as the literal, re-runnable reproduction procedure for the architecture documentation's deployment section.
- **Tenant isolation**: every table carries an organisation identifier; an EF Core global query filter enforces it automatically (resolved from the authenticated user's JWT claim), alongside the standard soft-delete filter.

## Testing Decisions

A good test exercises externally observable behaviour — HTTP responses, database state, UI-visible component behaviour — never internal implementation details, so refactoring internals doesn't break tests that don't care about them.

**Backend — two seams, deliberately minimised:**

1. **Primary seam (default for everything): HTTP integration tests.** A test host driving real requests through the full pipeline — controller → MediatR (validation + logging behaviours) → persistence → a real SQL Server instance running in a test container shared across a test collection (not spun up per test, to keep the suite fast) — asserting on HTTP responses and, where relevant, direct database state. This is the seam for all query handlers, all command handlers other than `Claim`'s own domain methods, role-based authorization enforcement, Hangfire job enqueueing and idempotency, and EF Core configuration itself (unique constraints, concurrency tokens, global query filters). Real SQL Server is used rather than an in-memory provider specifically because the things being graded — concurrency tokens, decimal precision, unique indexes — don't exist in an in-memory substitute.
2. **Narrow secondary seam, `Claim` only: pure in-memory unit tests.** Tests that call `Claim`'s domain methods directly — no HTTP, no database — justified specifically by the combinatorial branching in reserve authority tiers, the aggregate reserve cap, self-approval rejection, and the status transition state machine, where exhaustive case coverage through the HTTP seam would be prohibitively slow to run.

No other backend seams exist — reads, document storage, policy simulation, and background jobs are all covered by the primary HTTP seam.

**Frontend — one seam: the service layer.** Components are driven through their real templates and real typed services; only the network call itself is faked, so the service layer's own job (translating HTTP to typed models) stays under test rather than being mocked away.

**Prior art**: none — this is the first feature built in a greenfield repository, so these seams are being established here, not matched against existing precedent.

## Out of Scope

- A full Policy Administration System. Only a minimal seeded Policy/PolicyCoverage dataset is simulated — no policy issuance, endorsement, or billing.
- A real identity provider or SSO. Authentication is a self-issued JWT with no real password verification.
- Multi-currency reserves, real general-ledger system integration, or real email/push notifications for SLA breaches (the FRS explicitly states the SLA job sends neither).
- Investigation/fraud workflow detail beyond the `UnderInvestigation` status value itself.
- Any Claims module functionality beyond this FNOL intake + reserve management vertical slice — e.g. subrogation recovery processing, litigation management workflow, or payment/disbursement processing beyond reserving.
- An outbox pattern or distributed messaging (explicitly decided against, ADR-0003).
- NgRx or any global frontend state store (ADR-0005).
- Production-scale Azure infrastructure (autoscaling, multi-region, WAF) — a free/trial-tier single-instance deployment is sufficient per the assessment's own guidance.

## Further Notes

- Two source documents exist in the repo root: `DICEUS_Fullstack_Technical_Assessment.docx` (programme overview, evaluation criteria, deliverables) and `Claims_Module_Candidate_Specification.docx` (the detailed, self-contained FRS). Where they conflict — and they did, concretely, on where the reserve idempotency key lives (resolved ADR-0006 → ADR-0007) and on reserve component type names (`Indemnity`/`Expense`/`ALAE`/`SubrogationRecoverable` per the FRS, not the Assessment appendix's `Indemnity`/`Expense`/`Recovery`/`Litigation`) — the FRS governs, since it is explicitly self-contained and far more detailed.
- Per the assessment's own scope-management guidance, if time runs short, prioritise in this order: backend completeness and correctness, the deployed Azure application, then architecture documentation — ahead of frontend completeness.
- `ARCHITECTURE.md` and `AI-WORKFLOW.md` are graded deliverables, not an afterthought — the ADRs and `CONTEXT.md` already produced during grilling are direct source material for both, and should be kept current as implementation proceeds rather than reconstructed at the end.
