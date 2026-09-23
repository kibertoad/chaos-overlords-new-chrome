using System.Globalization;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void UpdateOnlineResolutionExpectation()
    {
        var expectsResolution = _online.IsConnected
            && _online.Stage == MultiplayerStage.WaitingForSeal
            && _online.ReadySubmissionAcknowledged
            && _online.SeatedSeats > 0
            && _online.ReadySeats >= _online.SeatedSeats;
        if (expectsResolution)
            _online.ResolutionExpectedSince ??= MonotonicClock.Now;
        else
            _online.ResolutionExpectedSince = null;
    }

    /// <summary>
    /// A successful final submission is returned only after the server has attempted to seal the
    /// turn. If every seat is ready and no successor arrives, resynchronise: the fact is in the
    /// server's durable log, and the stream that should have carried it may be dead without saying
    /// so.
    /// </summary>
    /// <remarks>
    /// This used to END the session, which is the wrong answer to a silence the session can simply
    /// go and break: the state is rebuilt from the log and the player keeps their city screen and
    /// their planning copy. A resync that itself fails permanently still fails the session, through
    /// the pump, which is where every other permanent failure is decided.
    ///
    /// The grace is re-armed rather than cleared, so a second silence of the same length asks
    /// again; the request is cheap and idempotent.
    /// </remarks>
    private void CheckOnlineResolutionWatchdog()
    {
        if (_session is not { } session
            || _online.ResolutionExpectedSince is not { } since
            || MonotonicClock.Now - since < OnlineResolutionGrace)
            return;
        var turn = _online.PlanningTurn;
        _diagnostics?.Write("multiplayer.turn-resolution.timeout",
            new Dictionary<string, string?>
            {
                ["turn"] = turn.ToString(CultureInfo.InvariantCulture),
                ["ready"] = _online.ReadySeats.ToString(CultureInfo.InvariantCulture),
                ["seated"] = _online.SeatedSeats.ToString(CultureInfo.InvariantCulture),
            });
        _online.ResolutionExpectedSince = MonotonicClock.Now;
        _online.Status = "NO SEALED TURN YET  RESYNCHRONISING WITH THE SERVER";
        session.RequestResync();
    }

    private string OnlineFailureMessage(string reason)
    {
        var message = $"ONLINE MATCH STOPPED: {reason.ToUpperInvariant()}";
        return _activeMultiplayerRecovery is { Completed: false }
            ? $"{message}  OPEN ONLINE AND RECONNECT"
            : message;
    }

    /// <summary>
    /// Stops a session that cannot recover itself and leaves its full diagnostic in a copyable
    /// modal on the Online screen, where the saved membership can immediately be reconnected.
    /// </summary>
    private void ShowOnlineMatchFailure(string reason)
    {
        var message = OnlineFailureMessage(reason);
        EndOnlineMatch(message);
        OpenOnline();
        _online.Status = _multiplayerRecoveries.Any(recovery => recovery.CanResume)
            ? "CLOSE THIS MESSAGE, THEN OPEN PREVIOUS SESSIONS TO RECONNECT"
            : "THE ONLINE SESSION COULD NOT CONTINUE";
        _online.ConnectionError = message;
        _online.ConnectionErrorCopyStatus = string.Empty;
    }
}
