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
}
