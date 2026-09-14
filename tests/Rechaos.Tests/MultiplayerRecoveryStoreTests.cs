using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class MultiplayerRecoveryStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory =
        Directory.CreateTempSubdirectory("rechaos-multiplayer-recovery-");

    [Fact]
    public void ActiveMembershipRoundTripsAndSuggestsReconnect()
    {
        var expected = Recovery(CleanExit: false, Completed: false);

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), expected));

        var loaded = Assert.IsType<MultiplayerRecovery>(MultiplayerRecoveryStore.Load(Path()));
        Assert.Equal(expected, loaded);
        Assert.True(loaded.ShouldSuggestReconnect);
    }

    [Fact]
    public void CleanExitIsKeptButDoesNotSuggestReconnect()
    {
        var recovery = Recovery(CleanExit: true, Completed: false);

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), recovery));

        Assert.False(MultiplayerRecoveryStore.Load(Path())!.ShouldSuggestReconnect);
    }

    /// <summary>
    /// A membership token is a full capability for its seat. Once the match is over the token opens
    /// nothing anybody wants, so keeping it on disk is a live credential for no benefit.
    /// </summary>
    [Fact]
    public void CompletedMembershipIsNotKept()
    {
        var completed = Recovery(CleanExit: true, Completed: true);

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), completed));

        Assert.Null(MultiplayerRecoveryStore.Load(Path()));
    }

    /// <summary>
    /// Written by a build that predates the sealed token, and still readable: a player upgrading
    /// mid-match keeps the reconnect they were promised.
    /// </summary>
    [Fact]
    public void ReadsTheUnsealedHistoryFormat()
    {
        var recovery = Recovery(CleanExit: false, Completed: false);
        File.WriteAllText(Path(), System.Text.Json.JsonSerializer.Serialize(
            new { FormatVersion = 2, Sessions = new[] { recovery } }));

        Assert.Equal(recovery, Assert.Single(MultiplayerRecoveryStore.LoadAll(Path())));
    }

    /// <summary>The token does not sit in the file as the player would read it back.</summary>
    [Fact]
    public void SealsTheTokenWhereThePlatformCan()
    {
        if (!OperatingSystem.IsWindows()) return;
        var recovery = Recovery(CleanExit: false, Completed: false);

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), recovery));

        Assert.DoesNotContain(recovery.Token, File.ReadAllText(Path()), StringComparison.Ordinal);
        Assert.Equal(recovery, MultiplayerRecoveryStore.Load(Path()));
    }

    /// <summary>A file copied from another account has a token this one cannot open.</summary>
    [Fact]
    public void DropsAMembershipWhoseSealedTokenWillNotOpen()
    {
        File.WriteAllText(Path(), System.Text.Json.JsonSerializer.Serialize(new
        {
            FormatVersion = 3,
            Sessions = new[]
            {
                new
                {
                    FormatVersion = MultiplayerRecovery.CurrentFormatVersion,
                    Server = "https://games.example.test/",
                    MatchId = "match-1",
                    PlayerId = "player-1",
                    JoinCode = "CODE1234",
                    DisplayName = "ADA",
                    IsHost = true,
                    CleanExit = false,
                    Completed = false,
                    ProtectedToken = "bm90LWEtcmVhbC1ibG9i"
                }
            }
        }));

        Assert.Empty(MultiplayerRecoveryStore.LoadAll(Path()));
    }

    [Fact]
    public void KeepsEveryUnfinishedMembershipInMostRecentOrder()
    {
        var first = Recovery(CleanExit: true, Completed: false);
        var second = first with { MatchId = "match-2", PlayerId = "player-2" };

        Assert.True(MultiplayerRecoveryStore.TrySaveAll(Path(), [second, first]));

        Assert.Equal([second, first], MultiplayerRecoveryStore.LoadAll(Path()));
        Assert.All(MultiplayerRecoveryStore.LoadAll(Path()), item => Assert.True(item.CanReconnect));
    }

    [Fact]
    public void ReadsTheLegacySingleMembershipFormat()
    {
        var legacy = Recovery(CleanExit: false, Completed: false);
        File.WriteAllText(Path(), System.Text.Json.JsonSerializer.Serialize(legacy));

        Assert.Equal(legacy, Assert.Single(MultiplayerRecoveryStore.LoadAll(Path())));
    }

    [Fact]
    public void CorruptRecoveryIsIgnored()
    {
        File.WriteAllText(Path(), "not-json");

        Assert.Null(MultiplayerRecoveryStore.Load(Path()));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private MultiplayerRecovery Recovery(bool CleanExit, bool Completed) => new(
        MultiplayerRecovery.CurrentFormatVersion,
        "https://games.example.test/",
        "match-1",
        "player-1",
        "cop_secret",
        "CODE1234",
        "ADA",
        IsHost: true,
        CleanExit,
        Completed);

    private string Path() => System.IO.Path.Combine(_directory.FullName, "recovery.json");
}
