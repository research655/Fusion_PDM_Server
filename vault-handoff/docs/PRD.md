# Vault PDM — Product Requirements

Source: "Vault PDM System – Technical Handoff Document", plus decisions captured
during handoff. **[decided]** = resolved; build to it.

## 1. Purpose
Custom PDM for Fusion 360 files with an ISO-style approval workflow, integrated
into Windows Explorer, for Spark Robotic (`@sparkrobotic.com`).

## 2. Roles & permissions
- **Admin** — full access incl. user management; may roll back files; may approve own work.
- **Engineer** — full file access; no user management; may NOT approve own work.
- **User** — read-only. This is the **assembler** role (target: ~30 users vs. <10 engineers). Read-only Users see **Production (released) and Prototype files only** — never work-in-progress, rejected, or obsolete revisions (so no one builds from an unreleased or superseded drawing). Enforced on every read path (card, content, search) via `FileVisibility`; non-visible files return 404 to a User. **[decided]**

## 3. File behavior
- Filenames unique across the Vault — **[decided]** comparison is **case-insensitive** and **excludes the extension** (so `Bracket.f3d` and `bracket.step` collide). See `FileNaming.ToKey`.
- Files read-only unless checked out. New files auto-check-out to creator.
- May be copied, "saved as new", printed.

## 4. Check-in / check-out
- Editing requires check-out. Checked-in = read-only. One holder at a time (409 if held).
- **Checkout is allowed only when the file is Initial or Under Change.** Rejected files are NOT directly checkout-able — **return** a rejected file to Initial / Under Change first (see §7a), then check out. Other states (Awaiting Approval, Production, Obsolete, Prototype) are not checkout-able (422). **[decided]**
- **Check-out/check-in may be repeated** within an editing cycle. Each check-in uploads the working copy to the vault (so progress is never stranded in local cache and the user can resume from any machine), but the file stays in its editable state and revision is unchanged — **nothing becomes permanent until approved**. **[decided]**

## 4a. Local cache & working-copy model **[decided]**
- Opening a file always serves a copy on the user's machine — never the vault file directly.
- **Checked out by you → persistent working copy.** Edits stay local; the vault version is unchanged until check-in. **Check-in uploads the working copy as the new vault content; revision does NOT change** (only approval changes revision). Each check-in bumps `contentVersion`.
- **Not checked out → read-only temporary cache.** The user can open and modify it locally, but to keep changes they must **Save As** to a personal location; the vault file is never modified and the temp copy is discarded.
- **Server guarantee:** only the check-out holder may change vault content. Check-in (or any content write) by a non-holder → HTTP 403.
- **Cache invalidation:** clients compare `contentVersion` and re-hydrate a read-only cached copy when it differs. `GET /files/{id}/content` returns the current bytes.

## 5. Number field **[decided]**
- User-entered, free-form, no format rules — but only **letters, numbers, spaces, hyphens, and underscores** are allowed. Non-empty. See `FileNumber`.

## 6. File states
`Initial`, `Awaiting Approval`, `Production`, `Under Change`, `Initial, Rejected`,
`Under Change, Rejected`, `Obsolete`, `Prototype`. Authoritative machine in `data-model.md` / `FileState.cs`.

## 6a. Prototype status **[decided]**
A **pseudo-Production** status for one-off, non-production items — test cables, test fixtures, rapid prototypes, special tooling. Purpose: let everyone view these without the overhead of formal release.
- **Only never-approved files** may become Prototype. Never-approved files live in `Initial`, so Prototype branches off `Initial` (`POST /files/prototype`); the file must be **checked in** first. Engineer/Admin only, **no approval by a second person**.
- **Toggles freely** back to `Initial` (`POST /files/prototype/exit`) and forward again, as many times as needed — Engineers/Admins only.
- **No revision** is assigned (Initial files have none) and **no history rows** are written for these transitions (intentionally lightweight to speed prototyping).
- **Visible to every user**, including read-only assemblers (same as Production). Enforced by `FileVisibility`.
- Not checkout-able directly: exit to `Initial` first, then check out.
- To later productionize a prototype: exit to Initial, then submit → approve as normal.

