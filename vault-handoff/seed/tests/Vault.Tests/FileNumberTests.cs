using Vault.Domain.Files;
using Xunit;

namespace Vault.Tests;

public class FileNumberTests
{
    [Theory]
    [InlineData("PART-001")]
    [InlineData("Bracket Assembly 2")]
    [InlineData("ENG_ITEM_42")]
    [InlineData("abc 123-XY_z")]
    public void Valid_numbers_pass(string n) => Assert.True(FileNumber.IsValid(n));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PART#1")]
    [InlineData("bad/slash")]
    [InlineData("dot.dot")]
    [InlineData("paren(s)")]
    public void Invalid_numbers_fail(string n) => Assert.False(FileNumber.IsValid(n));
}
