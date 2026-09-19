namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// Reconciles the local recovery browser with the servers that own those matches.  This runs
    /// separately from the selected-server health probe because a player can have recoveries on
    /// more than one server.
    /// </summary>
    private void BeginRecoveryReconciliation()
    {
        _recoveryReconciliationCancellation?.Cancel();
        _recoveryReconciliationCancellation?.Dispose();
        _recoveryReconciliationCancellation = new CancellationTokenSource();
        var saved = _multiplayerRecoveries.Where(recovery => recovery.CanReconnect).ToArray();
        _recoveryReconciliation = MultiplayerRecoveryReconciliation.FindUnavailableAsync(
            _http, saved, _recoveryReconciliationCancellation.Token);
    }

    private void PumpRecoveryReconciliation()
    {
        var reconciliation = _recoveryReconciliation;
        if (reconciliation is null || !reconciliation.IsCompleted) return;
        _recoveryReconciliation = null;
        if (!reconciliation.IsCompletedSuccessfully) return;
        var unavailable = reconciliation.Result;
        if (unavailable.Count == 0) return;
        _multiplayerRecoveries.RemoveAll(local => unavailable.Any(remote => SameMembership(local, remote)));
        SaveOnlineRecoveries();
        _online.RecoverySelection = Math.Clamp(
            _online.RecoverySelection, 0, Math.Max(0, RecoverableOnlineSessions.Count - 1));
    }
}
