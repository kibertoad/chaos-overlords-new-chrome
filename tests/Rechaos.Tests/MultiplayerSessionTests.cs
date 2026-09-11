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
public sealed class MultiplayerSessionTests
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
        Running(string ownPlayerId = "p1", string? deadlineAt = null)
    {
        var server = new FakeMultiplayerServer();
        var http = new HttpClient(server);
        var view = View(deadlineAt);
        server.Answer(HttpMethod.Get, $"/matches/{MatchId}", new MatchDetail(view, "CODE1234", ownPlayerId));
        server.Answer(HttpMethod.Post, "/report", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Post, "/snapshots", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Put, "/orders", new OwnSubmissionView(1, null, Ready: true, null));

        var handle = new MultiplayerClient(
                http, new MultiplayerClientOptions(new Uri("http://server.test")))
            .WithToken("cop_test")
            .Match(MatchId);
        var session = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            handle, BundledOriginalData.Load(), view, ownPlayerId, ResumeAfterSeq: 7));
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
    {
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var players = new[] { 0, 1 }
            .Select(slot => new SealedPlayerOrders(
                $"p{slot + 1}", slot, empty, OrderDigest.OfDocument(empty)))
            .ToArray();
        return new SealedOrdersView(turn, OrderDigest.OfSet(players), players);
    }

    private static string Frame(int seq, string type, string payload) =>
        $"id: {seq}\ndata: {{\"seq\":{seq},\"matchId\":\"{MatchId}\","
        + $"\"createdAt\":\"2026-09-10T12:00:00.000Z\",\"type\":\"{type}\",\"payload\":{payload}}}\n\n";

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
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        server.Events.Write(Frame(8, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(1, resolved.Turn);
        Assert.Equal(2, resolved.State.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, resolved.State.Coordinator.Phase);

        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the report");
        var report = Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/turns/1/report"));
        Assert.Contains(resolved.StateHash, report, StringComparison.Ordinal);
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

        server.Events.Write(Frame(8, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
        var first = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        server.Events.Write(Frame(9, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
        server.Events.Write(Frame(10, "turn.sealed", """{"turn":2,"orderSetHash":"bb"}"""));
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

        server.Events.Write(Frame(8, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
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

        server.Events.Write(Frame(8, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
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

        server.Events.Write(Frame(8, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
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
        var ours = MatchStateHasher.ComputeSha256(session.InitialState);

        server.Events.Write(Frame(8, "turn.desynced", Desync(ours)));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.True(desynced.IsHostRepair);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the upload");
        var upload = Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/snapshots"));
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

        server.Events.Write(Frame(8, "turn.desynced", Desync(new string('7', 64))));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.False(desynced.IsHostRepair);
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/snapshots"));
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
            new("p2", 1, "GRACE", WirePlayerStatus.Left, IsHost: false),
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
        var gang = session.InitialState.Players[0].Gangs[0].Id;
        var builder = new OrderDocumentBuilder(new PlayerId(0));
        builder.Cancel(new PlayerId(0), gang);

        session.QueueOrders(1, builder.Build(), ready: true);
        session.QueueOrders(1, builder.Build(), ready: false);

        await WaitFor<MultiplayerNotice.OrdersAccepted>(session);
        await Until(
            () => server.CallsTo(HttpMethod.Put, "/turns/1/orders") >= 1, "the submission");
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
        server.Events.Write(Frame(8, "turn.sealed", """{"turn":1,"orderSetHash":"aa"}"""));
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
            session.InitialDeadline);
    }

    /// <summary>A match with no turn timer has no deadline, rather than a nonsense one.</summary>
    [Fact]
    public async Task HasNoDeadlineWhenTheMatchHasNoTimer()
    {
        var (session, _, http) = Running();
        using var _unused = http;
        await using var __ = session;

        Assert.Null(session.InitialDeadline);
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
