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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CleanOrCompletedMembershipDoesNotSuggestReconnect(bool cleanExit, bool completed)
    {
        var recovery = Recovery(cleanExit, completed);

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), recovery));

        Assert.False(MultiplayerRecoveryStore.Load(Path())!.ShouldSuggestReconnect);
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
