# Architecture

A bounded vertical slice of a larger insurance Policy Administration System: FNOL (First Notice
of Loss) intake and reserve management, implemented end-to-end — database through API through
Angular frontend — and deployed to Azure. See [`CONTEXT.md`](CONTEXT.md) for domain terminology
and [`docs/adr/`](docs/adr/) for the full record of decisions this document draws on.

## Solution structure

A monorepo under `src/`:

```
src/
  backend/
    ClaimsModule.Domain/           entities, domain events, pure business rules — no dependencies
    ClaimsModule.Application/      commands, queries, event handlers, Hangfire jobs — depends on Domain only
    ClaimsModule.Infrastructure/   auth, storage, Hangfire wiring — implements Application's interfaces
    ClaimsModule.Persistence/      EF Core DbContext, migrations, repositories — implements Application's interfaces
    ClaimsModule.Api/              ASP.NET Core controllers, Program.cs composition root
    *.UnitTests / *.IntegrationTests
  frontend/claims-module-web/      Angular 18+ (standalone components, signals)
infra/                             Bicep templates (Azure provisioning)
.github/workflows/                 CI/CD pipeline
docs/adr/                          Architecture Decision Records
```

This is Clean Architecture's standard four rings (Domain → Application → Infrastructure/
Persistence → API), fixed by the assessment rather than a project choice. Dependencies point
inward only: `Domain` has no project references at all; `Application` depends on `Domain` and
defines the interfaces (`IApplicationDbContext`, `IStorageService`, `IClaimRepository`, ...) that
`Infrastructure` and `Persistence` implement; `Api` is the composition root that wires concrete
implementations into those interfaces via DI (`Program.cs`).

## Data model and relationships

`Claim` is the **sole aggregate root** in this slice ([ADR-0001](docs/adr/0001-claim-aggregate-boundary.md)).
It owns four child entity collections, loaded and persisted together through one
`ClaimRepository` + Unit of Work:

```
Claim (aggregate root)
 ├─ LossEvent            (1:1 — the incident: date, location, cause, estimated amount)
 ├─ ClaimParty[]          (1:many — claimant / witness / third party)
 ├─ ClaimRiskObject[]     (1:many — insured assets affected)
 └─ ReserveComponent[]    (1:many — one row per component type: Indemnity/Expense/ALAE/SubrogationRecoverable)
       └─ ReserveHistory[] (1:many — the append-only ledger; see "Reserves are event-sourced" below)
```

`Claim` also optionally references a `Policy` (`PolicyId`, nullable — FNOL can proceed before a
policy is linked). `ClaimDocument` and `ClaimAuditLog` reference `ClaimId` but live **outside**
the aggregate: a document attachment and an audit entry enforce no cross-entity invariant with the
rest of the claim, so they're written directly rather than through `ClaimRepository`.

Reference/lookup data — `CauseOfLossCode`, `ClaimStatusTransition`, `Policy`, `PolicyCoverage` —
is seeded once and shared across organisations, not tenant-scoped
([ADR-0009](docs/adr/0009-reference-and-policy-data-not-org-scoped.md)).

