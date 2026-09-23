namespace Rechaos.Game;

/// <summary>
/// What the previous-sessions browser says about a saved membership.
/// </summary>
/// <remarks>
/// Separate from the drawing so the wording and the decision behind it can be asserted without a
/// graphics device. A session this build cannot play is still listed: the seat is held and the
/// player is owed the reason, which is what <see cref="Note"/> puts beside the row and
/// <see cref="Footer"/> spells out under the list once that row is the selected one.
/// </remarks>
public static class OnlineHistoryPresentation
{
    /// <summary>Beside the row: short, because the row already carries the membership.</summary>
    public const string IncompatibleNote = "INCOMPATIBLE";

    /// <summary>Under the list, and the refusal when such a row is rejoined anyway.</summary>
    public const string IncompatibleReason = "INCOMPATIBLE SESSION  IT NEEDS ANOTHER GAME VERSION";

    public const string Hint = "SELECT A ROW  CONFIRM TO REJOIN";

    public static string Row(MultiplayerRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        var role = recovery.IsHost ? "HOST" : "PLAYER";
        return $"{recovery.DisplayName}  {recovery.JoinCode}  {role}";
    }

    public static string? Note(MultiplayerRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return recovery.IsCompatible ? null : IncompatibleNote;
    }

    /// <summary>The line under the list: the reason when it applies, the keys otherwise.</summary>
    public static string Footer(MultiplayerRecovery? selected) =>
        selected is { IsCompatible: false } ? IncompatibleReason : Hint;
}
