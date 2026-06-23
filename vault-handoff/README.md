# Vault — Custom PDM for Fusion 360 Files

A custom Product Data Management system for Fusion 360 files with an ISO-style
approval workflow: Windows Explorer integration, Google sign-in, check-in/out
enforcement, revision control (skips I and L), a metadata data card, Admin
rollback with hidden backups, and Asana notifications.

This is a **Cursor handoff bundle**: spec, API contract, Cursor rules, a scaffold,
and seeded + verified domain logic. Cursor's agents build the rest.

## Stack (decided)
ASP.NET Core (.NET 8, Minimal APIs) · `Vault.Domain` class library · PostgreSQL
via EF Core (Npgsql) · local NAS storage behind `IFileStore` (S3 swappable) ·
Google OAuth behind `IAuthProvider` (stub default) · Asana behind
`INotificationService` (stub default) · **WPF desktop app** (workflow/admin/search) ·
C#/WinFsp Explorer client (Windows-only) · xUnit.

Three deliverables, all .NET: **API** (this repo) · **WPF app** · **Explorer client**.
There is no web UI — approvers and managers use the WPF app. The WPF app and Explorer
client both consume **`Vault.Client`** (a typed `VaultApiClient`) over **`Vault.Contracts`**
(shared DTOs), so neither hand-rolls HTTP or redeclares types.

## Architecture (decided)
Client/server, not desktop-direct-to-DB. The **API + PostgreSQL** are the single
source of truth and the only place workflow rules are enforced (check-out locking,
unique name, state transitions, snapshot-on-approve, audit). The **WPF app** and
**Explorer client** are thin clients over the REST API and never touch the database
directly. Host the API on a NAS box or small VM on the LAN.

The backend, domain, and tests are cross-platform. The WPF app and Explorer client need Windows.

## Layout
```
vault/
├─ README.md
├─ TASKS.md                      ← phased plan for Cursor agents
├─ docs/
│  ├─ PRD.md                     ← requirements (all decisions captured)
│  ├─ data-model.md              ← entities, state machine, revision rule, backups
│  ├─ openapi.yaml               ← 20-endpoint API contract (build to this first)
│  ├─ workflow.mmd               ← Mermaid state diagram (kept in sync with the workflow)
│  ├─ infrastructure.md          ← server/NAS hardware spec, scale path, Desktop Connector boundary
│  └─ ui-commands.md             ← state→button→endpoint→role map for the UI
├─ .cursor/rules/                ← auto-applied rules (conventions + domain logic)
├─ scripts/bootstrap.ps1         ← scaffolds the solution, then copies seed/ over it
├─ deploy/                       ← Docker deploy for a NAS (Dockerfile, compose, README)
├─ CURSOR_HANDOFF.md             ← prompt to paste into Cursor
├─ seed/                         ← seeded source, copied into the projects by bootstrap
│  ├─ src/Vault.Domain/          ← VERIFIED revision + state machine + number/naming/approval/production policy
│  ├─ src/Vault.Contracts/       ← shared DTOs (used by API + clients)
│  ├─ src/Vault.Infrastructure/  ← VaultDbContext (EF Core) + IFileStore/IAuthProvider/INotificationService stubs
│  ├─ src/Vault.Api/             ← Program.cs (20 endpoints) + IVaultService + VaultService reference impl
│  ├─ src/Vault.Client/          ← VaultApiClient (typed client over all endpoints) for WPF + Explorer
│  └─ tests/Vault.Tests/         ← xUnit tests for the seeded logic
├─ .env.example
├─ appsettings.Development.json
└─ .gitignore
```

## Setup (Windows dev box, PowerShell, repo root)
### 1. Prerequisites
```powershell
winget install Microsoft.DotNet.SDK.8
winget install PostgreSQL.PostgreSQL.16
dotnet --version    # expect 8.x
psql --version      # expect 16.x
```
### 2. Scaffold + build + test
```powershell
.\scripts\bootstrap.ps1
```
This creates the solution, copies the seeded source over the templates, builds,
and runs the seeded tests. Expect a green build and passing tests.

### 3. Database
```powershell
psql -U postgres -c "CREATE DATABASE vault;"
```
### 4. Run
```powershell
dotnet run --project src/Vault.Api
# open the Swagger UI it prints (https://localhost:7xxx/swagger)
# endpoints return 501 until IVaultService is implemented (that is Cursor's Phase 2)
```

## Handing to Cursor
1. Open the repo in Cursor **on the Windows machine**.
2. Paste the prompt in **`CURSOR_HANDOFF.md`** into the Cursor agent panel (Ctrl+L).
3. `.cursor/rules/` loads automatically (revision rule, state machine, number/naming,
   approval segregation, rollback).
4. Work `TASKS.md`. **Freeze `docs/openapi.yaml` before any parallel work.**
5. One git worktree per component (`api`, `wpf`, `explorer-client`); keep
   `explorer-client` on Windows for filesystem testing.

## Deployment
The **API + PostgreSQL** run as containers on an always-on LAN host (mini-PC, an existing
Windows PC with Docker Desktop, or a small Linux box). The **UniFi NAS** holds the file
blobs and is mounted into the API over **NFS** (the UniFi UNAS can't run Docker itself).
Clients point at the host. The API runs `Database.Migrate()` on startup, so the schema
self-provisions once the EF migration exists. Full step-by-step (UniFi NFS export, TLS,
backups) is in `deploy/README.md`. Server/NAS **hardware sizing, the scale path, and the
Autodesk Desktop Connector boundary** are in `docs/infrastructure.md`.

## Not in this bundle (by design)
- **Secrets** — Google + Asana credentials go in env/user-secrets. See `.env.example`.
- **Explorer client code** — Phase 4, Windows-bound; design + sequencing are in `TASKS.md`.
- **IVaultService implementation** — intentionally a stub; that is the core of Cursor's Phase 2.

## What is verified vs scaffold
- **Verified (by simulation):** the pure domain logic (revision skip-I/L, file-number
  charset, name normalization, approval segregation, checkout-able states, production-exit
  gating) *and* the `VaultService` lifecycle — upload → check-out → submit →
  approve(+snapshot) → reject, plus begin-change / obsolete / reactivate — checked end to
  end (revisions A→B→C, one hidden snapshot per production revision, engineer self-approval
  blocked, admin allowed, reject routing, checkout matrix, repeatable check-in).
- **Scaffold (compile on first build):** the C# for entities, DbContext, integration
  stubs, the API/service wiring, and the `Vault.Client` typed client is standard but was
  not compiled in the authoring environment. Run `.\scripts\bootstrap.ps1`; `dotnet build`
  surfaces any minor fix-ups immediately, and `dotnet test` confirms the domain logic.

## Reference implementation
`VaultService` (registered by default) fully implements upload, check-out (with the
checkout-able guard), check-in (working-copy upload), submit, approve-with-snapshot,
reject, begin-change, obsolete, and reactivate as the worked pattern. `AuthenticateGoogle`,
`Search`, `ListRevisions`, and `Rollback` throw 501 with a TODO describing exactly how to
implement them. Integration ports (`IFileStore`, `IAuthProvider`, `INotificationService`)
ship with local/stub implementations so the app runs with no secrets. `Vault.Client`
exposes a typed `VaultApiClient` over all 20 endpoints for the WPF app and Explorer client.