## 7. Workflow
- New → Initial → Awaiting Approval → Production.
- Production → Under Change → Awaiting Approval → Production.
- **Only Admins or Engineers may move a file out of Production** — to Under Change (begin change) or to Obsolete. Users cannot (403). **[decided]**
- **Obsolete may be entered from Production OR Prototype**, by any Engineer/Admin. The pre-obsolete state is recorded (`ObsoletedFromState`). **[decided]**
- **Recover (Obsolete → exact prior state): Admin only.** Restores the file to the state it held when obsoleted — Production or Prototype. Engineers cannot recover (403 otherwise). **[decided]**
- "Begin change" is a state action (Production → Under Change); the file is then checkout-able.
- Rejection routes by origin track (initial → Initial,Rejected; change → Under Change,Rejected).

## 7a. Editing, return-to-editable, data card, and force-check-in **[decided]**
- **Write access = Engineer or Admin.** Read-only Users cannot check out, check in, or submit (403). See `WriteAccessPolicy`.
- **Check-in/out is repeatable** while a file is in `Initial` or `Under Change`. Best practice: **check in frequently** so work lives in the Vault, not only in a user's local cache (guards against weekend/internet interruptions). Check-in does not change state or revision.
- **Any checked-in file can be checked out by any write-access user.** While checked out, **only the holder** may edit/check in (others get 409 on checkout, 403 on check-in).
- **A file must be checked out BEFORE it is opened.** Check-out and check-in must **fail if the file is currently open** in an application. (Client-enforced — the API cannot see an OS file handle.)
- **Submit requires the file to be checked in** (422 otherwise). Submit is allowed from `Initial`, `Under Change`, and either rejected state (resubmit as-is). Endpoint `POST /files/submit-approval`.
- **Back to Initial / Under Change (`POST /files/return-to-editable`)** — one action, two uses:
  - **Undo submit:** the **submitter** pulls their own file back out of `Awaiting Approval` while still unapproved → `Initial` (new file) or `Under Change` (revised file). It simply reverses the submission and **does not notify the approver**. 403 if the caller didn't submit it.
  - **Return a rejected file to editable:** any **Engineer/Admin** moves `Initial,Rejected → Initial` or `Under Change,Rejected → Under Change` so it becomes checkout-able. A rejected file otherwise "floats" until returned or resubmitted.
- **Data card editable only while checked out:** Number, filename, and Description may be changed only by the check-out holder (`POST /files/{id}/card`). 403 otherwise.
- **Check-in is blocked (409) on a non-unique Number or a duplicate filename** within the vault; the same uniqueness is validated on card edit and upload.
- **Every state change requires a confirmation dialog (with Cancel)** in the client before it runs.
- **Force-check-in (Admin only, emergency):** `POST /files/force-check-in` releases another user's check-out **without uploading bytes**, and is only available while a file is **actively checked out**. The Vault keeps its last-remembered state; the previous holder's un-checked-in edits are abandoned and their later check-in will 403. Logged to history. **[decided]**

> **UI buttons:** all state changes are surfaced as **"change state - X"** buttons; revisions are automatic (no manual revision/rollback button). The full state→button→endpoint→role map is in `docs/ui-commands.md`.

## 8. Approval — segregation of duties **[decided]**
- Engineers must submit to a different Engineer or an Admin; an Engineer may NOT approve their own submission.
- Admins may submit to Engineers or other Admins, and MAY approve their own work.
- Users may not approve. See `ApprovalPolicy.CanApprove`. Self-approval by an Engineer → HTTP 403.

## 9. Revision system **[decided]**
- First approval = **A**; each approval increments; **skip I and L**: A B C D E F G H J K M N … Z, AA …
- Revision changes only on approval. See `RevisionSequence`.

