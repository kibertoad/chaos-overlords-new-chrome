using Rechaos.Multiplayer.Session;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary><see cref="_sentOrderVersion"/> before anything has been sent for the open turn.</summary>
    private const int UnsentOrders = -1;

    /// <summary>
    /// The <see cref="OrderDocumentBuilder.Version"/> the last queued document was built from.
    /// </summary>
    /// <remarks>
    /// The draft is checked every frame. Comparing this integer is what keeps that check from
    /// building and hashing the whole document sixty times a second to learn nothing changed.
    /// </remarks>
    private int _sentOrderVersion = UnsentOrders;

    /// <summary>
    /// The shortest gap between two drafts.
    /// </summary>
    /// <remarks>
    /// The draft is checked every frame and every change used to be a PUT, so a player dragging
    /// gangs about sent several a second into the per-player budget the stream, the reports and
    /// every read share — and a 429 there is refused for everybody on that seat. A draft only
    /// protects work against the clock sealing the turn first, so pacing it costs at most this much
    /// of the planning a player does in the very last second; the finished turn is never paced.
    /// </remarks>
    internal static readonly TimeSpan DraftInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When the last draft was queued, as a <see cref="System.Diagnostics.Stopwatch"/> timestamp, or
    /// null before the first; drafts wait out <see cref="DraftInterval"/> after it.
    /// </summary>
    /// <remarks>
    /// Monotonic, not the wall clock: a clock set back by the system would otherwise hold every
    /// draft for as long as it was set back.
    /// </remarks>
    private long? _lastDraftQueuedAt;

    /// <summary>
    /// The planning handle of the turn the player last ended, kept until that turn is replaced.
    /// </summary>
    /// <remarks>
    /// Ending a turn takes the handle away, so nothing can be added to a turn that has gone to the
    /// server. The server can still refuse the document and leave the turn open; this is what hands
    /// the player back the turn they planned, rather than an empty one, to change and end again.
    /// </remarks>
    private (int Turn, MatchActions Actions)? _submittedPlanning;

    /// <summary>Sends what the player planned and marks them ready; the turn seals on the last one.</summary>
    private void SubmitOnlineTurn()
    {
        if (_session is null || _actions?.OnlineTurn is not { } turn) return;
        if (!_online.PlanningIsOpen) return;
        // The orders are going to the server, so the picks that were waiting to give one are spent.
        _gangSelection.Clear();
        var document = turn.Build();
        _session.QueueOrders(_online.PlanningTurn, document, ready: true);
        _sentOrderVersion = turn.Orders.Version;
        _online.SentOrderDigest = OrderDigest.OfDocument(document);
        _online.ReadySubmissionPending = true;
        _online.ReadySubmissionAcknowledged = false;
        _online.ResolutionExpectedSince = null;
        _online.Stage = MultiplayerStage.WaitingForSeal;
        _submittedPlanning = (_online.PlanningTurn, _actions!);
        CloseOnlinePlanning();
        _message = "SENDING FINISHED TURN TO SERVER";
    }

    /// <summary>
    /// Hands the player back a finished turn whose document the server refused, to change and end
    /// again. Answers whether it did.
    /// </summary>
    /// <remarks>
    /// The session has already stopped carrying readiness for the turn, and the server never
    /// recorded the document, so the turn is still waiting on this seat. Left in
    /// <see cref="MultiplayerStage.WaitingForSeal"/> the player could do nothing about that: an
    /// untimed turn waited on them for good, and the WAIT mark that says so was hidden.
    /// </remarks>
    private bool ReopenRefusedTurn(int turn)
    {
        if (_online.Stage != MultiplayerStage.WaitingForSeal
            || turn != _online.PlanningTurn
            || _submittedPlanning is not { } submitted
            || submitted.Turn != turn)
            return false;
        _actions = submitted.Actions;
        _submittedPlanning = null;
        _online.Stage = MultiplayerStage.Playing;
        _online.ReadySubmissionAcknowledged = false;
        // The server kept none of the refused document, so the next draft goes even if it matches.
        _online.SentOrderDigest = null;
        _sentOrderVersion = UnsentOrders;
        return true;
    }

    /// <summary>Sends the turn so far, without saying the player is done.</summary>
    /// <remarks>
    /// The server replaces the document it holds, so a draft is never additive and can safely be
    /// superseded. Sending it preserves the player's work if the authoritative clock seals first.
    /// </remarks>
    private void SendOnlineDraft()
    {
        if (_session is null || !_online.PlanningIsOpen) return;
        if (_actions?.OnlineTurn is not { } turn) return;
        if (turn.Orders.Version == _sentOrderVersion) return;
        // Left unsent rather than dropped: the version still differs next frame, so the latest
        // document goes as soon as the interval is up, carrying every change made in between.
        if (_lastDraftQueuedAt is { } last
            && System.Diagnostics.Stopwatch.GetElapsedTime(last) < DraftInterval)
        {
            return;
        }
        _sentOrderVersion = turn.Orders.Version;
        var document = turn.Build();
        var digest = OrderDigest.OfDocument(document);
        if (string.Equals(digest, _online.SentOrderDigest, StringComparison.Ordinal)) return;
        _online.SentOrderDigest = digest;
        _lastDraftQueuedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        _session.QueueOrders(_online.PlanningTurn, document, ready: false);
    }
}
