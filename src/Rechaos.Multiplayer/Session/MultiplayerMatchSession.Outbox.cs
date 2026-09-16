using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The outbox: this player's order document on its way to the server, off the game thread.
/// </summary>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>Guards <see cref="_pending"/>, which the game thread writes and the outbox reads.</summary>
    private readonly object _outboxGate = new();

    /// <summary>Wakes the outbox. Counting, so a spurious wake costs one re-check of nothing.</summary>
    private readonly SemaphoreSlim _outboxSignal = new(0);

    /// <summary>Turns this client has already said it is done with, so a later draft carries it.</summary>
    private readonly HashSet<int> _locallyReadyTurns = [];

    private PendingOrders? _pending;

    /// <summary>
    /// Queues this player's order document for a turn, to be sent off the caller's thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a game loop, which cannot wait on a round trip. The answer arrives as
    /// <see cref="MultiplayerNotice.OrdersAccepted"/> or
    /// <see cref="MultiplayerNotice.OrdersRefused"/>.
    /// </para>
    /// <para>
    /// Only the latest document is kept: the server replaces what it held rather than adding to it,
    /// so a document superseded before it was ever sent has nothing in it the newer one lacks.
    /// Readiness is the exception and accumulates — having said "I am done" is not something a later
    /// draft of the same turn takes back.
    /// </para>
    /// </remarks>
    public void QueueOrders(int turn, OrderDocument document, bool ready)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_outboxGate)
        {
            if (ready) _locallyReadyTurns.Add(turn);
            var carriedReady = _locallyReadyTurns.Contains(turn)
                || (_pending is { } pending && pending.Turn == turn && pending.Ready);
            _pending = new PendingOrders(turn, document, carriedReady);
        }
        _outboxSignal.Release();
    }

    /// <summary>
    /// Sends this player's order document for a turn, with readiness, and waits for the answer.
    /// </summary>
    /// <remarks>
    /// The whole document goes every time, not a delta: the server replaces what it held, and the
    /// turn seals in this same call when readiness completes the roster. A submission that lands
    /// after the seal is refused rather than folded in, so a caller that loses that race re-plans
    /// against the next turn. For the game loop use <see cref="QueueOrders"/> instead.
    /// </remarks>
    public Task<OwnSubmissionView> SubmitOrdersAsync(
        int turn,
        OrderDocument document,
        bool ready,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CallAsync(
            token => _match.SubmitOrdersAsync(
                turn, new SubmitOrdersRequest(document, ready), token),
            cancellationToken);
    }

    /// <summary>
    /// Sends whatever the interface last queued, one document at a time.
    /// </summary>
    /// <remarks>
    /// A refusal is reported and forgotten rather than ending the session: a turn that sealed while
    /// the player was still typing is an ordinary race, and the next turn is still theirs to play.
    /// </remarks>
    private async Task DrainOutboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _outboxSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                PendingOrders? next;
                lock (_outboxGate)
                {
                    next = _pending;
                    _pending = null;
                }
                if (next is null) continue;
                try
                {
                    await CallAsync(
                        token => _match.SubmitOrdersAsync(
                            next.Turn, new SubmitOrdersRequest(next.Document, next.Ready), token),
                        cancellationToken).ConfigureAwait(false);
                    _notices.Enqueue(new MultiplayerNotice.OrdersAccepted(next.Turn, next.Ready));
                }
                catch (Exception exception) when (exception is MultiplayerApiException
                    or MultiplayerProtocolException)
                {
                    // The server had its say. A turn that sealed while the player was still
                    // planning is the ordinary case, and re-sending would be refused again.
                    _notices.Enqueue(
                        new MultiplayerNotice.OrdersRefused(next.Turn, Describe(exception)));
                }
                catch (Exception exception) when (exception is MultiplayerTimeoutException
                    or HttpRequestException or IOException)
                {
                    // Nobody refused anything: the request never got an answer. Throwing the
                    // document away here would lose a fully planned turn to a few seconds of bad
                    // connectivity, with no way to resubmit it — planning is already closed by the
                    // time this runs. Keep it and try again until the server answers or the turn
                    // seals, which comes back as a refusal above.
                    Report(connected: false, Describe(exception));
                    Requeue(next);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown.
        }
    }

    /// <summary>
    /// Puts a document that never reached the server back at the front of the outbox.
    /// </summary>
    /// <remarks>
    /// Unless the interface has queued another in the meantime: only the latest document counts,
    /// and a newer one has in it everything this one had.
    /// </remarks>
    private void Requeue(PendingOrders orders)
    {
        lock (_outboxGate)
        {
            if (_pending is not null) return;
            _pending = orders with
            {
                Ready = orders.Ready || _locallyReadyTurns.Contains(orders.Turn),
            };
        }
        _outboxSignal.Release();
    }

    /// <summary>An order document waiting to be sent, and whether it completes the player's turn.</summary>
    private sealed record PendingOrders(int Turn, OrderDocument Document, bool Ready);
}
