namespace Rechaos.Core.GameModel;

/// <summary>
/// Deterministic baseline planner for computer-controlled command turns.
/// The scoring is recreation-native and remains provisional until the original
/// difficulty branches and evaluation weights are recovered.
/// </summary>
public static class AiTurnPlanner
{
    public sealed record HireChoice(short GangDefinitionId, int SectorId);
    public sealed record HirePreparation(
        HireChoice? Choice,
        short? RejectedGangDefinitionId = null);

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
        foreach (var entry in player.Gangs
                     .Select((gang, slot) => (gang, slot))
                     .Where(entry => entry.gang.IsActive)
                     .OrderBy(entry => entry.gang.Id.Value))
        {
            var gang = entry.gang;
            var options = CommandOptionCatalog.LegalCommands(state, playerId, gang.Id)
                .Where(command => IsObservable(state, playerId, command))
                .Where(command => EstimatedCost(state, command) <= cashBudget)
                .ToArray();
            var choice = SelectRecoveredFamilyCommand(
                    state, player, gang, entry.slot, options)
                ?? options
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

    private static GameCommand? SelectRecoveredFamilyCommand(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<GameCommand> options)
    {
        if (state.AiPlanning.Family(player.Id, gangSlot) != 1) return null;

        var desiredAction = DesiredRecoveredFamilyAction(
            state, player, gang, gangSlot);
        if (desiredAction == GangAction.None) return null;

        var candidates = options.Where(command => command.Action == desiredAction);
        if (desiredAction == GangAction.Move
            && state.AiPlanning.PlannedAction(player.Id, gangSlot) == GangAction.Move)
        {
            var preparedSector = state.AiPlanning.PlannedTarget(player.Id, gangSlot).First;
            candidates = candidates.Where(command => command.Target.Id == preparedSector);
        }
        return candidates
            // Prepared live turns use the recovered mode-5 target. Retain the
            // recreation's deterministic target ranking only when this pure
            // query is invoked without its replay-recorded preparation boundary.
            .OrderByDescending(command => Score(state, player, gang, command))
            .ThenBy(command => command.Target.Id)
            .FirstOrDefault();
    }

    internal static void PrepareRecoveredFamilyCommands(MatchState state, PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var player = state.FindPlayer(playerId)
            ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        var sectorOwners = state.Sectors
            .Select(sector => sector.Owner?.Value ?? -1)
            .ToArray();
        var sectorDisabled = state.Sectors
            .Select(sector => sector.CrackdownActive)
            .ToArray();
        var sectorGangCounts = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(sectorId => player.Gangs.Count(gang =>
                gang.IsActive && gang.SectorId == sectorId))
            .ToArray();
        var playerOrder = Enumerable.Range(0, MatchLimits.PlayerCount).ToArray();

        foreach (var entry in player.Gangs.Select((gang, slot) => (gang, slot)))
        {
            if (!entry.gang.IsActive || state.AiPlanning.Family(playerId, entry.slot) != 1)
                continue;
            var desiredAction = DesiredRecoveredFamilyAction(
                state, player, entry.gang, entry.slot);
            if (desiredAction == GangAction.None) continue;
            if (desiredAction != GangAction.Move)
            {
                state.AiPlanning.SetPlannedAction(playerId, entry.slot, desiredAction);
                continue;
            }

            try
            {
                var target = OriginalAiSectorSelectionRules.Select(
                    mode: 5,
                    sourceSectorId: entry.gang.SectorId,
                    player: playerId,
                    family: 1,
                    sectorOwners,
                    sectorDisabled,
                    sectorGangCounts,
                    canSoloControl: sectorId =>
                        CanSoloControl(state, playerId, entry.gang, sectorId),
                    hasPriorChaos: sectorId => player.Gangs
                        .Select((gang, slot) => (gang, slot))
                        .Any(candidate => candidate.gang.IsActive
                            && candidate.gang.SectorId == sectorId
                            && state.AiPlanning.PreviousAction(playerId, candidate.slot)
                                == GangAction.Chaos),
                    isHostileOwner: owner =>
                        state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
                    isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                        .Setup.Controller == PlayerController.Human,
                    playerOrder,
                    state.Random);
                state.AiPlanning.SetPlannedAction(
                    playerId, entry.slot, GangAction.Move,
                    new AiActionTarget(checked((byte)target), 0));
            }
            catch (InvalidOperationException)
            {
                // The original zero-score post-filter edge is not yet bounded.
                // Leave the tuple empty so the provisional legal-command
                // fallback remains available instead of inventing a target.
            }
        }
    }

    private static GangAction DesiredRecoveredFamilyAction(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot)
    {
        if (state.AiPlanning.Family(player.Id, gangSlot) != 1)
            return GangAction.None;
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        return state.AiPlanning.PreviousAction(player.Id, gangSlot) switch
        {
            GangAction.None or GangAction.Chaos =>
                OriginalAiFamilyOneRules.SelectNoActionOrChaosContinuation(
                    gang.Force,
                    effectiveHeal,
                    state.Sectors[gang.SectorId].CrackdownActive,
                    state.AiPlanning.OlderAction(player.Id, gangSlot)),
            GangAction.Heal => OriginalAiFamilyOneRules.SelectHealContinuation(
                gang.Force,
                effectiveHeal,
                CanSoloControl(state, player.Id, gang)),
            _ => GangAction.None
        };
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

        if (AiPlanningPreparation.SelectHireRole(state, playerId) is not { } selection)
            return null;
        return PrepareHire(state, playerId, selection).Choice;
    }

    internal static HirePreparation PrepareHire(
        MatchState state,
        PlayerId playerId,
        OriginalAiHireRoleSelection selection)
    {
        var player = state.FindPlayer(playerId)
            ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.HirePool.Count != MatchLimits.HireOffersPerPlayer)
            return new HirePreparation(null);
        var offers = player.HirePool
            .Select(definitionId => state.Definitions.Gangs.Single(gang => gang.Id == definitionId))
            .ToArray();
        if (OriginalAiHireRules.SelectOfferIndex(
                offers, state.Setup.Scenario, selection.RankingMode, player.Cash) is not { } offerIndex)
        {
            var rejectedIndex = OriginalAiHireRules.SelectRejectedOfferIndex(
                offers, state.Setup.Scenario);
            return new HirePreparation(null, player.HirePool[rejectedIndex]);
        }
        var definitionId = player.HirePool[offerIndex];
        var placementMode = AiPlanningPreparation.PrepareHirePlacementMode(
            state, playerId, selection.Role);
        var sectorOwners = state.Sectors
            .Select(sector => sector.Owner?.Value ?? -1)
            .ToArray();
        var gangSectors = Enumerable.Repeat(
            OriginalAiHirePlacementRules.InactiveGangSector,
            OriginalAiHirePlacementRules.OriginalGangSlotCount).ToArray();
        for (var slot = 0; slot < player.Gangs.Count; slot++)
            if (player.Gangs[slot].IsActive) gangSectors[slot] = player.Gangs[slot].SectorId;
        var placement = OriginalAiHirePlacementRules.Select(
            playerId, placementMode, offerIndex, sectorOwners, gangSectors, state.Random);
        if (!placement.WritesDestination) return new HirePreparation(null);

        var choice = new HireChoice(definitionId, placement.TargetSectorId);
        return HireRules.Validate(
            state, playerId, choice.GangDefinitionId, choice.SectorId).IsValid
            ? new HirePreparation(choice)
            : new HirePreparation(null);
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

    internal static bool CanSoloControl(MatchState state, PlayerId playerId, MatchGangState gang)
        => CanSoloControl(state, playerId, gang, gang.SectorId);

    internal static bool CanSoloControl(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        var sector = state.Sectors[sectorId];
        if (sector.Owner == playerId || sector.CrackdownActive) return false;

        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var attack = ManualRules.ControlStrength([(gang.Force, statistics.Control)]);
        var defense = sector.Income;
        if (sector.Owner is { } owner && owner != playerId)
        {
            defense = checked(defense + ManualRules.ControlStrength(state.FindPlayer(owner)!.Gangs
                .Where(candidate => candidate.IsActive
                    && candidate.SectorId == sector.Id
                    && state.CanPlayerDetectGang(playerId, candidate.Id))
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

    private static int EstimatedCost(MatchState state, GameCommand command) => command.Action switch
    {
        GangAction.Bribe => ManualRules.BribeCost,
        GangAction.Equip => SpecialSiteRules.EquipmentCost(
            state, state.FindGang(command.Gang)!, state.Definitions.Items[command.Target.Id]),
        _ => 0
    };

    private static bool IsObservable(MatchState state, PlayerId player, GameCommand command) =>
        command.Action != GangAction.Attack
        || state.FindGang(new GangId(command.Target.Id)) is { } target
        && state.AiStrategy.IsHostile(player, target.Owner)
        && state.CanPlayerDetectGang(player, target.Id);

}
