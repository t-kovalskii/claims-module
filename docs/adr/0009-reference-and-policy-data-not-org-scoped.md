# Reference data and simulated policies are not organisation-scoped

spec.md's Implementation Decisions state tenant isolation applies via an org-scoped EF Core global query filter on "every table." Ticket 03 adds four tables — `CauseOfLossCodes`, `ClaimStatusTransitions`, `Policies`, `PolicyCoverages` — and none of them carry an `OrganizationId` or implement `IHasOrganization`; `ApplicationDbContext`'s organisation query filter (which only applies to `IHasOrganization` types) does not touch them.

This is a deliberate, narrow exception, not an oversight. The FRS's own data model (§9) defines all four tables with no `OrganisationId` column — they are reference/lookup data and simulated-policy data, seeded once via Ticket 03's SQL script and shared across whatever organisations exist, not claim data belonging to one tenant. Spec.md's "every table" tenant-isolation principle is about isolating the data that is actually tenant-sensitive — the `Claim` aggregate and its children, and `Users` — not a shared cause-of-loss code list or a simulated policy book that every organisation in this assessment sees identically.

Making these four tables org-scoped would require every reference row and every simulated policy to be duplicated per organisation, and the independently re-runnable SQL seed script would need to resolve an `OrganizationId` per insert the way `ApplicationDbContextSeeder` does for `Users` — real complexity with no behavioural payoff in a single-organisation seeded assessment, and a direct contradiction of the FRS's own schema.

## Consequences

Ticket 04 (FNOL intake) links a `Claim` to a `Policy` by `PolicyId` across this boundary: the `Claim` side is org-scoped (it's a child of the org-scoped `Users`/handler's organisation), the `Policy` side is not. This is intentional — any organisation's handler can link to the same shared simulated policy book. If a future ticket genuinely needs per-organisation reference data or separate policy books, revisit this ADR then; don't assume these four tables need an `OrganizationId` retrofitted silently.