**Tenant isolation**: every tenant-sensitive table (`Claim` and its children, `Users`) carries an
`OrganizationId`. `ApplicationDbContext` applies two EF Core global query filters automatically —
soft-delete (`IsDeleted`) and organisation scoping (`IHasOrganization`, resolved from the
authenticated user's JWT claim via `ICurrentUserService`) — so every query is filtered without
each handler having to remember to add a `.Where()`.

**Reserves are event-sourced** ([ADR-0007](docs/adr/0007-reserves-event-sourced-idempotency-on-history.md),
superseding [ADR-0006](docs/adr/0006-idempotency-key-on-audit-log.md)): `ReserveComponent` rows
are never updated directly. Every change — open, adjust, reverse — is an inserted, immutable
`ReserveHistory` row (`TransactionType`, a signed `Amount` delta, `PreviousBalance`→`NewBalance`).
`ReserveComponent.CurrentAmount` is a maintained projection of the sum of its `ReserveHistory`
rows, not an independently mutated value. `IdempotencyKey`
(`Reserve:{ReserveComponentId}:Change:{ChangeSequence}`), `ApprovalStatus`, `PostingStatus`, and
`PostingJobId` all live on `ReserveHistory`.

**Claim numbering** ([ADR-0008](docs/adr/0008-claim-number-counter-table.md)): a
`ClaimNumberCounters(OrganizationId, Year, LastValue)` table, incremented atomically via
`UPDATE ... OUTPUT INSERTED.LastValue`, generates `CLM-{YYYY}-{7-digit-zero-padded}` with no gaps
or reuse, resetting per organisation each year.

## CQRS flow: API → persistence

Every state change is a MediatR `Command`; every read is a `Query`
([ADR-0002](docs/adr/0002-direct-projection-for-reads.md) covers the read side specifically).

**Write path** (e.g. `POST /api/claims/{id}/reserves`):

```
Controller → IMediator.Send(command)
  → ValidationBehaviour (FluentValidation, pipeline-level — not just at the controller)
  → LoggingBehaviour (correlation ID, duration, outcome)
  → CommandHandler
      → ClaimRepository.GetByIdAsync (loads the whole aggregate)
      → Claim.SubmitReserveTransaction(...) (pure domain method — returns a result, raises domain events)
      → context.SaveChangesAsync()
          → DispatchDomainEventsInterceptor.SavingChanges (fires *inside* SaveChanges, before SQL is built)
              → publishes each domain event as a MediatR notification
              → event handlers (e.g. ReserveSubmittedForApprovalEventHandler) add a ClaimAuditLog row
          → the audit row(s) are swept into the *same* SaveChanges call, the *same* transaction
      → IBackgroundJobScheduler.Enqueue<PostGlReserveChangeJob> (if auto-approved)
```

**Read path** (e.g. `GET /api/claims`): query handlers project directly from `DbContext`
(`AsNoTracking()`, conditional `.Where()`, `.Select()` straight to response DTOs) — never through
`ClaimRepository`. Nine distinct read shapes need server-side filtering, counting, and paging,
which only composes correctly against `IQueryable`, not an already-materialized aggregate.
`ClaimRepository` + Unit of Work exist solely for the write side.

## Domain events and their handlers

Nine domain events, each with exactly one handler that writes exactly one `ClaimAuditLog` entry —
dispatched in-process via MediatR, in the same transaction as the triggering command
([ADR-0003](docs/adr/0003-in-process-domain-events-no-outbox.md); no outbox, since every consumer
lives in this one process and database):

| Event | Handler writes |
|---|---|
| `ClaimCreatedEvent` | `CLAIM_CREATED` |
| `ClaimStatusChangedEvent` | status-transition entry (all transitions except Closed/Reopened) |
| `ClaimClosedEvent` | `CLAIM_CLOSED` (carries the closure reason) |
| `ClaimReopenedEvent` | `CLAIM_REOPENED` (carries the reopen reason) |
| `ReserveSubmittedForApprovalEvent` | `RESERVE_CREATED` (routed to PendingApproval) |
| `ReserveAutoApprovedEvent` | `RESERVE_AUTO_APPROVED` |
| `ReserveApprovedEvent` | `RESERVE_APPROVED` |
| `ReserveRejectedEvent` | `RESERVE_REJECTED` |
| `ReserveRetractedEvent` | `RESERVE_RETRACTED` |

`SlaBreachScanJob` writes `SLA_BREACH_DETECTED` directly (not via a domain event — its scan is a
projected `AsNoTracking()` query with no tracked `Claim` to raise an event from), and
`PostGlReserveChangeJob` writes `GL_POSTING_SIMULATED` directly as a side effect of posting. Both
are deliberate, narrow exceptions to the domain-event pattern, not a second parallel convention to
extend.

## Background jobs: design and idempotency

Two Hangfire jobs, both deliberately **Hangfire-agnostic** — each takes only
`IApplicationDbContext` and `IDateTime`, no Hangfire-typed parameter — so a test can call
`ExecuteAsync` directly, independent of Hangfire's own scheduler:

- **`PostGlReserveChangeJob`** — simulates posting an approved reserve change to a general ledger.
  Enqueued fire-and-forget immediately on approval (auto-approval at submission, or manual
  approval). Idempotency (BR-R-06): checks `ReserveHistory.PostingStatus` first — a row already
  `Posted` is a no-op — with the `IdempotencyKey`'s unique index as the real concurrency safety
  net (the pre-check alone can't stop two concurrent workers both passing it before either
  commits; the unique-constraint violation on insert is caught and treated as "already
  processed").
- **`SlaBreachScanJob`** — a recurring job (every 15 minutes) that flags claims in Draft/Open with
  no update in 48+ hours, writing one `SLA_BREACH_DETECTED` entry per claim per rolling 24-hour
  window. **Never** changes `Claims.Status` — its query is `AsNoTracking()` throughout, so there
  is structurally no tracked change for `SaveChangesAsync` to ever persist back to `Claims`.

Both are registered in `Program.cs`, deliberately *after* the startup migration/seed block — the
first proven-good SQL connection should be the app's own, not Hangfire's `AcquireDistributedLock`.

## Azure architecture

```
                    ┌─────────────────────────────┐
  Browser  ───────▶ │  Static Web App (Standard)   │
                    │  Angular build, serves /     │
                    └──────────────┬───────────────┘
                                   │ linked backend
                                   │ (proxies /api/* server-to-server,
                                   │  no browser-side CORS needed)
                                   ▼
                    ┌─────────────────────────────┐
                    │  App Service (Linux, .NET 9) │
                    │  ASP.NET Core API + Hangfire │
                    └───┬───────────────────┬─────┘
                        │                   │
                        ▼                   ▼
              ┌──────────────────┐  ┌──────────────────┐
              │  Azure SQL (Basic)│  │  Blob Storage     │
              │  app DB + Hangfire│  │  claim-documents  │
              │  job storage      │  │  container        │
              └──────────────────┘  └──────────────────┘
```

The frontend's production config already points at a relative `apiBaseUrl: '/api'`, and every
backend controller is routed under `/api/*` — both already assumed Static Web Apps' "bring your
own backend" linking feature, which proxies matching requests to the linked App Service
server-to-server. That's the primary cross-origin path; the backend's own CORS middleware (reading
an allow-list from configuration) is a fallback for direct-origin access, not the primary
mechanism. Provisioning is a single re-runnable Bicep template (`infra/`) — see
[`README.md`](README.md#reproducing-the-azure-deployment) for the reproduction procedure, and
[Ticket 11](.scratch/claims-module/issues/11-cicd-azure-deployment.md) for the specific problems
hit provisioning it (regional capacity restrictions, an Azure-side Easy Auth side effect of the
linked-backend feature, GitHub Actions' secret-output masking behaviour).

## Key decisions and tradeoffs

The full rationale for each lives in its ADR; summarised here, newest-relevant-first:

- **Authentication is a self-issued, server-validated JWT, not a client-side fake**
  ([ADR-0004](docs/adr/0004-self-issued-jwt-for-mock-auth.md)). Only identity-proofing is mocked
  (`POST /api/auth/login` trusts a username with no password check) — the JWT itself is real,
  signed, and validated by standard ASP.NET Core middleware, because role-gated reserve-approval
  endpoints need a role signal the server actually trusts, not one a client could forge.
- **Reads bypass the repository** ([ADR-0002](docs/adr/0002-direct-projection-for-reads.md)) —
  accepted coupling to `DbContext` directly in query handlers, in exchange for server-side
  filtering/paging that an already-materialized repository result can't provide. Revisit if read
  complexity grows past filtered projections into reporting-style aggregation.
- **No outbox** ([ADR-0003](docs/adr/0003-in-process-domain-events-no-outbox.md)) — an outbox
  solves cross-process delivery reliability; every event consumer here is in-process against the
  same database Hangfire's own durable job storage already uses.
- **Claim numbering via a counter table, not a SQL `SEQUENCE`**
  ([ADR-0008](docs/adr/0008-claim-number-counter-table.md)) — a native `SEQUENCE` is a single
  global counter, not partitionable by organisation/year without dynamic DDL; the counter table
  gives the right partition key natively.
- **Angular signals + services, not NgRx** ([ADR-0005](docs/adr/0005-signals-not-ngrx.md)) —
  nothing in this app has state that's genuinely cross-feature or has complex derived-state
  graphs; NgRx's ceremony would add boilerplate without improving correctness here.
- **Reference/policy data isn't organisation-scoped**
  ([ADR-0009](docs/adr/0009-reference-and-policy-data-not-org-scoped.md)) — a deliberate, narrow
  exception to the "every table is tenant-scoped" principle, matching the FRS's own schema: shared
  lookup data isn't tenant-sensitive data.
