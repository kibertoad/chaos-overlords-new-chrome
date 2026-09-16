namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    private bool IsValidNotificationHistory(
        IReadOnlyList<GameNotification> notifications,
        long nextSequence,
        MatchRuntimeRestore restore) =>
        GameNotificationValidator.IsValidHistory(
            notifications, nextSequence, restore.Turn, restore.Events)
        && notifications.All(notification =>
            notification.Gang is not { } gang || WasGangIdIssued(gang));

    /// <summary>
    /// Whether a gang id is one this match handed out, whether or not the gang still exists.
    /// </summary>
    /// <remarks>
    /// A hire that reuses a dead gang's roster slot retires that gang's id while notifications and
    /// events still carry it — an Elimination notice is queued for every gang destroyed in combat —
    /// so requiring the roster to still hold it would refuse an ordinary save and an ordinary
    /// repair snapshot. Ids are handed out above every id in the roster and a slot is only ever
    /// replaced, so everything ever issued is at or below the highest live id. Presentation already
    /// copes with a notice whose gang has gone.
    /// </remarks>
    private bool WasGangIdIssued(GangId gang) =>
        gang.Value >= 0 && gang.Value < NextGangId().Value;

    private void RestoreOutcome(MatchRuntimeRestore restore)
    {
        Outcome = restore.Outcome is null
            ? null
            : MatchOutcomeValidator.Freeze(restore.Outcome);
        var outcomeEvents = _events.Where(gameEvent =>
            gameEvent.Kind == GameEventKind.MatchEnded).ToArray();
        if (Outcome is null && outcomeEvents.Length != 0
            || Outcome is not null && (!MatchOutcomeValidator.IsValid(
                    Outcome, Setup, Coordinator.Turn)
                || outcomeEvents.Length != 1
                || outcomeEvents[0].MatchOutcome is not { } details
                || !MatchOutcomeValidator.Matches(Outcome, details)))
            throw new ArgumentException(
                "Restored match outcome is invalid.", nameof(restore));
    }

    private static bool IsSha256(string value) =>
        value?.Length == 64 && value.All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F');
}
