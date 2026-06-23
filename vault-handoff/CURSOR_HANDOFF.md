# Cursor Handoff

Open this repo in Cursor (on the Windows machine), open the agent panel (Ctrl+L),
and paste the prompt block below.

> Prerequisite: `scripts/bootstrap.ps1` has been run and `dotnet build` + `dotnet test`
> are green. See `README.md` for setup. The contract lives in `docs/openapi.yaml`;
> project rules in `.cursor/rules/` load automatically.

---

```text
You're picking up a seeded handoff package for "Vault" — a custom .NET 8 PDM for Fusion 360 files. The repo already contains the spec, API contract, Cursor rules, a project scaffold, verified domain logic, and a worked reference implementation. Finish it without re-deciding what's already decided.

START HERE
1. Read README.md, TASKS.md, and docs/ (PRD.md, data-model.md, openapi.yaml). The files in .cursor/rules/ load automatically — follow them.
2. If not already done, run scripts/bootstrap.ps1 and confirm `dotnet build` and `dotnet test` are GREEN before writing any code. Report the result.

HARD RULES
- docs/openapi.yaml is the source of truth for all 15 endpoints. Build to it; if it's wrong, fix the contract first, then code. Freeze it before any parallel work.
- Do NOT reimplement or modify the verified domain logic in Vault.Domain (RevisionSequence, FileStateMachine, FileNumber, FileNaming, ApprovalPolicy, ProductionPolicy). Use it.
- VaultService is the worked pattern (upload, check-out, check-in, submit, approve+snapshot, reject, begin-change, obsolete, reactivate). Copy that pattern.
- Integrations stay behind IFileStore/IAuthProvider/INotificationService with stub defaults; no secrets in source.
- Surface domain exceptions as the documented HTTP codes (400/403/404/409/422).
- All .NET. The WPF app and Explorer client consume Vault.Client (VaultApiClient); the Explorer client is Windows-only; there is no web UI.

THEN WORK TASKS.md IN ORDER
- Phase 1: confirm entities/DbContext, create the EF migration (`dotnet ef migrations add Init`), freeze the contract.
- Phase 2: implement the four methods that throw NotImplementedException in VaultService (AuthenticateGoogle, Search, ListRevisions, Rollback) following their in-code TODOs. Add integration tests for the 400/403/404/409/422 cases.
- Phase 3 (Windows): WPF app — data card, search, approval queue, admin, rollback — via Vault.Client. No business logic, no direct DB access.
- Phase 4 (Windows): Explorer client with the local cache / working-copy model described in TASKS.md and PRD §4a.

Confirm build/test pass first, then propose your Phase 1 plan before editing. Ask me before changing any decision recorded in PRD.md.
```
