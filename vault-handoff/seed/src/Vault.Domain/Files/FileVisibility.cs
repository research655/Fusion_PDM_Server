using Vault.Domain.Entities;

namespace Vault.Domain.Files;

/// <summary>
/// Read-side access rule. Read-only Users (e.g. assemblers) may see released (Production)
/// files and Prototype files (one-off tooling / test items) — never work-in-progress,
/// rejected, or obsolete revisions. Engineers and Admins see every state. Applied on every
/// read path: get card, download content, and search.
/// </summary>
public static class FileVisibility
{
    public static bool CanView(UserRole role, FileState state)
        => role != UserRole.User || state is FileState.Production or FileState.Prototype;
}
