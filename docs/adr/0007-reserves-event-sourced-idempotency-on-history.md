# Reserves are event-sourced; idempotency key lives on ReserveHistory

Supersedes ADR-0006. The FRS (§6.6, §9.5, §9.6) is explicit that reserves follow an event-sourcing pattern: `ReserveComponent` rows are never updated directly — every change is an inserted `ReserveHistory` row (`TransactionType`: Add/Adjust/Reverse, a signed `Amount` delta, `PreviousBalance`→`NewBalance`). `ReserveComponent.CurrentAmount` is a maintained projection of the sum of `ReserveHistory`, not an independent value.

`ReserveHistory` already carries `ApprovalStatus`, `PostingStatus`, `PostingJobId`, `ChangeSequence`, and — critically — `IdempotencyKey` (`Reserve:{ReserveId}:Change:{ChangeSequence}`) as defined columns. The GL posting Hangfire job checks for an existing `ReserveHistory` row with that key (and a unique index on it as the real concurrency safety net, same reasoning as ADR-0006) before simulating the posting, rather than checking `ClaimAuditLog`.

ADR-0006 put the idempotency key on `ClaimAuditLog` based on the Assessment document's condensed appendix table alone, before the full FRS's entity-level detail had been pulled. The FRS is self-contained and more precise; where the two conflict, the FRS wins. `ClaimAuditLog` still gets a `GL_POSTING_SIMULATED` entry when the job runs (per BR-A-02), but the entry is a side effect of the posting, not the idempotency guard for it.

## Consequences

`Claim.ApproveReserve(...)` and any command that mutates reserves must insert a new `ReserveHistory` row rather than updating `ReserveComponent` fields directly, then recompute and persist `ReserveComponent.CurrentAmount` from the inserted row in the same transaction. Repository/aggregate code and EF Core configuration for this area need to reflect the ledger shape from the start — retrofitting event-sourcing onto an already-built mutable-row design would be expensive, which is why this was worth catching now during grilling rather than during implementation.
