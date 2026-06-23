namespace Vault.Domain.Entities;

/// <summary>
/// A logical vault. The CAD vault ships first; a documentation vault (and any later
/// vault) is added by inserting one row here — filename uniqueness and search are
/// scoped per repository, so a second vault is additive, not a schema change.
/// </summary>
public class Repository
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";     // stable code, e.g. "CAD" / "DOCS"; unique
    public string Name { get; set; } = "";     // display name, e.g. "CAD Files"
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Seeded vaults. Upload defaults to <see cref="DefaultKey"/> when none is supplied.</summary>
public static class WellKnownRepositories
{
    public const string DefaultKey = "CAD";
    public static readonly Guid DefaultId = Guid.Parse("0a000000-0000-0000-0000-000000000cad");
}
