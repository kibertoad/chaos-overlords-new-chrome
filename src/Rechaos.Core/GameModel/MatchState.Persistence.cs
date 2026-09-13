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
            notification.Gang is not { } gang || FindGang(gang) is not null);

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
