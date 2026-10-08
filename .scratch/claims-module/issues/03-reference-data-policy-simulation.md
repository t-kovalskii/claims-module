# 03 — Reference data + policy simulation

**What to build:** the reference and simulated-policy data FNOL intake depends on — queryable via the API, seeded with realistic data including the scenario the FRS specifies for exercising the loss-date-outside-policy-period warning.

**Blocked by:** 01

**Status:** done

- [x] Cause-of-loss codes, claim status transitions (with required permission per transition), and a small set of simulated policies with coverages are seeded via an independently re-runnable SQL script.
- [x] The seeded policies include at least one expired policy, matching the FRS's own example, usable to exercise the loss-date-outside-effective-period warning later.
- [x] `GET /api/reference/cause-of-loss-codes` returns only active codes and supports filtering by peril category.
- [x] `GET /api/reference/claim-statuses` returns every status along with its valid next transitions and the permission each requires.
- [x] `GET /api/policies/search` finds policies by partial policy number or client name.
- [x] `GET /api/policies/{id}/coverage` returns the coverages for a given policy.
- [x] All four endpoints require authentication and are verifiable directly via Swagger.

## Comments

Implemented: `CauseOfLossCode`, `ClaimStatusTransition`, `Policy`, `PolicyCoverage` entities
(Domain), EF configurations + migration `AddReferenceDataAndPolicies` (Persistence), the
independently re-runnable `Scripts/SeedReferenceData.sql` (embedded + run automatically on every
app start via `ReferenceDataSeeder`, also runnable standalone via sqlcmd), four MediatR query
handlers projecting directly off `IApplicationDbContext` (ADR-0002), and `ReferenceController` /
`PoliciesController`. `RequiredPermission` on `ClaimStatusTransition` is a judgement call where
the FRS doesn't name an explicit role for every transition — documented inline on the entity and
in the seed script. The four new tables are deliberately not organisation-scoped — see
ADR-0009. Covered by 14 new HTTP-seam integration tests (`ReferenceDataTests`, `PoliciesTests`)
against the real SQL Server test container, per spec.md's Testing Decisions.
