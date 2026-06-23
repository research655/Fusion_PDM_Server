using Microsoft.EntityFrameworkCore;
using Vault.Contracts;
using Vault.Domain.Abstractions;
using Vault.Domain.Approvals;
using Vault.Domain.Entities;
using Vault.Domain.Exceptions;
using Vault.Domain.Files;
using Vault.Domain.Revisions;
using Vault.Infrastructure.Data;

namespace Vault.Api.Services;

/// <summary>
/// Reference implementation. The upload -> check-out -> submit -> approve(+snapshot)
/// path and reject are fully worked as the pattern for Cursor to copy. The remaining
/// methods throw NotImplementedException (HTTP 501) with a TODO describing the pattern.
/// </summary>
public sealed class VaultService : IVaultService
{
    private readonly VaultDbContext _db;
    private readonly IFileStore _store;
    private readonly IAuthProvider _auth;
    private readonly INotificationService _notify;
    private readonly string _allowedDomain;

    public VaultService(VaultDbContext db, IFileStore store, IAuthProvider auth,
                        INotificationService notify, IConfiguration config)
    {
        _db = db;
        _store = store;
        _auth = auth;
        _notify = notify;
        _allowedDomain = config["Auth:AllowedDomain"] ?? "sparkrobotic.com";
    }

