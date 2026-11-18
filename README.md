# ArchLens

Own your architecture as a **C4 model**, navigate between C4 levels, keep **immutable versions** and
**compare versions visually** with a stable layout. A deterministic (no LLM) semantic diff and a
docker-compose drift check (rule R1) tell you what changed and which views it touches.

## Core is the single source of truth

Everything about the model is defined and computed **only in .NET**: its shape, validation, derived C4 views,
the semantic diff, the compare graph, the compose draft and drift. The web app holds only UI state (the unsaved
snapshot being edited, selection, viewport) and renders what Core returns: after each edit it asks Core for the
views (`POST /views`) and the problems (`POST /validate`); the compare view draws Core's compare graph as is.

The HTTP contract is **generated from Core's code** (code-first):

| Artifact | Produced by |
|---|---|
| `packages/contracts/openapi.json` | `dotnet build services/ArchLens.Core` (ASP.NET OpenAPI + `Microsoft.Extensions.ApiDescription.Server`) |
| `packages/contracts/model.schema.json` | `tools/ArchLens.SchemaExport` (`JsonSchemaExporter` over the Domain types) |
| `packages/contracts/generated/ts/` | [Hey API](https://heyapi.dev) from `openapi.json`: TypeScript types, zod schemas and a fetch SDK |

Regenerate all of them with `pnpm contracts:gen`. The BFF validates its inputs with the generated zod schemas, so
request rules (required members, lengths, formats) come from Core's records. Nothing in `packages/contracts` is edited by hand; CI regenerates
it and fails on `git diff`.

## Repository layout

| Path | What |
|---|---|
| `services/ArchLens.Domain` | Pure library: model, validation, views, semantic diff, compare graph, compose draft, drift R1 |
| `services/ArchLens.Core` | ASP.NET Core minimal API over Domain: workspaces, models, immutable versions (Postgres), diff, views, compare, drafts |
| `apps/web` | TanStack Start app: UI (React Flow canvas, ELK in a web worker) and the **BFF**; Better Auth (dev login / GitHub) |
| `packages/contracts` | Generated contract artifacts (see above) |
| `tools/ArchLens.SchemaExport` | Writes `model.schema.json` from the Domain types |
| `tests/` | xUnit: Domain unit tests, Core integration tests (Testcontainers Postgres) |
| `reference-models/shopflow` | ShopFlow base/head snapshots (with a hand-tuned layout) and compose files; the hand-checked expected diff is a Verify snapshot in `tests/ArchLens.Domain.Tests` |
| `architecture/archlens.model.json` | ArchLens's own architecture as an ArchLens model |

The browser talks only to the web app. The web server calls Core server-side with a short-lived HS256 JWT it mints
per request (`iss=archlens-web`, `aud=archlens-core`, `sub` = user id, 5 min). Core authenticates every request
except `/health`, `/openapi/*` and `/docs`, rejects tokens without `sub` or living longer than 5 minutes, and
answers 404 for resources owned by someone else.

## Prerequisites

- Node 24 and pnpm 10 (`corepack enable`)
- .NET SDK 10.0.404+ (`global.json`)
- Docker (Postgres, Testcontainers, and the full stack)

## Run

Everything in containers (http://localhost:3000):

```bash
cp .env.example .env
docker compose up --build
```

Or from source (Core and web with hot reload), in 4 commands:

```bash
pnpm install
cp .env.example .env && cp apps/web/.env.example apps/web/.env.local && docker compose up -d postgres
pnpm core:dev        # Core on http://localhost:5080 (applies migrations in Development)
pnpm dev             # web on http://localhost:3000
```

The database schema is one migration. If your local Postgres volume was created by an earlier build, reset it once
with `docker compose down -v`.

### Configuration and secrets

All secrets in `.env.example`, `apps/web/.env.example` and `appsettings.Development.json` are **for local use only**.
Never commit `.env` or `.env.local`.

- **Sign-in.** The dev login ("Sign in as developer") is the default: `ARCHLENS_DEV_LOGIN=true`. It is the only
  switch, and the web app refuses to start with it unless `BETTER_AUTH_URL` is localhost.
- **GitHub sign-in** is wired but not configured: the OAuth App is not created yet. To enable it later, create a
  GitHub OAuth App with callback `http://localhost:3000/api/auth/callback/github` and set `GITHUB_CLIENT_ID` and
  `GITHUB_CLIENT_SECRET` (in `apps/web/.env.local`, or in `.env` for Docker).
- `ARCHLENS_JWT_SECRET` must be the same for web and Core (at least 32 characters). In Core it maps onto the
  `ArchLens:Jwt:Secret` option, validated at startup: Core refuses to start without it.
- `GET /health` is an ASP.NET health check that includes the Postgres connection.
- The web app parses its configuration at startup and refuses to start when it is invalid.

## API docs

- Served by Core: `http://localhost:5080/openapi/v1.json`, Scalar UI at **`http://localhost:5080/docs`**
- The same document, committed: [`packages/contracts/openapi.json`](packages/contracts/openapi.json)

## Tests and reports

Snapshot tests use [Verify](https://github.com/VerifyTests/Verify): the expected outputs (the ShopFlow diff, the compare
graph, the served OpenAPI document) are committed `*.verified.*` files next to the tests. A changed
output fails the test and writes a `*.received.*` file; review it and rename it to `.verified.` to accept the change.

```bash
dotnet test ArchLens.slnx          # Domain unit + Core integration (needs Docker)
pnpm -C apps/web test             # Vitest: editor command reducer, canvas graph helpers
pnpm test:report                  # writes artifacts/test-results/: results_*.trx, results_*.html, web-junit.xml
```

`artifacts/` is git-ignored. CI (`.github/workflows/ci.yml`) runs three jobs: contracts (regenerate + diff), core
(build + all .NET tests, uploads `artifacts/test-results`), web (typecheck, lint, format check, Vitest, build).

## Demo scenario (checklist)

1. Open http://localhost:3000 and click **Sign in as developer**.
2. **Create workspace** "hell yeah" → **From ShopFlow template** → version 1 (the template carries a hand-tuned layout).
3. The canvas shows the context level. Double-click ShopFlow → containers; double-click Order Service → components.
   The breadcrumbs go back up.
4. Edit: rename "Mobile App" and change its technology; add container "Cache" (Redis); drag from API Gateway's right
   handle to Cache to add a relationship; select the Notification Service → Mobile App edge and delete it. Drag nodes;
   **Auto layout** re-lays out the current level. Problems found by Core appear in the side panel.
5. **Save version** with a message → v2 (immutable: no update/delete endpoint, and a Postgres trigger rejects
   `UPDATE`/`DELETE`).
6. **History** → pick v1 and v2 → **Compare**: Core's compare graph with v1 positions, added green, removed red
   dashed, modified amber with "was: …"; the panel groups the changes and lists the affected views with reasons.
   The rename is one modified element.
7. **Import from docker-compose** → paste `reference-models/shopflow/compose-drift.yml`, target ShopFlow → the draft in
   the same compare view plus the R1 panel ("service 'worker' is missing from the model") → **Accept draft** → v3.
8. **Export** downloads the version JSON; on the workspace page **Import model** of that file creates a new model
   with the same content.
9. API docs at http://localhost:5080/docs.


## Decisions worth knowing

- **Opaque ids.** New elements and relationships get UUIDs (the web generates them for edits, Core for compose
  drafts); imported and reference models keep their own ids (e.g. DSL ids like `apiGateway`). Ids are never derived
  from names, so a rename is always one modification.
- **The diff ignores `properties`** (e.g. `composeService`) and `layout`: they do not change what the model says.
- **Compose-created containers are tagged `deployed`**: they come from a deployment manifest, so rule R1 expects a
  service for them from then on.
- **The container view lifts other systems' containers to their system**: only this system's containers are opened
  in its container view; a neighbour inside another system appears as that system.
- **Compare positions**: base layout for unchanged, modified and removed elements; head layout for new ones unless it
  would cover a base box; otherwise the client places the node (ELK) without moving positioned ones.
- Accepting a draft accepts all of it; selective acceptance and highlighting changes on all C4 levels at once are
  planned for 09.12. Compose analysis runs synchronously inside Core.
