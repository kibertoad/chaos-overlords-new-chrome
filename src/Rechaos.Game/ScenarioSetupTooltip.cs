using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class ScenarioSetupTooltip
{
    public static IReadOnlyList<string> Lines(ScenarioId scenario, GameDuration duration)
    {
        var definition = ScenarioCatalog.Get(scenario);
        var rules = scenario switch
        {
            ScenarioId.Greed => new[]
            {
                "TIMED: THE HIGHEST CASH TOTAL WINS.",
                "SPENDING NOW MUST PAY OFF BEFORE TIME EXPIRES."
            },
            ScenarioId.Power => new[]
            {
                "TIMED: THE MOST CONTROLLED SECTORS WINS.",
                "EXPANSION MATTERS; CASH AND SUPPORT DO NOT SCORE."
            },
            ScenarioId.Acceptance => new[]
            {
                "TIMED: THE HIGHEST SUPPORT TOTAL WINS.",
                "CASH AND CONTROLLED SECTORS DO NOT SCORE."
            },
            ScenarioId.Dominance => DominanceRules(duration),
            ScenarioId.KillEmAll => new[]
            {
                "NO TIME LIMIT: BE THE LAST ACTIVE OVERLORD.",
                "A RIVAL IS DESTROYED ONLY WHEN THEIR LAST GANG DIES",
                "AND THEY OWN NO SECTOR; EITHER ONE KEEPS THEM ALIVE."
            },
            ScenarioId.Big40 => new[]
            {
                "NO TIME LIMIT: FIRST TO CONTROL 40 SECTORS WINS.",
                "SECTOR CONTROL IS THE ONLY OBJECTIVE."
            },
            ScenarioId.Eliminate => new[]
            {
                "NO TIME LIMIT: KILL EVERY OPPOSING RIGHT HANDS.",
                "LOSING A RIGHT HANDS ELIMINATES THAT PLAYER."
            },
            ScenarioId.Siege => new[]
            {
                "NO TIME LIMIT: CONTROL ALL SIX HQ SECTORS AT ONCE.",
                "THE OBJECTIVE SECTORS ARE MARKED WITH GRAY PYLONS."
            },
            ScenarioId.BigMan => new[]
            {
                "NO TIME LIMIT: FIRST TO 40 BIG MAN POINTS WINS.",
                "EACH CENTRAL SECTOR YOU CONTROL ADDS 1 POINT PER TURN."
            },
            ScenarioId.Armageddon => new[]
            {
                "NO TIME LIMIT: CONTROL ALL 64 SECTORS.",
                "EVERY PLAYER STARTS WITH $500 AND ALL ITEMS RESEARCHED."
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        return new[] { definition.Name, definition.Objective.ToUpperInvariant() }
            .Concat(rules).ToArray();
    }

    private static string[] DominanceRules(GameDuration duration)
    {
        var weights = ScenarioCatalog.Weights(duration);
        return
        [
            "TIMED: CASH, SUPPORT, AND SECTOR CONTROL ALL SCORE.",
            $"WEIGHTS: CASH {weights.Cash}, SUPPORT {weights.Support}, SECTOR {weights.ControlledSector}."
        ];
    }
}

public static class DurationSetupTooltip
{
    public static IReadOnlyList<string> Lines(GameDuration duration)
    {
        var label = duration switch
        {
            GameDuration.SixMonths => "6 MONTHS",
            GameDuration.OneYear => "1 YEAR",
            GameDuration.TwoYears => "2 YEARS",
            GameDuration.FourYears => "4 YEARS",
            _ => throw new ArgumentOutOfRangeException(nameof(duration))
        };
        return
        [
            label,
            $"{ScenarioCatalog.Turns(duration)} TURNS.",
            "GREED, POWER, ACCEPTANCE, AND DOMINANCE END HERE.",
            "OBJECTIVE MODES CONTINUE UNTIL THEIR GOAL IS MET."
        ];
    }
}
