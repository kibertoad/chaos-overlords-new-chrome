using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>Sends what the player planned and marks them ready; the turn seals on the last one.</summary>
    private void SubmitOnlineTurn()
    {
        if (_session is null || _actions?.OnlineTurn is not { } turn) return;
        if (!_online.PlanningIsOpen) return;
        var document = turn.Build();
        _session.QueueOrders(_online.PlanningTurn, document, ready: true);
        _online.SentOrderDigest = OrderDigest.OfDocument(document);
        _online.ReadySubmissionPending = true;
        _online.ReadySubmissionAcknowledged = false;
        _online.ResolutionExpectedSince = null;
        _online.Stage = MultiplayerStage.WaitingForSeal;
        CloseOnlinePlanning();
        _message = "SENDING FINISHED TURN TO SERVER";
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
        var document = turn.Build();
        var digest = OrderDigest.OfDocument(document);
        if (string.Equals(digest, _online.SentOrderDigest, StringComparison.Ordinal)) return;
        _online.SentOrderDigest = digest;
        _session.QueueOrders(_online.PlanningTurn, document, ready: false);
    }
}
