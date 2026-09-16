using System.Net;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>The fake server and the waiting the session tests are written against.</summary>
public sealed partial class MultiplayerSessionTests
{
    private const string MatchId = "m1";
    private const int Seed = 1996;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly MultiplayerGameSettings GameSettings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", WirePlayerStatus.Active, IsHost: false),
    ];

    /// <summary>
    /// A session against a fake server, with the routes a running match needs already answered.
    /// </summary>
    /// <remarks>
    /// Each test overrides only the route it is about, so what a test sets up is what it is testing.
    /// </remarks>
    private static (MultiplayerMatchSession Session, FakeMultiplayerServer Server, HttpClient Http)
        Running(
            string ownPlayerId = "p1",
            string? deadlineAt = null,
            MatchView? matchView = null,
            Action<FakeMultiplayerServer>? configure = null,
            bool joinedInProgress = false)
    {
        var server = new FakeMultiplayerServer();
        var http = new HttpClient(server);
        var view = matchView ?? View(deadlineAt);
        server.Answer(HttpMethod.Get, $"/matches/{MatchId}", new MatchDetail(view, "CODE1234", ownPlayerId));
        server.Answer(HttpMethod.Post, "/report", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Post, "/snapshots", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Put, "/orders", new OwnSubmissionView(1, null, Ready: true, null));
        if (view.CurrentTurn > 1)
            server.Answer(HttpMethod.Get, "/events", new EventPage(HistoricalEvents(view)));
        configure?.Invoke(server);

        var handle = new MultiplayerClient(
                http, new MultiplayerClientOptions(new Uri("http://server.test")))
            .WithToken("cop_test")
            .Match(MatchId);
        var session = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            handle, BundledOriginalData.Load(), view, ownPlayerId, ResumeAfterSeq: 7,
            joinedInProgress));
        return (session, server, http);
    }

    private static MatchView View(string? deadlineAt = null) => new(
        MatchId,
        MatchStatus.Running,
        new MatchSettings("ADA'S CITY", 2, 300, MatchVisibility.Private, GameSettings.ToWire()),
        "p1",
        Seed,
        CurrentTurn: 1,
        Roster,
        new TurnView(1, TurnStatus.Open, "2026-09-10T12:00:00.000Z", deadlineAt, null, null, [], []),
        PreviousTurn: null,
        LastEventSeq: 7,
        "2026-09-10T11:59:00.000Z");

    /// <summary>The sealed set for a turn nobody ordered anything in, with honest digests.</summary>
    private static SealedOrdersView SealedOrders(int turn)
        => SealedOrdersForSlots(turn, 0, 1);

    private static SealedOrdersView SealedOrdersForSlots(int turn, params int[] slots)
    {
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var players = slots
            .Select(slot => new SealedPlayerOrders(
                $"p{slot + 1}", slot, empty, OrderDigest.OfDocument(empty)))
            .ToArray();
        return new SealedOrdersView(turn, OrderDigest.OfSet(players), players);
    }

    private static string Frame(int seq, string type, string payload) =>
        $"id: {seq}\ndata: {{\"seq\":{seq},\"matchId\":\"{MatchId}\","
        + $"\"createdAt\":\"2026-09-10T12:00:00.000Z\",\"type\":\"{type}\",\"payload\":{payload}}}\n\n";

    private static string SealedFrame(int seq, int turn) => Frame(
        seq,
        "turn.sealed",
        $$"""{"turn":{{turn}},"orderSetHash":"{{SealedOrders(turn).OrderSetHash}}"}""");

    private static IReadOnlyList<MatchEvent> HistoricalEvents(MatchView view)
    {
        var seals = Enumerable.Range(1, view.CurrentTurn - 1)
            .ToDictionary(turn => turn * 5);
        return Enumerable.Range(1, view.LastEventSeq)
            .Select<int, MatchEvent>(seq => seals.TryGetValue(seq, out var turn)
                ? new TurnSealedEvent(
                    seq,
                    MatchId,
                    "2026-09-10T12:00:00.000Z",
                    new TurnSealedEventPayload(turn, SealedOrders(turn).OrderSetHash))
                : new TurnOpenedEvent(
                    seq,
                    MatchId,
                    "2026-09-10T12:00:00.000Z",
                    new TurnOpenedEventPayload(Math.Max(1, seq / 5), null)))
            .ToArray();
    }

    private static MatchView ViewAtTurn(int turn) => View() with
    {
        CurrentTurn = turn,
        Turn = new TurnView(
            turn,
            TurnStatus.Open,
            "2026-09-10T12:05:00.000Z",
            "2026-09-10T12:10:00.000Z",
            null,
            null,
            [],
            []),
        PreviousTurn = new TurnView(
            turn - 1,
            TurnStatus.Confirmed,
            "2026-09-10T12:00:00.000Z",
            null,
            "2026-09-10T12:04:00.000Z",
            new string('a', 64),
            [],
            ["p1", "p2"]),
        LastEventSeq = 20,
    };

    /// <summary>
    /// Drains notices until one of the wanted kind arrives, or gives up.
    /// </summary>
    /// <remarks>
    /// Everything the session does happens on a background task, so a test has to wait for an answer
    /// rather than look for one. Notices that arrive on the way are kept, because a test that asserts
    /// what did <em>not</em> happen needs to have seen everything that did.
    /// </remarks>
    private static async Task<T> WaitFor<T>(
        MultiplayerMatchSession session,
        List<MultiplayerNotice>? seen = null)
        where T : MultiplayerNotice
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            while (session.TryDequeueNotice(out var notice))
            {
                seen?.Add(notice);
                if (notice is T wanted) return wanted;
            }
            await Task.Delay(15).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"no {typeof(T).Name} notice arrived within {Patience.TotalSeconds:0}s");
    }

    /// <summary>Waits until a condition about the server holds, or gives up.</summary>
    private static async Task Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(15).ConfigureAwait(false);
        }
        throw new TimeoutException($"{what} did not happen within {Patience.TotalSeconds:0}s");
    }

    private static string Desync(params string[] candidates)
    {
        var hashes = string.Join(',', candidates.Select(hash => $"\"{hash}\""));
        return $"{{\"turn\":1,\"reports\":[],\"candidateStateHashes\":[{hashes}]}}";
    }

    private static string Envelope(string reason) => JsonSerializer.Serialize(new
    {
        error = new
        {
            code = "conflict",
            message = "refused",
            details = new { reason },
            requestId = "req1",
        },
    });
}
