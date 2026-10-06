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
/// A spectator rebuilding a match from what the server has released, against a fake server.
/// </summary>
/// <remarks>
/// The server decides what is released; these tests hold the client to rebuilding exactly the
/// state the players reached from it, and to refusing anything that does not add up.
/// </remarks>
public sealed class MultiplayerSpectatorSessionTests
{
    private const string MatchId = "m1";
    private const int Seed = 1996;
    private const string CreatedAt = "2026-10-06T12:00:00.000Z";

    private static readonly OriginalData Definitions = BundledOriginalData.Load();

    private static readonly MultiplayerGameSettings GameSettings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
    ];

    [Fact]
    public async Task WaitsInTheLobbyWithoutAskingForAState()
    {
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Lobby, 0, 0));

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);

        Assert.False(session.HasState);
        Assert.Null(session.ShownTurn);
        Assert.Null(session.CloneState());
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/snapshots/latest"));
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/events"));
    }

    [Fact]
    public async Task WaitsForTheBootstrapSnapshotOfAStartedMatch()
    {
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 1, 0));
        server.Answer(
            HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);

        Assert.False(session.HasState);
        Assert.False(await session.PollAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RebuildsTheReleasedTurnsTheWayThePlayersPlayedThem()
    {
        var reference = Bootstrap();
        var bootstrap = SnapshotOf(0, reference.State);
        SealedTurnApplier.Apply(reference, SealedOrders(1));
        SealedTurnApplier.Apply(reference, SealedOrders(2));

        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 5, 2));
        server.Answer(HttpMethod.Get, "/snapshots/latest", bootstrap);
        server.AnswerOnce(HttpMethod.Get, "/events", Page(5, Opened(1, 1), Sealed(2, 1), Opened(3, 2), Sealed(5, 2)));
        server.Answer(HttpMethod.Get, "/events", Page(5));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);

        Assert.Equal(2, session.ShownTurn);
        var shown = session.CloneState()!;
        Assert.Equal(3, shown.Coordinator.Turn);
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(reference.State),
            MatchStateHasher.ComputeFingerprint(shown));
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/3/orders"));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
    }

    [Fact]
    public async Task FollowsTurnsAsTheyAreReleasedFromWhereTheLogLeftOff()
    {
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 3, 0));
        server.Answer(HttpMethod.Get, "/snapshots/latest", SnapshotOf(0, Bootstrap().State));
        server.AnswerOnce(HttpMethod.Get, "/events", Page(1, Opened(1, 1)));
        server.Answer(HttpMethod.Get, "/events", Page(1));

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);
        Assert.Equal(0, session.ShownTurn);
        Assert.False(await session.PollAsync(TestContext.Current.CancellationToken));

        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 4, 1));
        // A page whose events the server filtered out still moves the cursor.
        server.AnswerOnce(HttpMethod.Get, "/events", Page(6, Sealed(4, 1)));
        server.AnswerOnce(HttpMethod.Get, "/events", Page(9));
        server.Answer(HttpMethod.Get, "/events", Page(9));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        Assert.True(await session.PollAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, session.ShownTurn);
        var after = server.Requests
            .Where(request => request.Path.EndsWith("/events", StringComparison.Ordinal))
            .Select(request => request.Query)
            .ToArray();
        Assert.Equal(
            ["?after=0&limit=200", "?after=1&limit=200", "?after=1&limit=200", "?after=1&limit=200", "?after=6&limit=200", "?after=9&limit=200"],
            after);
    }

    [Fact]
    public async Task ReadsOnPastAPageThatEndsOnAnEventItReturned()
    {
        var reference = Bootstrap();
        var bootstrap = SnapshotOf(0, reference.State);
        SealedTurnApplier.Apply(reference, SealedOrders(1));
        SealedTurnApplier.Apply(reference, SealedOrders(2));

        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 5, 2));
        server.Answer(HttpMethod.Get, "/snapshots/latest", bootstrap);
        // A full page, or one cut short by the server's scan, ends with the cursor on its last event.
        server.AnswerOnce(HttpMethod.Get, "/events", Page(2, Opened(1, 1), Sealed(2, 1)));
        server.AnswerOnce(HttpMethod.Get, "/events", Page(4, Opened(3, 2), Sealed(4, 2)));
        server.Answer(HttpMethod.Get, "/events", Page(4));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);

        Assert.Equal(2, session.ShownTurn);
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(reference.State),
            MatchStateHasher.ComputeFingerprint(session.CloneState()!));
    }

    [Fact]
    public async Task HandsASeatOverAtTheBoundaryTheLogAnnouncedIt()
    {
        var reference = Bootstrap();
        var bootstrap = SnapshotOf(0, reference.State);
        SealedTurnApplier.Apply(reference, SealedOrders(1));
        reference.TransferPlayerToComputer(new PlayerId(1));
        SealedTurnApplier.Apply(reference, SealedOrdersForSlots(2, 0));

        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 5, 2));
        server.Answer(HttpMethod.Get, "/snapshots/latest", bootstrap);
        server.AnswerOnce(
            HttpMethod.Get,
            "/events",
            Page(
                6,
                Opened(1, 1),
                Sealed(2, 1),
                TakenOver(3, "p2"),
                Opened(4, 2),
                Sealed(6, 2, SealedOrdersForSlots(2, 0))));
        server.Answer(HttpMethod.Get, "/events", Page(6));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrdersForSlots(2, 0));

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);

        var shown = session.CloneState()!;
        Assert.Equal(PlayerController.Computer, shown.FindPlayer(new PlayerId(1))!.Setup.Controller);
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(reference.State),
            MatchStateHasher.ComputeFingerprint(shown));
    }

    [Fact]
    public async Task RefusesASnapshotPastTheReleasedTurn()
    {
        var replay = Bootstrap();
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 2, 0));
        server.Answer(HttpMethod.Get, "/snapshots/latest", SnapshotOf(1, replay.State));

        await Assert.ThrowsAsync<MultiplayerProtocolException>(
            () => MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesASealedSetThatIsNotTheOneTheLogAnnounced()
    {
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Running, 4, 1));
        server.Answer(HttpMethod.Get, "/snapshots/latest", SnapshotOf(0, Bootstrap().State));
        server.Answer(HttpMethod.Get, "/events", Page(2, Opened(1, 1), Sealed(2, 1)));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrdersForSlots(1, 0));

        var refusal = await Assert.ThrowsAsync<MultiplayerProtocolException>(
            () => MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken));
        Assert.Contains("digest", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsCompleteOnceAnEndedMatchHasShownEveryTurn()
    {
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(HttpMethod.Get, $"/spectate/{MatchId}", ViewOf(MatchStatus.Abandoned, 2, 1));
        server.Answer(HttpMethod.Get, "/snapshots/latest", SnapshotOf(0, Bootstrap().State));
        server.AnswerOnce(HttpMethod.Get, "/events", Page(2, Opened(1, 1), Sealed(2, 1)));
        server.Answer(HttpMethod.Get, "/events", Page(2));
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        var session = await MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken);

        Assert.True(session.IsComplete);
        Assert.Equal(1, session.ShownTurn);
    }

    [Fact]
    public async Task RefusesAMatchFromAnotherSessionVersion()
    {
        var (handle, server, http) = Watching();
        using var _ = http;
        server.Answer(
            HttpMethod.Get,
            $"/spectate/{MatchId}",
            ViewOf(MatchStatus.Running, 4, 1) with { SessionVersion = MultiplayerSessionVersion.Current + 1 });

        await Assert.ThrowsAsync<MultiplayerProtocolException>(
            () => MultiplayerSpectatorSession.StartAsync(handle, Definitions, TestContext.Current.CancellationToken));
    }

    private static (SpectatorHandle Handle, FakeMultiplayerServer Server, HttpClient Http) Watching()
    {
        var server = new FakeMultiplayerServer();
        var http = new HttpClient(server);
        var handle = new MultiplayerClient(http, new MultiplayerClientOptions(new Uri("http://server.test")))
            .WithToken("cos_test")
            .Spectator(MatchId);
        return (handle, server, http);
    }

    private static MatchReplayRecorder Bootstrap()
    {
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(Definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        return replay;
    }

    private static SnapshotView SnapshotOf(int turn, MatchState state) => new(
        turn,
        NativeSaveSerializer.CurrentFormatVersion,
        MultiplayerProtocolVersion.Current,
        MultiplayerSessionVersion.Current,
        MatchStateHasher.ComputeFingerprint(state),
        "p1",
        CreatedAt,
        MatchStateClone.ToBase64(state));

    private static SpectatorMatchView ViewOf(MatchStatus status, int currentTurn, int releasedTurn) => new(
        MatchId,
        MultiplayerProtocolVersion.Current,
        MultiplayerSessionVersion.Current,
        status,
        new MatchSettings("ADA'S CITY", 2, 300, MatchVisibility.Private, GameSettings.ToWire(), 2),
        Roster,
        currentTurn,
        DelayTurns: 2,
        releasedTurn,
        releasedTurn >= 1 ? Seed : null,
        CreatedAt);

    private static SealedOrdersView SealedOrders(int turn) => SealedOrdersForSlots(turn, 0, 1);

    private static SealedOrdersView SealedOrdersForSlots(int turn, params int[] slots)
    {
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var players = slots
            .Select(slot => new SealedPlayerOrders($"p{slot + 1}", slot, empty, OrderDigest.OfDocument(empty)))
            .ToArray();
        return new SealedOrdersView(turn, OrderDigest.OfSet(players), players);
    }

    private static SpectatorEventPage Page(int cursor, params MatchEvent[] events) => new(events, cursor);

    private static TurnOpenedEvent Opened(int seq, int turn) =>
        new(seq, MatchId, CreatedAt, new TurnOpenedEventPayload(turn, null));

    private static TurnSealedEvent Sealed(int seq, int turn, SealedOrdersView? set = null) =>
        new(seq, MatchId, CreatedAt, new TurnSealedEventPayload(turn, (set ?? SealedOrders(turn)).OrderSetHash));

    private static MatchPlayerTakenOverEvent TakenOver(int seq, string playerId) =>
        new(seq, MatchId, CreatedAt, new MatchPlayerTakenOverEventPayload(playerId));

    private static string Envelope(string reason) => JsonSerializer.Serialize(new
    {
        error = new
        {
            code = "not_found",
            message = "refused",
            details = new { reason },
            requestId = "req1",
        },
    });
}
