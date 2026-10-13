# Claim numbers generated via a per-org-per-year counter table, not a SQL SEQUENCE

BR-C-04 requires claim numbers unique per organisation, format `CLM-{YYYY}-{7-digit-zero-padded}`, generated atomically with no gaps and no reuse — even for soft-deleted claims. FRS §5.3 allows either a database `SEQUENCE` object or a dedicated counter table with optimistic/atomic concurrency.

We chose a `ClaimNumberCounters(OrganisationId, Year, LastValue)` table, incremented via `UPDATE ... OUTPUT INSERTED.LastValue WHERE OrganisationId = @orgId AND Year = @year` (inserting a new `(org, year)` row starting at 1 if none exists). The sequence resets to 1 at the start of each year, per organisation.

A native SQL Server `SEQUENCE` object is a single global counter and isn't partitionable by an arbitrary key — getting per-organisation uniqueness would mean creating a `SEQUENCE` per org (and per year, to reset), which requires dynamic DDL whenever a new org or year appears. A table keyed by `(OrganisationId, Year)` gives the right partition natively, and the atomic `UPDATE ... OUTPUT` pattern is the FRS's own primary suggested mechanism.
