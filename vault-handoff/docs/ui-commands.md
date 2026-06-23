# Vault — UI Commands (buttons exposed to users)

How the UI turns the API into clickable commands. **Show a button only when it is legal for the
file's current state and the user's role** — the same rules the server enforces.

## Golden rules
- **Every "Change State >" action shows a confirmation window with a Cancel option** before it runs. Nothing changes state on a single click.
- **A file must be checked out BEFORE it is opened.** The client opens files only after check-out.
- **Check-out and check-in must FAIL if the file is currently open** in an application (client enforces this; the server can't see an OS file handle).
- **Revisions are automatic** — there is no manual revision or rollback button. Revision letters are assigned by the system on approval only.

## "Change State >" buttons (the only state options)
Labels name the result. One label can map to different endpoints by current state; the server
applies the role/permission rules.

| Current state | Button label | Endpoint | Who can click |
|---|---|---|---|
| Initial | Change State > Submit | POST /files/submit-approval (must be checked in) | Engineer/Admin |
| Initial | Change State > Prototype | POST /files/prototype (must be checked in) | Engineer/Admin |
| Under Change | Change State > Submit | POST /files/submit-approval (must be checked in) | Engineer/Admin |
| Awaiting Approval | Change State > Production | POST /files/approve | Engineer/Admin (not own work, if Engineer) |
| Awaiting Approval (new file) | Change State > Initial Rejected | POST /files/reject | Engineer/Admin |
| Awaiting Approval (revised file) | Change State > Revision Rejected | POST /files/reject | Engineer/Admin |
| Awaiting Approval (new file) | Change State > Back to Initial (undo submit) | POST /files/return-to-editable | the submitter |
| Awaiting Approval (revised file) | Change State > Back to Under Change (undo submit) | POST /files/return-to-editable | the submitter |
| Initial Rejected | Change State > Submit (resubmit as-is) | POST /files/submit-approval | Engineer/Admin |
| Initial Rejected | Change State > Back to Initial | POST /files/return-to-editable | Engineer/Admin |
| Revision Rejected | Change State > Submit (resubmit as-is) | POST /files/submit-approval | Engineer/Admin |
| Revision Rejected | Change State > Back to Under Change | POST /files/return-to-editable | Engineer/Admin |
| Production | Change State > Under Change (new Revision) | POST /files/begin-change | Engineer/Admin |
| Production | Change State > Obsolete | POST /files/obsolete | Engineer/Admin |
| Prototype | Change State > Obsolete | POST /files/obsolete | Engineer/Admin |
| Prototype | Change State > Back to Initial | POST /files/prototype/exit | Engineer/Admin |

Notes:
- **Submit** is allowed from Initial, Under Change, and either rejected state, and the file **must be checked in** first.
- **Production** is the approve click; **Initial Rejected / Revision Rejected** are the reject click (the system shows the one matching the file's track).
- **"Back to ..." (undo submit)** just reverses the submission and returns the file to the state it was in before submitting; it does **not** notify the approver. From a rejected file, the same "Back to ..." returns it to editable so it can be checked out.
- **Change State > Obsolete** works from Production **or** Prototype, by any Engineer/Admin. The file remembers which state it was in (for Recover).

## Action buttons (not state changes)
| Button | Endpoint | Who | When enabled |
|---|---|---|---|
| Check Out | POST /files/check-out | Engineer/Admin | file is Initial or Under Change and checked in |
| Check In | POST /files/check-in | the check-out holder | file is checked out by this user, and not open in an app |

- **Check-in is blocked (error) if the Number is not unique, or the filename is a duplicate** within the vault.

## Editing the data card
- The **data card (Number, filename, Description) is editable only while the file is checked out** by the editor (POST /files/{id}/card). A duplicate Number or filename is rejected (and re-checked at check-in).

## Admin context menu (Admin-only; greyed unless conditions are met)
- **Force Check-In** -- POST /files/force-check-in. Visible to **Admins only**. **Enabled only when the file is actively checked out** by someone. Emergency use: releases the lock, keeping the last vaulted state.
- **Recover Obsolete File** -- Admin-only feature:
  - A button opens a **list of all Obsoleted files** (optionally limited to files obsoleted within the last *X* years).
  - **Right-click** an obsoleted file -> shows a **list of its previous revisions**.
  - A **checkbox** selects the desired revision; **Confirm** restores that file to its exact Vault location and the **exact state it held when obsoleted (Production or Prototype)**.
  - Server pieces: list obsoleted files (search), list revisions (GET /files/{id}/revisions), restore state (POST /files/reactivate), restore a chosen revision (POST /files/rollback).

## What each role sees
- **User (assembler, read-only):** Search + Open/View only -- the published **drawing PDF / web viewer**, not the raw .f3d. No state, check-out, or card-edit buttons. Sees only Production and Prototype files.
- **Engineer:** every state button; cannot approve own work; no Admin context-menu items.
- **Admin:** everything, including the Admin context menu (Force Check-In, Recover Obsolete File).

## Open assumption (confirm)
- For **"Back to Initial / Under Change" from a rejected file**, the actor is assumed to be any **Engineer/Admin** (write access). For the **undo-submit** case (from Awaiting Approval) it is restricted to the **original submitter**. Say if the rejected case should also be submitter-only.
