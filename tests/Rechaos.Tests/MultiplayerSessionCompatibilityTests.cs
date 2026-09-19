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

public sealed partial class MultiplayerSessionTests
{
    [Fact]
    public async Task NewDraftCancelsASupersededInFlightDraft()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var blocked = server.BlockOnce(HttpMethod.Put, "/turns/1/orders");
        var oldDocument = new OrderDocument(1, []);
        var gang = session.Bootstrap.State.Players[0].Gangs[0].Id;
        var builder = new OrderDocumentBuilder(new PlayerId(0));
        builder.Cancel(new PlayerId(0), gang);
        var latestDocument = builder.Build();

        session.QueueOrders(1, oldDocument, ready: false);
        await Until(
            () => server.CallsTo(HttpMethod.Put, "/turns/1/orders") == 1,
            "the superseded draft to enter the transport");
        session.QueueOrders(1, latestDocument, ready: false);

        await WaitFor<MultiplayerNotice.OrdersAccepted>(session);
        await Until(
            () => server.CallsTo(HttpMethod.Put, "/turns/1/orders") == 2,
            "the latest replacement draft");
        var bodies = server.BodiesSentTo(HttpMethod.Put, "/turns/1/orders");
        Assert.Equal(2, bodies.Count);
        Assert.Contains("cancel", bodies[1], StringComparison.OrdinalIgnoreCase);
        Assert.False(blocked.Task.IsCompleted);
    }

    [Fact]
    public async Task RestartReappliesExpenseBearingTurnsExactlyOnce()
    {
        IReadOnlyList<PlayerView> richRoster =
        [
            Roster[0] with { DisplayName = "SMGFUNDAGE" },
            Roster[1],
        ];
        var definitions = BundledOriginalData.Load();
        var expected = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, richRoster));
        CommandPhase.Enter(expected);
        var player = expected.State.Players[0];
        var offer = player.HirePool
            .OrderByDescending(id => HireRules.InitialCost(
                definitions.Gangs.Single(gang => gang.Id == id)))
            .First();
        var sector = expected.State.Sectors.Single(item => item.Owner == player.Id).Id;
        var builder = new OrderDocumentBuilder(player.Id);
        builder.QueueHire(player.Id, offer, sector);
        var expenseDocument = builder.Build();
        var emptyDocument = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        SealedPlayerOrders[] players =
        [
            new("p1", 0, expenseDocument, OrderDigest.OfDocument(expenseDocument)),
            new("p2", 1, emptyDocument, OrderDigest.OfDocument(emptyDocument)),
        ];
        var sealedTurn = new SealedOrdersView(1, OrderDigest.OfSet(players), players);
        var expectedHash = SealedTurnApplier.Apply(expected, sealedTurn);
        var expectedCash = expected.State.Players[0].Cash;
        Assert.True(expected.State.Players[0].Statistics.CashSpent > 0);

        var view = ViewAtTurn(2) with { Players = richRoster, LastEventSeq = 4 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(2, MatchId, "2026-09-10T12:01:00.000Z",
                new(1, sealedTurn.OrderSetHash)),
            new TurnConfirmedEvent(3, MatchId, "2026-09-10T12:02:00.000Z",
                new(1, expectedHash)),
            new TurnOpenedEvent(4, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
        ];
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", sealedTurn);
                fake.Answer(HttpMethod.Get, "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(expectedCash, resumed.State.Players[0].Cash);
        Assert.Equal(expectedHash, MatchStateHasher.ComputeSha256(resumed.State));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    [Fact]
    public void RefusesToStartAMatchFromAnotherSessionVersion()
    {
        var exception = Assert.Throws<MultiplayerProtocolException>(() => Running(
            matchView: View() with
            {
                SessionVersion = MultiplayerSessionVersion.Current + 1
            }));

        Assert.Contains("session version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The protocol says whether this build and the server can talk, which the handshake has
    /// already settled by the time a match is read. A match created by a client that spoke an older
    /// one is the same stored session, so it is played rather than refused.
    /// </summary>
    [Fact]
    public async Task StartsAMatchCreatedUnderAnOlderProtocolVersion()
    {
        var (session, _, http) = Running(
            matchView: View() with { ProtocolVersion = MultiplayerProtocolVersion.Current - 1 });
        using var _disposeHttp = http;
        await using var _disposeSession = session;

        Assert.False(session.IsRestoring);
        Assert.Equal(1, session.Bootstrap.State.Coordinator.Turn);
    }

    /// <summary>A repair belonging to another session version is refused before it is decoded.</summary>
    [Fact]
    public async Task RefusesARepairFromAnotherSessionVersion()
    {
        var (session, server, http) = Running(ownPlayerId: "p2");
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/snapshots/3", new SnapshotView(
            3,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current + 1,
            new string('a', 64),
            "p1",
            "2026-09-10T12:00:00.000Z",
            "QUJD"));

        server.Events.Write(Frame(8, "snapshot.available", """{"turn":3,"stateHash":"aa"}"""));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("session version", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestartRejectsHistoricalStateThatDoesNotMatchTheConfirmedHash()
    {
        var view = ViewAtTurn(2) with { LastEventSeq = 3 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(2, MatchId, "2026-09-10T12:01:00.000Z",
                new(1, SealedOrders(1).OrderSetHash)),
            new TurnConfirmedEvent(3, MatchId, "2026-09-10T12:02:00.000Z",
                new(1, new string('a', 64)))
        ];
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
            });
        using var _ = http;
        await using var __ = session;

        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("confirmed turn 1", failed.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/turns/1/orders"));
    }

    /// <summary>A roster that does not seat this client is refused before a city is generated.</summary>
    [Fact]
    public void RefusesToStartWhenThisClientIsNotOnTheRoster()
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        var handle = new MultiplayerClient(
                http, new MultiplayerClientOptions(new Uri("http://server.test")))
            .WithToken("cop_test")
            .Match(MatchId);

        var failure = Assert.Throws<MultiplayerProtocolException>(
            () => MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
                handle, BundledOriginalData.Load(), View(), "nobody", ResumeAfterSeq: 0)));
        Assert.Contains("roster", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}
