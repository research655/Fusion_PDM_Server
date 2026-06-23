# Vault — build plan for Cursor agents

Work top to bottom. **Phase 1 must be frozen before any parallel work.**
Seeded code (domain logic, entities, DbContext, API scaffold) is already in `seed/`
and copied into the projects by `scripts/bootstrap.ps1`.

## Phase 0 — Fusion feasibility spike  (GO / NO-GO — do this FIRST)
The PDM mechanics are built; the risk is whether Fusion 360 fits a local-vault model.
Prove this before investing in the WPF/Explorer clients or any documentation vault.
- [ ] **File round-trip:** get a real Fusion design out as `.f3d`/`.f3z`, into the vault (upload/check-in), back out, and re-opened in Fusion intact. Fusion is cloud-native — `.f3d` is an *export*, not its natural source of truth.
- [ ] **Assembly references / where-used:** take an **assembly with referenced components + a drawing**; confirm check-out → edit → check-in and read-only viewing keep the reference graph intact. The current model treats files independently (see PRD §18) — decide if a `FileReference` (parent/child + version) table is needed.
- [ ] **Desktop Connector boundary:** ADC mirrors *Autodesk cloud* to Explorer and caches on C: only — it cannot point at the NAS. Vault is a **separate Explorer mount** over the NAS. Define the workflow boundary: design in Fusion (cloud/ADC) → bring into Vault at controlled release. Do **not** try to integrate with or replace ADC.
- [ ] **Assembler derivatives:** confirm the release step can publish a **drawing PDF** + a **3D web-viewer** link (Autodesk's free viewer) so ~30 assemblers view released work without Fusion seats (PRD §17).
- [ ] **Decision:** record go/no-go. If references can't be made to survive, that's the signal to stop before expanding.

## Phase 0a — Scaffold
- [ ] Run `scripts/bootstrap.ps1`. Confirm `dotnet build` and `dotnet test` pass (seeded domain tests are green).

## Phase 1 — Contract + domain  (single agent, no parallelism)
- [ ] Review `docs/openapi.yaml` (20 endpoints). Treat as the frozen contract.
- [ ] Extend `FileStateMachine` tests for full state coverage (all triggers, all states).
- [ ] Finalize EF entities + `VaultDbContext` (seeded); create the initial migration (`dotnet ef migrations add Init`).
- [ ] Define interfaces `IFileStore`, `IAuthProvider`, `INotificationService` with stub implementations.
- [ ] **Freeze the contract.** Commit/tag.

## Phase 2 — Backend (implement IVaultService)  (parallelizable after Phase 1)
- [x] Reference `VaultService` implements upload, check-out (with checkout-able guard), check-in, submit, approve(+snapshot), reject, begin-change, and obsolete. Use it as the pattern.
- [ ] Integration ports `IFileStore`/`IAuthProvider`/`INotificationService` exist with local/stub impls; build the real ones in Phase 5.
- [ ] Implement the remaining 501 methods following the in-code TODOs:
- [ ] `SearchAsync`: IQueryable over `_db.Files.Include(f => f.Repository)` with the contract filters; scope to one vault (resolve `query.Repository`, default `CAD`); apply read visibility (role `User` → Production only, see `FileVisibility`); never return backups.
- [ ] Integration tests per endpoint incl. the 400/403/409/422 cases.

## Phase 2b — Rollback (Admin)
- [ ] `GET /files/{id}/revisions` — Admin only (403 otherwise); list snapshots as rollback targets.
- [ ] `POST /files/rollback` — Admin only; copy snapshot binary to a new live key, restore revision + data-card fields, set Production, append history `Rolled back to {rev} by {admin}`.

## Phase 3 — WPF desktop app  (parallel worktree, WINDOWS ONLY)
- [ ] WPF (.NET 8) app, thin client over the REST API — NO business logic, NO direct DB access.
- [ ] Screens: data card (all fields + history log), metadata search, approval queue (approve/reject + reason), Admin user/role management, Admin rollback (list archived revisions, confirm).
- [ ] Reference `Vault.Client`; use `VaultApiClient` for all calls. Catch `VaultApiException` and surface `StatusCode` as clear messages (409 conflict, 422 invalid action, 403 not allowed).

## Phase 4 — Explorer client  (parallel worktree, WINDOWS ONLY)
- [ ] Local cache layer with two modes:
  - **Working copy** (checked out by this user): persistent, writable; on check-in, POST the bytes to `/files/check-in`.
  - **Read-only cache** (not checked out / held by someone else): hydrate from `GET /files/{id}/content`, mark read-only; never write back. User keeps edits only via Save As to a personal path.
- [ ] Cache invalidation: track `contentVersion`; re-hydrate a read-only copy when the server's value differs.
- [ ] On checkout, hydrate a fresh working copy from the vault (don't trust a prior temp copy).
- [ ] Sync client first: watched folder mirrored to the API; OS read-only attribute on non-checked-out files; prove the full loop.
- [ ] Then evaluate WinFsp for a virtual-drive UX (files-on-demand). Validate with its memfs sample first.
- [ ] Map check-out → writable working copy, check-in → upload + read-only, new file → auto check-out + upload.

## Phase 5 — Go-live integrations
- [ ] Real `IAuthProvider` (Google OAuth; restrict to @sparkrobotic.com).
- [ ] Real `INotificationService` (Asana).
- [ ] Optional `IFileStore` S3 implementation.

## Parallelization
- One worktree per component: `api`, `web`, `explorer-client`. Keep `explorer-client` on Windows.
- Shared contract = `docs/openapi.yaml`. Any change pauses parallel work until re-frozen.
