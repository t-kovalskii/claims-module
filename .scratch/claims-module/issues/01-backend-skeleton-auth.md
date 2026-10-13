# 01 — Backend walking skeleton + auth

**What to build:** the full backend foundation — Clean Architecture project layout, database connectivity, structured logging, consistent error handling, and real-but-mocked authentication — so every later ticket has somewhere to plug in. Verifiable the moment someone can log in via the API and successfully call a protected endpoint with the returned token.

**Blocked by:** None — can start immediately

**Status:** ready-for-agent

- [ ] The five Clean Architecture projects (Domain, Application, Infrastructure, Persistence, API) exist with correct dependency direction — Domain has no outward dependencies, Application depends only on Domain.
- [ ] A SQL Server 2022 instance runs locally via Docker Compose, and an initial EF Core migration applies cleanly against it.
- [ ] MediatR is wired with pipeline behaviours for FluentValidation and structured logging (Serilog), applied uniformly to every command and query, not configured per-handler.
- [ ] Unhandled exceptions, validation failures, and not-found conditions all return RFC 7807 `ProblemDetails` responses via a global exception handler, with no leaked internals on 500s.
- [ ] Hangfire is wired against the same SQL Server instance with its dashboard reachable locally; no jobs are defined yet.
- [ ] A login endpoint issues a signed JWT carrying role and organisation claims for each of three seeded users (handler, supervisor, manager), with no real password verification.
- [ ] An example protected endpoint rejects requests with no token or the wrong role, and succeeds for a correctly-scoped token.
- [ ] The data model conventions (GUID PKs, `DECIMAL(19,4)` monetary fields, `DATETIMEOFFSET(7)` timestamps, soft delete, audit columns, organisation isolation via a global query filter, row-version concurrency) are established at the EF Core configuration level, ready for entities to adopt as they're added.
