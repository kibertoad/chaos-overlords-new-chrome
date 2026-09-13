using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class AiPolicyPresentation
{
    public static string Label(AiPolicyMode policy) => policy switch
    {
        AiPolicyMode.Original => "ORIGINAL",
        AiPolicyMode.Advanced => "ADVANCED",
        _ => throw new ArgumentOutOfRangeException(nameof(policy))
    };

    public static string SelectionMessage(AiPolicyMode policy) =>
        $"AI POLICY: {Label(policy)} - F1 FOR EXACT DIFFERENCES";

    public static IReadOnlyList<string> Tooltip(AiPolicyMode policy) =>
        Enum.IsDefined(policy)
            ?
            [
                $"AI POLICY: {Label(policy)} (SELECTED)",
                "ORIGINAL REPRODUCES SHIPPED DECISIONS.",
                "IDLE FALLBACK: HEAL INJURY, ATTACK A",
                "DETECTABLE LOCAL RIVAL, OR CONTROL HERE.",
                "OTHERWISE THE GANG REMAINS IDLE.",
                "EXPERT ONLY (CRIME LORD/HOMICIDAL):",
                "FORCE 8+ IN OWN SECTOR MOVES OUT WHEN IDLE",
                "OR REPEATING HIDE, SNITCH, OR BRIBE.",
                "ADDED MOVES KEEP 1 DEFENDER AND STOP FOR A",
                "DETECTABLE LOCAL RIVAL; ORIGINAL MOVES REMAIN.",
                "BEST OBJECTIVE/INCOME MOVE; LOWER ID TIES.",
                "ATTACK TIES: LOWER FORCE, THEN LOWER ID.",
                "NO RNG, CASH, STAT, VISION, OR ODDS BONUS. F1: MORE."
            ]
            : throw new ArgumentOutOfRangeException(nameof(policy));
}
