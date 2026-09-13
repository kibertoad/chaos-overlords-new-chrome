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
