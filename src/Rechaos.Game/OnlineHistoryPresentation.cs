namespace Rechaos.Game;

/// <summary>
/// What the previous-sessions browser says about a saved membership.
/// </summary>
/// <remarks>
/// Separate from the drawing so the wording can be asserted without a graphics device. Only
/// memberships this build can resume reach the browser (see
/// <see cref="MultiplayerRecovery.CanResume"/>), so a row never has to explain a refusal.
/// </remarks>
public static class OnlineHistoryPresentation
{
    public const string Hint = "SELECT A ROW  CONFIRM TO REJOIN";

    /// <summary>
    /// Shown under the list when no keyring took the seats' tokens and the recovery file holds
    /// them in clear; see <see cref="MultiplayerRecoveryStore.KeepsTokensInClear"/>.
    /// </summary>
    public const string TokensInClearNote = "NO KEYRING FOUND  SEATS ARE SAVED UNENCRYPTED";

    public static string Row(MultiplayerRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return $"{recovery.DisplayName}  {recovery.JoinCode}  {Role(recovery)}";
    }

    /// <summary>Beside the row: the seat the membership holds.</summary>
    public static string Role(MultiplayerRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return recovery.IsHost ? "HOST" : "PLAYER";
    }
}
