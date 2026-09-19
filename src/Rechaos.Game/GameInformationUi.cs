using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GameInformationLayout
{
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Ok => new(161, 293, 49, 22);
    public static int ValueLeft => 228;
    public static int PlayerNameLeft => 240;
    public static int IntelligenceRight => 408;
    public static int ObjectiveY => SharedPanelLayout.Y(27);
    public static int AiMentalityY => SharedPanelLayout.Y(45);
    public static int TurnTimeLimitY => SharedPanelLayout.Y(63);

    public static Rectangle PlayerColor(int player)
    {
        ValidatePlayer(player);
        return new Rectangle(ValueLeft, PlayerY(player), 5, 7);
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
    /// <summary>
    /// Native Game Information gives timed objectives their selected duration in
    /// parentheses; objective scenarios have no duration suffix.
    /// </summary>
    public static string ScenarioLabel(ScenarioId scenario, GameDuration duration)
    {
        var definition = ScenarioCatalog.Get(scenario);
        return definition.IsTimed
            ? $"{definition.Name} ({DurationSetupTooltip.Label(duration)})"
            : definition.Name;
    }

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
