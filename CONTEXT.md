# Claims Module

A standalone greenfield implementation of FNOL (First Notice of Loss) intake and reserve management, as a bounded vertical slice of a larger insurance Policy Administration System (PAS).

## Language

**Claim**:
The aggregate root representing a formal request for indemnification following a covered loss event. Owns its `LossEvent`, `ClaimParty` list, `ClaimRiskObject` list, and `ReserveComponent` list as a single transactional and persistence boundary — loaded and saved together through one repository.
_Avoid_: Case, File

**ReserveComponent**:
A row per component type (`Indemnity`, `Expense`, `ALAE`, `SubrogationRecoverable`) on a `Claim`, holding a `CurrentAmount` that is a maintained projection of `ReserveHistory` — never an independently mutated value. A child entity of `Claim` — never loaded, persisted, or repository-accessed independently.
_Avoid_: Reserve (as an entity name — "reserve" is fine as the general business concept, but the entity is always `ReserveComponent`). Also avoid "Recovery"/"Litigation" as component types — the FRS defines exactly four: `Indemnity`, `Expense` (investigation/expert fees), `ALAE` (attorney/court costs — distinct from `Expense`), `SubrogationRecoverable` (the only one that may go negative).

**ReserveHistory**:
The append-only ledger of reserve transactions. Each row is an immutable transaction (`Add`/`Adjust`/`Reverse`) against one `ReserveComponent` — carrying its own approval status, posting status, and idempotency key — never an update to the component itself. This is the authoritative source of truth for a component's balance; `ReserveComponent.CurrentAmount` is derived from it.
_Avoid_: ReserveTransaction (the entity is always `ReserveHistory`; "transaction" is fine as the general business concept it represents)

**LossEvent**:
The incident (date, location, cause) that gave rise to a `Claim`. A child entity owned by that `Claim`.

**ClaimParty**:
A claimant, witness, or third party associated with a `Claim`.

**ClaimRiskObject**:
An insured asset referenced by a `Claim` as having sustained damage.

**ClaimDocument**:
Metadata for a supporting file attached to a `Claim` (filename, content type, size, storage path, uploader, timestamp) — the file bytes themselves live in the configured `IStorageService` (Azure Blob or local filesystem), never in the database. Referenced by `ClaimId` but not part of the `Claim` aggregate: attaching a document enforces no cross-entity invariant, so it's written directly, not through `ClaimRepository`.
_Avoid_: Attachment, File (ambiguous with other domain meanings of "file")
