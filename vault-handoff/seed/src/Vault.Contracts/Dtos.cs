namespace Vault.Contracts;

public record UserDto(Guid Id, string Email, string DisplayName, string Role);

public record FileCardDto(
    Guid Id, string Repository, string Number, string Name, string Description, string? Revision, string State,
    Guid? CheckedOutBy,
    Guid DesignedBy, DateTimeOffset DesignedDate,
    Guid? ApprovedBy, DateTimeOffset? ApprovedDate,
    Guid? ChangedBy, DateTimeOffset? ChangedDate,
    DateTimeOffset CreatedDate, DateTimeOffset UpdatedDate,
    int ContentVersion);

/// <summary>Admin-only view of an archived revision (no storage keys exposed).</summary>
public record RevisionBackupDto(Guid Id, string Revision, Guid? ApprovedBy, DateTimeOffset? ApprovedDate, DateTimeOffset CreatedAt);

/// <summary>Binary download of the current vault file (server-side carrier; clients use the Stream directly).</summary>
public record FileDownload(Stream Content, string FileName);

public record GoogleAuthRequest(string Code);
public record AuthResponse(string Token, UserDto User);
public record FileIdRequest(Guid FileId);
public record RejectRequest(Guid FileId, string? Reason);
public record RollbackRequest(Guid FileId, string TargetRevision);
public record UpdateCardRequest(string? Number, string? Name, string? Description);   // edit data card while checked out

public record SearchQuery(
    string? Number, string? Description, string? Revision, string? State, string? Designer,
    DateOnly? CreatedFrom, DateOnly? CreatedTo, DateOnly? UpdatedFrom, DateOnly? UpdatedTo,
    string? Repository = null);   // vault key, e.g. "CAD"; defaults to CAD in the service when omitted
