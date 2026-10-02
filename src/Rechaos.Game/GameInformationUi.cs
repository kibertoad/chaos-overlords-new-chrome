using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GameInformationLayout
{
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Ok => new(161, 293, 49, 22);

    /// <summary>SCR-UI-008: a press outside this rectangle is refused.</summary>
    public static Rectangle InputBounds => new(104, 124, 344, 209);
    public static int ValueLeft => 228;
    public static int PlayerNameLeft => 240;
    public static int IntelligenceRight => 408;
    public static int ObjectiveY => SharedPanelLayout.Y(27);
    public static int AiMentalityY => SharedPanelLayout.Y(45);
    public static int TurnTimeLimitY => SharedPanelLayout.Y(63);

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
    private static readonly int[] ScenarioLengths = [26, 52, 104, 208];

    /// <summary>
    /// RULE-UI-009 game_info_scenario_text: the scenario's name, and for the four timed scenarios
    /// the length whose turn count equals the game's in parentheses.
    /// </summary>
    public static string ScenarioLabel(ScenarioId scenario, GameDuration duration)
    {
        var number = ExecutableStrings.ScenarioNumber(scenario);
        var name = ExecutableStrings.ScenarioTitle(scenario);
        if (number > 3) return name;
        var turnLimit = ScenarioCatalog.Turns(duration);
        // A length outside the four leaves the name in the suffix.
        var suffix = name;
        for (var k = 0; k < ScenarioLengths.Length; k++)
            if (turnLimit == ScenarioLengths[k])
                suffix = ExecutableStrings.Get(0x36 + k);
        return $"{name} ({suffix})";
    }

    /// <summary>RULE-UI-009 game_info_mentality_text.</summary>
    public static string Mentality(AiDifficulty mentality) =>
        ExecutableStrings.Get(0x2E + (int)mentality);

    /// <summary>
    /// The Mentality field. With Advanced AI on, DEV-AI-003 names the computer-player policy after
    /// the Mentality; with it off the field is the original's.
    /// </summary>
    public static string MentalityField(AiDifficulty mentality, AiPolicyMode policy) =>
        policy == AiPolicyMode.Original
            ? Mentality(mentality)
            : $"{Mentality(mentality)} {AiPolicyPresentation.Label(policy)}";

    /// <summary>RULE-UI-009 game_info_limit_text.</summary>
    public static string PlanningLimit(PlanningTimeLimit limit) =>
        ExecutableStrings.Get(0x32 + (int)limit);

    public static string Intelligence(PlayerController controller) => controller switch
    {
        PlayerController.Human => ExecutableStrings.Get(0x3A),
        PlayerController.Computer => ExecutableStrings.Get(0x3B),
        _ => throw new ArgumentOutOfRangeException(nameof(controller))
    };

    /// <summary>
    /// RULE-UI-009 player_status_text: an eliminated player is labelled eliminated whether it was
    /// a human or a computer.
    /// </summary>
    public static string PlayerLabel(PlayerController controller, PlayerStatus status) => status switch
    {
        PlayerStatus.Active => Intelligence(controller),
        PlayerStatus.Eliminated => ExecutableStrings.Get(0x3C),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
