using Vault.Domain.Files;
using Xunit;

namespace Vault.Tests;

public class FileNamingTests
{
    [Theory]
    [InlineData("Bracket.f3d", "bracket")]
    [InlineData("bracket.step", "bracket")]   // same key as above -> collision
    [InlineData("BRACKET.F3D", "bracket")]
    [InlineData("Part-001", "part-001")]      // no extension
    [InlineData("Sub Assembly.dwg", "sub assembly")]
    public void ToKey_strips_extension_and_lowercases(string name, string expected)
        => Assert.Equal(expected, FileNaming.ToKey(name));
}
