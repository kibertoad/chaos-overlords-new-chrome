using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalPlayerNameTests
{
    [Theory]
    [InlineData("Ada Lovelace", "ADA LOVELA")]
    [InlineData("a\u0001b", "A B")]
    [InlineData("\u738b", "")]
    [InlineData("\u017fMGISLANDS", "SMGISLANDS")]
    [InlineData("SMG\u0131SLANDS", "SMG SLANDS")]
    [InlineData("A\u00dfB", "A B")]
    [InlineData("A\ufb01B", "A B")]
    [InlineData("A\u0149B", "A B")]
    [InlineData("SMG\u212aICKASS", "SMG ICKASS")]
    public void ProjectMatchesTheNativeFixedNameRecord(string modernName, string expected)
    {
        Assert.Equal(expected, OriginalPlayerName.Project(modernName));
    }

    [Fact]
    public void ProjectOrFallbackKeepsAPlayableNameWhenTheLobbyNameHasNoNativeGlyphs()
    {
        Assert.Equal("PLAYER 1", OriginalPlayerName.ProjectOrFallback("\u738b", "player 1"));
    }
}
