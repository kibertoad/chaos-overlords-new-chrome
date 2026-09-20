using System.Net;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The three boundaries a session used to end itself at, each of which it can ride out.
/// </summary>
/// <remarks>
/// A session that dies is not the same as a match that is lost: the recovery record makes the manual
/// reconnect work either way. What it cannot give back is the city screen, the planning copy and the
/// player's sense that nothing has gone wrong, and each of these three used to spend all three over
/// a condition that resolves itself.
/// </remarks>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A 4xx that did not come from this server's error handler is an outage, not a verdict.
    /// </summary>
    /// <remarks>
    /// A reverse proxy answering 404 for every path while the backend restarts, a tunnel that has
    /// gone stale, another service that took the port: each produces a status with no error
    /// envelope, and the session read them all as "this match is gone".
    /// </remarks>
    [Fact]
    public async Task AStatusOnlyRefusalOnTheStreamIsRetriedRatherThanEndingTheSession()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        // A proxy's own page, twice, then the real server again.
        server.AnswerOnce(HttpMethod.Get, "/stream", "<html>502 Bad Gateway</html>", HttpStatusCode.NotFound);
        server.AnswerOnce(HttpMethod.Get, "/stream", "<html>403 Forbidden</html>", HttpStatusCode.Forbidden);
        server.DropStream();

        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        await Until(
            () => server.CallsTo(HttpMethod.Get, "/stream") >= 3,
            "the stream was retried past the proxy's refusals");
        server.Events.Write(SealedFrame(8, 1));

        // The session is alive and applying turns, which is what the proxy never had a view on.
        await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// The same rule for the membership question: only the server's own reason retires a seat.
    /// </summary>
    [Fact]
    public async Task AStatusOnlyUnauthorizedIsNotReadAsARevokedMembership()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        // A gateway whose own credentials lapsed answers 401 for every path, with its own page.
        server.AnswerOnce(HttpMethod.Put, "/orders", "unauthorized", HttpStatusCode.Unauthorized);
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        session.QueueOrders(1, EmptyOrders, ready: true);

        // The submission is retried and lands, rather than retiring the seat.
        await Until(
            () => server.Requests.Count(request => request.Path.EndsWith("/orders", StringComparison.Ordinal)) >= 2,
            "the submission was retried past the gateway's refusal");
        await WaitFor<MultiplayerNotice.OrdersAccepted>(session);
    }

    /// <summary>
    /// A resync asked for from outside drops the connection and rebuilds from the durable log.
    /// </summary>
    /// <remarks>
    /// This is what the game's resolution watchdog now does instead of ending the session. The
    /// server seals in the same request that completes the roster, so the ready `PUT` succeeds on a
    /// fresh connection while `turn.sealed` goes out on a stream a suspended laptop has silently
    /// killed — and the seal is sitting in the log the whole time.
    /// </remarks>
    [Fact]
    public async Task RequestResyncRebuildsFromTheServerWithoutEndingTheSession()
    {
        var (session, server, http) = Running(
            matchView: ViewAtTurn(2) with { LastEventSeq = 3 },
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(
                [
                    new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
                    new TurnSealedEvent(
                        2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
                    new TurnConfirmedEvent(
                        3, MatchId, "2026-09-10T12:01:30.000Z", new(1, HashAfterTurns(1))),
                ]));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        // The restore the session runs at startup, so the pump is reading the live stream.
        await WaitFor<MultiplayerNotice.Resumed>(session, seen);
        var before = server.CallsTo(HttpMethod.Get, "/stream");

        session.RequestResync();

        // The connection is dropped, the state rebuilt from the durable log, and a new stream
        // opened — all without the session ending.
        await WaitFor<MultiplayerNotice.Resumed>(session, seen);
        await Until(
            () => server.CallsTo(HttpMethod.Get, "/stream") > before,
            "the resync opened a fresh stream");
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// The stream's retry window bounds silent waiting, not the session.
    /// </summary>
    /// <remarks>
    /// The outbox on this same session has always treated it that way: it reports the exhausted
    /// window on its own lane, requeues the document and opens another one. The stream instead
    /// called <c>Fail</c>, so a six-minute sleep or a slow redeploy kept the player's draft and lost
    /// their session. With a budget set — which is what the headless smoke test wants — it still
    /// ends, and that is the difference this asserts.
    /// </remarks>
    [Fact]
    public async Task AnExhaustedStreamWindowDoesNotEndTheSession()
    {
        // A window that closes almost at once, so the test reaches the end of one without waiting
        // out the real five minutes.
        var window = new RetryPolicy(
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(10),
            MaxAttempts: 0,
            MaxElapsed: TimeSpan.FromMilliseconds(50));
        var (session, server, http) = Running(
            streamRetryPolicy: window,
            // The rebuild between two windows asks the server what it holds, so the routes it reads
            // have to be there; a missing one is a permanent refusal and a different test.
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage([]));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/1/orders/mine",
                    new OwnSubmissionView(1, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        // Nothing but refusals for a while: every window closes with nothing delivered.
        for (var refusal = 0; refusal < 40; refusal++)
            server.AnswerOnce(HttpMethod.Get, "/stream", "<html>bad gateway</html>", HttpStatusCode.BadGateway);
        server.DropStream();

        await Until(
            () =>
            {
                while (session.TryDequeueNotice(out var notice)) seen.Add(notice);
                return server.CallsTo(HttpMethod.Get, "/stream") >= 12
                    || seen.Any(notice => notice is MultiplayerNotice.Failed);
            },
            "the stream kept trying past its first closed window");
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);

        // The modal is told how long it has been trying rather than being torn down.
        Assert.Contains(
            seen.OfType<MultiplayerNotice.ConnectionChanged>(),
            change => change.Detail?.Contains("Still trying", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>And with a budget — what the headless smoke test wants — it does end.</summary>
    [Fact]
    public async Task AnExhaustedStreamWindowEndsTheSessionWhenABudgetSaysSo()
    {
        var window = new RetryPolicy(
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(10),
            MaxAttempts: 0,
            MaxElapsed: TimeSpan.FromMilliseconds(50));
        var (session, server, http) = Running(
            streamOutageBudget: TimeSpan.FromMilliseconds(1), streamRetryPolicy: window);
        using var _ = http;
        await using var __ = session;

        server.Answer(HttpMethod.Get, "/stream", "<html>bad gateway</html>", HttpStatusCode.BadGateway);
        server.DropStream();

        var failed = await WaitFor<MultiplayerNotice.Failed>(session);
        Assert.Contains("reconnect", failed.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
