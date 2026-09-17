using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GameInformationLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static int ValueLeft => SharedPanelLayout.X(100);
    public static int PlayerNameLeft => SharedPanelLayout.X(112);
    public static int IntelligenceRight => SharedPanelLayout.X(280);
    public static int ObjectiveY => SharedPanelLayout.Y(27);
    public static int AiMentalityY => SharedPanelLayout.Y(45);
    public static int TurnTimeLimitY => SharedPanelLayout.Y(63);

    public static Rectangle PlayerColor(int player)
    {
        ValidatePlayer(player);
        return new Rectangle(SharedPanelLayout.X(100), PlayerY(player), 5, 7);
    }

    public static int PlayerY(int player)
    {
        ValidatePlayer(player);
        return SharedPanelLayout.Y(90 + player * OriginalFontLayout.LineHeight);
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
