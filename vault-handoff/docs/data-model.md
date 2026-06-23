# Vault — Data Model, State Machine, Revision Rule

## Entities

### users
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| email | text unique | must be `@sparkrobotic.com` |
| display_name | text | used in rollback history line |
| role | text | `Admin` \| `Engineer` \| `User` (stored as string) |
| created_at | timestamptz | |

### repositories  (logical vaults; CAD ships first, DOCS added later as one row)
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| key | text unique | stable code, e.g. `CAD`, `DOCS` |
| name | text | display name, e.g. `CAD Files` |
| created_at | timestamptz | |

Seeded with the default `CAD` vault (id `0a000000-0000-0000-0000-000000000cad`). Adding a documentation vault = insert one row; no schema change.

### files
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| repository_id | uuid FK repositories | which vault the file lives in; defaults to CAD |
| number | text | user-entered; charset = letters/numbers/space/hyphen/underscore; **unique per `repository_id` (case-insensitive)** |
| name | text | filename as uploaded (with extension) |
| name_key | text | `lower(name without extension)`; **unique per `repository_id`** |
| description | text | |
| revision | text null | null until first approval, then A, B, C… |
| state | text | one of the file states |
| obsoleted_from_state | text null | the state held when obsoleted (`Production` or `Prototype`); used by Recover; null otherwise |
| origin | text null | `InitialTrack` \| `ChangeTrack` while Awaiting Approval; drives reject routing |
| checked_out_by | uuid null FK users | null = checked in / read-only |
| submitted_by | uuid null FK users | who submitted for approval; used for self-approval check |
| designed_by | uuid FK users | + designed_date |
| approved_by | uuid null FK users | + approved_date |
| changed_by | uuid null FK users | + changed_date |
| storage_key | text | live binary pointer in IFileStore |
| content_version | int | bumped on every content change (upload, check-in, rollback); client cache invalidation |
| created_at / updated_at | timestamptz | |

### file_revision_backups  (HIDDEN — never returned by search or GET /files/{id})
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| file_id | uuid FK files | |
| revision | text | the snapshotted revision (unique per file) |
| number / description | text | data-card metadata at snapshot time |
| approved_by | uuid null FK users | + approved_date |
| storage_key | text | archived binary, separate from the live file |
| created_at | timestamptz | when the snapshot was taken (at approval) |

Unique index on (file_id, revision). Created on every approval into Production.

> **Uniqueness note:** the live `files` uniqueness is a composite index on `(repository_id, name_key)` — case-insensitive and extension-excluded, scoped per vault.

> **Read visibility:** role `User` (assemblers) may read only `Production` and `Prototype` files on every read path (GET card, GET content, search). Other states return 404 to a User. Engineers/Admins see all states. See `FileVisibility`.

### file_history (append-only)
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| file_id | uuid FK files | |
| event | text | CheckedOut, CheckedIn, Submitted, Approved, Rejected, Revised, RolledBack |
| from_state / to_state | text null | |
| revision | text null | revision after the event, if changed |
| actor | uuid FK users | |
| at | timestamptz | |
| note | text null | rollback: `Rolled back to {rev} by {admin display name}` |

## State machine (authoritative)

States: Initial, AwaitingApproval, Production, UnderChange, InitialRejected,
UnderChangeRejected, Obsolete, Prototype. Checkout-able (`FileStateMachine.IsCheckOutable`):
**Initial and UnderChange only** — checkout from any other state → HTTP 422. Rejected files must
be **returned** to Initial/UnderChange first; Prototype must exit to Initial first.

| Trigger | From | To | Side effect |
|---|---|---|---|
| Create | (none) | Initial | auto check-out to creator |
| Submit | Initial | AwaitingApproval | origin=InitialTrack; submitted_by=actor; **must be checked in**; Asana task |
| Submit | InitialRejected | AwaitingApproval | origin=InitialTrack; submitted_by=actor; resubmit as-is; notify approver |
| Submit | UnderChange | AwaitingApproval | origin=ChangeTrack; submitted_by=actor; **must be checked in**; Asana task |
| Submit | UnderChangeRejected | AwaitingApproval | origin=ChangeTrack; submitted_by=actor; resubmit as-is; notify approver |
| Approve | AwaitingApproval | Production | check ApprovalPolicy; revision=Next(revision); SNAPSHOT to backups; notify |
| Reject | AwaitingApproval (InitialTrack) | InitialRejected | notify creator |
| Reject | AwaitingApproval (ChangeTrack) | UnderChangeRejected | notify revisor |
| ReturnToEditable | AwaitingApproval (InitialTrack) | Initial | **undo submit** — submitter only; clear submitted_by/origin; **no approver notice** |
| ReturnToEditable | AwaitingApproval (ChangeTrack) | UnderChange | **undo submit** — submitter only; clear submitted_by/origin; **no approver notice** |
| ReturnToEditable | InitialRejected | Initial | Engineer/Admin; rejected → clean editable (then checkout-able) |
| ReturnToEditable | UnderChangeRejected | UnderChange | Engineer/Admin; rejected → clean editable (then checkout-able) |
| (card edit) | Initial / UnderChange (checked out) | — | holder only; edit number/name/description; re-validate uniqueness; no state change |
| BeginChange | Production | UnderChange | Admin/Engineer only (ProductionPolicy); set changed_by; then checkout-able |
| MarkObsolete | Production **or** Prototype | Obsolete | Admin/Engineer; records `obsoleted_from_state`; read-only |
| Reactivate (Recover) | Obsolete | Production **or** Prototype | Admin only; restores `obsoleted_from_state` exactly; clears it |
| Rollback | Production | Production | Admin only; replace live file from snapshot; history `Rolled back to {rev} by {admin}` |
| EnterPrototype | Initial | Prototype | Admin/Engineer; file must be checked in; never-approved only; **no revision, NO history row** |
| ExitPrototype | Prototype | Initial | Admin/Engineer; **no revision, NO history row** |

Any transition not in this table throws (→ HTTP 422). Guard seeded in `FileState.cs`.

> **Check-out/in & force-check-in (no state change):** check-out/in is repeatable in `Initial`/`UnderChange`; the holder field (`checked_out_by`) gates who can edit. Check-out/in/submit require write access (Engineer/Admin; `WriteAccessPolicy`). **Force-check-in** (Admin only) clears `checked_out_by` without uploading bytes — content/revision unchanged, logged to history; the prior holder's later check-in 403s.
Approve also enforces `ApprovalPolicy.CanApprove` (→ HTTP 403 on self-approval by Engineer).

## Revision rule
Alphabet = `A B C D E F G H J K M N O P Q R S T U V W X Y Z` (A–Z minus I and L, 24 letters);
bijective base-24; first approval → `A`. Seeded + tested in `RevisionSequence.cs`.
