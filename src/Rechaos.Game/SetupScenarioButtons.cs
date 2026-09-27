using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class SetupScenarioButtons
{
    private static readonly ScenarioId[] OrderedScenarios =
    [
        ScenarioId.KillEmAll, ScenarioId.Big40,
        ScenarioId.Siege, ScenarioId.Eliminate,
        ScenarioId.BigMan, ScenarioId.Armageddon,
        ScenarioId.Greed, ScenarioId.Power,
        ScenarioId.Acceptance, ScenarioId.Dominance
    ];
    public static IReadOnlyList<ScenarioId> VisualOrder => OrderedScenarios;

    public static ScenarioId ScenarioForButton(int button)
    {
        if (button < 0 || button >= OrderedScenarios.Length)
            throw new ArgumentOutOfRangeException(nameof(button));
        return OrderedScenarios[button];
    }

    public static int ButtonForScenario(ScenarioId scenario)
    {
        var button = Array.IndexOf(OrderedScenarios, scenario);
        return button >= 0 ? button : throw new ArgumentOutOfRangeException(nameof(scenario));
    }
}
