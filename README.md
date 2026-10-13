# Claims Module

FNOL intake and reserve management — a bounded vertical slice of a larger insurance Policy
Administration System. See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the design,
[`AI-WORKFLOW.md`](AI-WORKFLOW.md) for how it was built, and [`CONTEXT.md`](CONTEXT.md) for domain
terminology.

**Live deployment**: backend Swagger at
[`https://app-claimsmodule-api-prod-22pelf.azurewebsites.net/swagger/index.html`](https://app-claimsmodule-api-prod-22pelf.azurewebsites.net/swagger/index.html),
frontend at [`https://red-grass-0e0033010.6.azurestaticapps.net`](https://red-grass-0e0033010.6.azurestaticapps.net).
Log in with any seeded username, no password: `handler1`, `supervisor1`, or `manager1`.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) (for local SQL Server, and for the integration test suite's
  Testcontainers-managed instance)
- [Angular CLI](https://angular.dev/tools/cli) (`npm install -g @angular/cli`), or just use the
  project's local `npx ng`
- The `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

## Local setup

**1. Start SQL Server:**

```bash
docker compose up -d
```

This starts SQL Server 2022 on `localhost:14330` (SA password `ClaimsModule!2024`, see
`docker-compose.yml`) — the same engine version as the deployed Azure SQL target.

**2. Run the backend:**

```bash
cd src/backend
dotnet run --project ClaimsModule.Api --launch-profile https
```

This listens on `https://localhost:7050` (and `http://localhost:5121`). **Migrations and seed
data run automatically on every startup** — no separate step needed locally: `Program.cs` calls
`context.Database.MigrateAsync()`, then seeds the three mock-auth users (`handler1`/`supervisor1`/
`manager1`, one each of Handler/Supervisor/Manager role) and the reference data + simulated
policies (cause-of-loss codes, claim-status transitions, policies and coverages — including the
FRS's own expired-policy example, `POL-2024-001001`, used to exercise the loss-date-outside-
policy-period warning) via an idempotent, re-runnable SQL script
(`ClaimsModule.Persistence/Scripts/SeedReferenceData.sql`). Swagger is available at
`https://localhost:7050/swagger`.

**3. Run the frontend:**

```bash
cd src/frontend/claims-module-web
npm ci
npm start   # ng serve, http://localhost:4200
```

The dev build's `environment.development.ts` points `apiBaseUrl` at `https://localhost:7050/api`
directly (no proxy needed locally — CORS is open for `http://localhost:4200` in Development).

### Running migrations manually

Not needed for normal local development (see above), but if you need to generate or apply a
migration explicitly:

```bash
cd src/backend
dotnet ef database update \
  --project ClaimsModule.Persistence --startup-project ClaimsModule.Api
# add a new migration:
dotnet ef migrations add <Name> \
  --project ClaimsModule.Persistence --startup-project ClaimsModule.Api
```

### Running the seed script manually

Also not needed locally (it runs automatically), but to run it directly against a database, e.g.
via `sqlcmd` or Azure Data Studio: `src/backend/ClaimsModule.Persistence/Scripts/SeedReferenceData.sql`.
Every `INSERT` is guarded by a `WHERE NOT EXISTS` check plus a `TRY/CATCH` around the unique-key
violation, so re-running it against a database that already has the data is a safe no-op.

### Running tests

```bash
cd src/backend
dotnet test ClaimsModule.sln -c Release
```

The integration test suite (`ClaimsModule.Api.IntegrationTests`) spins up its own disposable SQL
Server container via Testcontainers — Docker must be running, but nothing else needs to be set up
first. (CI only gates on the unit test suites; see the note in `.github/workflows/deploy.yml` and
[Ticket 11](.scratch/claims-module/issues/11-cicd-azure-deployment.md)'s Comments for why.)

Frontend:

```bash
cd src/frontend/claims-module-web
npm test
```

## Configuration reference

Backend configuration (`src/backend/ClaimsModule.Api/appsettings.json`, overridable via
`appsettings.{Environment}.json`, environment variables with `__` as the section separator, or —
in Azure — App Service application settings):

| Key | Purpose | Local default |
|---|---|---|
| `ConnectionStrings:ClaimsModuleDb` | SQL Server connection string (also used for Hangfire's job storage) | points at the `docker-compose` instance |
| `Jwt:SecretKey` | Symmetric key signing issued JWTs | set in `appsettings.Development.json` only — **must** be provided in any other environment |
| `Jwt:Issuer` / `Jwt:Audience` | JWT validation parameters | `ClaimsModule.Api` / `ClaimsModule.Clients` |
| `Jwt:ExpiryMinutes` | Token lifetime | `60` |
| `Storage:Provider` | `Local` or `AzureBlob` | `Local` |
| `Storage:Local:RootPath` / `Storage:Local:PublicBaseUrl` / `Storage:Local:SigningKey` | Local-filesystem document storage (used when `Storage:Provider=Local`) | `App_Data/documents`, `https://localhost:7050`, dev-only key |
| `Storage:AzureBlob:ConnectionString` / `Storage:AzureBlob:ContainerName` | Blob storage (used when `Storage:Provider=AzureBlob`) | unset locally |
| `Cors:AllowedOrigins` | Array of allowed CORS origins (in addition to `localhost:4200`, which is added automatically in Development only) | empty |

Frontend configuration (`src/frontend/claims-module-web/src/environments/`):

| File | `apiBaseUrl` |
|---|---|
| `environment.development.ts` | `https://localhost:7050/api` |
| `environment.ts` (production build) | `/api` — relies on the Static Web App's linked-backend proxy in the deployed environment; see `ARCHITECTURE.md`'s Azure architecture section |

## Reproducing the Azure deployment

The whole deployment is one re-runnable Bicep template plus a GitHub Actions pipeline — see
[`infra/`](infra/) and [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml). To stand up
your own instance from scratch:

1. **Azure**: `az login` to a subscription you control.
2. **GitHub**: create a repo and push this code; `gh auth login` if using the GitHub CLI.
3. **Azure AD app + OIDC federated credential** (no stored Azure secret — `azure/login@v2`
   authenticates via OIDC):
   ```bash
   az ad app create --display-name "<name>"                      # note the appId
   az ad sp create --id <appId>
   az role assignment create --assignee-object-id <sp-object-id> \
     --assignee-principal-type ServicePrincipal --role Contributor \
     --scope /subscriptions/<subscription-id>
   az ad app federated-credential create --id <appId> --parameters '{
     "name": "github-main-branch",
     "issuer": "https://token.actions.githubusercontent.com",
     "subject": "repo:<owner>/<repo>:ref:refs/heads/main",
     "audiences": ["api://AzureADTokenExchange"]
   }'
   ```
   GitHub's actual OIDC token subject may use the ID-qualified form instead
   (`repo:<owner>@<ownerId>/<repo>@<repoId>:ref:refs/heads/main`) — if `azure/login` fails with
   `AADSTS700213: No matching federated identity record found`, add a second federated credential
   with the subject from the error message.
4. **GitHub Actions secrets** (`gh secret set <NAME> -R <owner>/<repo>`):
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (from step 3),
   `SQL_ADMIN_LOGIN` (any value, default `claimsadmin`), `SQL_ADMIN_PASSWORD` and
   `JWT_SECRET_KEY` (generate strong random values — e.g. `openssl rand -base64 32`).
5. **Push to `main`** (or run the workflow manually via `gh workflow run` /
   the Actions tab — it's also `workflow_dispatch`-triggered). The pipeline builds and tests the
   backend, builds the frontend, deploys `infra/main.bicep` (idempotent — safe to re-run),
   publishes the backend, applies EF Core migrations against the live database, and deploys the
   frontend.
6. **Region note**: `infra/main.bicep`'s `location` parameter defaults to `centralus`. A brand
   new/trial Azure subscription can reject SQL provisioning in some regions
   (`ProvisioningDisabled` or "region not accepting new customers") — if that happens, pick a
   different region for both the Bicep `location` parameter and the workflow's `AZURE_LOCATION`
   env var; the resource group's own location is pinned separately so changing it doesn't try to
   "move" an already-existing resource group.

See [Ticket 11's Comments](.scratch/claims-module/issues/11-cicd-azure-deployment.md) for the full
list of problems hit reproducing this the first time and how each was resolved.