## 10. Rollback & backup **[decided]**
- Every revision that reaches **Production** is snapshotted (binary + data-card metadata) into a hidden archive **at the moment of approval**.
- Archived snapshots are **invisible in all normal views** (search, data card). An Admin enumerates them only via the Admin-only revisions endpoint to pick a rollback target.
- **Admins only** may roll back. A rollback **replaces the live file** (content + revision + data-card fields) with the chosen snapshot and sets state to Production.
- History records a line: `Rolled back to {revision} by {admin display name}`.
- All snapshots are retained (immutable archive); rollback changes which one is active.

## 11. Data card (per file)
`Number`, `Description`, `Revision`, `State`, `Designed By`/date, `Approved By`/date,
`Changed By`/date, `Created Date`, `Updated Date`, plus a full history log.

## 12. Duplicate prevention
Reject upload if the normalized name key already exists (HTTP 409).

## 13. Search
Filter by `Number`, `Description`, `Revision`, `State`, `Designer`, `Repository`, created/updated date ranges.
Scoped to one vault (repository, default `CAD`). Read-only Users receive only Production and Prototype files.
Archived snapshots are never returned.

## 14. Asana integration
- Task on entering Awaiting Approval; notify creator/revisor on approve/reject; notify approver on resubmission.
- Behind `INotificationService`; stub default, real client config-enabled.

## 15. API
See `openapi.yaml`. 20 endpoints: auth, content download, the core workflow actions, the two
Prototype actions, **return-to-editable** (undo submit / un-reject), **data-card edit**,
force-check-in, and the Admin-only `GET /files/{id}/revisions` + `POST /files/rollback`.

## 16. Repository (vault) dimension **[decided]**
- Every file belongs to a **repository** (a logical vault). The **CAD** vault ships first and is the default.
- A future **documentation** vault is added by inserting one `Repository` row — not a schema change. Filename uniqueness and search are **scoped per repository**, so the same number/name can exist independently in CAD and DOCS.
- Upload takes an optional `repository` key (defaults to `CAD`). Search is scoped to one vault (default `CAD`). See `Repository`, `WellKnownRepositories`.
- **Scope now:** CAD only. The dimension exists so documentation is additive later; do not build a second vault until the Fusion feasibility test passes.

## 17. Assembler viewing (low-level Users) **[decided]**
- Assemblers must be **self-sufficient** viewing released CAD + drawings, **without consuming Fusion seats**.
- **Strategy:** on release/approval, publish a **drawing PDF** and view **3D in Autodesk's free web viewer** (lightweight viewer). Assemblers receive the **published derivatives (PDF + viewer)**, not the source `.f3d`.
- **Status:** future "published derivatives" requirement attached to the approval/release step. **No code in the current scope** — documented so the data model and release flow can accommodate it (a file gains published-PDF / viewer-reference outputs at approval).

## 18. File references / where-used (Fusion feasibility) **[open — validate early]**
- The current model treats each file **independently** (unique name, no reference graph).
- **Fusion assemblies reference component files.** A real CAD PDM must track those references ("where-used"): checking out/viewing an assembly needs its components, and Desktop Connector's Reference Explorer manages these relationships today.
- This is a **key feasibility item**, not yet modeled. Validate in the early spike (see `TASKS.md`); likely becomes a `FileReference` (parent/child + version) addition if the spike confirms it's needed.

## 19. Prototype entry for brand-new items **[resolved — option a]**
**Decision:** Prototype may be reached only by a file that has **never been approved**. Never-approved files live in `Initial`, so Prototype branches off `Initial` (`Initial ⇄ Prototype`), revision stays null. Implemented; see §6a.

## Resolved during handoff
- Number: user-entered, charset-restricted, and **unique per vault (case-insensitive)**. **[decided]**
- Unique name: case-insensitive, extension excluded. Duplicate Number **or** filename **blocks check-in (409)** and card edit and upload. **[decided]**
- Approval: engineers cannot self-approve; admins can. **[decided]**
- Rejected files are NOT directly checkout-able: **return** them to `Initial` / `Under Change` first, then check out. (Reverses the earlier handoff decision.) **[decided]**
- Obsolete is reachable from **Production or Prototype**; only Admins **recover** an Obsolete file, restoring its **exact prior state** (Production or Prototype). **[decided]**
