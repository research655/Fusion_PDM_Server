using Vault.Domain.Entities;

namespace Vault.Domain.Abstractions;

/// <summary>Notification port (Asana in prod). Stub default is a no-op logger.</summary>
public interface INotificationService
{
    Task OnSubmittedForApprovalAsync(VaultFile file, bool isResubmission, CancellationToken ct);
    Task OnApprovedAsync(VaultFile file, CancellationToken ct);
    Task OnRejectedAsync(VaultFile file, string? reason, CancellationToken ct);
}
