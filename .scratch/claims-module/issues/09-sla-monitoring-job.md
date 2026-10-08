# 09 — SLA monitoring job

**What to build:** the recurring background sweep that flags claims going stale, purely as an audit signal with no status side effects.

**Blocked by:** 01, 04

**Status:** done

- [x] A recurring Hangfire job runs every 15 minutes and finds claims in Draft or Open status with no update in the last 48+ hours.
- [x] Each flagged claim gets exactly one `SLA_BREACH_DETECTED` audit log entry per 24-hour window — running the job repeatedly within that window does not add duplicates.
- [x] The job never modifies `Claims.Status` under any circumstance.
- [x] A test can trigger the job directly (without waiting 15 real minutes) against seeded claims with manipulated `UpdatedAt` timestamps and observe the correct audit entries.

## Comments

Implemented `SlaBreachScanJob` (`ClaimsModule.Application/Claims/Jobs/SlaBreachScanJob.cs`) —
the first real Hangfire job in this codebase (Ticket 01 wired Hangfire with none defined yet).
It takes only `IApplicationDbContext` and `IDateTime`, no Hangfire dependency at all, so it's
directly callable from a test via `ExecuteAsync()` exactly as the ticket requires, independent
of Hangfire's own scheduler. `Program.cs` registers it with
`RecurringJob.AddOrUpdate<SlaBreachScanJob>("sla-breach-scan", job => job.ExecuteAsync(...),
"*/15 * * * *")`.

**Logic**: scans `Claims` where `Status` is `Draft`/`Open` and `(LastModifiedAt ?? CreatedAt) <=
now - 48h` (all `AsNoTracking()` — the query never touches a tracked `Claim`, so there is no
tracked change for `SaveChangesAsync` to ever persist back to `Claims.Status`, satisfying the
"never modifies Status" requirement structurally, not just by omission). For each stale claim,
checks whether a `SLA_BREACH_DETECTED` audit entry already exists with `CreatedAt` within the
last 24h (a rolling window anchored to the most recent flag, not a fixed calendar bucket) and
skips it if so; otherwise inserts a new `ClaimAuditLog` row with `CreatedByUserId = null` (a
system-triggered entry, following `ClaimCreatedEventHandler`'s pattern for `CreatedAt`/
`CreatedByUserId`). Added `AuditEventTypes.SlaBreachDetected = "SLA_BREACH_DETECTED"`.

**Judgement calls** (later tickets/reviewers should know these):
- The ticket's own wording says "use `Claim.UpdatedAt`" but no such column exists —
  `AuditableEntity` only has `LastModifiedAt`, which `AuditableEntitySaveChangesInterceptor`
  (Ticket 01) only stamps on an actual post-creation `Modified` save, staying `null` for a claim
  that's never been touched since intake. Interpreted "no update in 48+ hours" as `LastModifiedAt
  ?? CreatedAt`, i.e. "last touched" falls back to creation time rather than being vacuously
  never-stale.
- Unlike the GL-posting job (ADR-0007: idempotency key + unique index as the real concurrency
  safety net), this job's 24h throttle is a read-then-insert check with no DB-level uniqueness
  backing it — two truly concurrent runs of the same recurring job could theoretically both pass
  the check and double-insert. Not treated as a defect: FRS/ticket language here is "per 24-hour
  window," a time-based rolling throttle that doesn't reduce to a single discrete idempotency key
  the way `Reserve:{Id}:Change:{Seq}` does, and Hangfire doesn't run two instances of the same
  recurring job id concurrently under normal operation. Flagging in case a future ticket wants a
  harder guarantee.
- The job writes its `ClaimAuditLog` row directly (not via a domain event + handler, unlike every
  other audit entry so far per ADR-0003/Ticket 04). Deliberate: the job never loads a `Claim`
  aggregate (its scan is a projected `AsNoTracking()` query, by design, so there's no tracked
  entity to raise a domain event from). Flagged by `/code-review`'s Standards pass as a second,
  parallel path for producing audit entries — accepted as a one-off, not a pattern to repeat if a
  third audit-writing mechanism shows up later.
- Found and fixed a real startup-ordering bug while wiring this in: `Program.cs` originally called
  `RecurringJob.AddOrUpdate` *before* the `context.Database.MigrateAsync()`/seeding block, making
  Hangfire's `AcquireDistributedLock` SQL connection the very first thing the app does against a
  fresh SQL Server instance. Under the integration test suite's parallel per-test-class
  Testcontainers instances, this was consistently flaky (`SqlException`: "pre-login handshake"
  failures), failing pre-existing test classes (`ClaimsTests`, `PoliciesTests`, etc.) that have
  nothing to do with this ticket. Moved the registration to *after* the migration/seed block, so
  it's the second thing to touch SQL Server rather than the first proven connection — full suite
  (47 integration tests + 6 Application unit tests + 2 Infrastructure unit tests) now passes
  cleanly and quickly (~25s) with no flakiness across repeated runs.
- Test file (`SlaBreachScanJobTests.cs`) manipulates `CreatedAt`/`LastModifiedAt` and an existing
  audit entry's `CreatedAt` via raw `ExecuteSqlInterpolatedAsync` `UPDATE` statements rather than
  through EF Core's normal tracked-entity path — going through `SaveChanges` would hit
  `AuditableEntitySaveChangesInterceptor`, which stamps `LastModifiedAt` to the real current time
  for any `Modified` entity and would silently undo the simulated staleness.
- `/code-review` (Standards + Spec, parallel sub-agents) found no hard violations on either axis.
  Noted-but-accepted judgement calls: the test file's `GetActiveCauseOfLossCodeIdAsync()` helper
  and private `CreateClaimResponseDto` record duplicate members already duplicated across
  `ClaimsTests.cs`/`ReferenceDataTests.cs` — pre-existing team pattern, not a regression; and no
  test asserts Hangfire's own job-storage registration (`"sla-breach-scan"` actually landing in
  Hangfire's recurring-jobs table) — deliberately not added, since it would require reading the
  process-wide static `Hangfire.JobStorage.Current`, which is overwritten by whichever
  `ApiWebApplicationFactory` instance started most recently when test classes run in parallel —
  exactly the kind of cross-test-class fragility this ticket's Program.cs fix just removed.
