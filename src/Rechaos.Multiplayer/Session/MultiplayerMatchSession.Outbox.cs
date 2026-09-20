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
    private CancellationTokenSource? _inFlightOrders;

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
        CancellationTokenSource? superseded;
        lock (_outboxGate)
        {
            if (ready) _locallyReadyTurns.Add(turn);
            var carriedReady = _locallyReadyTurns.Contains(turn)
                || (_pending is { } pending && pending.Turn == turn && pending.Ready);
            _pending = new PendingOrders(turn, document, carriedReady);
            // A whole-document replacement makes the older request disposable. In particular, do
            // not spend the reconnect window retrying a stale draft while a newer one waits.
            superseded = _inFlightOrders;
        }
        // Cancelled OUTSIDE the lock. `Cancel` runs its registrations synchronously on the calling
        // thread — the game thread here — and the retry loop's continuation can resume inline
        // through it: out of `Task.Delay`, through the lane's recovery report, through the
        // `finally`, and on into the next submission's synchronous prologue, all while this lock
        // was still held. It is reentrant, so nothing deadlocked; what it did was run the outbox's
        // own bookkeeping under a lock taken by the interface, from the interface's thread.
        superseded?.Cancel();
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
            lane: null,
            cancellationToken);
    }

    /// <summary>The outbox's whole life, ending the session if it cannot go on.</summary>
    private async Task RunOutboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DrainOutboxAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown, or the pump failed first and stopped the session.
        }
        catch (Exception exception)
        {
            Fail(exception, Volatile.Read(ref _outboxOperation) ?? "submit_orders");
        }
    }

    /// <summary>
    /// Sends whatever the interface last queued, one document at a time.
    /// </summary>
    /// <remarks>
    /// A refusal is reported and forgotten rather than ending the session: a turn that sealed while
    /// the player was still typing is an ordinary race, and the next turn is still theirs to play.
    /// A revoked membership is the exception — the seat is gone, and no document will ever be
    /// taken from it again — and ends the session the way it would had the pump met it.
    /// </remarks>
    private async Task DrainOutboxAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await _outboxSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            PendingOrders? next;
            CancellationTokenSource? requestCancellation = null;
            lock (_outboxGate)
            {
                next = _pending;
                _pending = null;
                if (next is not null)
                {
                    requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                    _inFlightOrders = requestCancellation;
                }
            }
            if (next is null) continue;
            try
            {
                await CallAsync(
                    token => _match.SubmitOrdersAsync(
                        next.Turn, new SubmitOrdersRequest(next.Document, next.Ready), token),
                    _outboxLane,
                    requestCancellation!.Token).ConfigureAwait(false);
                _notices.Enqueue(new MultiplayerNotice.OrdersAccepted(next.Turn, next.Ready));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && requestCancellation!.IsCancellationRequested)
            {
                // QueueOrders replaced this document. The latest pending document is the only
                // one worth sending, and its signal is already waiting for the next loop. Whatever
                // this one had reported about its attempts describes nothing still being tried.
                _outboxLane.Recovered();
            }
            catch (MultiplayerApiException exception) when (MultiplayerFailureText.IsMembershipRevoked(exception))
            {
                throw;
            }
            catch (RetryExhaustedException exception) when (TransientFailure.CanRetryAfterExhaustion(exception))
            {
                // CallAsync wraps the last unanswered transient failure when its five-minute
                // retry window expires. The order PUT remains an idempotent whole-document
                // replacement, so retain it and start a new window instead of ending the match
                // (or silently throwing away a turn the player already closed locally).
                _outboxLane.Failed(Describe(exception), exception.Attempts);
                Requeue(next);
            }
            catch (MultiplayerApiException exception) when (exception.Reason == "not_active")
            {
                // The seat was handed to the computer, or left, between the player planning this
                // document and it reaching the server. It is not the end of the membership — the
                // player is still on the roster and can take the seat back — so the session goes
                // on watching the match, exactly as `ReportAsync` does with the same 403, and
                // stops offering documents the server will not take.
                _ownSeatIsComputerControlled = true;
                _notices.Enqueue(
                    new MultiplayerNotice.OrdersRefused(next.Turn, Describe(exception)));
            }
            catch (Exception exception) when (exception is MultiplayerApiException
                or MultiplayerProtocolException)
            {
                // The server had its say. A turn that sealed while the player was still
                // planning is the ordinary case, and re-sending would be refused again.
                _notices.Enqueue(
                    new MultiplayerNotice.OrdersRefused(next.Turn, Describe(exception)));
            }
            finally
            {
                lock (_outboxGate)
                {
                    if (ReferenceEquals(_inFlightOrders, requestCancellation))
                        _inFlightOrders = null;
                }
                requestCancellation?.Dispose();
            }
        }
    }

    /// <summary>
    /// Returns a document that received no answer to the front of the outbox.
    /// </summary>
    /// <remarks>
    /// A newer draft wins if the game thread supplied one while this document was reconnecting.
    /// Otherwise preserve the readiness bit accumulated for the turn and make the next five-minute
    /// retry window start immediately. This does not duplicate a document: the server's PUT is a
    /// whole-document replacement and a possible earlier accepted request is equivalent to the
    /// one being retried.
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
