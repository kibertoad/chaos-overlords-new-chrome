using System.Net;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The state-hash report as background work: what the player stops waiting for, and what the
/// session must still guarantee once nobody is waiting.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>One attempt per window, and the next window opens immediately.</summary>
    private static readonly RetryPolicy ShortCallWindow = new(
        TimeSpan.FromMilliseconds(2),
        TimeSpan.FromMilliseconds(4),
        MaxAttempts: 0,
        MaxElapsed: TimeSpan.FromMilliseconds(30));

    [Fact]
    public async Task HandsOverTheNextTurnWhileItsHashReportIsStillInFlight()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var report = server.BlockOnce(HttpMethod.Post, "/turns/1/report");
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        server.Events.Write(SealedFrame(8, 1));
        var first = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the blocked report");

        // The report has not received an HTTP response, but it must not hold the event pump or the
        // player's next planning handover hostage.
        server.Events.Write(SealedFrame(9, 2));
        var second = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(1, first.Turn);
        Assert.Equal(2, second.Turn);
        report.SetResult();
    }

    /// <summary>
    /// A report that outlasts its reconnect window is a connection to keep trying, not a match to
    /// end.
    /// </summary>
    /// <remarks>
    /// The rule the pump and the outbox already follow, now that the report is neither's work any
    /// more: the hash is an idempotent restatement, so a server restart that lasts longer than one
    /// window must cost nothing but the wait. Ending the session on it would have thrown the player
    /// out of a match whose only outstanding business was a report the server is still waiting for.
    /// </remarks>
    [Fact]
    public async Task AReportThatOutlastsItsWindowKeepsTryingRatherThanEndingTheMatch()
    {
        var (session, server, http) = Running(callRetryPolicy: ShortCallWindow);
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        server.Answer(HttpMethod.Post, "/report", null, HttpStatusCode.ServiceUnavailable);

        server.Events.Write(SealedFrame(8, 1));
        var seen = new List<MultiplayerNotice>();
        await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);
        // Enough attempts that at least one whole window has closed unanswered and another opened.
        await Until(
            () => server.CallsTo(HttpMethod.Post, "/turns/1/report") >= 8,
            "the report to be retried past its window");

        // The turn after it resolves throughout, and the session is still alive to resolve it.
        server.Events.Write(SealedFrame(9, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/turns/2/report"));

        server.Answer(HttpMethod.Post, "/report", null, HttpStatusCode.NoContent);
        await Until(
            () => server.CallsTo(HttpMethod.Post, "/turns/2/report") == 1,
            "the backed-up reports to drain");

        // In turn order, still: the server settles one turn at a time, so the turn that was being
        // retried has to reach it before the turn behind it does.
        var paths = server.Requests
            .Where(request => request.Method == HttpMethod.Post
                && request.Path.EndsWith("/report", StringComparison.Ordinal))
            .Select(request => request.Path)
            .ToArray();
        Assert.EndsWith("/turns/2/report", paths[^1], StringComparison.Ordinal);
        Assert.All(paths[..^1], path => Assert.EndsWith("/turns/1/report", path, StringComparison.Ordinal));
    }

    /// <summary>
    /// A report the server refuses as already settled leaves the connection reading as connected.
    /// </summary>
    /// <remarks>
    /// The report lane is its own, and only the attempt that succeeds clears it. A refusal that is
    /// swallowed as the success it is — <c>turn_confirmed</c> here — is the other way a report
    /// stops being tried, and on the final turn it can be the last report there will ever be: a
    /// lane left failing then holds the whole session's health down, and the reconnect banner stays
    /// up for the rest of the match with nothing behind it.
    /// </remarks>
    [Fact]
    public async Task AReportRefusedAsAlreadyConfirmedLeavesTheConnectionHealthy()
    {
        var (session, server, http) = Running(callRetryPolicy: ShortCallWindow);
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        // One failure to put the lane down, then the verdict this report was asking for.
        server.AnswerOnce(HttpMethod.Post, "/turns/1/report", null, HttpStatusCode.ServiceUnavailable);
        server.Answer(HttpMethod.Post, "/report", Envelope("turn_confirmed"), HttpStatusCode.Conflict);

        server.Events.Write(SealedFrame(8, 1));
        var seen = new List<MultiplayerNotice>();
        await WaitForConnection(session, connected: true, seen);

        Assert.Contains(seen, notice => notice is MultiplayerNotice.ConnectionChanged
        {
            IsConnected: false,
        });
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// The same, for the seat that was handed to the computer: the lane clears, and there is
    /// nothing later to clear it.
    /// </summary>
    [Fact]
    public async Task AReportRefusedBecauseTheSeatIsNoLongerPlayedLeavesTheConnectionHealthy()
    {
        var (session, server, http) = Running(callRetryPolicy: ShortCallWindow);
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        server.AnswerOnce(HttpMethod.Post, "/turns/1/report", null, HttpStatusCode.ServiceUnavailable);
        server.Answer(
            HttpMethod.Post,
            "/report",
            new ErrorEnvelope(new ErrorEnvelopeError(
                ErrorCode.Forbidden,
                "You are no longer part of this match",
                new ErrorEnvelopeErrorDetails("not_active"),
                RequestId: null)),
            HttpStatusCode.Forbidden);

        server.Events.Write(SealedFrame(8, 1));
        var seen = new List<MultiplayerNotice>();
        await WaitForConnection(session, connected: true, seen);

        // And nothing is offered to a seat the server no longer takes reports from.
        server.Events.Write(SealedFrame(9, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/turns/2/report"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// Leaving the match sends the hashes the server has not heard yet.
    /// </summary>
    /// <remarks>
    /// A turn settles only once every human seat has reported it, so a report dropped on the way
    /// out holds every other player at that turn until its deadline. Cancelling the session's token
    /// first threw away exactly the reports of a player who quit the moment a turn resolved —
    /// which, now that resolving no longer waits for the round trip, is an ordinary way to leave.
    /// </remarks>
    [Fact]
    public async Task LeavingTheMatchSendsAHashTheServerHasNotHeardYet()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var blocked = server.BlockOnce(HttpMethod.Post, "/turns/1/report");
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        server.Events.Write(SealedFrame(8, 1));
        await WaitFor<MultiplayerNotice.TurnResolved>(session);
        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the blocked report");
        // Queued behind the one the server is holding, and never sent before this point.
        server.Events.Write(SealedFrame(9, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session);
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/turns/2/report"));

        var stopping = session.StopAsync();
        blocked.SetResult();
        await stopping;

        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/turns/2/report"));
    }

    /// <summary>
    /// Leaving does not wait on a server that is not answering.
    /// </summary>
    /// <remarks>
    /// The other half of the flush. A report is worth a moment of the quitting player's time and no
    /// more, because the game has a menu to get back to — and nothing is lost by giving up on it,
    /// since a client that returns replays the seal out of the history and reports it again.
    /// </remarks>
    [Fact]
    public async Task LeavingTheMatchDoesNotWaitOutAServerThatIsNotAnswering()
    {
        var (session, server, http) = Running(reportFlushGrace: TimeSpan.FromMilliseconds(50));
        using var _ = http;
        await using var __ = session;
        var blocked = server.BlockOnce(HttpMethod.Post, "/turns/1/report");
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        server.Events.Write(SealedFrame(8, 1));
        await WaitFor<MultiplayerNotice.TurnResolved>(session);
        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the blocked report");

        await session.StopAsync().WaitAsync(Patience, CancellationToken.None);

        blocked.SetResult();
    }

    /// <summary>Drains notices until the connection is reported one way, or gives up.</summary>
    private static async Task WaitForConnection(
        MultiplayerMatchSession session,
        bool connected,
        List<MultiplayerNotice> seen)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            while (session.TryDequeueNotice(out var notice))
            {
                seen.Add(notice);
                if (notice is MultiplayerNotice.ConnectionChanged changed
                    && changed.IsConnected == connected)
                {
                    return;
                }
            }
            await Task.Delay(15).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"the connection was never reported as {(connected ? "up" : "down")} "
            + $"within {Patience.TotalSeconds:0}s");
    }
}
