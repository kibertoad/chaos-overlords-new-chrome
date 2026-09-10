namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    /// <summary>
    /// Recreation-native ranking retained only for callers without a usable
    /// recovered family command. These weights are not original-game evidence.
    /// </summary>
    private static GameCommand? SelectProvisionalFallbackCommand(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        IReadOnlyList<GameCommand> options) =>
        options
            .Where(command => IsObservableFallbackAttack(state, player.Id, command))
            .OrderByDescending(command => ScoreProvisionalCommand(state, player, gang, command))
            .ThenBy(command => command.Action)
            .ThenBy(command => command.Target.Kind)
            .ThenBy(command => command.Target.Id)
            .ThenBy(command => command.SecondaryTarget?.Id ?? -1)
            .FirstOrDefault();

    private static int ScoreProvisionalCommand(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        GameCommand command)
    {
        var objective = state.Setup.Scenario;
        return command.Action switch
        {
            GangAction.Attack => AttackValue(state, player, command, objective),
            GangAction.Control => ControlValue(state, player, gang, objective),
            GangAction.Influence => 650 + InfluenceValue(state, command.Target.Id, objective),
            GangAction.Heal => HealValue(state, gang),
            GangAction.Equip => EquipmentValue(state, command),
            GangAction.Research => 500 - player.RemainingResearch(
                state.Definitions, checked((short)command.Target.Id)),
            GangAction.Move => 420 + DestinationValue(state, player.Id, command.Target.Id, objective),
            GangAction.Chaos => 480 + (objective is ScenarioId.Greed or ScenarioId.Dominance ? 80 : 0),
            GangAction.Hide => 300,
            GangAction.Snitch => 280,
            GangAction.Bribe => 260,
            GangAction.Sell => 120,
            GangAction.Give => 100,
            GangAction.Terminate => -10_000,
            _ => 0
        };
    }

    private static int AttackValue(
        MatchState state,
        MatchPlayerState player,
        GameCommand command,
        ScenarioId objective)
    {
        var target = state.FindGang(new GangId(command.Target.Id))!;
        var aggression = DifficultyAttackBias(state.Setup.AiMentality);
        var denyHuman = state.Setup.AiMentality == AiDifficulty.HomicidalManiac
            && state.FindPlayer(target.Owner)!.Setup.Controller == PlayerController.Human
            ? 300
            : 0;
        return 700 + CombatObjectiveBonus(objective) + aggression + denyHuman - target.Force;
    }

    internal static int DifficultyAttackBias(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Goon => -300,
        AiDifficulty.Criminal => 0,
        AiDifficulty.CrimeLord => 250,
        AiDifficulty.HomicidalManiac => 900,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    private static int HealValue(MatchState state, MatchGangState gang)
    {
        var heal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        return OriginalAiFamilyOneRules.CanHeal(
                gang.Force, heal, OriginalAiFamilyOneRules.CommonHealForceLimit)
            ? 800 + ManualRules.MaximumForce - gang.Force
            : -1_000;
    }

    private static int ControlValue(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        ScenarioId objective)
    {
        var sector = state.Sectors[gang.SectorId];
        if (sector.Owner == player.Id) return 250 + ControlObjectiveBonus(objective, sector);

        // The original planner's selector 0x2c only proceeds when one gang's
        // Force + Control strictly exceeds the sector and defending strength.
        // Keep the recreation's objective weights, but do not rank a known
        // futile solo attempt above useful actions.
        return (CanSoloControl(state, player.Id, gang) ? 850 : -1_000)
            + ControlObjectiveBonus(objective, sector);
    }

    private static int CombatObjectiveBonus(ScenarioId scenario) => scenario switch
    {
        ScenarioId.KillEmAll or ScenarioId.Eliminate => 500,
        _ => 0
    };

    private static int ControlObjectiveBonus(
        ScenarioId scenario,
        MatchSectorState sector) => scenario switch
    {
        ScenarioId.Power or ScenarioId.Big40 or ScenarioId.Armageddon => 400,
        ScenarioId.Siege when sector.IsImportant => 600,
        ScenarioId.BigMan when sector.Id is 27 or 28 or 35 or 36 => 600,
        ScenarioId.Greed or ScenarioId.Dominance => sector.Income * 20,
        _ => 0
    };

    private static int InfluenceValue(MatchState state, int siteTarget, ScenarioId scenario)
    {
        var site = state.FindSite(siteTarget)!;
        var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
        return scenario == ScenarioId.Acceptance
            ? definition.Support * 30
            : definition.Cash * 20 + definition.Support * 5;
    }

    private static int EquipmentValue(MatchState state, GameCommand command)
    {
        var item = state.Definitions.Items[command.Target.Id];
        var stats = item.Stats;
        var utility = stats.Combat + stats.Defense + stats.Stealth + stats.Detect
            + stats.Chaos + stats.Control + stats.Heal + stats.Influence + stats.Research
            + stats.Strength + stats.Blade + stats.Range + stats.Fighting + stats.MartialArts;
        return 550 + utility * 10 - item.Cost;
    }

    internal static int DestinationValue(
        MatchState state,
        PlayerId player,
        int sectorId,
        ScenarioId scenario)
    {
        var sector = state.Sectors[sectorId];
        var value = sector.Owner == player ? 0 : 100;
        if (scenario == ScenarioId.Siege && sector.IsImportant) value += 300;
        if (scenario == ScenarioId.BigMan && sectorId is 27 or 28 or 35 or 36) value += 300;
        if (scenario == ScenarioId.Eliminate
            && OriginalCityGenerator.HeadquartersCandidates.Contains(sectorId)) value += 300;
        return value + sector.Income * 10;
    }

    private static bool IsObservableFallbackAttack(
        MatchState state,
        PlayerId player,
        GameCommand command) =>
        command.Action != GangAction.Attack
        || state.FindGang(new GangId(command.Target.Id)) is { } target
        && state.AiStrategy.IsHostile(player, target.Owner)
        && state.CanPlayerDetectGang(player, target.Id);
}
