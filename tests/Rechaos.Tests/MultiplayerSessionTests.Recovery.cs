using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// How the session keeps itself in step with the server when the stream, the outbox or a restart
/// leaves it behind: silence, gaps, stale repairs, and a loop that cannot go on.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    private static readonly OrderDocument EmptyOrders =
        new(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);

    private static string Readiness(int seq, string playerId = "p2") =>
        Frame(seq, "turn.readiness", $$"""{"turn":1,"playerId":"{{playerId}}","ready":true}""");

    /// <summary>The state every client is on after the empty sealed sets for the first turns.</summary>
    private static string HashAfterTurns(int turns)
    {
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(BundledOriginalData.Load(), Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        for (var turn = 1; turn <= turns; turn++) SealedTurnApplier.Apply(replay, SealedOrders(turn));
        return MatchStateHasher.ComputeSha256(replay.State);
    }

    /// <summary>
    /// A stream that goes quiet past the heartbeat is dropped and reopened from the last sequence.
    /// </summary>
    /// <remarks>
    /// A socket the network has forgotten about never says so. Without a deadline measured from
    /// the last byte, the session would sit on it for the rest of the match, missing every turn.
    /// </remarks>
    [Fact]
    public async Task ReconnectsWhenTheStreamGoesSilent()
    {
        var (session, server, http) = Running(streamIdleTimeout: TimeSpan.FromMilliseconds(250));
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") >= 2, "the reconnect");
        // Written straight after the reconnect, well inside the idle window, so it lands on the
        // connection the session is now reading.
        server.Events.Write(Readiness(8));
        var readiness = await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen);

        Assert.Equal(1, readiness.Ready);
        var dropped = Assert.IsType<MultiplayerNotice.ConnectionChanged>(
            seen.First(notice => notice is MultiplayerNotice.ConnectionChanged));
        Assert.False(dropped.IsConnected);
        Assert.Contains("carried nothing", dropped.Detail, StringComparison.Ordinal);
        Assert.All(
            server.Requests.Where(request => request.Path.EndsWith("/stream", StringComparison.Ordinal)),
            request => Assert.Equal("7", request.LastEventId));
        Assert.Contains(seen, notice => notice is MultiplayerNotice.ConnectionChanged { IsConnected: true });
    }

    /// <summary>
    /// A live event that skips past the next sequence is not acted on: the session reconciles with
    /// the server from the last sequence it applied, exactly as a restart would.
    /// </summary>
    [Fact]
    public async Task ResynchronisesFromTheLastAppliedSequenceWhenTheStreamSkipsOne()
    {
        var view = View() with { LastEventSeq = 9 };
        MatchEvent[] missed =
        [
            new TurnOpenedEvent(8, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnOpenedEvent(9, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
        ];
        var (session, server, http) = Running(configure: fake =>
        {
            fake.Answer(HttpMethod.Get, $"/matches/{MatchId}", new MatchDetail(view, "CODE1234", "p1"));
            fake.Answer(
                HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
            fake.Answer(HttpMethod.Get, "/events", new EventPage(missed));
            fake.Answer(
                HttpMethod.Get,
                "/turns/1/orders/mine",
                new OwnSubmissionView(1, null, Ready: false, OrdersHash: null));
        });
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();
        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") == 1, "the stream");

        server.Events.Write(Readiness(9));
        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session, seen);

        Assert.Equal(1, resumed.State.Coordinator.Turn);
        Assert.NotNull(resumed.Turn);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.ReadinessChanged { Ready: 1 });
        Assert.Contains(seen, notice => notice is MultiplayerNotice.ConnectionChanged
        {
            IsConnected: false,
            Detail: { } detail,
        } && detail.Contains("jumped from sequence 7 to 9", StringComparison.Ordinal));
        var history = Assert.Single(
            server.Requests, request => request.Path.EndsWith("/events", StringComparison.Ordinal));
        Assert.Contains("after=7", history.Query, StringComparison.Ordinal);

        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") == 2, "the resumed stream");
        Assert.Equal(
            "9",
            server.Requests.Last(request => request.Path.EndsWith("/stream", StringComparison.Ordinal)).LastEventId);
        server.Events.Write(Readiness(10));
        Assert.Equal(1, (await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen)).Ready);
        Assert.Contains(seen, notice => notice is MultiplayerNotice.ConnectionChanged { IsConnected: true });
    }

    /// <summary>A sealed set answered for a turn other than the one asked for is refused.</summary>
    [Fact]
    public async Task RefusesASealedSetAnsweredForAnotherTurn()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var other = SealedOrders(2);
        server.Answer(HttpMethod.Get, "/turns/1/orders", other);

        server.Events.Write(Frame(
            8, "turn.sealed", $$"""{"turn":1,"orderSetHash":"{{other.OrderSetHash}}"}"""));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("set for turn 2", failed.Reason, StringComparison.Ordinal);
    }

    /// <summary>A seal for a turn the match has not reached cannot be applied to the turn it is on.</summary>
    [Fact]
    public async Task RefusesASealForATurnAheadOfTheMatch()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        server.Events.Write(SealedFrame(8, 2));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("sealed turn 2 while the match was on turn 1", failed.Reason, StringComparison.Ordinal);
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
    }

    /// <summary>
    /// A repair for a turn older than the one last resolved is a repeat, and adopting it would
    /// throw away every turn applied since.
    /// </summary>
    [Fact]
    public async Task IgnoresARepairForATurnTheMatchHasLeftBehind()
    {
        var (session, server, http) = Running(ownPlayerId: "p2");
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        server.Events.Write(SealedFrame(8, 1));
        server.Events.Write(SealedFrame(9, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session);
        Assert.Equal(3, (await WaitFor<MultiplayerNotice.TurnResolved>(session)).State.Coordinator.Turn);

        server.Events.Write(Frame(
            10,
            "snapshot.available",
            $$"""{"turn":0,"formatVersion":1,"stateHash":"aa","uploadedByPlayerId":"p1"}"""));
        server.Events.Write(Readiness(11, "p1"));
        var seen = new List<MultiplayerNotice>();
        await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen);

        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/snapshots/0"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Resynced or MultiplayerNotice.Failed);
    }

    /// <summary>The host is already on the state it uploaded, so it neither loads nor re-reports it.</summary>
    [Fact]
    public async Task TheHostDoesNotReloadTheRepairItUploaded()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var ours = MatchStateHasher.ComputeSha256(session.Bootstrap.State);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        server.Events.Write(Frame(8, "turn.desynced", Desync(ours)));
        await WaitFor<MultiplayerNotice.Desynced>(session);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the repair upload");

        server.Events.Write(Frame(
            9,
            "snapshot.available",
            $"{{\"turn\":1,\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion},"
            + $"\"stateHash\":\"{ours}\",\"uploadedByPlayerId\":\"p1\"}}"));
        server.Events.Write(Readiness(10));
        var seen = new List<MultiplayerNotice>();
        await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen);

        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/snapshots/1"));
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/turns/1/report"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Resynced);
    }

    /// <summary>
    /// A seal the log never confirms after it is one this client's report never reached the server
    /// for, so the restart makes that report.
    /// </summary>
    [Fact]
    public async Task RestartReportsTheLastReconstructedSealTheLogNeverConfirmed()
    {
        var view = ViewAtTurn(2) with { LastEventSeq = 3 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new TurnOpenedEvent(3, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
        ];
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        await WaitFor<MultiplayerNotice.Resumed>(session);

        var report = Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/turns/1/report"));
        Assert.Contains(HashAfterTurns(1), report, StringComparison.Ordinal);
    }

    /// <summary>A seal the log went on to confirm needs nothing said about it again.</summary>
    [Fact]
    public async Task RestartDoesNotReReportASealTheLogConfirmed()
    {
        var view = ViewAtTurn(2) with { LastEventSeq = 4 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new TurnConfirmedEvent(3, MatchId, "2026-09-10T12:01:30.000Z", new(1, HashAfterTurns(1))),
            new TurnOpenedEvent(4, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
        ];
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/turns/1/report"));
    }

    /// <summary>
    /// A turn that resolves between reading the view and reading the snapshot leaves the snapshot
    /// ahead of the view; the view is the authority, so it is read again rather than the session ended.
    /// </summary>
    [Fact]
    public async Task RestartReReadsTheViewWhenTheSnapshotIsAheadOfIt()
    {
        var definitions = BundledOriginalData.Load();
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        SealedTurnApplier.Apply(replay, SealedOrders(2));
        var snapshot = new SnapshotView(
            2,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MatchStateHasher.ComputeSha256(replay.State),
            "p1",
            "2026-09-10T12:04:00.000Z",
            MatchStateClone.ToBase64(replay.State));
        var (session, server, http) = Running(
            matchView: ViewAtTurn(2),
            configure: fake =>
            {
                fake.AnswerOnce(
                    HttpMethod.Get, $"/matches/{MatchId}", new MatchDetail(ViewAtTurn(2), "CODE1234", "p1"));
                fake.Answer(
                    HttpMethod.Get, $"/matches/{MatchId}", new MatchDetail(ViewAtTurn(3), "CODE1234", "p1"));
                fake.Answer(HttpMethod.Get, "/snapshots/latest", snapshot);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(HistoricalEvents(ViewAtTurn(3))));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/3/orders/mine",
                    new OwnSubmissionView(3, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(3, resumed.State.Coordinator.Turn);
        Assert.Equal(2, server.CallsTo(HttpMethod.Get, $"/matches/{MatchId}"));
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
    }

    /// <summary>
    /// A revoked membership met by the outbox is the end of the session, not a refused document,
    /// and the pump stops with it.
    /// </summary>
    [Fact]
    public async Task ARevokedMembershipMetByTheOutboxEndsTheWholeSession()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Put, "/orders", Envelope("revoked"), HttpStatusCode.Unauthorized);
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        var seen = new List<MultiplayerNotice>();

        session.QueueOrders(1, EmptyOrders, ready: true);
        var failed = await WaitFor<MultiplayerNotice.Failed>(session, seen);

        Assert.Equal("You are no longer in this match.", failed.Reason);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.OrdersRefused);

        server.Events.Write(SealedFrame(8, 1));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        while (session.TryDequeueNotice(out var notice)) seen.Add(notice);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.TurnResolved);
        Assert.Single(seen, notice => notice is MultiplayerNotice.Failed);
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    /// <summary>A pump that has failed for good takes the outbox down with it.</summary>
    [Fact]
    public async Task AFailedPumpStopsTheOutboxToo()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(
            HttpMethod.Get, "/turns/1/orders", SealedOrders(1) with { OrderSetHash = new string('0', 64) });
        server.Events.Write(SealedFrame(8, 1));
        await WaitFor<MultiplayerNotice.Failed>(session);

        session.QueueOrders(1, EmptyOrders, ready: true);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(0, server.CallsTo(HttpMethod.Put, "/turns/1/orders"));
        var seen = new List<MultiplayerNotice>();
        while (session.TryDequeueNotice(out var notice)) seen.Add(notice);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.OrdersAccepted or MultiplayerNotice.Failed);
    }

    /// <summary>A document that supersedes one still being retried takes its retry log with it.</summary>
    [Fact]
    public async Task ASupersededRetryClearsItsDisconnectedReport()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.AnswerOnce(HttpMethod.Put, "/orders", null, HttpStatusCode.BadGateway);
        var seen = new List<MultiplayerNotice>();

        session.QueueOrders(1, EmptyOrders, ready: false);
        var down = await WaitFor<MultiplayerNotice.ConnectionChanged>(session, seen);
        Assert.False(down.IsConnected);

        var block = server.BlockOnce(HttpMethod.Put, "/orders");
        session.QueueOrders(1, EmptyOrders, ready: false);
        var up = await WaitFor<MultiplayerNotice.ConnectionChanged>(session, seen);

        Assert.True(up.IsConnected);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.OrdersAccepted);
        block.SetResult();
        await WaitFor<MultiplayerNotice.OrdersAccepted>(session, seen);
    }

    /// <summary>A vote fired after the session stopped is cancelled rather than sent.</summary>
    [Fact]
    public async Task AVoteAfterTheSessionStoppedIsCancelledNotSent()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await session.StopAsync();
        var requests = server.Requests.Count;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.VoteOnTakeoverAsync(
                "p2", TakeoverChoice.Wait, TestContext.Current.CancellationToken));

        Assert.Equal(requests, server.Requests.Count);
    }

    /// <summary>Stopping a session that has already stopped answers the same wind-down.</summary>
    [Fact]
    public async Task StoppingTwiceIsHarmless()
    {
        var (session, _, http) = Running();
        using var _unused = http;

        await session.StopAsync();
        await session.StopAsync();
        await session.DisposeAsync();

        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        await lobby.StopAsync();
        await lobby.StopAsync();
    }
}
