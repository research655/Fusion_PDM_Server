using System.Text.RegularExpressions;

namespace Vault.Domain.Files;

/// <summary>
/// "Number" data-card field: user-entered, free-form, but restricted to
/// letters, numbers, spaces, hyphens, and underscores. Must be non-empty.
/// </summary>
public static partial class FileNumber
{
    [GeneratedRegex(@"^[A-Za-z0-9 _-]+$")]
    private static partial Regex Allowed();

    public static bool IsValid(string number)
        => !string.IsNullOrWhiteSpace(number) && Allowed().IsMatch(number);

    public static void Validate(string number)
    {
        if (!IsValid(number))
            throw new ArgumentException(
                "Number may contain only letters, numbers, spaces, hyphens, and underscores.",
                nameof(number));
    }
}
