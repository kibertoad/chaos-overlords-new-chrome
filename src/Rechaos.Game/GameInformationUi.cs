using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GameInformationLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public const int ValueLeft = 204;
    public const int PlayerNameLeft = 216;
    public const int IntelligenceRight = 384;
    public const int ObjectiveY = 152;
    public const int AiMentalityY = 170;
    public const int TurnTimeLimitY = 188;

    public static Rectangle PlayerColor(int player)
    {
        ValidatePlayer(player);
        return new Rectangle(204, PlayerY(player), 5, 7);
    }

    public static int PlayerY(int player)
    {
        ValidatePlayer(player);
        return 215 + player * OriginalFontLayout.LineHeight;
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
