# Claims Module — Business Logic

What this system does, in plain terms, and exactly where the rules live in code. This
is a bounded vertical slice of a larger Policy Administration System (PAS): **FNOL
(First Notice of Loss) intake + reserve management** for insurance claims.

Source of truth for every rule below: `.scratch/claims-module/spec.md` (the FRS —
Functional Requirements Spec) and `src/backend/ClaimsModule.Domain/Entities/Claim.cs`,
which is the single place almost all of this logic actually executes.

---

## 1. The core entity: Claim

A `Claim` is the **only aggregate root** in the system (ADR-0001). Everything else —
the loss event, parties, risk objects, and reserve components — is a child entity
loaded and saved with it as one transactional unit, through one repository.

```
Claim
├── LossEvent          (1:1 — what happened: date, cause, description, location)
├── ClaimParty[]        (claimants, witnesses, third parties)
├── ClaimRiskObject[]    (vehicles/property/equipment that were damaged)
└── ReserveComponent[]   (money set aside per component type)
      └── ReserveHistory[]  (immutable ledger of every change to that component)
```

A claim always starts in **Draft** status and is given a unique, sequential
**claim number** on creation (see §6).

---

## 2. Claim status lifecycle

Seven statuses (`ClaimStatus` enum): `Draft`, `Open`, `UnderInvestigation`,
`PendingPayment`, `Closed`, `Reopened`, `Withdrawn`.

### Valid transitions (seeded in `ClaimStatusTransitions`, `SeedReferenceData.sql`)

| From | To | Minimum role |
|---|---|---|
| Draft | Open | Handler |
| Open | UnderInvestigation | Handler |
| Open | PendingPayment | Handler |
| Open | Closed | Supervisor |
| Open | Withdrawn | Handler |
| UnderInvestigation | Open | Handler |
| UnderInvestigation | PendingPayment | Handler |
| UnderInvestigation | Closed | Supervisor |
| UnderInvestigation | Withdrawn | Handler |
| PendingPayment | Closed | Supervisor |
| Closed | Reopened | Supervisor |
| Reopened | Open | Supervisor |

Roles have a strict hierarchy — `Handler < Supervisor < Manager` — so "minimum role"
is a simple ordinal comparison; a Supervisor or Manager can always do what a Handler
can. Any pair not in this table is rejected outright, and the error response lists the
claim's actual valid next statuses.

**`Reopened` is transient**: the FRS's own transition table has it land immediately on
`Open` — there's no second, separately-authorised step. A Supervisor "reopening" a
claim is really doing `Closed → Open` in one call, with a mandatory reason.

### Per-transition business conditions (beyond the from/to/role table)

Checked in `Claim.GetUnmetConditions()`:

- **→ Open**: requires at least one active `Claimant` party.
- **→ PendingPayment**: requires at least one reserve component with an
  **Approved or AutoApproved** latest transaction — i.e. real money has actually been
  set aside, not just requested.
- **→ Closed**: requires at least one active Claimant, **and** no reserve component may
  currently have a transaction sitting in `PendingApproval` — nothing can close while
  money is still awaiting a decision.
- **→ Reopened**: requires a non-blank reason.
- **→ Withdrawn**: requires a non-blank reason.

If any condition fails, the API returns **422** with the specific unmet condition(s) —
not a generic "can't do that."

---

## 3. FNOL intake (creating a claim)

Validated by `CreateClaimCommandValidator` before a claim is ever persisted
(`ValidationBehaviour` runs this in the MediatR pipeline, ahead of the handler):

- **Loss date**: required, cannot be in the future.
- **Loss description**: required, minimum 20 characters.
- **Cause of loss**: must reference an active `CauseOfLossCodes` row (10 seeded codes —
  Fire, Flood, Theft, Vehicle Collision, etc.).
- **Risk objects**: each one needs a non-empty asset description.
- **Policy linkage is optional**, but if omitted it's only a **warning**, not a
  rejection — you can file a claim with the policy unknown and link it later.
- **Loss-date-outside-policy-period** is also only a **warning**: if a policy *is*
  linked but the loss date falls outside its effective/expiration window, the claim
  still gets created, flagged for review. (The seeded `POL-2023-000099` /
  "Archived Corp" policy exists specifically to exercise this case.)

