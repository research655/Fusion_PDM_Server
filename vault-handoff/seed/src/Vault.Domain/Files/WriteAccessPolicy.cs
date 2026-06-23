using Vault.Domain.Entities;

namespace Vault.Domain.Files;

/// <summary>
/// Write-access gate. Only Admins and Engineers may check out, check in, submit, or use
/// Prototype. Read-only Users (assemblers) may not — they can only view released/Prototype files.
/// </summary>
public static class WriteAccessPolicy
{
    public static bool CanEdit(UserRole role) => role is UserRole.Admin or UserRole.Engineer;
}
