using System.Text;

namespace Vault.Domain.Revisions;

/// <summary>
/// Engineering revision letters that SKIP I and L (per spec).
/// First approval = ordinal 1 = "A". Sequence: A B C D E F G H J K M N ... Z, AA, AB ...
/// Bijective base-24 over the 24-letter alphabet below.
/// </summary>
public static class RevisionSequence
{
    private const string Alphabet = "ABCDEFGHJKMNOPQRSTUVWXYZ"; // A-Z minus I and L
    private const int Radix = 24;

    /// <summary>Ordinal is 1-based: 1 -> "A".</summary>
    public static string FromOrdinal(int ordinal)
    {
        if (ordinal < 1)
            throw new ArgumentOutOfRangeException(nameof(ordinal), "Revision ordinal is 1-based (1 = A).");

        var sb = new StringBuilder();
        var n = ordinal;
        while (n > 0)
        {
            n--;
            sb.Insert(0, Alphabet[n % Radix]);
            n /= Radix;
        }
        return sb.ToString();
    }

    /// <summary>Convert a revision string back to its 1-based ordinal.</summary>
    public static int ToOrdinal(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Revision is required.", nameof(revision));

        var n = 0;
        foreach (var c in revision.ToUpperInvariant())
        {
            var idx = Alphabet.IndexOf(c);
            if (idx < 0)
                throw new ArgumentException($"Invalid revision letter '{c}' (I and L are not used).", nameof(revision));
            n = n * Radix + (idx + 1);
        }
        return n;
    }

    /// <summary>Next revision. Pass null for an unapproved file to get "A".</summary>
    public static string Next(string? current)
        => FromOrdinal(current is null ? 1 : ToOrdinal(current) + 1);
}
