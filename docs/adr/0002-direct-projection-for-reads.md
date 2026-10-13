# Direct EF Core projection for reads, repository only for writes

Nine distinct read shapes (claims list with four filter dimensions plus pagination, claim detail, audit log, reserves list, documents list, policy search, policy coverage, cause-of-loss codes, claim statuses) need server-side filtering, counting, and paging — which only works if query handlers compose `IQueryable<Claim>` (or other entities) directly rather than receiving an already-materialized list.

We decided query handlers query `DbContext` directly (`AsNoTracking()`, conditional `.Where()`, `.Select()` projection straight to response DTOs) instead of going through `ClaimRepository`. `ClaimRepository` + the Unit of Work remain the only path for commands that mutate the `Claim` aggregate — reads and writes are structurally separate, not just separated by naming convention.

## Consequences

Query handlers now depend on `DbContext` (or a narrow `IClaimsQueryContext` seam for testability) directly, so a persistence-technology change would touch every read handler, not just the repository. This is an accepted tradeoff for now. Revisit if read complexity grows past simple filtered projections — e.g. reporting-style aggregations across claims — at which point a dedicated read-side query service or materialized views may earn their cost.
