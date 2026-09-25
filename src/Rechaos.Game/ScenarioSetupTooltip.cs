using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class ScenarioSetupTooltip
{
    // RULE-OBJECTIVE-004: each scenario's end condition as the original tests it; RULE-OBJECTIVE-001:
    // every scenario also ends when one Overlord is left.
    public static IReadOnlyList<string> Lines(ScenarioId scenario, GameDuration duration)
    {
        var definition = ScenarioCatalog.Get(scenario);
        var turns = ScenarioCatalog.Turns(duration);
        var rules = scenario switch
        {
            ScenarioId.Greed => new[]
            {
                $"ENDS AFTER TURN {turns}: THE HIGHEST CASH WINS.",
                "SPENDING NOW MUST PAY OFF BEFORE TIME EXPIRES."
            },
            ScenarioId.Power => new[]
            {
                $"ENDS AFTER TURN {turns}: THE MOST SECTORS WINS.",
                "EXPANSION MATTERS; CASH AND SUPPORT DO NOT SCORE."
            },
            ScenarioId.Acceptance => new[]
            {
                $"ENDS AFTER TURN {turns}: THE MOST SUPPORT WINS.",
                "CASH AND CONTROLLED SECTORS DO NOT SCORE."
            },
            ScenarioId.Dominance => DominanceRules(duration, turns),
            ScenarioId.KillEmAll => new[]
            {
                "NO TIME LIMIT: BE THE LAST ACTIVE OVERLORD.",
                "A RIVAL IS DESTROYED ONLY WHEN THEIR LAST GANG DIES",
                "AND THEY OWN NO SECTOR; EITHER ONE KEEPS THEM ALIVE."
            },
            ScenarioId.Big40 => new[]
            {
                "NO TIME LIMIT: ENDS WHEN AN OVERLORD HOLDS 40 SECTORS.",
                "SECTOR CONTROL IS THE ONLY OBJECTIVE."
            },
            ScenarioId.Eliminate => new[]
            {
                "NO TIME LIMIT: ENDS WHEN ONE OVERLORD IS LEFT.",
                "LOSING A RIGHT HANDS ELIMINATES THAT PLAYER."
            },
            ScenarioId.Siege => new[]
            {
                "NO TIME LIMIT: ENDS WHEN ONE OVERLORD HOLDS ALL SIX HQS.",
                "THE OBJECTIVE SECTORS ARE MARKED WITH GRAY PYLONS."
            },
            ScenarioId.BigMan => new[]
            {
                "NO TIME LIMIT: ENDS AT 40 BIG MAN POINTS.",
                "EACH CENTRAL SECTOR YOU CONTROL ADDS 1 POINT PER TURN."
            },
            ScenarioId.Armageddon => new[]
            {
                "NO TIME LIMIT: ENDS WHEN AN OVERLORD HOLDS ALL 64 SECTORS.",
                "EVERY PLAYER STARTS WITH $500 AND ALL ITEMS RESEARCHED."
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        return new[] { definition.Name, definition.Objective.ToUpperInvariant() }
            .Concat(rules)
            .Append(scenario is ScenarioId.KillEmAll or ScenarioId.Eliminate
                ? "IT HAS NO OTHER END TEST."
                : "IT ALSO ENDS WHEN ONE OVERLORD IS LEFT.")
            .ToArray();
    }

    private static string[] DominanceRules(GameDuration duration, int turns)
    {
        var weights = ScenarioCatalog.Weights(duration);
        return
        [
            $"ENDS AFTER TURN {turns}: CASH, SUPPORT, AND SECTORS SCORE.",
            $"WEIGHTS: CASH {weights.Cash}, SUPPORT {weights.Support}, SECTOR {weights.ControlledSector}."
        ];
    }
}

public static class DurationSetupTooltip
{
    /// <summary>How long a match runs, in the words the setup screen and the lobby both use.</summary>
    public static string Label(GameDuration duration) => duration switch
    {
        GameDuration.SixMonths => "6 MONTHS",
        GameDuration.OneYear => "1 YEAR",
        GameDuration.TwoYears => "2 YEARS",
        GameDuration.FourYears => "4 YEARS",
        _ => throw new ArgumentOutOfRangeException(nameof(duration))
    };

    public static IReadOnlyList<string> Lines(GameDuration duration)
    {
        var label = Label(duration);
        return
        [
            label,
            $"{ScenarioCatalog.Turns(duration)} TURNS.",
            "GREED, POWER, ACCEPTANCE, AND DOMINANCE END HERE.",
            "OBJECTIVE MODES CONTINUE UNTIL THEIR GOAL IS MET."
        ];
    }
}
