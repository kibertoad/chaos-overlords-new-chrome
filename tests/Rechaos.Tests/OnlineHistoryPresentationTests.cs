using Rechaos.Game;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineHistoryPresentationTests
{
    [Fact]
    public void RowNamesThePlayerTheCodeAndTheRole()
    {
        Assert.Equal("ADA  CODE1234  HOST", OnlineHistoryPresentation.Row(Recovery()));
        Assert.Equal(
            "ADA  CODE1234  PLAYER",
            OnlineHistoryPresentation.Row(Recovery() with { IsHost = false }));
    }

    [Fact]
    public void RoleIsWhatTheSeatHolds()
    {
        Assert.Equal("HOST", OnlineHistoryPresentation.Role(Recovery()));
        Assert.Equal("PLAYER", OnlineHistoryPresentation.Role(Recovery() with { IsHost = false }));
    }

    private static MultiplayerRecovery Recovery() => new(
        MultiplayerRecovery.CurrentFormatVersion,
        "https://games.example.test/",
        "match-1",
        "player-1",
        "cop_secret",
        "CODE1234",
        "ADA",
        IsHost: true,
        CleanExit: false,
        Completed: false,
        SessionVersion: MultiplayerSessionVersion.Current);
}
