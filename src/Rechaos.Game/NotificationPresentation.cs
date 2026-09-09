using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class NotificationPresentation
{
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