    public async Task<FileCardDto> UploadAsync(Stream content, string fileName, string number,
                                               string description, Guid actor, string? repositoryKey,
                                               CancellationToken ct)
    {
        FileNumber.Validate(number);                                   // -> 400 on bad charset

        var repoKey = string.IsNullOrWhiteSpace(repositoryKey)
            ? WellKnownRepositories.DefaultKey
            : repositoryKey.Trim();
        var repo = await _db.Repositories.FirstOrDefaultAsync(r => r.Key == repoKey, ct)
                   ?? throw new NotFoundException($"Repository '{repoKey}'");   // -> 404

        var key = FileNaming.ToKey(fileName);
        await EnsureNumberAndNameUniqueAsync(repo.Id, number, key, Guid.Empty, ct); // -> 409 on duplicate Number/filename

        var storageKey = await _store.SaveAsync(content, fileName, ct);
        var now = DateTimeOffset.UtcNow;

        var file = new VaultFile
        {
            Id = Guid.NewGuid(),
            RepositoryId = repo.Id,
            Repository = repo,           // so ToCard can surface the key without a reload
            Number = number,
            Name = fileName,
            NameKey = key,
            Description = description,
            Revision = null,
            State = FileState.Initial,
            CheckedOutBy = actor,        // new files auto check-out to the creator
            DesignedBy = actor,
            DesignedDate = now,
            StorageKey = storageKey,
            ContentVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Files.Add(file);
        AddHistory(file, "Created", null, FileState.Initial.ToString(), null, actor, now);
        AddHistory(file, "CheckedOut", null, null, null, actor, now);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    public async Task<FileCardDto> GetAsync(Guid id, Guid actor, CancellationToken ct)
    {
        var file = await Load(id, ct);
        await EnsureVisibleAsync(file, actor, ct);                      // assemblers see only Production
        return ToCard(file);
    }

    public async Task CheckOutAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        await RequireWriteAccessAsync(actor, ct);                      // read-only Users cannot check out -> 403
        var file = await Load(fileId, ct);
        if (!FileStateMachine.IsCheckOutable(file.State))
            throw new InvalidOperationException("Only Initial or Under Change files can be checked out."); // -> 422
        if (file.CheckedOutBy is { } holder && holder != actor)
            throw new CheckoutConflictException();                     // -> 409 (held by someone else)
        file.CheckedOutBy = actor;
        Touch(file);
        AddHistory(file, "CheckedOut", null, null, file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
    }

    public async Task CheckInAsync(Guid fileId, Stream content, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        if (file.CheckedOutBy != actor)
            throw new ForbiddenActionException("Only the user who holds the check-out may check in changes."); // -> 403

        // Card edits made while checked out (Number / filename) are validated here: a duplicate
        // Number or filename blocks the check-in (409). NameKey is kept in sync with the filename.
        file.NameKey = FileNaming.ToKey(file.Name);
        await EnsureNumberAndNameUniqueAsync(file.RepositoryId, file.Number, file.NameKey, file.Id, ct);

        // The working copy becomes the new vault content. Revision is UNCHANGED (only approval changes it).
        file.StorageKey = await _store.SaveAsync(content, file.Name, ct);
        file.ContentVersion += 1;        // signals clients with a cached read-only copy to re-hydrate
        file.CheckedOutBy = null;        // read-only again
        Touch(file);
        AddHistory(file, "CheckedIn", null, null, file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<FileDownload> GetContentAsync(Guid id, Guid actor, CancellationToken ct)
    {
        var file = await Load(id, ct);
        await EnsureVisibleAsync(file, actor, ct);                      // no WIP bytes to assemblers
        var stream = await _store.OpenAsync(file.StorageKey, ct);
        return new FileDownload(stream, file.Name);
    }

    public async Task SubmitForApprovalAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        await RequireWriteAccessAsync(actor, ct);                      // read-only Users cannot submit -> 403
        var file = await Load(fileId, ct);
        if (file.CheckedOutBy is not null)
            throw new InvalidOperationException("Check the file in before submitting it for approval."); // -> 422
        var from = file.State;
        var isResubmission = from is FileState.InitialRejected or FileState.UnderChangeRejected;

        file.Origin = FileStateMachine.OriginFor(from);
        file.State = FileStateMachine.Next(from, FileTrigger.Submit);  // -> 422 if invalid
        file.SubmittedBy = actor;
        Touch(file);
        AddHistory(file, "Submitted", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);

        await _notify.OnSubmittedForApprovalAsync(file, isResubmission, ct);
    }

    public async Task<FileCardDto> ApproveAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        var approver = await _db.Users.FindAsync(new object?[] { actor }, ct)
                       ?? throw new NotFoundException("User");

        if (!ApprovalPolicy.CanApprove(approver.Role, actor, file.SubmittedBy))
            throw new ForbiddenActionException("You may not approve this submission."); // -> 403

        var from = file.State;
        file.State = FileStateMachine.Next(from, FileTrigger.Approve); // -> 422 if invalid
        file.Revision = RevisionSequence.Next(file.Revision);          // null -> A, then B, ...
        file.ApprovedBy = actor;
        file.ApprovedDate = DateTimeOffset.UtcNow;
        file.Origin = null;
        file.SubmittedBy = null;
        file.CheckedOutBy = null;                                      // production is read-only
        Touch(file);

        // Snapshot the new production revision into the hidden archive.
        var archiveKey = await _store.CopyAsync(file.StorageKey, $"backup/{file.Id:N}/{file.Revision}", ct);
        _db.RevisionBackups.Add(new FileRevisionBackup
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            Revision = file.Revision!,
            Number = file.Number,
            Description = file.Description,
            ApprovedBy = file.ApprovedBy,
            ApprovedDate = file.ApprovedDate,
            StorageKey = archiveKey,
            CreatedAt = file.UpdatedAt
        });

        AddHistory(file, "Approved", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);

        await _notify.OnApprovedAsync(file, ct);
        return ToCard(file);
    }

    public async Task RejectAsync(Guid fileId, string? reason, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        var from = file.State;
        file.State = FileStateMachine.Next(from, FileTrigger.Reject, file.Origin); // routes by origin; -> 422
        file.SubmittedBy = null;
        Touch(file);
        AddHistory(file, "Rejected", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt, reason);
        await _db.SaveChangesAsync(ct);

        await _notify.OnRejectedAsync(file, reason, ct);
    }

    /// <summary>
    /// "Back to Initial / Back to Under Change." Returns a file to its pre-submission editable state.
    /// Two cases: (1) undo a pending submission (from AwaitingApproval) — only the submitter, routes by
    /// track; (2) return a rejected file to editable (from a Rejected state) — any write-access user.
    /// No approver notification in either case.
    /// </summary>
    public async Task<FileCardDto> ReturnToEditableAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        var from = file.State;

        if (from is FileState.AwaitingApproval)
        {
            if (file.SubmittedBy != actor)
                throw new ForbiddenActionException("Only the user who submitted this file may undo the submission."); // -> 403
        }
        else
        {
            await RequireWriteAccessAsync(actor, ct);                   // returning a rejected file: -> 403 for Users
        }

        file.State = FileStateMachine.Next(from, FileTrigger.ReturnToEditable, file.Origin); // -> 422 unless Awaiting/Rejected
        file.SubmittedBy = null;
        file.Origin = null;
        Touch(file);
        AddHistory(file, "ReturnedToEditable", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);   // editable again -> checkout-able (no approver notification)
    }

