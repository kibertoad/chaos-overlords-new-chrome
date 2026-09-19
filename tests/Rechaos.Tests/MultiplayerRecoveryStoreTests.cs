using Rechaos.Game;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

public sealed class MultiplayerRecoveryStoreTests : IDisposable
{
    private static readonly DateTimeOffset LastPlayed =
        new(2026, 4, 17, 21, 5, 0, TimeSpan.FromHours(2));

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
    public async Task ReconciliationDropsOnlyMembershipsTheServerHasDefinitelyRetired()
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        var deleted = Recovery(CleanExit: true, Completed: false) with { MatchId = "deleted" };
        var unreachable = Recovery(CleanExit: true, Completed: false) with { MatchId = "offline" };
        server.Answer(HttpMethod.Get, "/matches/deleted", null, System.Net.HttpStatusCode.Unauthorized);
        server.Answer(HttpMethod.Get, "/matches/offline", null, System.Net.HttpStatusCode.BadGateway);

        var unavailable = await MultiplayerRecoveryReconciliation.FindUnavailableAsync(
            http, [deleted, unreachable], TestContext.Current.CancellationToken);

        Assert.Equal([deleted], unavailable);
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

    /// <summary>
    /// The session password is kept in the clear, because the player who resumes is the one who
    /// has to read it out to whoever joins next.
    /// </summary>
    [Fact]
    public void SessionPasswordIsKeptWithTheMembership()
    {
        var recovery = Recovery(CleanExit: false, Completed: false) with { Password = "GANGWAR" };

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), recovery));

        Assert.Equal(recovery, MultiplayerRecoveryStore.Load(Path()));
    }

    /// <summary>A file from a build that wrote no password reads back as a session without one.</summary>
    [Fact]
    public void MembershipWithoutAStoredPasswordHasNone()
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
                    Token = "cop_secret"
                }
            }
        }));

        Assert.Equal(string.Empty, Assert.Single(MultiplayerRecoveryStore.LoadAll(Path())).Password);
    }

    /// <summary>
    /// The session version rides with the membership so the browser can say a seat cannot be
    /// taken without dialing the server for it first.
    /// </summary>
    [Fact]
    public void SessionVersionIsKeptWithTheMembership()
    {
        var recovery = Recovery(CleanExit: false, Completed: false) with
        {
            SessionVersion = MultiplayerSessionVersion.Current + 1
        };

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), recovery));

        var loaded = MultiplayerRecoveryStore.Load(Path())!;
        Assert.Equal(recovery, loaded);
        Assert.False(loaded.IsCompatible);
        Assert.False(loaded.CanResume);
        // Still held: the seat is the player's, and the browser owes them the reason it cannot be
        // taken rather than dropping the row.
        Assert.True(loaded.CanReconnect);
        Assert.False(loaded.ShouldSuggestReconnect);
    }

    /// <summary>A file from a build that wrote no session version is the first one.</summary>
    [Fact]
    public void MembershipWithoutAStoredSessionVersionIsTheInitialOne()
    {
        WriteMembershipWithoutNewRecoveryMetadata();

        var loaded = Assert.Single(MultiplayerRecoveryStore.LoadAll(Path()));
        Assert.Equal(MultiplayerSessionVersion.Initial, loaded.SessionVersion);
        Assert.True(loaded.CanResume);
    }

    /// <summary>
    /// The match's name reaches every member on the wire, not only the host who typed it, so the
    /// list of unfinished sessions can name the game whichever seat the player held.
    /// </summary>
    [Fact]
    public void SessionNameAndLastUpdateAreKeptForAJoinerToo()
    {
        var joiner = Recovery(CleanExit: false, Completed: false) with { IsHost = false };

        Assert.True(MultiplayerRecoveryStore.TrySave(Path(), joiner));

        var loaded = Assert.Single(MultiplayerRecoveryStore.LoadAll(Path()));
        Assert.Equal("NIGHT OF THE LONG KNIVES", loaded.SessionName);
        Assert.Equal(LastPlayed, loaded.LastUpdatedAt);
    }

    /// <summary>
    /// A file from a build that stored neither reads back as a membership that knows less about
    /// itself, not as one that cannot be resumed.
    /// </summary>
    [Fact]
    public void MembershipWithoutASessionNameOrUpdateTimeKeepsItsSeat()
    {
        WriteMembershipWithoutNewRecoveryMetadata();

        var loaded = Assert.Single(MultiplayerRecoveryStore.LoadAll(Path()));
        Assert.Equal(string.Empty, loaded.SessionName);
        Assert.Null(loaded.LastUpdatedAt);
        Assert.True(loaded.CanReconnect);
    }

    private void WriteMembershipWithoutNewRecoveryMetadata()
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
                    IsHost = false,
                    CleanExit = false,
                    Completed = false,
                    Token = "cop_secret"
                }
            }
        }));

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
        Completed,
        Password: string.Empty,
        SessionName: "NIGHT OF THE LONG KNIVES",
        LastUpdatedAt: LastPlayed);

    private string Path() => System.IO.Path.Combine(_directory.FullName, "recovery.json");
}
