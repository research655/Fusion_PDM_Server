# Vault Project — Plain-English Status and Next Steps

I'll keep this simple. Think of the project as building a **controlled library for your CAD files** — it tracks every file, who can edit it, which version is official, and who approved it.

Right now you have a **starter kit** (a blueprint plus a partial build). It is not a finished, running program yet. Below is what's done, what's left, and the exact steps to test and ship it.

---

## Section 1 — What's Been Built (and Where It Lives)

Everything is inside the `vault-handoff.zip` you downloaded. Here are the main parts in plain terms.

### The plan and rules (the "blueprint") — fully done
These are written documents that explain how the system should work. They live in the **`docs/`** folder:
- **`PRD.md`** — the full list of requirements and decisions (what the system must do).
- **`data-model.md`** — what information is stored about each file.
- **`openapi.yaml`** — the list of 20 commands the system understands (upload, approve, etc.).
- **`workflow.mmd`** — the diagram of file states (the flowchart you can attach to Asana).
- **`ui-commands.md`** — the exact buttons users will click, and who is allowed to click each.
- **`infrastructure.md`** — the hardware to buy and how it connects.

Rules for the AI coding tool (Cursor) live in **`.cursor/rules/`**.

### The "rules engine" — written and double-checked
This is the brain of the system: the logic for revision letters, approvals, file states, and who can see or edit what. It lives in **`seed/src/Vault.Domain/`**.

I checked this logic by **re-doing the math in a simple test script** (so the rules are proven correct), but the real code still needs to be compiled and run on your computer (more on that in Section 3).

Verified rules include:
- Revision letters go A, B, C… and **skip I and L**.
- The file states and the legal moves between them.
- **Only Engineers and Admins can edit** (check out, check in, submit). Assemblers are view-only.
- **Only Initial and Under Change files can be checked out.** A rejected file must first be sent **back to Initial / Under Change** (the "undo submit / return to editable" action).
- **Submitting a file for approval requires it to be checked in first** (so work isn't stuck in one person's local copy).
- Engineers can't approve their own work; Admins can.
- **Assemblers (read-only users) only see Production and Prototype files** — not work-in-progress.
- **Prototype status** — a "view-only-but-not-official" state for one-off test items (test cables, fixtures). No approval, no revision, no history, and **everyone can see it**.
- **Obsolete** can be applied to a Production **or** a Prototype file (any Engineer/Admin). An Admin can later **recover** it to the exact state it was in (Production or Prototype).
- **The data card (number, filename, description) can only be edited while the file is checked out.** Check-in is blocked if the number or filename would duplicate another file.
- A person who submitted a file can send it **back to Initial / Under Change** to undo the submission (no approver is notified); the same action returns a **rejected** file to an editable state.
- Admins can **force a check-in** in emergencies (to free a file someone else left checked out).
- Each file lives in a **"vault"** (CAD now; documents can be added later with no rebuild).

### The server (the "central brain") — mostly built
This is the part all apps talk to. It lives in **`seed/src/Vault.Api/`**.
- It has all **20 commands** wired up.
- The core workflow is **fully worked as a finished example**: upload, check-out, check-in, submit, approve, reject, back-to-editable (undo submit / return a rejected file), edit-data-card, begin-change, make-obsolete, recover-obsolete, enter/exit prototype, force-check-in, and the two "view" commands.
- **4 commands are still placeholders** (explained in Section 2).

Supporting pieces:
- **`seed/src/Vault.Infrastructure/`** — the database setup and file storage.
- **`seed/src/Vault.Contracts/`** — the shared "data shapes" the apps pass around.
- **`seed/src/Vault.Client/`** — a ready-made helper so the desktop apps can talk to the server easily.
- **`seed/tests/Vault.Tests/`** — automated tests for the rules.

### The deployment kit — built
Files to install the system on a server later:
- **`scripts/bootstrap.ps1`** — sets up the projects and runs the tests.
- **`deploy/`** — the files to run it on a server next to your NAS (storage box), plus a step-by-step `deploy/README.md`.
- **`CURSOR_HANDOFF.md`** — the instructions you paste into Cursor to have it finish the code.

---

## Section 2 — What's Still Left to Do

The starter kit is a strong foundation, but it is **not a finished, running app**. Here's the remaining work, simplest first.

