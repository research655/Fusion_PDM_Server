using Vault.Domain.Entities;

namespace Vault.Domain.Files;

/// <summary>
/// Only Admins and Engineers may move a Production file to Under Change (begin change)
/// or to Obsolete. Users cannot.
/// </summary>
public static class ProductionPolicy
{
    public static bool CanChangeProductionState(UserRole role)
        => role is UserRole.Admin or UserRole.Engineer;
}
