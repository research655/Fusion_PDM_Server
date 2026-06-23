namespace Vault.Domain.Files;

/// <summary>
/// Uniqueness rule: filename WITHOUT extension, lower-cased.
/// Case-insensitive and extension-agnostic, so "Bracket.f3d" and "bracket.step" collide.
/// Store this key on the file row with a UNIQUE index.
/// </summary>
public static class FileNaming
{
    public static string ToKey(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        var withoutExt = Path.GetFileNameWithoutExtension(fileName);
        return withoutExt.Trim().ToLowerInvariant();
    }
}
