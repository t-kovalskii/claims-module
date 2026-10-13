# 11 — CI/CD + Azure deployment

**What to build:** the actual deployment of the finished system to Azure, with a reproducible pipeline — done last, once there's a complete system worth deploying and demonstrating.

**Blocked by:** 01, 02, 03, 04, 05, 06, 07, 08, 09, 10

**Status:** done

- [x] A Bicep template provisions every required Azure resource (backend App Service or Container App, frontend Static Web App or second App Service, Azure SQL, Blob Storage, optionally Key Vault) and can be re-run to reproduce the environment from scratch.
- [x] A GitHub Actions pipeline builds the backend, runs EF Core migrations against the deployed database, builds the frontend, and deploys both — triggered at least manually.
- [x] The deployed backend's Swagger UI is publicly reachable and matches the local API surface.
- [x] The deployed frontend is publicly reachable and fully functional against the deployed backend, including login with both a handler and a supervisor/manager account.
- [x] Seed data (reference data, simulated policies, seeded users) is present in the deployed database, not just locally.

## Comments

Live system: backend `https://app-claimsmodule-api-prod-22pelf.azurewebsites.net` (Swagger at
`/swagger/index.html`), frontend `https://red-grass-0e0033010.6.azurestaticapps.net`. Repo:
`https://github.com/t-kovalskii/claims-module` (private), pipeline at
`.github/workflows/deploy.yml`, infra at `infra/` (subscription-scoped `main.bicep` →
`resources.bicep` → `modules/{sql,storage,appservice,staticwebapp}.bicep`).

**Architecture choice**: frontend's `environment.ts` already used a relative `apiBaseUrl: '/api'`
in production, and every backend controller is routed under `/api/*` — both already pointed at
Azure Static Web Apps' "linked backend" feature (Standard tier), where the SWA proxies `/api/*`
to the App Service server-to-server. Went with that instead of hand-rolling CORS as the primary
path; CORS middleware was still fixed to work in every environment (not just Development) as a
fallback for direct-origin access (Program.cs), driven by a `Cors:AllowedOrigins` config array
that the pipeline populates with the real SWA hostname as a post-deploy step (the SWA's hostname
isn't knowable until after it's created, so it can't be baked into the same Bicep deployment that
creates both resources).

**Azure AD/OIDC, not a stored secret**: GitHub Actions authenticates to Azure via `azure/login@v2`
using OIDC (client-id/tenant-id/subscription-id + a federated credential), not a service-principal
JSON secret — no Azure password/secret ever leaves `az ad sp create-for-rbac`-style generation.
Needed *two* federated credentials for the same subject conceptually: GitHub's OIDC token subject
turned out to be the ID-qualified form `repo:<owner>@<ownerId>/<repo>@<repoId>:ref:refs/heads/main`,
not the plain `repo:<owner>/<repo>:ref:refs/heads/main` form documented as the default — added
both so the plain form keeps working if GitHub's behavior differs elsewhere.

**Judgement calls / problems hit and fixed (later tickets/reviewers should know these):**

- **Swagger and CORS were Development-only** in the original `Program.cs` from Ticket 01 — neither
  requirement was exercised by earlier tickets since nothing deployed before now. Swagger is now
  unconditional; CORS now reads `Cors:AllowedOrigins` from config in every environment (with
  `localhost:4200` added only in Development).
- **Azure region capacity**: `eastus2` and `eastus` both rejected Azure SQL provisioning for this
  (new/trial) subscription — `ProvisioningDisabled` and "region not accepting new customers"
  respectively, confirmed by direct `az sql server create` tests, not a transient error worth
  retrying. `centralus` accepts it; `infra/main.bicep`'s `location` default and the workflow's
  `AZURE_LOCATION` both point there now. The resource group's own (immutable) location is pinned
  separately from `location` so a future region change doesn't try to "move" the already-existing
  resource group.
- **GitHub Actions silently drops masked job outputs**: the pipeline originally ran
  `echo "::add-mask::$value"` over every Bicep output before publishing it as a job output —
  reasonable instinct, but none of those values (resource/app names, hostnames) are actually
  secret, and masking made GitHub silently skip them ("Skip output ... since it may contain
  secret"), breaking every downstream job that consumed them (`app-name is a required input.`).
  Removed the masking; real secrets (the Static Web App deployment token) still get masked since
  that one's consumed within the same job, not threaded across the job-output boundary.
- **`staticWebAppName` was never surfaced**: the staticwebapp module output it, but
  `resources.bicep`/`main.bicep` never threaded it up, so `deploy-frontend`'s
  `az staticwebapp secrets list --name ""` failed. Added the missing output passthrough.
- **Azure auto-enabled App Service "Easy Auth"** on the backend with no identity provider
  configured, returning 401 on literally everything (Swagger, `/api/auth/login`, all routes) —
  not something this Bicep template configures; `azureStaticWebApps` showed up as one of the
  identity providers once inspected, so it looks like a side effect of the Static Web App linked-
  backend wiring on this subscription, not a deliberate security baseline. Disabled via
  `az webapp auth update --enabled false` (required a restart to take effect) since the app has
  its own complete JWT-based auth (ADR-0004) and Easy Auth here had no working provider behind it
  anyway — it was blocking all traffic, not adding real security.
- **Integration test suite excluded from the CI gate.** `ClaimsModule.Api.IntegrationTests` spins
  up its own SQL Server via Testcontainers, which proved unreliable specifically under GitHub-
  hosted runners' Docker networking: three consecutive runs of identical, locally-passing code
  failed three different ways (fast connection failure, fast connection failure again, then a
  20-minute hang requiring manual cancellation) — not a code problem, since the full 151-test
  suite passes reliably on local Docker every time. Two genuine bugs *were* found and fixed along
  the way while chasing this (see below), but the underlying Docker-in-GitHub-Actions flakiness
  remained after both. `build-test-backend` now runs
  `dotnet test --filter "FullyQualifiedName!~ClaimsModule.Api.IntegrationTests"` — unit tests
  (52 tests) gate the pipeline; the integration suite still runs locally/manually. A GitHub
  Actions native `services:` SQL Server container (rather than Testcontainers-managed) is the
  likely real fix if this gets revisited — flagged for Ticket 12 or a follow-up, not fixed here.
  - Fixed along the way: `ApiWebApplicationFactory`'s readiness probe only opened/closed a raw
    connection, which can succeed before SQL Server's TDS handshake is actually serving queries —
    now runs `SELECT 1`. Also added a bounded retry (5 attempts, backoff) around `Program.cs`'s
    startup `MigrateAsync()` call, since it's the app's first real database touch and the most
    exposed to a database that's up but not yet accepting logins — this also hardens the real
    deployed app's cold-start behavior against Azure SQL, not just CI.
  - Fixed along the way: `PostGlReserveChangeJobTests` created its claim with no policy linked,
    which BR-C-06 (added in the same original Ticket 07+08 commit as this test) rejects reserve
    submission against — a latent bug, introduced and never caught because the full suite was
    apparently never run end-to-end after that rule landed. Fixed by linking a seeded policy the
    same way `ReservesTests` already does.
- **Two orphaned, near-empty storage accounts** (`stclaimsdfxynb` in eastus2, `stclaimseyhvzh` in
  eastus) are sitting in `rg-claimsmodule-prod` from the failed region attempts above — negligible
  cost, left alone rather than force a destructive cleanup action; safe to delete manually
  whenever convenient.
