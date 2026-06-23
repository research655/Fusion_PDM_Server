using Vault.Contracts;

namespace Vault.Api.Services;

/// <summary>
/// Placeholder so the app compiles and runs. Every method is unimplemented and
/// maps to HTTP 501. TODO(Cursor): implement against VaultDbContext, the seeded
/// domain logic (RevisionSequence, FileStateMachine, FileNumber, FileNaming,
/// ApprovalPolicy), IFileStore, and INotificationService.
/// </summary>
public sealed class StubVaultService : IVaultService
{
    private static Task<T> NotDone<T>() => throw new NotImplementedException("Not implemented yet.");
    private static Task NotDone() => throw new NotImplementedException("Not implemented yet.");

    public Task<AuthResponse> AuthenticateGoogleAsync(GoogleAuthRequest req, CancellationToken ct) => NotDone<AuthResponse>();
    public Task<FileCardDto> UploadAsync(Stream content, string fileName, string number, string description, Guid actor, string? repositoryKey, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> GetAsync(Guid id, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task CheckOutAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone();
    public Task CheckInAsync(Guid fileId, Stream content, Guid actor, CancellationToken ct) => NotDone();
    public Task<FileDownload> GetContentAsync(Guid id, Guid actor, CancellationToken ct) => NotDone<FileDownload>();
    public Task SubmitForApprovalAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone();
    public Task<FileCardDto> ApproveAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task RejectAsync(Guid fileId, string? reason, Guid actor, CancellationToken ct) => NotDone();
    public Task<FileCardDto> ReturnToEditableAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> UpdateCardAsync(Guid fileId, UpdateCardRequest req, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> ForceCheckInAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> BeginChangeAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> MarkObsoleteAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> ReactivateAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> EnterPrototypeAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<FileCardDto> ExitPrototypeAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
    public Task<IReadOnlyList<FileCardDto>> SearchAsync(SearchQuery query, Guid actor, CancellationToken ct) => NotDone<IReadOnlyList<FileCardDto>>();
    public Task<IReadOnlyList<RevisionBackupDto>> ListRevisionsAsync(Guid fileId, Guid actor, CancellationToken ct) => NotDone<IReadOnlyList<RevisionBackupDto>>();
    public Task<FileCardDto> RollbackAsync(Guid fileId, string targetRevision, Guid actor, CancellationToken ct) => NotDone<FileCardDto>();
}
