using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class NotificationPresentation
{
    public static bool IsLastTurnReport(GameNotification notification, GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.Kind switch
        {
            GameNotificationKind.Control => relatedEvent?.Resolution?.Successes > 0,
            GameNotificationKind.Influence or GameNotificationKind.Research =>
                relatedEvent?.Resolution is { PreviousValue: > 0, ResultValue: 0 },
            GameNotificationKind.ControlLost or GameNotificationKind.Crackdown
                or GameNotificationKind.Elimination or GameNotificationKind.Objective => true,
            _ => false
        };
    }

    public static string LastTurnStatus(GameNotification notification) => notification.Kind switch
    {
        GameNotificationKind.Control => "SECTOR CONTROL ATTAINED.",
        GameNotificationKind.ControlLost => "SECTOR CONTROL LOST.",
        GameNotificationKind.Influence => "SITE INFLUENCE ATTAINED.",
        GameNotificationKind.Research => "RESEARCH COMPLETED.",
        GameNotificationKind.Crackdown => "POLICE CRACKDOWN.",
        GameNotificationKind.Elimination => "GANG OR PLAYER ELIMINATED.",
        GameNotificationKind.Objective => "OBJECTIVE STATUS UPDATED.",
        _ => throw new ArgumentOutOfRangeException(nameof(notification))
    };

    public static string Describe(GameNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var location = notification.SectorId is { } sector ? $" SECTOR {sector + 1}" : "";
        var gang = notification.Gang is { } gangId ? $" GANG {gangId.Value}" : "";
        var label = notification.Kind switch
        {
            GameNotificationKind.ControlLost => "CONTROL LOST",
            GameNotificationKind.Crackdown => "POLICE CRACKDOWN",
            GameNotificationKind.Police => "POLICE ATTACK",
            GameNotificationKind.CommandResult => "COMMAND RESULT",
            _ => SplitWords(notification.Kind.ToString()).ToUpperInvariant()
        };
        return $"T{notification.Turn} {label}{gang}{location}";
    }

    private static string SplitWords(string value) =>
        string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
}
