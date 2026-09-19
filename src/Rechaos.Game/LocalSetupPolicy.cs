using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class LocalSetupPolicy
{
    public const int DefaultHumanPlayerCount = 1;
    public const int MaximumPlayerNameCharacters = 10;

    public static string DefaultPlayerName(int playerIndex)
    {
        if (playerIndex is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        return $"PLAYER#{playerIndex + 1}";
    }

    /// <summary>
    /// Applies the original setup dialog's accepted text to a player name.
    /// </summary>
    /// <remarks>
    /// Its input buffer starts empty and the native copy helper does nothing when it remains so.
    /// A space is not empty: it is a glyph the native helper stores in the fixed record.
    /// </remarks>
    public static string NameAfterModalEntry(string existingName, string enteredName)
    {
        ArgumentNullException.ThrowIfNull(existingName);
        ArgumentNullException.ThrowIfNull(enteredName);
        return enteredName.Length == 0 ? existingName : enteredName;
    }
}