- **Compile and run it once.** The code has never been built or run yet (this computer didn't have the tools). The first build may need small fixes.
- **Create the database tables.** A one-time setup command (called a "migration") hasn't been run yet.
- **Finish the 4 placeholder commands** (Cursor does this):
  1. **Google sign-in** (log in with @sparkrobotic.com).
  2. **Search** (find files by number, name, etc.).
  3. **List old versions** (for admins).
  4. **Roll back** (restore an older approved version).
- **Build the two Windows apps** (Cursor does this):
  - The **desktop app** (buttons and screens for approving, searching, admin tasks). The button list is already written in `docs/ui-commands.md`.
  - The **File Explorer part** (makes vault files appear in Windows like a normal folder).
- **Turn on the real connections** (currently fake placeholders): real **Google login** and real **Asana** notifications.
- **Run the big "go / no-go" test (most important).** Prove that a real Fusion 360 assembly — with its linked part files and a drawing — can go **into** the vault and come back **out** without breaking. If this fails, you stop before building more. This is in `TASKS.md` as **Phase 0**.

---

## Section 3 — Your Step-by-Step Path to Testing and Deployment

Do these in order. Commands are copy-paste ready. "PowerShell" is the blue command window in Windows.

### Step 1 — Install the tools (one time)
Install these on your Windows dev computer:
- **.NET 8 SDK** (the build tool)
- **PostgreSQL 16** (the database)
- **Git** and **Cursor**

### Step 2 — Unzip and build the starter kit
1. Unzip `vault-handoff.zip` to `C:\dev\vault`.
2. Open **PowerShell**, then run:
```powershell
cd C:\dev\vault
Set-ExecutionPolicy -Scope Process Bypass -Force
.\scripts\bootstrap.ps1
```
✅ **Check:** it should finish with the build passing and the rule tests green. If you see errors, copy them into Cursor and ask it to fix them.

### Step 3 — Let Cursor finish the code
1. Open the `C:\dev\vault` folder in **Cursor**.
2. Open **`CURSOR_HANDOFF.md`**, copy its contents, and paste into Cursor's chat.
3. Have Cursor work through **`TASKS.md`** — the 4 placeholder commands, the database setup command, and later the two Windows apps.

✅ **Check:** Cursor reports the build still passes and tests are green.

### Step 4 — Create the database
In **PowerShell**:
```powershell
psql -U postgres -c "CREATE DATABASE vault;"
```
Then open **`C:\dev\vault\src\Vault.Api\appsettings.Development.json`** and put your PostgreSQL password in the connection line.

### Step 5 — Run the server and test it
In **PowerShell**:
```powershell
cd C:\dev\vault
dotnet run --project src\Vault.Api
```
It will print a web address. Open it in your browser to reach **Swagger** (a built-in test page with a button for every command).

✅ **Check:** Upload a test file, **check it in**, **submit** it, then **approve** it, and confirm it becomes "Production" with revision "A". (Submit only works after the file is checked in.)
⚠️ **Important:** editing actions (check-out, check-in, submit) require an **Engineer or Admin** user. Before testing, add one user with that role and use their ID as the `X-User-Id` value. Anyone unknown is treated as a read-only assembler and will be blocked from editing. (Approval already needed this, so it's the same setup.)

### Step 6 — Run the big Fusion test (go / no-go)
Using Fusion 360 and your Autodesk connector, take a **real assembly with linked parts and a drawing**, push it into the vault, pull it back out, and open it again.

✅ **Check:** the parts stay linked and nothing breaks.
🛑 **If it fails:** stop and reassess before building the rest. This is the make-or-break test.

### Step 7 — Build and test the Windows apps
After Cursor builds the desktop app and the File Explorer part, test on a Windows machine: check a file out, edit, check it back in, submit, approve it, and confirm an assembler only sees released (and Prototype) files.

### Step 8 — Deploy to a server (go live)
1. Set up the **app server** (the small server from `docs/infrastructure.md`) on your network.
2. On the **UniFi NAS**, turn on **NFS** sharing and note the export path.
3. On the app server, open **`deploy/`**, fill in the `.env` file (NAS address, NAS path, database password), then run:
```bash
cd deploy
docker compose up -d
```
4. Point the Windows apps at the server's address.

✅ **Check:** repeat the upload-and-approve test from the server instead of your laptop.
📄 Full deploy details (NFS setup, security, backups) are in **`deploy/README.md`**.

---

Want me to expand any single step into a more detailed walkthrough — for example, **Step 6 (the Fusion test)** or **Step 8 (the server setup)**? Just tell me which number.
