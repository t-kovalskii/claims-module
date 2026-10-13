---
status: superseded by ADR-0007
---

# Idempotency key lives on ClaimAuditLog, not a dedicated ledger table

**Superseded:** written before pulling the FRS's full §9.6 `ReserveHistory` schema, which already defines `IdempotencyKey`, `PostingStatus`, and `PostingJobId` as columns on the reserve ledger itself. See ADR-0007.

BR-R-05 requires the GL posting Hangfire job to be re-entrant safe using a key of the form `Reserve:{ReserveId}:Change:{ChangeSequence}`. Hangfire dedupes only by its own internal job ID, which differs per enqueue, so the job needs its own idempotency check.

We added a nullable `IdempotencyKey` column with a unique index to `ClaimAuditLog`, rather than introducing a separate `ProcessedJobKeys`/ledger table. The job checks for an existing audit entry with that key before simulating the GL posting; if found, it no-ops. `ClaimAuditLog` already is the record of what happened for a claim, so reusing it avoids a parallel concept existing solely to answer "did this run already" for one job type.

The unique index is also the real safety net, not just the pre-check: the job catches a unique-constraint violation on insert and treats it as "already processed," since two concurrent Hangfire workers could otherwise both pass the existence check before either commits.
