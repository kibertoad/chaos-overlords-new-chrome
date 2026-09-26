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

    /// <summary>The document <see cref="_inFlightOrders"/> is sending. Guarded by <see cref="_outboxGate"/>.</summary>
    private PendingOrders? _inFlightDocument;

    /// <summary>
    /// The latest turn the log has sealed; a draft for it or earlier has nothing left to protect.
    /// Guarded by <see cref="_outboxGate"/>.
    /// </summary>
    private int _sealedThroughTurn;

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
        // The old request may also finish and dispose its source after the read above; it then
        // needs no cancelling, and the replacement is already queued with its wake just below.
        CancelUnlessDisposed(superseded);
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
                if (next is not null && IsRetiredDraft(next)) next = null;
                if (next is not null)
                {
                    requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                    _inFlightOrders = requestCancellation;
                    _inFlightDocument = next;
                }
            }
            if (next is null) continue;
            // Only a finished turn answers to the connection's health. A draft is insurance
            // against the clock sealing the turn before the player ends it, sent on every change
            // and superseded by the next one, so a failed attempt at one is retried quietly: the
            // reconnect modal it used to raise blocked a player who had nothing to do about it.
            var lane = next.Ready ? _outboxLane : null;
            try
            {
                await CallAsync(
                    token => _match.SubmitOrdersAsync(
                        next.Turn, new SubmitOrdersRequest(next.Document, next.Ready), token),
                    lane,
                    requestCancellation!.Token,
                    onQuietRetry: (exception, _) => _notices.Enqueue(
                        new MultiplayerNotice.DraftDelayed(next.Turn, Describe(exception))))
                    .ConfigureAwait(false);
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
                if (lane is null)
                    _notices.Enqueue(new MultiplayerNotice.DraftDelayed(next.Turn, Describe(exception)));
                else
                    lane.Failed(Describe(exception), exception.Attempts, exception);
                Requeue(next);
            }
            catch (MultiplayerApiException exception) when (exception.Reason == "not_active")
            {
                // The seat was handed to the computer, or left, between the player planning this
                // document and it reaching the server. It is not the end of the membership — the
                // player is still on the roster and can take the seat back — so the session goes
                // on watching the match, exactly as `SendReportAsync` does with the same 403, and
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
                var withdrawn = next.Ready
                    && RefusesTheDocument(exception)
                    && WithdrawReadiness(next.Turn);
                _notices.Enqueue(
                    new MultiplayerNotice.OrdersRefused(next.Turn, Describe(exception), withdrawn));
            }
            finally
            {
                lock (_outboxGate)
                {
                    if (ReferenceEquals(_inFlightOrders, requestCancellation))
                    {
                        _inFlightOrders = null;
                        _inFlightDocument = null;
                    }
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
            var requeued = orders with
            {
                Ready = orders.Ready || _locallyReadyTurns.Contains(orders.Turn),
            };
            // A draft whose turn sealed while it was unanswered is not worth another window.
            if (IsRetiredDraft(requeued)) return;
            _pending = requeued;
        }
        _outboxSignal.Release();
    }

    /// <summary>
    /// Stops sending drafts of turns the log has sealed, including one still being retried.
    /// </summary>
    /// <remarks>
    /// A draft only protects planning against the clock, so once its turn is sealed the server has
    /// nothing to take it for: it would answer <c>turn_not_open</c> at best. A newer draft already
    /// cancels the older one's retries, but a player who changes nothing on the next turn sends no
    /// newer draft, and the stale one used to go on retrying — a whole five-minute window, then
    /// another — against a server that was perhaps already struggling. A finished turn is left
    /// alone: the interface waits on its answer to say whether the server took it.
    /// </remarks>
    private void RetireDraftsThrough(int sealedTurn)
    {
        CancellationTokenSource? stale = null;
        lock (_outboxGate)
        {
            if (sealedTurn <= _sealedThroughTurn) return;
            _sealedThroughTurn = sealedTurn;
            if (_pending is { } pending && IsRetiredDraft(pending)) _pending = null;
            if (_inFlightDocument is { } inFlight && IsRetiredDraft(inFlight)) stale = _inFlightOrders;
        }
        // Outside the lock, for the reason `QueueOrders` gives.
        CancelUnlessDisposed(stale);
    }

    /// <summary>Whether the document is a draft of a turn already sealed. Call under <see cref="_outboxGate"/>.</summary>
    private bool IsRetiredDraft(PendingOrders orders) =>
        !orders.Ready && orders.Turn <= _sealedThroughTurn;

    /// <summary>
    /// Whether a refusal was of the document itself, which the server never recorded, rather than
    /// of the turn it was sent for.
    /// </summary>
    /// <remarks>
    /// A malformed response is not one: the server may have taken the document before its answer
    /// went wrong, so nothing is known about what it holds.
    /// </remarks>
    private static bool RefusesTheDocument(Exception exception) =>
        exception is MultiplayerApiException
        {
            Code: ErrorCode.ValidationFailed or ErrorCode.PayloadTooLarge or ErrorCode.BadRequest,
        };

    /// <summary>
    /// Stops carrying readiness forward for a turn whose finished document the server refused.
    /// </summary>
    /// <remarks>
    /// Readiness otherwise accumulates, so the next draft of the turn would say "done" for a player
    /// who is being handed the turn back to change. A newer ready document already queued for the
    /// turn speaks for itself, and nothing is withdrawn under it.
    /// </remarks>
    private bool WithdrawReadiness(int turn)
    {
        lock (_outboxGate)
        {
            if (_pending is { Ready: true } pending && pending.Turn == turn) return false;
            _locallyReadyTurns.Remove(turn);
            return true;
        }
    }

    /// <summary>An order document waiting to be sent, and whether it completes the player's turn.</summary>
    private sealed record PendingOrders(int Turn, OrderDocument Document, bool Ready);
}
