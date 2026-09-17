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
            GameNotificationKind.Control => relatedEvent?.Resolution?.Successes > 0,
            GameNotificationKind.Influence or GameNotificationKind.Research =>
                relatedEvent?.Resolution is { PreviousValue: > 0, ResultValue: 0 },
            GameNotificationKind.Elimination => relatedEvent?.Kind == GameEventKind.PlayerEliminated,
            GameNotificationKind.HireInsufficientCash or GameNotificationKind.HireSectorFull
                or GameNotificationKind.HireGangLimit => relatedEvent?.Kind == GameEventKind.HireFailed,
            GameNotificationKind.ControlLost or GameNotificationKind.Crackdown => true,
            _ => false
        };
    }

    public static string LastTurnStatus(GameNotification notification) =>
        LastTurnStatus(notification, null);

    public static string LastTurnStatus(
        GameNotification notification,
        GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (relatedEvent is { Kind: GameEventKind.CommandFailed, Resolution: { } resolution })
        {
            if (resolution.Code == CommandResolutionCode.InsufficientCash)
            {
                if (relatedEvent.Action == GangAction.Bribe) return "INSUFFICIENT CASH TO BRIBE.";
                if (relatedEvent.Action == GangAction.Equip) return "INSUFFICIENT CASH TO EQUIP.";
            }
            var action = SplitWords(relatedEvent.Action.ToString()).ToUpperInvariant();
            var reason = resolution.Code switch
            {
                CommandResolutionCode.InsufficientCash => "NOT ENOUGH CASH",
                CommandResolutionCode.ItemUnavailable => "ITEM NO LONGER AVAILABLE",
                CommandResolutionCode.DestinationFull => "DESTINATION IS FULL",
                CommandResolutionCode.TargetHidden => "TARGET IS HIDDEN",
                CommandResolutionCode.TargetEvaded => "TARGET EVADED",
                CommandResolutionCode.SectorInCrackdown => "SECTOR IS IN POLICE CRACKDOWN",
                CommandResolutionCode.UnsupportedAction => "ACTION IS NOT SUPPORTED",
                _ => "ORDER COULD NOT BE COMPLETED"
            };
            return $"{action} FAILED: {reason}.";
        }

        return notification.Kind switch
        {
            GameNotificationKind.Control => "SECTOR CONTROL ATTAINED.",
            GameNotificationKind.ControlLost => "SECTOR CONTROL LOST.",
            GameNotificationKind.Influence => "SITE COOPERATION ACHIEVED.",
            GameNotificationKind.Research => "RESEARCH COMPLETED.",
            GameNotificationKind.Crackdown => "POLICE CRACKDOWN.",
            GameNotificationKind.Elimination => "PLAYER HAS BEEN ELIMINATED.",
            GameNotificationKind.HireInsufficientCash => "INSUFFICIENT CASH TO HIRE.",
            GameNotificationKind.HireSectorFull => "UNABLE TO HIRE, SECTOR AT CAPACITY.",
            GameNotificationKind.HireGangLimit => "UNABLE TO HIRE, MAX GANGS REACHED.",
            _ => throw new ArgumentOutOfRangeException(nameof(notification))
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
