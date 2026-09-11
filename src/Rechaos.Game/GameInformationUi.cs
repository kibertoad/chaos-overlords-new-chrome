using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GameInformationLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public const int ValueLeft = 199;
    public const int PlayerNameLeft = 211;
    public const int IntelligenceRight = 384;
    public const int ObjectiveY = 153;
    public const int AiMentalityY = 174;
    public const int TurnTimeLimitY = 195;

    public static Rectangle PlayerColor(int player)
    {
        ValidatePlayer(player);
        return new Rectangle(199, PlayerY(player), 6, 7);
    }

    public static int PlayerY(int player)
    {
        ValidatePlayer(player);
        return 219 + player * 10;
    }

    private static void ValidatePlayer(int player)
    {
        if (player is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
    }
}

public static class GameInformationPresentation
{
    public static string Intelligence(PlayerController controller) => controller switch
    {
        PlayerController.Human => "HUMAN",
        PlayerController.Computer => "AI",
        _ => throw new ArgumentOutOfRangeException(nameof(controller))
    };

    public static bool OpensAtNewGame(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return setup.Players.Count(player => player.Controller == PlayerController.Human) > 1;
    }
}