Everything else (missing cause code, loss date in the future, description too short)
is a **hard block** — the claim is never created.

`ClientName` is denormalized at creation: taken from the linked policy if one exists,
otherwise from the first Claimant party.

---

## 4. Reserve management

A reserve is money set aside against a claim's expected cost. There are four component
types (`ReserveComponentType`), of which only `SubrogationRecoverable` may go negative:

- `Indemnity`
- `Expense`
- `ALAE` (attorney/court costs — distinct from `Expense`)
- `SubrogationRecoverable` (money expected back, so it's the one exception to "must be positive")

### Event-sourced, not CRUD (ADR-0007)

`ReserveComponent.CurrentAmount` is **never written directly** — it's a projection
recomputed from an append-only `ReserveHistory` ledger. Every change (`Add`, `Adjust`,
`Reverse`) is a new immutable row carrying its own `PreviousBalance` → `NewBalance`,
approval status, and idempotency key. This is what makes "show me how this balance was
reached" a simple history query instead of forensic reconstruction.

### Authority tiers (`ReserveApprovalThresholds`)

Applied to the **absolute value of each individual transaction**, never a running
total:

| Transaction amount | Outcome |
|---|---|
| ≤ $10,000 | **Auto-approved** immediately, no human step |
| $10,000 – $100,000 | Requires **Supervisor or Manager** approval |
| > $100,000 | Requires **Manager** approval |

A reserve component's **first** transaction is `Add`; every one after is `Adjust`.
Both go through the same authority-tier routing.

### The aggregate cap — BR-R-05

Separately from the per-transaction tiers: if approving a pending transaction would
push the **claim's total approved reserves** (across all components) over
**$10,000,000**:

1. It requires Manager approval regardless of the transaction's own size.
2. The Manager must explicitly pass an `overrideAggregateCap: true` flag the first
   time this happens on the claim.
3. Once set, `Claim.AggregateCapOverrideApproved` stays `true` for the claim's
   lifetime — a later approval that again exceeds the cap doesn't force the Manager to
   re-assert it. There's no "un-set" action.

### Approve / reject / retract (`Claim.cs`)

- **Approve**: blocked if the transaction isn't `PendingApproval`, if the approver is
  the same user who submitted it (**self-approval is always rejected, compared by user
  ID — a Manager can't approve their own submission either**), or if the actor's role
  is below the required tier for the amount (or the aggregate-cap rule above).
- **Reject**: requires a reason; the row stays `Rejected` in history permanently — the
  original submitter can submit a **new** transaction with a revised amount. Nothing
  needs to be cleared first.
- **Retract**: only the *original submitter* can retract their own still-`PendingApproval`
  transaction, and only while it's pending. This sets it to `Cancelled` — a
  deliberately separate status from `Rejected` to distinguish "the submitter changed
  their mind" from "a reviewer said no." A component with a `PendingApproval` latest
  transaction blocks any *new* submission until it's retracted, rejected, or approved.

### Prerequisite for opening any reserve

A policy must already be linked to the claim (`PolicyId` not null) — reserves can't be
opened on a claim with an unknown policy (BR-C-06).

### GL posting simulation (`PostGlReserveChangeJob`)

Fires automatically — fire-and-forget via Hangfire — the moment a transaction becomes
approved (whether auto-approved at submission or manually approved later). It:

1. No-ops immediately if the `ReserveHistory` row is already `Posted` (handles retries).
2. Writes one `GL_POSTING_SIMULATED` audit log entry describing the simulated
   double-entry posting (`DR Change in Outstanding Reserves / CR Outstanding Loss
   Reserves`).
3. Marks the row `Posted`.

Idempotency is enforced two ways: the pre-check above, *and* a unique index on
`ReserveHistory.IdempotencyKey` (format `Reserve:{componentId}:Change:{sequence}`) —
the unique index is the actual safety net against two workers racing the same job.

---

## 5. Background jobs (Hangfire)

### SLA breach scan (`SlaBreachScanJob`) — recurring, every 15 minutes

Finds every claim in `Draft` or `Open` whose `LastModifiedAt` (or `CreatedAt`, if never
modified) is **48+ hours** old, and writes one `SLA_BREACH_DETECTED` audit log entry
per claim.

- **Never touches `Claim.Status`** — purely an audit-log side effect. The status
  lifecycle is governed only by the explicit transition rules in §2.
- **Throttled to one new entry per claim per rolling 24 hours** — an ongoing breach
  doesn't flood the audit log with a new row every 15 minutes.
- No emails, no push notifications — out of scope per the FRS.

### GL posting — see §4 above.

Both jobs are deliberately **Hangfire-agnostic**: they only depend on the DB context
and a clock, so they can be invoked and tested directly without spinning up Hangfire's
scheduler.

---

## 6. Claim numbering (ADR-0008)

Format: `CLM-{YYYY}-{7-digit zero-padded sequence}`, e.g. `CLM-2026-0000298`.

- One counter **per organisation, per year**, stored in `ClaimNumberCounters`.
- Incremented via an atomic `UPDATE ... OUTPUT INSERTED` — not a read-then-write — so
  two concurrent submissions can never collide or skip a number.
- Resets to 1 each new year, per organisation.
- Runs inside the same transaction as claim creation, so a failed creation rolls the
  counter back too (no permanently burned numbers from a failed submission).

---

## 7. Roles & authentication (ADR-0004)

Three roles, strictly hierarchical: `Handler < Supervisor < Manager`.

- **Mock identity-proofing, real tokens**: `POST /api/auth/login` takes only a
  username, matches it (case-insensitively) against a seeded user — **no password
  check at all** — but issues a real, server-validated JWT carrying the user's role and
  organisation as claims. Every authorization check downstream (role-gated status
  transitions, reserve approval, etc.) is enforced against that JWT server-side, not
  just hidden in the UI.
- Seeded accounts (`ApplicationDbContextSeeder`): `handler1` (Handler), `supervisor1`
  (Supervisor), `manager1` (Manager), all under one seeded organisation ("Acme
  Insurance").
- Every table carries an `OrganizationId`; an EF Core global query filter enforces
  tenant isolation automatically from the JWT's organisation claim.

---

## 8. Reference & policy data (simulated PAS)

Since this is a slice of a larger system that doesn't exist yet, policy and reference
data are **simulated** via an independently re-runnable SQL script
(`SeedReferenceData.sql`), not EF migration seed data — so a reviewer can re-run it
directly with `sqlcmd` against any environment:

- **10 cause-of-loss codes** (`COL-FIRE`, `COL-FLOOD`, `COL-THEFT`, …), each with a
  peril category; only active codes are selectable at intake.
- **5 simulated policies** with coverages, including one deliberately **expired**
  policy (`POL-2023-000099` / "Archived Corp") used to exercise the loss-date warning.
- The claim status transition matrix itself (§2's table) — also reference data, not
  hardcoded in application logic, so the valid-transitions-and-required-roles are
  queryable the same way the cause-of-loss codes are.

---

## 9. Error handling contract

Every failure maps to an RFC 7807 `ProblemDetails` response via a global exception
handler (`GlobalExceptionHandler`) — no handler needs its own try/catch, no 500 leaks
internals:

| Failure | HTTP status | Body carries |
|---|---|---|
| FluentValidation failure | 400 | `errors`: field → message[] dictionary |
| Not found | 404 | `detail`: the specific message |
| Auth failure | 401 | `detail`: the specific message |
| Business rule violation (status transition, reserve rule) | **422** | `detail` + extras: `unmetConditions`, `validNextStatuses`, or `requiredRole`, whichever apply |
| Anything unhandled | 500 | generic message only, nothing leaked |

The frontend's global HTTP error interceptor reads `detail` (and, for 422s,
`unmetConditions`) to show the real domain message in the snackbar instead of a
generic "something went wrong."

---

## 10. What's deliberately out of scope

- A real Policy Administration System — only a minimal simulated policy dataset.
- A real identity provider/SSO — self-issued JWT, no password verification.
- Multi-currency reserves, real GL integration, real email/push notifications for SLA
  breaches.
- Fraud/investigation workflow beyond the `UnderInvestigation` status value itself.
- Subrogation recovery processing, litigation management, or payment/disbursement
  beyond reserving.
- An outbox pattern — domain events are dispatched in-process, in the same transaction,
  via MediatR notifications (ADR-0003); Hangfire's own durable storage already covers
  the delivery guarantee an outbox would add.
