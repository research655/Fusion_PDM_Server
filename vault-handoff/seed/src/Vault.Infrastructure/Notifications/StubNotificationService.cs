using Microsoft.Extensions.Logging;
using Vault.Domain.Abstractions;
using Vault.Domain.Entities;

namespace Vault.Infrastructure.Notifications;

/// <summary>No-op logger. TODO(Cursor): real Asana tasks + notifications.</summary>
public sealed class StubNotificationService : INotificationService
{
    private readonly ILogger<StubNotificationService> _log;
    public StubNotificationService(ILogger<StubNotificationService> log) => _log = log;

    public Task OnSubmittedForApprovalAsync(VaultFile file, bool isResubmission, CancellationToken ct)
    {
        _log.LogInformation("[stub] {File} submitted for approval (resubmission={Resub})", file.Name, isResubmission);
        return Task.CompletedTask;
    }

    public Task OnApprovedAsync(VaultFile file, CancellationToken ct)
    {
        _log.LogInformation("[stub] {File} approved -> revision {Rev}", file.Name, file.Revision);
        return Task.CompletedTask;
    }

    public Task OnRejectedAsync(VaultFile file, string? reason, CancellationToken ct)
    {
        _log.LogInformation("[stub] {File} rejected: {Reason}", file.Name, reason);
        return Task.CompletedTask;
    }
}
