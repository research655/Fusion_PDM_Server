using Vault.Domain.Revisions;
using Xunit;

namespace Vault.Tests;

public class RevisionSequenceTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(8, "H")]
    [InlineData(9, "J")]   // skips I
    [InlineData(10, "K")]
    [InlineData(11, "M")]  // skips L
    [InlineData(24, "Z")]
    [InlineData(25, "AA")]
    public void FromOrdinal_skips_I_and_L(int ordinal, string expected)
        => Assert.Equal(expected, RevisionSequence.FromOrdinal(ordinal));

    [Fact]
    public void Next_from_null_is_A() => Assert.Equal("A", RevisionSequence.Next(null));

    [Theory]
    [InlineData("A", "B")]
    [InlineData("H", "J")]
    [InlineData("K", "M")]
    [InlineData("Z", "AA")]
    public void Next_increments(string current, string expected)
        => Assert.Equal(expected, RevisionSequence.Next(current));

    [Fact]
    public void Round_trips()
        => Assert.Equal("AB", RevisionSequence.FromOrdinal(RevisionSequence.ToOrdinal("AB")));

    [Theory]
    [InlineData("I")]
    [InlineData("L")]
    public void Invalid_letters_throw(string bad)
        => Assert.Throws<ArgumentException>(() => RevisionSequence.ToOrdinal(bad));
}
