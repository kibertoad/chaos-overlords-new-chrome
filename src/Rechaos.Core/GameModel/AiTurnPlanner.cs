namespace Rechaos.Core.GameModel;

/// <summary>
/// Deterministic baseline planner for computer-controlled command turns.
/// The scoring is recreation-native and remains provisional until the original
/// difficulty branches and evaluation weights are recovered.
/// </summary>
public static class AiTurnPlanner
{
    public sealed record HireChoice(short GangDefinitionId, int SectorId);

    public static IReadOnlyList<GameCommand> Plan(MatchState state, PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Coordinator.Phase != TurnPhase.Command || state.Coordinator.ActivePlayer != playerId)
            throw new InvalidOperationException("AI planning requires that player's active Command phase.");
        var player = state.FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Setup.Controller != PlayerController.Computer)
            throw new ArgumentException("AI planning requires a computer-controlled player.", nameof(playerId));

        var cashBudget = Math.Max(0, player.Cash);
        var commands = new List<GameCommand>();
        foreach (var gang in player.Gangs.Where(gang => gang.IsActive).OrderBy(gang => gang.Id.Value))
        {
            var choice = CommandOptionCatalog.LegalCommands(state, playerId, gang.Id)
                .Where(command => IsObservable(state, playerId, command))
                .Where(command => EstimatedCost(state, command) <= cashBudget)
                .OrderByDescending(command => Score(state, player, gang, command))
                .ThenBy(command => command.Action)
                .ThenBy(command => command.Target.Kind)
                .ThenBy(command => command.Target.Id)
                .ThenBy(command => command.SecondaryTarget?.Id ?? -1)
                .FirstOrDefault();
            if (choice is null) continue;
            commands.Add(choice);
            cashBudget -= EstimatedCost(state, choice);
        }
        return commands;
    }

    public static HireChoice? ChooseHire(MatchState state, PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Coordinator.Phase is not (TurnPhase.Command or TurnPhase.Hire)
            || state.Coordinator.ActivePlayer != playerId)
            throw new InvalidOperationException("AI hiring requires that player's active planning turn.");
        var player = state.FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Setup.Controller != PlayerController.Computer)
            throw new ArgumentException("AI hiring requires a computer-controlled player.", nameof(playerId));

        return player.HirePool
            .SelectMany(definitionId => state.Sectors
                .Where(sector => sector.Owner == playerId)
                .Select(sector => new HireChoice(definitionId, sector.Id)))
            .Where(choice => HireRules.Validate(
                state, playerId, choice.GangDefinitionId, choice.SectorId).IsValid)
            .OrderByDescending(choice => HireValue(state, choice.GangDefinitionId))
            .ThenBy(choice => choice.GangDefinitionId)
            .ThenBy(choice => choice.SectorId)
            .FirstOrDefault();
    }

    private static int Score(
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
            GangAction.Heal => gang.Force < ManualRules.MaximumForce
                ? 800 + ManualRules.MaximumForce - gang.Force
                : 100,
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

    internal static bool CanSoloControl(MatchState state, PlayerId playerId, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var sector = state.Sectors[gang.SectorId];
        if (sector.Owner == playerId || sector.CrackdownActive) return false;

        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var attack = ManualRules.ControlStrength([(gang.Force, statistics.Control)]);
        var defense = sector.Income;
        if (sector.Owner is { } owner && owner != playerId)
        {
            defense = checked(defense + ManualRules.ControlStrength(state.FindPlayer(owner)!.Gangs
                .Where(candidate => candidate.IsActive && !candidate.Hidden && candidate.SectorId == sector.Id)
                .Select(candidate =>
                {
                    var candidateStatistics = EffectiveStatisticsCalculator.ForGang(state, candidate);
                    return (candidate.Force, candidateStatistics.Control);
                })));
            defense = checked(defense + sector.Sites
                .Where(site => site.InfluencedBy == owner)
                .Sum(site => state.Definitions.Sites.Single(
                    definition => definition.Id == site.DefinitionId).Support));
        }
        return attack > defense;
    }

    private static int CombatObjectiveBonus(ScenarioId scenario) => scenario switch
    {
        ScenarioId.KillEmAll or ScenarioId.Eliminate => 500,
        _ => 0
    };

    private static int ControlObjectiveBonus(ScenarioId scenario, MatchSectorState sector) => scenario switch
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

    private static int DestinationValue(
        MatchState state,
        PlayerId player,
        int sectorId,
        ScenarioId scenario)
    {
        var sector = state.Sectors[sectorId];
        var value = sector.Owner == player ? 0 : 100;
        if (scenario == ScenarioId.Siege && sector.IsImportant) value += 300;
        if (scenario == ScenarioId.BigMan && sectorId is 27 or 28 or 35 or 36) value += 300;
        return value + sector.Income * 10;
    }

    private static int EstimatedCost(MatchState state, GameCommand command) => command.Action switch
    {
        GangAction.Bribe => ManualRules.BribeCost,
        GangAction.Equip => SpecialSiteRules.EquipmentCost(
            state, state.FindGang(command.Gang)!, state.Definitions.Items[command.Target.Id]),
        _ => 0
    };

    private static bool IsObservable(MatchState state, PlayerId player, GameCommand command) =>
        command.Action != GangAction.Attack
        || state.CanPlayerDetectGang(player, new GangId(command.Target.Id));

    private static int HireValue(MatchState state, short definitionId)
    {
        var gang = state.Definitions.Gangs.Single(value => value.Id == definitionId);
        return gang.Force * 20 + gang.TechLevel * 10 - gang.Upkeep * 15 - HireRules.InitialCost(gang);
    }
}