    /// <summary>
    /// Edit the data card (Number / filename / Description). Allowed ONLY while the caller holds the
    /// check-out. Number and filename are re-validated for uniqueness (also re-checked at check-in).
    /// </summary>
    public async Task<FileCardDto> UpdateCardAsync(Guid fileId, UpdateCardRequest req, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        if (file.CheckedOutBy != actor)
            throw new ForbiddenActionException("Check the file out first — the data card is editable only while you hold the check-out."); // -> 403

        if (req.Number is { } number)
        {
            FileNumber.Validate(number);                                // -> 400 on bad charset
            file.Number = number.Trim();
        }
        if (req.Name is { } name && !string.IsNullOrWhiteSpace(name))
        {
            file.Name = name.Trim();
            file.NameKey = FileNaming.ToKey(file.Name);
        }
        if (req.Description is { } description)
            file.Description = description;

        await EnsureNumberAndNameUniqueAsync(file.RepositoryId, file.Number, file.NameKey, file.Id, ct); // -> 409
        Touch(file);
        AddHistory(file, "CardEdited", null, null, file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    public async Task<FileCardDto> BeginChangeAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        var user = await _db.Users.FindAsync(new object?[] { actor }, ct) ?? throw new NotFoundException("User");
        if (!ProductionPolicy.CanChangeProductionState(user.Role))
            throw new ForbiddenActionException("Only Admins or Engineers may move a file out of Production."); // -> 403

        var from = file.State;
        file.State = FileStateMachine.Next(from, FileTrigger.BeginChange);  // -> 422 if not Production
        file.ChangedBy = actor;
        file.ChangedDate = DateTimeOffset.UtcNow;
        Touch(file);
        AddHistory(file, "BeginChange", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);   // now Under Change -> checkout-able
    }

    public async Task<FileCardDto> MarkObsoleteAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        var user = await _db.Users.FindAsync(new object?[] { actor }, ct) ?? throw new NotFoundException("User");
        if (!ProductionPolicy.CanChangeProductionState(user.Role))
            throw new ForbiddenActionException("Only Admins or Engineers may obsolete a file."); // -> 403

        var from = file.State;
        if (from is not (FileState.Production or FileState.Prototype))
            throw new InvalidOperationException("Only Production or Prototype files can be obsoleted."); // -> 422
        file.ObsoletedFromState = from;                                     // remembered so Recover restores it exactly
        file.State = FileState.Obsolete;
        Touch(file);
        AddHistory(file, "Obsoleted", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    /// <summary>
    /// "Recover Obsolete File" (Admin only). Restores an obsolete file to the EXACT state it held when
    /// obsoleted — Production or Prototype. (A specific revision can additionally be restored via the
    /// rollback path; see RollbackAsync / ListRevisionsAsync.)
    /// </summary>
    public async Task<FileCardDto> ReactivateAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        var file = await Load(fileId, ct);
        var user = await _db.Users.FindAsync(new object?[] { actor }, ct) ?? throw new NotFoundException("User");
        if (user.Role != UserRole.Admin)
            throw new ForbiddenActionException("Only Admins may recover an obsolete file."); // -> 403
        if (file.State is not FileState.Obsolete)
            throw new InvalidOperationException("File is not obsolete."); // -> 422

        var target = file.ObsoletedFromState;
        if (target is not (FileState.Production or FileState.Prototype))
            target = FileState.Production;                                  // fallback for legacy rows
        var from = file.State;
        file.State = target.Value;
        file.ObsoletedFromState = null;
        Touch(file);
        AddHistory(file, "Recovered", from.ToString(), file.State.ToString(), file.Revision, actor, file.UpdatedAt);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    // ---------- Prototype (pseudo-Production; no revision, no history; never-approved files only) ----------

    /// <summary>
    /// Initial -> Prototype. Engineers/Admins only, no approval. Only never-approved files are in
    /// Initial, so this naturally satisfies "Prototype only if never approved." The file must be
    /// checked in. Deliberately writes NO history row and does NOT assign a revision.
    /// </summary>
    public async Task<FileCardDto> EnterPrototypeAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        await RequireWriteAccessAsync(actor, ct);                                       // -> 403 for Users
        var file = await Load(fileId, ct);
        if (file.CheckedOutBy is not null)
            throw new InvalidOperationException("Check the file in before moving it to Prototype."); // -> 422

        file.State = FileStateMachine.Next(file.State, FileTrigger.EnterPrototype);     // -> 422 unless Initial
        Touch(file);                       // bumps UpdatedAt only; no history, no revision change
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    /// <summary>Prototype -> Initial. Engineers/Admins only. No history, no revision change.</summary>
    public async Task<FileCardDto> ExitPrototypeAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        await RequireWriteAccessAsync(actor, ct);                                       // -> 403 for Users
        var file = await Load(fileId, ct);
        file.State = FileStateMachine.Next(file.State, FileTrigger.ExitPrototype);      // -> 422 unless Prototype
        Touch(file);
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    /// <summary>
    /// EMERGENCY, Admin only. Releases another user's check-out WITHOUT uploading new bytes.
    /// The Vault keeps whatever it last remembered (last check-in, or the original upload for a
    /// new file that was never checked in — likely an empty/placeholder file). The previous
    /// holder's un-checked-in local edits are abandoned; their stale check-in will 403 since they
    /// no longer hold the lock. Someone else can now check out from the most recent vaulted state.
    /// </summary>
    public async Task<FileCardDto> ForceCheckInAsync(Guid fileId, Guid actor, CancellationToken ct)
    {
        var admin = await _db.Users.FindAsync(new object?[] { actor }, ct) ?? throw new NotFoundException("User");
        if (admin.Role != UserRole.Admin)
            throw new ForbiddenActionException("Only Admins may force a check-in."); // -> 403

        var file = await Load(fileId, ct);
        if (file.CheckedOutBy is null)
            throw new InvalidOperationException("File is not checked out."); // -> 422 (nothing to force)

        var previousHolder = file.CheckedOutBy;
        file.CheckedOutBy = null;                 // release the lock; content/revision unchanged
        Touch(file);
        AddHistory(file, "ForceCheckedIn", null, null, file.Revision, actor, file.UpdatedAt,
                   $"Force check-in by admin {admin.DisplayName}; released hold from {previousHolder}");
        await _db.SaveChangesAsync(ct);
        return ToCard(file);
    }

    // ---------- Not yet implemented — follow the patterns above ----------

    public Task<AuthResponse> AuthenticateGoogleAsync(GoogleAuthRequest req, CancellationToken ct)
        => throw new NotImplementedException(
            $"TODO: await _auth.ValidateAsync(code); require email ends with @{_allowedDomain} (else ForbiddenActionException); upsert User; issue a session token.");

    public Task<IReadOnlyList<FileCardDto>> SearchAsync(SearchQuery query, Guid actor, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: build an IQueryable over _db.Files.Include(f => f.Repository) applying the contract filters " +
            "(number/description/revision/state/designer/date ranges). Scope by repository: resolve query.Repository " +
            "(default WellKnownRepositories.DefaultKey) to its id and filter on it. Apply read visibility: if the caller " +
            "is role User, restrict to State == Production (see FileVisibility). Never include RevisionBackups.");

    public Task<IReadOnlyList<RevisionBackupDto>> ListRevisionsAsync(Guid fileId, Guid actor, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: require Admin (else ForbiddenActionException); return _db.RevisionBackups.Where(b => b.FileId == fileId) mapped to RevisionBackupDto.");

    public Task<FileCardDto> RollbackAsync(Guid fileId, string targetRevision, Guid actor, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: require Admin; find the backup (FileId,targetRevision) or 404; _store.CopyAsync archive -> new live key; restore Revision + Number/Description/ApprovedBy/Date + StorageKey; state Production; AddHistory(\"RolledBack\", note: $\"Rolled back to {targetRevision} by {admin.DisplayName}\").");

    // ---------- helpers ----------

    private async Task<VaultFile> Load(Guid id, CancellationToken ct)
        => await _db.Files.Include(f => f.Repository).FirstOrDefaultAsync(f => f.Id == id, ct)
           ?? throw new NotFoundException("File");

    /// <summary>
    /// Enforces the read-side rule: a role-User caller may only see Production files.
    /// An unknown caller is treated as least-privilege (User), so unreleased work is
    /// never exposed by a missing/misconfigured identity. 404 (not 403) hides existence.
    /// </summary>
    private async Task EnsureVisibleAsync(VaultFile file, Guid actor, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object?[] { actor }, ct);
        var role = user?.Role ?? UserRole.User;
        if (!FileVisibility.CanView(role, file.State))
            throw new NotFoundException("File");
    }

    /// <summary>Loads the caller and requires Engineer/Admin (write access); else 403.</summary>
    private async Task<User> RequireWriteAccessAsync(Guid actor, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object?[] { actor }, ct) ?? throw new NotFoundException("User");
        if (!WriteAccessPolicy.CanEdit(user.Role))
            throw new ForbiddenActionException("Write access (Engineer or Admin) is required for this action."); // -> 403
        return user;
    }

    /// <summary>
    /// Number (case-insensitive) and filename key must be unique within the vault. Pass the file's
    /// own id as <paramref name="selfId"/> to exclude it (use Guid.Empty for a brand-new file).
    /// </summary>
    private async Task EnsureNumberAndNameUniqueAsync(Guid repositoryId, string number, string nameKey, Guid selfId, CancellationToken ct)
    {
        var num = number.Trim().ToLower();
        if (await _db.Files.AnyAsync(f => f.RepositoryId == repositoryId && f.Id != selfId && f.Number.ToLower() == num, ct))
            throw new DuplicateNumberException(number);                  // -> 409
        if (await _db.Files.AnyAsync(f => f.RepositoryId == repositoryId && f.Id != selfId && f.NameKey == nameKey, ct))
            throw new DuplicateNameException(nameKey);                   // -> 409
    }

    private static void Touch(VaultFile f) => f.UpdatedAt = DateTimeOffset.UtcNow;

    private void AddHistory(VaultFile f, string @event, string? from, string? to,
                            string? revision, Guid actor, DateTimeOffset at, string? note = null)
        => _db.History.Add(new FileHistory
        {
            Id = Guid.NewGuid(),
            FileId = f.Id,
            Event = @event,
            FromState = from,
            ToState = to,
            Revision = revision,
            Actor = actor,
            At = at,
            Note = note
        });

    private static FileCardDto ToCard(VaultFile f) => new(
        f.Id, f.Repository?.Key ?? "", f.Number, f.Name, f.Description, f.Revision, f.State.ToString(),
        f.CheckedOutBy, f.DesignedBy, f.DesignedDate,
        f.ApprovedBy, f.ApprovedDate, f.ChangedBy, f.ChangedDate,
        f.CreatedAt, f.UpdatedAt, f.ContentVersion);
}
