using System.Diagnostics;
using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;
using static Rechaos.Tests.MultiplayerSpectatorSessionTests;

namespace Rechaos.Tests;

/// <summary>
/// The spectator view's background watch, driven against a fake server: the states the view draws
/// (waiting, following, ended) are the notices these tests read.
/// </summary>
/// <remarks>Spectating is part of DEV-NET-001: online play has no original to compare with.</remarks>
public sealed class MultiplayerSpectatorWatchTests
{
    private static readonly OriginalData Definitions = BundledOriginalData.Load();
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task JoinsByCodeAndWaitsWhileTheMatchIsInTheLobby()
    {
        var (watch, server, http) = Watch();
        using var _ = http;
        await using var __ = watch;
        var lobby = ViewOf(MatchStatus.Lobby, 0, 0);
        server.Answer(HttpMethod.Post, "/spectate", new SpectatorMembership(
            lobby, new SpectatorView("s1", "EVE", JoinedAt: "2026-10-06T12:00:00.000Z"), "cos_s1"));
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", lobby);

        watch.Join(new SpectateRequest("CODE1234", "EVE", Password: null));

        var joined = await Next<SpectatorNotice.Joined>(watch);
        Assert.Equal("s1", joined.Membership.Spectator.Id);
        Assert.Equal("cos_s1", joined.Membership.Token);
        var waiting = await Next<SpectatorNotice.Progressed>(watch);
        Assert.False(waiting.HasState);
        Assert.Null(waiting.State);
        Assert.False(waiting.IsComplete);
        var lines = SpectatorViewPresentation.Describe(
            waiting.View, waiting.HasState, waiting.ShownTurn, waiting.IsComplete, connected: true);
        Assert.Equal("WAITING FOR THE HOST TO START", lines.Standing);
        Assert.Equal("2 TURNS BEHIND", lines.Delay);
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/snapshots/latest"));
    }

    [Fact]
    public async Task FollowsEachTurnTheServerReleases()
    {
        var (watch, server, http) = Watch();
        using var _ = http;
        await using var __ = watch;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 3, 0));
        server.Answer(HttpMethod.Get, "/snapshots/latest", SnapshotOf(0, Bootstrap().State));
        server.AnswerOnce(HttpMethod.Get, "/events", Page(1, Opened(1, 1)));
        server.Answer(HttpMethod.Get, "/events", Page(1));

        watch.Resume(MatchId, "cos_s1");

        var first = await Next<SpectatorNotice.Progressed>(watch);
        Assert.True(first.HasState);
        Assert.NotNull(first.State);
        Assert.Equal(0, first.ShownTurn);
        Assert.Equal(
            "TURN 1 OF 3",
            SpectatorViewPresentation.Describe(first.View, true, first.ShownTurn, false, true).Progress);

        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.AnswerOnce(HttpMethod.Get, "/events", Page(4, Sealed(4, 1)));
        server.Answer(HttpMethod.Get, "/events", Page(4));
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 4, 1));

        var next = await Next<SpectatorNotice.Progressed>(watch, progressed => progressed.ShownTurn == 1);
        Assert.NotNull(next.State);
        Assert.Equal(2, next.State!.Coordinator.Turn);
        var lines = SpectatorViewPresentation.Describe(next.View, true, next.ShownTurn, false, true);
        Assert.Equal("TURN 2 OF 4", lines.Progress);
        Assert.Equal("FOLLOWING THE MATCH", lines.Standing);
    }

    [Fact]
    public async Task EndsWithTheReasonWhenTheTokenStopsWorking()
    {
        var (watch, server, http) = Watch();
        using var _ = http;
        await using var __ = watch;
        server.Answer(
            HttpMethod.Get, $"/spectate/{MatchId}", Envelope("invalid_token"), HttpStatusCode.Unauthorized);

        watch.Resume(MatchId, "cos_gone");

        var ended = await Next<SpectatorNotice.Ended>(watch);
        Assert.True(ended.MembershipGone);
        Assert.Equal("You are no longer in this match.", ended.Reason);
    }

    [Fact]
    public async Task StopsPollingOnceTheEndedMatchIsFullyShown()
    {
        var (watch, server, http) = Watch();
        using var _ = http;
        await using var __ = watch;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Abandoned, 2, 1));
        server.Answer(HttpMethod.Get, "/snapshots/latest", SnapshotOf(0, Bootstrap().State));
        // The fake answers by path, not by cursor, so the released page is served once and every
        // later read is the drained page a real server gives after it.
        server.AnswerOnce(HttpMethod.Get, "/events", Page(2, Opened(1, 1), Sealed(2, 1)));
        server.Answer(HttpMethod.Get, "/events", Page(2));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        watch.Resume(MatchId, "cos_s1");

        var last = await Next<SpectatorNotice.Progressed>(watch);
        Assert.True(last.IsComplete);
        Assert.Equal(1, last.ShownTurn);
        Assert.Equal(
            "THE MATCH WAS ABANDONED",
            SpectatorViewPresentation.Describe(last.View, true, last.ShownTurn, true, true).Standing);
        var reads = server.CallsTo(HttpMethod.Get, $"/spectate/{MatchId}");
        await Task.Delay(Poll * 10, TestContext.Current.CancellationToken);
        Assert.Equal(reads, server.CallsTo(HttpMethod.Get, $"/spectate/{MatchId}"));
    }

    [Fact]
    public async Task LeavingEndsTheTokenOnTheServer()
    {
        var (watch, server, http) = Watch();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Lobby, 0, 0));
        server.Answer(HttpMethod.Post, $"/spectate/{MatchId}/leave", null, HttpStatusCode.NoContent);

        watch.Resume(MatchId, "cos_s1");
        await Next<SpectatorNotice.Progressed>(watch);
        await watch.LeaveAsync();
        await watch.StopAsync();

        Assert.Equal(1, server.CallsTo(HttpMethod.Post, $"/spectate/{MatchId}/leave"));
    }

    private static (MultiplayerSpectatorWatch Watch, FakeMultiplayerServer Server, HttpClient Http) Watch()
    {
        var server = new FakeMultiplayerServer();
        var http = new HttpClient(server);
        var watch = new MultiplayerSpectatorWatch(
            http, new MultiplayerClientOptions(new Uri("http://server.test")), Definitions, Poll);
        return (watch, server, http);
    }

    /// <summary>The next notice of the kind asked for, skipping others, within the test's patience.</summary>
    private static async Task<T> Next<T>(MultiplayerSpectatorWatch watch, Func<T, bool>? matching = null)
        where T : SpectatorNotice
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < Patience)
        {
            while (watch.TryDequeueNotice(out var notice))
            {
                if (notice is T wanted && (matching is null || matching(wanted))) return wanted;
                if (notice is SpectatorNotice.Ended ended && typeof(T) != typeof(SpectatorNotice.Ended))
                    Assert.Fail($"the watch ended: {ended.Reason}");
            }
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"no {typeof(T).Name} within {Patience}");
    }
}
