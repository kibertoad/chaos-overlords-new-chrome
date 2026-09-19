using System.Net;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// The session, driven the way a match drives it: frames arriving on the event stream, and the calls
/// each one leads to.
/// </summary>
/// <remarks>
/// This is the part of the client a desync or a dropped match actually comes out of, and it was the
/// part with no tests. Everything here is about how the session reacts to a server rather than about
/// the rules — <see cref="MultiplayerSealedTurnTests"/> covers what a turn does to a match.
/// </remarks>
public sealed partial class MultiplayerSessionTests
{
    [Fact]
    public async Task RestartReplaysSealedTurnsAndRestoresTheCurrentSubmission()
    {
        var view = ViewAtTurn(3);
        var draft = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
                fake.Answer(HttpMethod.Get, "/turns/3/orders", SealedOrders(3));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/3/orders/mine",
                    new OwnSubmissionView(3, draft, Ready: true, OrderDigest.OfDocument(draft)));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.True(session.IsRestoring);
        Assert.Equal(3, resumed.State.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, resumed.State.Coordinator.Phase);
        Assert.True(resumed.Submission.Ready);
        // The planning copy arrives built, with the draft already replayed onto it.
        Assert.Equal(0, Assert.IsType<SpeculativeTurn>(resumed.Turn).Orders.Count);
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/3/orders/mine"));

        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") == 1, "the resumed stream");
        Assert.Equal(
            "20",
            server.Requests.Single(request => request.Path.EndsWith("/stream")).LastEventId);
        server.Events.Write(SealedFrame(21, 3));
        var next = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        Assert.Equal(3, next.Turn);
        Assert.Equal(4, next.State.Coordinator.Turn);
    }

    [Fact]
    public async Task RestartUsesTheLatestSnapshotBeforeReplayingLaterTurns()
    {
        var definitions = BundledOriginalData.Load();
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        var snapshot = new SnapshotView(
            1,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            MatchStateHasher.ComputeSha256(replay.State),
            "p1",
            "2026-09-10T12:04:00.000Z",
            MatchStateClone.ToBase64(replay.State));
        var (session, server, http) = Running(
            matchView: ViewAtTurn(3),
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", snapshot);
                fake.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/3/orders/mine",
                    new OwnSubmissionView(3, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(3, resumed.State.Coordinator.Turn);
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
    }

    [Fact]
    public async Task RestartReplaysADepartureAtItsExactTurnBoundary()
    {
        var view = ViewAtTurn(3) with { LastEventSeq = 9 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(
                2, MatchId, "2026-09-10T12:01:00.000Z",
                new(1, SealedOrders(1).OrderSetHash)),
            new LobbyPlayerLeftEvent(
                3, MatchId, "2026-09-10T12:02:00.000Z",
                new("p2", LobbyPlayerLeftEventPayloadReason.Left)),
            new MatchTakeoverVoteRequestedEvent(
                4, MatchId, "2026-09-10T12:02:00.000Z", new("p2", 1)),
            new MatchTakeoverVoteCastEvent(
                5, MatchId, "2026-09-10T12:02:00.000Z",
                new("p2", "p1", MatchTakeoverVoteCastEventPayloadDecision.Computer)),
            new MatchPlayerTakenOverEvent(
                6, MatchId, "2026-09-10T12:02:00.000Z", new("p2")),
            new TurnOpenedEvent(7, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
            new TurnSealedEvent(
                8, MatchId, "2026-09-10T12:03:00.000Z",
                new(2, SealedOrdersForSlots(2, 0).OrderSetHash)),
            new TurnOpenedEvent(9, MatchId, "2026-09-10T12:04:00.000Z", new(3, null)),
        ];
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrdersForSlots(2, 0));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/3/orders/mine",
                    new OwnSubmissionView(3, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        var definitions = BundledOriginalData.Load();
        var expected = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(expected);
        SealedTurnApplier.Apply(expected, SealedOrders(1));
        expected.TransferPlayerToComputer(new PlayerId(1));
        SealedTurnApplier.Apply(expected, SealedOrdersForSlots(2, 0));
        Assert.Equal(PlayerController.Computer, resumed.State.Players[1].Setup.Controller);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(expected.State),
            MatchStateHasher.ComputeSha256(resumed.State));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/events"));
    }

    /// <summary>
    /// A reconnect whose only snapshot is the bootstrap one resumes from it.
    /// </summary>
    /// <remarks>
    /// The host uploads a snapshot for turn 0 before a turn has been played, and it remains the
    /// ordinary recovery baseline. Refusing it left a host that crashed during
    /// turn 1 unable to rejoin its own match at all: every retry read the same row.
    /// </remarks>
    [Fact]
    public async Task RestartResumesFromTheTurnZeroBootstrapSnapshot()
    {
        var definitions = BundledOriginalData.Load();
        var bootstrap = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(bootstrap);
        var snapshot = new SnapshotView(
            0,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            MatchStateHasher.ComputeSha256(bootstrap.State),
            "p1",
            "2026-09-10T11:59:30.000Z",
            MatchStateClone.ToBase64(bootstrap.State));
        var (session, server, http) = Running(
            matchView: ViewAtTurn(2),
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", snapshot);
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(2, resumed.State.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, resumed.State.Coordinator.Phase);
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    /// <summary>
    /// A player who joins a running match builds the city its peers are already playing.
    /// </summary>
    /// <remarks>
    /// Their own row is on the roster by the time they bootstrap, so generating a city from it
    /// would seat them under their own name where every peer seated a computer player under a
    /// derived one. Both feed the state hash, so turn one would desync.
    /// </remarks>
    [Fact]
    public async Task ALatePlayerTakesTheCityItsPeersAreOnRatherThanGeneratingItsOwn()
    {
        var definitions = BundledOriginalData.Load();
        IReadOnlyList<PlayerView> seatedAtStart = [Roster[0]];
        var bootstrap = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, seatedAtStart));
        CommandPhase.Enter(bootstrap);
        var snapshot = new SnapshotView(
            0,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            MatchStateHasher.ComputeSha256(bootstrap.State),
            "p1",
            "2026-09-10T11:59:30.000Z",
            MatchStateClone.ToBase64(bootstrap.State));
        IReadOnlyList<PlayerView> withLateJoiner =
        [
            Roster[0],
            new("late-1", 1, "DAVE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
        ];
        var view = View() with { Players = withLateJoiner, LastEventSeq = 2 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new MatchLatePlayerJoinedEvent(
                2, MatchId, "2026-09-10T12:00:30.000Z", new("late-1", 1)),
        ];
        var (session, _, http) = Running(
            ownPlayerId: "late-1",
            matchView: view,
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", snapshot);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/1/orders/mine",
                    new OwnSubmissionView(1, null, Ready: false, OrdersHash: null));
            },
            joinedInProgress: true);
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        var expected = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, seatedAtStart));
        CommandPhase.Enter(expected);
        expected.TransferPlayerToHuman(new PlayerId(1));
        Assert.True(session.IsRestoring);
        Assert.Equal(1, resumed.State.Coordinator.Turn);
        Assert.Equal("PLAYER 2", resumed.State.Players[1].Setup.Name);
        Assert.Equal(PlayerController.Human, resumed.State.Players[1].Setup.Controller);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(expected.State),
            MatchStateHasher.ComputeSha256(resumed.State));
    }

    /// <summary>
    /// The client the server promotes is the one that repairs the next desync.
    /// </summary>
    /// <remarks>
    /// A flag read once at bootstrap left every surviving client waiting for a host that had left,
    /// so nobody uploaded and the pause never lifted.
    /// </remarks>
    [Fact]
    public async Task APromotedHostRepairsTheNextDesync()
    {
        var (session, server, http) = Running(ownPlayerId: "p2");
        using var _ = http;
        await using var __ = session;
        Assert.False(session.IsHost);
        IReadOnlyList<PlayerView> afterPromotion =
        [
            new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Left, IsHost: false),
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: true),
        ];
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(
                View() with { Players = afterPromotion, HostPlayerId = "p2" }, "CODE1234", "p2"));

        server.Events.Write(Frame(8, "lobby.hostChanged", """{"hostPlayerId":"p2"}"""));
        await WaitFor<MultiplayerNotice.MatchUpdated>(session);

        Assert.True(session.IsHost);
        var ours = MatchStateHasher.ComputeSha256(session.Bootstrap.State);
        server.Events.Write(Frame(9, "turn.desynced", Desync(ours)));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.True(desynced.IsHostRepair);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the repair upload");
    }

    [Fact]
    public async Task RestartDuringTurnOneRestoresAnApprovedComputerSeat()
    {
        IReadOnlyList<PlayerView> afterLeaving =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Computer, IsHost: false),
        ];
        var view = View() with { Players = afterLeaving, LastEventSeq = 6 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new LobbyPlayerLeftEvent(
                2, MatchId, "2026-09-10T12:01:00.000Z",
                new("p2", LobbyPlayerLeftEventPayloadReason.Left)),
            new MatchTakeoverVoteRequestedEvent(
                3, MatchId, "2026-09-10T12:01:00.000Z", new("p2", 1)),
            new MatchTakeoverVoteCastEvent(
                4, MatchId, "2026-09-10T12:01:00.000Z",
                new("p2", "p1", MatchTakeoverVoteCastEventPayloadDecision.Computer)),
            new MatchPlayerTakenOverEvent(
                5, MatchId, "2026-09-10T12:01:00.000Z", new("p2")),
            new LobbyHostChangedEvent(
                6, MatchId, "2026-09-10T12:01:00.000Z",
                new("p1")),
        ];
        var (session, _, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/1/orders/mine",
                    new OwnSubmissionView(1, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.True(session.IsRestoring);
        Assert.Equal(1, resumed.State.Coordinator.Turn);
        Assert.Equal(PlayerController.Computer, resumed.State.Players[1].Setup.Controller);
    }

    [Fact]
    public async Task RestartRefusesAGapInTheAuthoritativeEventHistory()
    {
        var view = ViewAtTurn(2) with { LastEventSeq = 3 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(
                3, MatchId, "2026-09-10T12:01:00.000Z",
                new(1, SealedOrders(1).OrderSetHash)),
        ];
        var (session, _, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
            });
        using var _ = http;
        await using var __ = session;

        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("jumped from sequence 1 to 3", failed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestartRefusesACurrentSubmissionWhoseDigestDoesNotMatch()
    {
        var draft = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var (session, _, http) = Running(
            matchView: ViewAtTurn(2),
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, draft, Ready: false, new string('0', 64)));
            });
        using var _ = http;
        await using var __ = session;

        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("digest", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A sealed turn is fetched, checked, applied, and the hash it produced is reported.
    /// </summary>
    /// <remarks>
    /// The loop the whole match runs on. Reporting is the half nothing local would show: a client that
    /// applied the turn and never said so would look perfectly healthy and stall every peer waiting on
    /// its confirmation.
    /// </remarks>
    [Fact]
    public async Task ResolvesASealedTurnAndReportsTheHashItReached()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        server.Events.Write(SealedFrame(8, 1));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(1, resolved.Turn);
        Assert.Equal(2, resolved.State.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, resolved.State.Coordinator.Phase);

        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the report");
        var report = Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/turns/1/report"));
        Assert.Contains(resolved.StateHash, report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmedTurnDoesNotUploadAnotherFullSnapshot()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        server.Events.Write(SealedFrame(8, 1));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        server.Events.Write(Frame(
            9,
            "turn.confirmed",
            $$"""{"turn":1,"stateHash":"{{resolved.StateHash}}"}"""));

        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the report");
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/snapshots"));
        var report = Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/turns/1/report"));
        Assert.Contains("\"seatSummaries\"", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// A seal for a turn already applied is dropped rather than applied twice.
    /// </summary>
    /// <remarks>
    /// Delivery is at least once, so this is not an edge case — a resume, or the repair sweep
    /// finishing, repeats facts the client already holds. Applying one twice would advance the match a
    /// turn further than every peer, which is a desync that looks like nothing went wrong.
    /// </remarks>
    [Fact]
    public async Task DropsARepeatOfASealItHasAlreadyApplied()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        server.Events.Write(SealedFrame(8, 1));
        var first = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        server.Events.Write(SealedFrame(9, 1));
        server.Events.Write(SealedFrame(10, 2));
        var second = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        // Turn 2 resolving is what proves the repeat was dropped rather than merely slow: the session
        // handles the log in order, so anything it was going to do about turn 1 it did first.
        Assert.Equal(1, first.Turn);
        Assert.Equal(2, second.Turn);
        Assert.Equal(3, second.State.Coordinator.Turn);
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    /// <summary>
    /// A call that fails once is retried, and the match carries on.
    /// </summary>
    /// <remarks>
    /// This is the fix for a session that a single bad gateway used to end for good: the calls a
    /// received fact leads to are idempotent, so the only correct response to a server having a bad
    /// moment is to ask again.
    /// </remarks>
    [Fact]
    public async Task KeepsGoingWhenASealedSetTakesASecondAttempt()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.AnswerOnce(HttpMethod.Get, "/turns/1/orders", null, HttpStatusCode.BadGateway);
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        server.Events.Write(SealedFrame(8, 1));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(1, resolved.Turn);
        Assert.Equal(2, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    /// <summary>
    /// A sealed set that does not match the digest the seal announced is refused.
    /// </summary>
    /// <remarks>
    /// The digest is the only thing standing between a client and a set somebody substituted, so
    /// failing to verify has to end the session rather than be retried: asking again would fetch the
    /// same wrong answer.
    /// </remarks>
    [Fact]
    public async Task EndsTheSessionWhenASealedSetDoesNotMatchItsDigest()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var honest = SealedOrders(1);
        server.Answer(
            HttpMethod.Get,
            "/turns/1/orders",
            honest with { OrderSetHash = new string('0', 64) });

        server.Events.Write(SealedFrame(8, 1));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("digest", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A revoked token ends the session, because there is nothing left to read.</summary>
    [Fact]
    public async Task EndsTheSessionWhenItsMembershipIsGone()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", Envelope("revoked"), HttpStatusCode.Unauthorized);

        server.Events.Write(SealedFrame(8, 1));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Equal("You are no longer in this match.", failed.Reason);
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    /// <summary>
    /// The host repairs a desync only with a hash the players themselves reported.
    /// </summary>
    /// <remarks>
    /// Otherwise the host arbitrates a disagreement it is a party to: desync deliberately, upload a
    /// doctored state, and the match adopts it.
    /// </remarks>
    [Fact]
    public async Task UploadsARepairOnlyWhenItsOwnHashIsOneThePlayersReported()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var ours = MatchStateHasher.ComputeSha256(session.Bootstrap.State);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");

        server.Events.Write(Frame(8, "turn.desynced", Desync(ours)));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.True(desynced.IsHostRepair);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the upload");
        var upload = Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/snapshots"),
            body => body.Contains("\"turn\":1", StringComparison.Ordinal)
                && body.Contains(ours, StringComparison.Ordinal));
        Assert.Contains(ours, upload, StringComparison.Ordinal);
        // The body is a native save, so the version that describes it is the native save format's.
        Assert.Contains(
            $"\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion}",
            upload,
            StringComparison.Ordinal);
    }

    /// <summary>A host whose own state is the odd one out has nothing it is allowed to upload.</summary>
    [Fact]
    public async Task DoesNotUploadARepairWhenItsOwnStateIsTheOddOneOut()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");

        server.Events.Write(Frame(8, "turn.desynced", Desync(new string('7', 64))));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.False(desynced.IsHostRepair);
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/snapshots"));
    }

    /// <summary>
    /// A repair written by a newer build is named as one, not discovered as a decoding failure.
    /// </summary>
    /// <remarks>
    /// The snapshot is a native save, and saying which format it is in is the difference between a
    /// player being told their game is out of date and being told the connection was lost.
    /// </remarks>
    [Fact]
    public async Task RefusesARepairFromANewerSaveFormat()
    {
        var (session, server, http) = Running(ownPlayerId: "p2");
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/snapshots/3", new SnapshotView(
            3,
            NativeSaveSerializer.CurrentFormatVersion + 1,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            new string('a', 64),
            "p1",
            "2026-09-10T12:00:00.000Z",
            "QUJD"));

        server.Events.Write(Frame(8, "snapshot.available", """{"turn":3,"stateHash":"aa"}"""));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("save format", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A repair whose bytes are not a match this build can read says so.</summary>
    [Fact]
    public async Task RefusesARepairWhoseBodyIsNotAMatch()
    {
        var (session, server, http) = Running(ownPlayerId: "p2");
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/snapshots/3", new SnapshotView(
            3,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            new string('a', 64),
            "p1",
            "2026-09-10T12:00:00.000Z",
            "bm90IGEgc2F2ZQ=="));

        server.Events.Write(Frame(8, "snapshot.available", """{"turn":3,"stateHash":"aa"}"""));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("this build can read", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Readiness is counted against the seats the turn seals on.
    /// </summary>
    /// <remarks>
    /// "Waiting for the other players" does not say whether one opponent is deciding or four have
    /// closed the game, which is the one thing a player waiting on a turn wants to know.
    /// </remarks>
    [Fact]
    public async Task CountsReadinessAgainstTheSeatedRoster()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        server.Events.Write(Frame(8, "turn.readiness", """{"turn":1,"playerId":"p2","ready":true}"""));
        var first = await WaitFor<MultiplayerNotice.ReadinessChanged>(session);

        Assert.Equal(1, first.Ready);
        Assert.Equal(2, first.Seated);

        server.Events.Write(Frame(9, "turn.readiness", """{"turn":1,"playerId":"p2","ready":false}"""));
        var withdrawn = await WaitFor<MultiplayerNotice.ReadinessChanged>(session);

        Assert.Equal(0, withdrawn.Ready);
    }

    /// <summary>
    /// Readiness names the seats, not only how many of them there are.
    /// </summary>
    /// <remarks>
    /// The city screen marks the opponents who are still drafting under their own portraits, and a
    /// player id means nothing to a portrait: the seat behind it is what the interface can draw.
    /// </remarks>
    [Fact]
    public async Task ReportsWhichSeatsHaveFinishedTheTurn()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        server.Events.Write(Frame(8, "turn.readiness", """{"turn":1,"playerId":"p2","ready":true}"""));
        var readiness = await WaitFor<MultiplayerNotice.ReadinessChanged>(session);

        Assert.Equal([1], readiness.ReadySlots.Order());
        Assert.Equal([0, 1], readiness.AwaitedSlots.Order());
    }

    /// <summary>Readiness from a turn that has moved on does not carry over to the next.</summary>
    [Fact]
    public async Task ForgetsReadinessWhenTheTurnChanges()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        server.Events.Write(Frame(8, "turn.readiness", """{"turn":1,"playerId":"p2","ready":true}"""));
        await WaitFor<MultiplayerNotice.ReadinessChanged>(session);
        server.Events.Write(Frame(9, "turn.readiness", """{"turn":2,"playerId":"p1","ready":true}"""));
        var nextTurn = await WaitFor<MultiplayerNotice.ReadinessChanged>(session);

        Assert.Equal(2, nextTurn.Turn);
        Assert.Equal(1, nextTurn.Ready);
    }

    /// <summary>
    /// A seat whose player has left stops being one the tally waits on.
    /// </summary>
    /// <remarks>
    /// The server stops waiting on a departed seat, so a denominator that kept counting it would sit
    /// one short of a total the match is never going to reach — and a player reading "1/2" has no way
    /// to tell that from an opponent who is simply taking their time.
    /// </remarks>
    [Fact]
    public async Task StopsWaitingOnASeatWhosePlayerHasLeft()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        IReadOnlyList<PlayerView> afterLeaving =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Left, IsHost: false),
        ];
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(View() with { Players = afterLeaving }, "CODE1234", "p1"));

        server.Events.Write(Frame(8, "lobby.playerLeft", """{"playerId":"p2","reason":"left"}"""));
        await WaitFor<MultiplayerNotice.MatchUpdated>(session);
        server.Events.Write(Frame(9, "turn.readiness", """{"turn":1,"playerId":"p1","ready":true}"""));
        var readiness = await WaitFor<MultiplayerNotice.ReadinessChanged>(session);

        Assert.Equal(1, readiness.Ready);
        Assert.Equal(1, readiness.Seated);
    }

    [Fact]
    public async Task ADepartedSeatIsComputerPlannedOnlyAfterTheApprovedTakeoverEvent()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        IReadOnlyList<PlayerView> afterLeaving =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Left, IsHost: false),
        ];
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(View() with { Players = afterLeaving }, "CODE1234", "p1"));
        IReadOnlyList<PlayerView> pendingVote =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.TakeoverPending, IsHost: false),
        ];
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(View() with { Players = pendingVote }, "CODE1234", "p1"));
        IReadOnlyList<PlayerView> computerControlled =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Computer, IsHost: false),
        ];
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(View() with { Players = computerControlled }, "CODE1234", "p1"));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrdersForSlots(1, 0));

        server.Events.Write(Frame(8, "lobby.playerLeft", """{"playerId":"p2","reason":"left"}"""));
        server.Events.Write(Frame(9, "match.takeoverVoteRequested", """{"playerId":"p2","turn":1}"""));
        server.Events.Write(Frame(10, "match.takeoverVoteCast",
            """{"playerId":"p2","voterPlayerId":"p1","decision":"computer"}"""));
        server.Events.Write(Frame(11, "match.playerTakenOver", """{"playerId":"p2"}"""));
        server.Events.Write(SealedFrameForSlots(12, 1, 0));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        var definitions = BundledOriginalData.Load();
        var expected = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(expected);
        expected.TransferPlayerToComputer(new PlayerId(1));
        SealedTurnApplier.Apply(expected, SealedOrdersForSlots(1, 0));
        Assert.Equal(PlayerController.Computer, resolved.State.Players[1].Setup.Controller);
        Assert.Equal(MatchStateHasher.ComputeSha256(expected.State), resolved.StateHash);

        IReadOnlyList<PlayerView> returnedRoster =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
        ];
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(View() with { Players = returnedRoster, CurrentTurn = 2 }, "CODE1234", "p1"));
        var secondTurn = SealedOrdersForSlots(2, 0, 1);
        server.Answer(HttpMethod.Get, "/turns/2/orders", secondTurn);
        server.Events.Write(Frame(
            13, "match.playerReturned", """{"playerId":"p2","replacedComputer":true}"""));
        server.Events.Write(Frame(
            14, "turn.sealed",
            $$"""{"turn":2,"orderSetHash":"{{secondTurn.OrderSetHash}}"}"""));

        var returned = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        expected.TransferPlayerToHuman(new PlayerId(1));
        SealedTurnApplier.Apply(expected, secondTurn);
        Assert.Equal(PlayerController.Human, returned.State.Players[1].Setup.Controller);
        Assert.Equal(MatchStateHasher.ComputeSha256(expected.State), returned.StateHash);
    }

    [Fact]
    public async Task MissingOrdersAloneDoNotTransferAnActiveHumanSeat()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var sealedOrders = SealedOrdersForSlots(1, 0);
        server.Answer(HttpMethod.Get, "/turns/1/orders", sealedOrders);

        server.Events.Write(Frame(
            8,
            "turn.sealed",
            $$"""{"turn":1,"orderSetHash":"{{sealedOrders.OrderSetHash}}"}"""));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(PlayerController.Human, resolved.State.Players[1].Setup.Controller);
    }

    /// <summary>
    /// An abandoned match is reported, not waited out.
    /// </summary>
    /// <remarks>
    /// As final as a finish and easier to miss. A client watching only for <c>finished</c> would sit
    /// there holding a countdown for a turn nobody is ever going to seal.
    /// </remarks>
    [Fact]
    public async Task ReportsAMatchTheServerHasGivenUpOn()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        server.Events.Write(Frame(8, "match.statusChanged", """{"status":"abandoned"}"""));

        await WaitFor<MultiplayerNotice.MatchAbandoned>(session);
    }

    /// <summary>
    /// The outbox sends the document the player ended up with, and keeps their readiness.
    /// </summary>
    /// <remarks>
    /// The document is a whole-document replace, so a draft superseded before it was ever sent has
    /// nothing in it the newer one lacks — but readiness is not part of the document's content, and
    /// having said "I am done" is not something a later draft of the same turn takes back.
    /// </remarks>
    [Fact]
    public async Task SendsTheLatestDocumentAndKeepsReadinessOnceItIsGiven()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var gang = session.Bootstrap.State.Players[0].Gangs[0].Id;
        var builder = new OrderDocumentBuilder(new PlayerId(0));
        builder.Cancel(new PlayerId(0), gang);

        session.QueueOrders(1, builder.Build(), ready: true);
        await Until(
            () => server.CallsTo(HttpMethod.Put, "/turns/1/orders") == 1,
            "the ready submission");
        session.QueueOrders(1, builder.Build(), ready: false);

        await WaitFor<MultiplayerNotice.OrdersAccepted>(session);
        await WaitFor<MultiplayerNotice.OrdersAccepted>(session);
        await Until(
            () => server.CallsTo(HttpMethod.Put, "/turns/1/orders") == 2,
            "the replacement submission");
        Assert.All(
            server.BodiesSentTo(HttpMethod.Put, "/turns/1/orders"),
            body => Assert.Contains("\"ready\":true", body, StringComparison.Ordinal));
    }

    /// <summary>
    /// A refused submission is said out loud and the match carries on.
    /// </summary>
    /// <remarks>
    /// The ordinary cause is a turn that sealed while the player was still planning it, which costs
    /// them that turn and nothing more. Ending the session over it would turn a lost turn into a lost
    /// match.
    /// </remarks>
    [Fact]
    public async Task ReportsARefusedSubmissionWithoutEndingTheMatch()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(
            HttpMethod.Put, "/orders", Envelope("turn_not_open"), HttpStatusCode.Conflict);

        session.QueueOrders(1, new OrderDocument(1, []), ready: true);
        var refused = await WaitFor<MultiplayerNotice.OrdersRefused>(session);

        Assert.Equal(1, refused.Turn);
        Assert.Contains("sealed", refused.Reason, StringComparison.OrdinalIgnoreCase);

        // And the session is still driving the match.
        server.Events.Write(SealedFrame(8, 1));
        Assert.Equal(1, (await WaitFor<MultiplayerNotice.TurnResolved>(session)).Turn);
    }

    /// <summary>
    /// Turn one's deadline comes from the match view, because its event predates the stream.
    /// </summary>
    /// <remarks>
    /// The event that opened turn 1 was published before this client started reading, and resuming
    /// from the view's sequence number skips it by design — so without reading the view there would be
    /// no countdown on screen for the first turn of every match.
    /// </remarks>
    [Fact]
    public async Task TakesTheFirstTurnsDeadlineFromTheMatchView()
    {
        var (session, _, http) = Running(deadlineAt: "2026-09-10T12:05:00.000Z");
        using var _unused = http;
        await using var __ = session;

        Assert.Equal(
            DateTimeOffset.Parse("2026-09-10T12:05:00.000Z", System.Globalization.CultureInfo.InvariantCulture),
            session.Bootstrap.Deadline);
    }

    /// <summary>A match with no turn timer has no deadline, rather than a nonsense one.</summary>
    [Fact]
    public async Task HasNoDeadlineWhenTheMatchHasNoTimer()
    {
        var (session, _, http) = Running();
        using var _unused = http;
        await using var __ = session;

        Assert.Null(session.Bootstrap.Deadline);
    }

}
