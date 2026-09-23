using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalPlayerNameTests
{
    [Theory]
    [InlineData("Ada Lovelace", "ADA LOVELA")]
    [InlineData("a\u0001b", "A B")]
    [InlineData("smgm\u0131lk", "SMGM LK")]
    [InlineData("long \u017f", "LONG S")]
    [InlineData("\u738b", "")]
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
