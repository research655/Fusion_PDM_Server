using Vault.Contracts;

namespace Vault.Api.Services;

/// <summary>
/// Application surface Cursor implements. One method per endpoint.
/// Throw domain exceptions; Program.cs maps them to HTTP status codes.
/// </summary>
public interface IVaultService
{
    Task<AuthResponse> AuthenticateGoogleAsync(GoogleAuthRequest req, CancellationToken ct);
    Task<FileCardDto> UploadAsync(Stream content, string fileName, string number, string description, Guid actor, string? repositoryKey, CancellationToken ct);
    Task<FileCardDto> GetAsync(Guid id, Guid actor, CancellationToken ct);
    Task CheckOutAsync(Guid fileId, Guid actor, CancellationToken ct);
    Task CheckInAsync(Guid fileId, Stream content, Guid actor, CancellationToken ct);
    Task<FileDownload> GetContentAsync(Guid id, Guid actor, CancellationToken ct);
    Task SubmitForApprovalAsync(Guid fileId, Guid actor, CancellationToken ct);
    Task<FileCardDto> ApproveAsync(Guid fileId, Guid actor, CancellationToken ct);
    Task RejectAsync(Guid fileId, string? reason, Guid actor, CancellationToken ct);
    Task<FileCardDto> ReturnToEditableAsync(Guid fileId, Guid actor, CancellationToken ct); // undo submit / return rejected -> Initial/Under Change
    Task<FileCardDto> UpdateCardAsync(Guid fileId, UpdateCardRequest req, Guid actor, CancellationToken ct); // edit Number/filename/Description while checked out
    Task<FileCardDto> ForceCheckInAsync(Guid fileId, Guid actor, CancellationToken ct);       // Admin only, emergency: release another user's check-out
    Task<FileCardDto> BeginChangeAsync(Guid fileId, Guid actor, CancellationToken ct);  // Admin/Engineer: Production -> Under Change
    Task<FileCardDto> MarkObsoleteAsync(Guid fileId, Guid actor, CancellationToken ct); // Admin/Engineer: Production -> Obsolete
    Task<FileCardDto> ReactivateAsync(Guid fileId, Guid actor, CancellationToken ct);   // Admin only: Obsolete -> Production
    Task<FileCardDto> EnterPrototypeAsync(Guid fileId, Guid actor, CancellationToken ct); // Admin/Engineer: Initial -> Prototype (never-approved; no rev/history)
    Task<FileCardDto> ExitPrototypeAsync(Guid fileId, Guid actor, CancellationToken ct);  // Admin/Engineer: Prototype -> Initial (no rev/history)
    Task<IReadOnlyList<FileCardDto>> SearchAsync(SearchQuery query, Guid actor, CancellationToken ct);

    // Admin-only:
    Task<IReadOnlyList<RevisionBackupDto>> ListRevisionsAsync(Guid fileId, Guid actor, CancellationToken ct);
    Task<FileCardDto> RollbackAsync(Guid fileId, string targetRevision, Guid actor, CancellationToken ct);
}
