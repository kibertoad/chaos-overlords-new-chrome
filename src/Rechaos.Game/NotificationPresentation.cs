using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class NotificationPresentation
{
    public static bool IsLastTurnReport(GameNotification notification, GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (relatedEvent is { Kind: GameEventKind.CommandFailed, Resolution: { } failure })
            return failure.Code == CommandResolutionCode.InsufficientCash
                && relatedEvent.Action is GangAction.Bribe or GangAction.Equip;
        return notification.Kind switch
        {
            // RULE-EVENT-012: a winner with no Control order has a report without an event.
            GameNotificationKind.Control => relatedEvent is null || relatedEvent.Resolution?.Successes > 0,
            GameNotificationKind.Influence or GameNotificationKind.Research =>
                relatedEvent?.Resolution is { PreviousValue: > 0, ResultValue: 0 },
            GameNotificationKind.Elimination => relatedEvent?.Kind == GameEventKind.PlayerEliminated,
            GameNotificationKind.HireInsufficientCash or GameNotificationKind.HireSectorFull
                or GameNotificationKind.HireGangLimit => relatedEvent?.Kind == GameEventKind.HireFailed,
            GameNotificationKind.ControlLost or GameNotificationKind.Crackdown => true,
            _ => false
        };
    }

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
