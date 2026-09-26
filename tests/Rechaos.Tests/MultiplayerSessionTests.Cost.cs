using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What the session costs the player: round trips on a reconnect, and work on the game thread.
/// </summary>
/// <remarks>
/// None of this changes what the session does, which is why nothing else notices when it grows.
/// These are budgets rather than facts about the implementation: raising one deliberately is a
/// one-line change, and raising one by accident is what they stop.
/// </remarks>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A reconnect fetches the sealed sets it needs concurrently rather than one at a time.
    /// </summary>
    /// <remarks>
    /// Replay is sequential by nature — turn N is applied before N+1 — but fetching is not: a
    /// sealed set is immutable and served as such. Every set used to be asked for only when its
    /// turn came round, so a reconnect that had to replay ten turns spent ten round trips end to
    /// end, each under its own deadline, with the player watching.
    /// </remarks>
    [Fact]
    public async Task AReconnectFetchesTheSealedSetsItReplaysConcurrently()
    {
        var view = ViewAtTurn(5) with { LastEventSeq = 8 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new TurnSealedEvent(3, MatchId, "2026-09-10T12:02:00.000Z", new(2, SealedOrders(2).OrderSetHash)),
            new TurnSealedEvent(4, MatchId, "2026-09-10T12:03:00.000Z", new(3, SealedOrders(3).OrderSetHash)),
            new TurnSealedEvent(5, MatchId, "2026-09-10T12:04:00.000Z", new(4, SealedOrders(4).OrderSetHash)),
            new TurnOpenedEvent(6, MatchId, "2026-09-10T12:05:00.000Z", new(5, null)),
            new TurnOpenedEvent(7, MatchId, "2026-09-10T12:06:00.000Z", new(5, null)),
            new TurnOpenedEvent(8, MatchId, "2026-09-10T12:07:00.000Z", new(5, null)),
        ];
        var blocks = new List<TaskCompletionSource>();
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                for (var turn = 1; turn <= 4; turn++)
                {
                    fake.Answer(HttpMethod.Get, $"/turns/{turn}/orders", SealedOrders(turn));
                    // Held until released, so a fetch that has started stays visibly started.
                    blocks.Add(fake.BlockOnce(HttpMethod.Get, $"/turns/{turn}/orders"));
                }
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/5/orders/mine",
                    new OwnSubmissionView(5, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        // Several sets are asked for while the FIRST one is still unanswered, which is the whole
        // point: sequential replay does not have to mean sequential fetching.
        await Until(
            () => server.CallsTo(HttpMethod.Get, "/orders") >= 2,
            "a second sealed-set fetch started before the first was answered");

        foreach (var block in blocks) block.TrySetResult();
        await WaitFor<MultiplayerNotice.Resumed>(session);
    }

    /// <summary>
    /// The host writes a checkpoint every few confirmed turns, so a reconnect has less to replay.
    /// </summary>
    [Fact]
    public async Task TheHostCheckpointsEveryFewConfirmedTurns()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");

        var seq = 8;
        var hash = string.Empty;
        for (var turn = 1; turn <= 10; turn++)
        {
            server.Answer(HttpMethod.Get, $"/turns/{turn}/orders", SealedOrders(turn));
            server.Events.Write(SealedFrame(seq++, turn));
            hash = (await WaitFor<MultiplayerNotice.TurnResolved>(session)).StateHash;
            // Confirmations for the turns in between must not provoke an upload.
            server.Events.Write(Frame(
                seq++, "turn.confirmed", $$"""{"turn":{{turn}},"stateHash":"{{hash}}"}"""));
            if (turn < 10)
            {
                await Task.Delay(30, TestContext.Current.CancellationToken);
                Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/snapshots"));
            }
        }

        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the checkpoint");
        var checkpoint = server.BodiesSentTo(HttpMethod.Post, "/snapshots")[1];
        Assert.Contains("\"turn\":10", checkpoint, StringComparison.Ordinal);
        Assert.Contains(hash, checkpoint, StringComparison.Ordinal);
    }

    /// <summary>
    /// A checkpoint the server is slow to take does not hold the match behind it.
    /// </summary>
    /// <remarks>
    /// The upload used to run inside the pump, so a checkpoint met by a rate limit or a slow server
    /// held the next seal — and the reconnect modal stood over the host's turn — for as long as its
    /// retry window lasted, over a stream that was working the whole time.
    /// </remarks>
    [Fact]
    public async Task AStalledCheckpointDoesNotHoldTheNextTurn()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var stalled = server.BlockOnce(HttpMethod.Post, "/snapshots");

        var seq = await ConfirmTurnsAsync(session, server, seq: 8, lastTurn: 10);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the checkpoint");

        server.Answer(HttpMethod.Get, "/turns/11/orders", SealedOrders(11));
        server.Events.Write(SealedFrame(seq, 11));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(11, resolved.Turn);
        Assert.False(stalled.Task.IsCompleted);
        stalled.SetResult();
    }

    /// <summary>A checkpoint the server refuses for a rate limit is given up without a reconnect report.</summary>
    [Fact]
    public async Task ARateLimitedCheckpointIsGivenUpQuietly()
    {
        var (session, server, http) = Running(backgroundRetryPolicy: new RetryPolicy(
            TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), MaxAttempts: 2));
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            server.AnswerOnce(
                HttpMethod.Post, "/snapshots", Envelope("rate_limited"), HttpStatusCode.TooManyRequests);
        }
        var seen = new List<MultiplayerNotice>();

        await ConfirmTurnsAsync(session, server, seq: 8, lastTurn: 10, seen);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 3, "both checkpoint attempts");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        while (session.TryDequeueNotice(out var notice)) seen.Add(notice);

        Assert.Equal(3, server.CallsTo(HttpMethod.Post, "/snapshots"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.ConnectionChanged
            or MultiplayerNotice.Failed);
    }

    /// <summary>Seals and confirms turns 1 to <paramref name="lastTurn"/>; answers the next free sequence.</summary>
    private static async Task<int> ConfirmTurnsAsync(
        MultiplayerMatchSession session,
        FakeMultiplayerServer server,
        int seq,
        int lastTurn,
        List<MultiplayerNotice>? seen = null)
    {
        for (var turn = 1; turn <= lastTurn; turn++)
        {
            server.Answer(HttpMethod.Get, $"/turns/{turn}/orders", SealedOrders(turn));
            server.Events.Write(SealedFrame(seq++, turn));
            var hash = (await WaitFor<MultiplayerNotice.TurnResolved>(session, seen)).StateHash;
            server.Events.Write(Frame(
                seq++, "turn.confirmed", $$"""{"turn":{{turn}},"stateHash":"{{hash}}"}"""));
        }
        return seq;
    }

    /// <summary>
    /// A speculative turn records without re-hashing the match to check itself first.
    /// </summary>
    /// <remarks>
    /// Every mutation cost two full serialisations and SHA-256s of the whole match: one asking
    /// whether the state had moved behind the recorder's back, and one for the step's fingerprint.
    /// The first is an invariant about aliasing, and the speculative copy has exactly one owner —
    /// so it goes, and a click costs half what it did. The journal stays, because a bug report
    /// filed from an online match attaches the turn being planned.
    /// </remarks>
    [Fact]
    public void ASpeculativeTurnRecordsWithoutVerifyingItself()
    {
        var definitions = BundledOriginalData.Load();
        var authoritative = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(authoritative);
        var turn = SpeculativeTurn.For(authoritative.State, definitions, slot: 0);

        Assert.False(turn.Replay.IsVerifying);
        // The authoritative recorder, whose journal becomes a bug report's archive, still checks.
        Assert.True(authoritative.IsVerifying);

        // And the speculative journal is still a journal: a report filed mid-turn carries it.
        turn.Cancel(new GangId(1));
        Assert.True(turn.Replay.StepCount > 0);
        using var captured = new MemoryStream();
        MatchReplaySerializer.Save(captured, turn.Replay);
        Assert.True(captured.Length > 0);
    }
}
