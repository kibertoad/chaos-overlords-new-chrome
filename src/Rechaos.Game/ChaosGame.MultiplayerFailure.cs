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
            _online.ResolutionExpectedSince ??= DateTimeOffset.UtcNow;
        else
            _online.ResolutionExpectedSince = null;
    }

    /// <summary>
    /// A successful final submission is returned only after the server has attempted to seal the
    /// turn. If every seat is ready but no successor arrives, reconnect instead of displaying an
    /// unbounded wait: reconnecting replays the server's durable event history.
    /// </summary>
    private void CheckOnlineResolutionWatchdog()
    {
        if (_online.ResolutionExpectedSince is not { } since
            || DateTimeOffset.UtcNow - since < OnlineResolutionGrace)
            return;
        var turn = _online.PlanningTurn;
        var details = $"Server acknowledged all {_online.SeatedSeats} ready players for turn "
            + $"{turn}, but no sealed turn arrived within {OnlineResolutionGrace.TotalSeconds:0} "
            + "seconds. Reconnect to replay the authoritative event history.";
        _diagnostics?.Write("multiplayer.turn-resolution.timeout",
            new Dictionary<string, string?>
            {
                ["turn"] = turn.ToString(CultureInfo.InvariantCulture),
                ["ready"] = _online.ReadySeats.ToString(CultureInfo.InvariantCulture),
                ["seated"] = _online.SeatedSeats.ToString(CultureInfo.InvariantCulture),
            });
        _online.ResolutionExpectedSince = null;
        ShowOnlineMatchFailure(details);
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
        _online.Status = _multiplayerRecoveries.Any(recovery => recovery.CanReconnect)
            ? "CLOSE THIS MESSAGE, THEN OPEN PREVIOUS SESSIONS TO RECONNECT"
            : "THE ONLINE SESSION COULD NOT CONTINUE";
        _online.ConnectionError = message;
        _online.ConnectionErrorCopyStatus = string.Empty;
    }
}
