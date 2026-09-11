namespace Rechaos.Core.GameModel;

/// <summary>
/// Deterministic planner for computer-controlled command turns. Recovered
/// family decisions are authoritative; the recreation-native fallback used
/// outside a prepared family trace is isolated in AiTurnPlanner.ProvisionalFallback.cs.
/// </summary>
public static partial class AiTurnPlanner
{
    private readonly record struct RecoveredFamilyChoice(
        GangAction Action,
        int? TargetId = null,
        EquipmentSlot? EquipmentSlot = null);

    private readonly record struct ObjectiveTarget(MatchGangState Gang, int Slot);
    private readonly record struct PreparedObjectiveChoice(
        GangAction Action,
        AiActionTarget Target);

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
                .Where(command => EstimatedCost(state, command) <= cashBudget)
                .ToArray();
            var choice = SelectRecoveredFamilyCommand(
                state, player, gang, entry.slot, options);
            if (choice is null
                && PreservesPreparedNoAction(state, playerId, entry.slot))
                continue;
            choice ??= SelectProvisionalFallbackCommand(
                state, player, gang, options);
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
        var family = state.AiPlanning.Family(player.Id, gangSlot);
        var preparedAction = state.AiPlanning.PlannedAction(player.Id, gangSlot);
        var choice = preparedAction != GangAction.None
            ? new RecoveredFamilyChoice(preparedAction,
                PreparedCommandTargetId(state, player.Id, gangSlot, preparedAction))
            : family == 1
                ? DesiredRecoveredFamilyChoice(state, player, gang, gangSlot)
                : default;
        if (choice.Action == GangAction.None) return null;

        var candidates = options.Where(command => command.Action == choice.Action);
        if (choice.TargetId is { } targetId)
            candidates = candidates.Where(command => command.Target.Id == targetId);
        candidates = candidates.Where(command => IsDetectableAttack(state, player.Id, command));
        return candidates
            // Prepared live turns use the recovered mode-5 target. Retain the
            // recreation's deterministic target ranking only when this pure
            // query is invoked without its replay-recorded preparation boundary.
            .OrderByDescending(command =>
                ScoreProvisionalCommand(state, player, gang, command))
            .ThenBy(command => command.Target.Id)
            .FirstOrDefault();
    }

    private static bool PreservesPreparedNoAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot) =>
        state.AiPlanning.HasPlanned(playerId)
        && state.AiPlanning.Family(playerId, gangSlot) is 0 or 3 or 4 or 5 or 12 or 13 or 14
        && state.AiPlanning.PlannedAction(playerId, gangSlot) == GangAction.None;

    private static void PrepareFamilyElevenCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder,
        IReadOnlyList<int> familySlots)
    {
        var player = state.FindPlayer(playerId)!;
        state.AiPlanning.SetFormationSector(playerId, gangSlot, gang.SectorId);
        if (OriginalAiEquipmentRules.SelectFamilyElevenUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Equip,
                new AiActionTarget(checked((byte)upgrade.ItemId), 0));
            if (upgrade.Slot is EquipmentSlot.Weapon or EquipmentSlot.Armor)
                state.AiPlanning.SetEquipmentCooldown(
                    playerId, gangSlot, upgrade.Slot,
                    OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                        state.Definitions.Items[upgrade.ItemId].Cost));
            return;
        }

        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        if (OriginalAiFamilyElevenRules.ShouldHeal(
                gang.Force, effectiveHeal,
                state.AiPlanning.PreviousAction(playerId, gangSlot)))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        if (state.Sectors[gang.SectorId].Owner == playerId)
        {
            PrepareFamilyElevenMove(
                state, playerId, gang, gangSlot, mode: 10,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
            return;
        }
        var target = VisibleOpponentsInSector(state, playerId, gang.SectorId)
            .FirstOrDefault();
        if (target.Gang is not null)
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Attack,
                new AiActionTarget(
                    checked((byte)target.Gang.Owner.Value),
                    checked((byte)target.Slot)));
            return;
        }

        var leaderSlot = OriginalAiFamilyElevenRules.FormationLeaderSlot(
            familySlots, gangSlot);
        var isLeader = leaderSlot == gangSlot;
        PrepareFamilyElevenMove(
            state, playerId, gang, gangSlot, isLeader ? 10 : 16,
            sectorOwners, sectorDisabled, sectorGangCounts, playerOrder,
            isLeader ? null : state.AiPlanning.FormationSector(playerId, leaderSlot));
    }

    private static void PrepareFamilyElevenMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        int mode,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder,
        int? formationSectorId = null)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode,
            gang.SectorId,
            playerId,
            family: 11,
            sectorOwners,
            sectorDisabled,
            sectorGangCounts,
            canSoloControl: _ => true,
            hasPriorChaos: _ => false,
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            playerOrder,
            state.Random,
            hasHumanPlayers: state.Setup.Players.Any(candidate =>
                candidate.Controller == PlayerController.Human),
            formationSectorId: formationSectorId);
        SetRecoveredMoveAction(state, playerId, gangSlot, target);
        if (mode == 10 && state.Sectors[gang.SectorId].Owner != playerId)
            state.AiPlanning.SetFormationSector(playerId, gangSlot, target);
    }

    private static void PrepareObjectiveFamilyCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        int family,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var plannedAction = state.AiPlanning.PlannedAction(playerId, gangSlot);
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        var objectiveSector = OriginalAiObjectiveFamilyRules.IsObjectiveSector(
            state.Setup.Scenario, gang.SectorId);
        var ownsObjective = objectiveSector
            && state.Sectors[gang.SectorId].Owner == playerId;
        var visibleOpponents = objectiveSector
            ? VisibleOpponentsInSector(state, playerId, gang.SectorId)
            : [];
        var hasVisibleOpponent = visibleOpponents.Count > 0;
        if (objectiveSector && state.Sectors[gang.SectorId].Owner != playerId)
        {
            var choice = SelectContestedObjectiveChoice(
                state, playerId, gang, effectiveHeal, visibleOpponents);
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, choice.Action, choice.Target);
            plannedAction = choice.Action;
        }
        else if (ownsObjective && hasVisibleOpponent)
        {
            var choice = SelectObjectiveAttackChoice(
                state, gang, effectiveHeal, visibleOpponents, visibleOpponents,
                OriginalAiObjectiveFamilyRules.OwnedObjectiveAttackAttempts);
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, choice.Action, choice.Target);
            plannedAction = choice.Action;
        }
        if (OriginalAiObjectiveFamilyRules.ShouldHealOwnedObjectiveWithoutVisibleOpponent(
                state.Setup.Scenario,
                gang.SectorId,
                ownsObjective,
                hasVisibleOpponent,
                gang.Force,
                effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            plannedAction = GangAction.Heal;
        }
        else if (ownsObjective && !hasVisibleOpponent)
        {
            plannedAction = PrepareOwnedObjectiveEquipmentOrInfluence(
                state, playerId, gang, gangSlot);
        }
        if (family == 14
            && OriginalAiObjectiveFamilyRules.ShouldFamilyFourteenTerminalHeal(
                state.Setup.Scenario,
                gang.SectorId,
                plannedAction,
                state.AiPlanning.PreviousAction(playerId, gangSlot),
                gang.Force,
                effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            state.AiPlanning.SetFamily(playerId, gangSlot, 13);
            return;
        }
        if (!OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
                state.Setup.Scenario, gang.SectorId, plannedAction)) return;

        var target = OriginalAiSectorSelectionRules.Select(
            OriginalAiObjectiveFamilyRules.SelectionMode(state.Setup.Scenario, family),
            gang.SectorId,
            playerId,
            family,
            sectorOwners,
            sectorDisabled,
            sectorGangCounts,
            sectorId => CanSoloControl(state, playerId, gang, sectorId),
            _ => false,
            owner => state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            owner => state.FindPlayer(new PlayerId(owner))?.Setup.Controller
                == PlayerController.Human,
            playerOrder,
            state.Random);
        SetRecoveredMoveAction(state, playerId, gangSlot, target);
    }

    private static int? PreparedCommandTargetId(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        GangAction action)
    {
        var target = state.AiPlanning.PlannedTarget(playerId, gangSlot);
        if (action is GangAction.Move or GangAction.Equip or GangAction.Research)
            return target.First;
        if (action == GangAction.Influence)
        {
            var player = state.FindPlayer(playerId);
            return player is not null && gangSlot < player.Gangs.Count
                ? player.Gangs[gangSlot].SectorId * MatchLimits.SitesPerSector
                    + target.First
                : -1;
        }
        if (action != GangAction.Attack) return null;
        var targetPlayer = state.FindPlayer(new PlayerId(target.First));
        return targetPlayer is not null && target.Second < targetPlayer.Gangs.Count
            ? targetPlayer.Gangs[target.Second].Id.Value
            : -1;
    }

    private static PreparedObjectiveChoice SelectContestedObjectiveChoice(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int effectiveHeal,
        IReadOnlyList<ObjectiveTarget> visible)
    {
        var visibleWeight = visible.Count == 0
            ? 0
            : VisibleOpponentWeight(state, playerId, visible[0].Gang.Owner);
        var turnsRemaining = ScenarioCatalog.Turns(state.Setup.Duration)
            - (state.Coordinator.Turn - 1);
        if (!OriginalAiObjectiveFamilyRules.ShouldScanContestedObjectiveTargets(
                turnsRemaining, visibleWeight))
            return new PreparedObjectiveChoice(GangAction.Control, AiActionTarget.None);

        var owner = state.Sectors[gang.SectorId].Owner;
        var targetPool = owner is { } sectorOwner
            && state.AiStrategy.IsHostile(playerId, sectorOwner)
            && visibleWeight == 10
                ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                        .Setup.Controller == PlayerController.Human)
                    .ToArray()
                : visible.Where(candidate => candidate.Gang.Owner == owner)
                    .ToArray();
        return SelectObjectiveAttackChoice(
            state, gang, effectiveHeal, visible, targetPool,
            OriginalAiObjectiveFamilyRules.ContestedAttackAttempts);
    }

    private static PreparedObjectiveChoice SelectObjectiveAttackChoice(
        MatchState state,
        MatchGangState gang,
        int effectiveHeal,
        IReadOnlyList<ObjectiveTarget> visible,
        IReadOnlyList<ObjectiveTarget> targetPool,
        int attempts)
    {
        ObjectiveTarget? selected = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var ordinal = state.Random.NextInclusive(Math.Max(1, targetPool.Count));
            selected = ordinal <= targetPool.Count ? targetPool[ordinal - 1] : null;
            if (selected is null) break;
            if (ordinal > visible.Count) continue;
            var retryTarget = visible[ordinal - 1].Gang;
            var attackerStats = EffectiveStatisticsCalculator.ForGang(state, gang);
            var targetStats = EffectiveStatisticsCalculator.ForGang(state, retryTarget);
            if (OriginalAiObjectiveFamilyRules.AcceptContestedAttackRetry(
                    gang.Force, attackerStats.Combat, attackerStats.Defense,
                    retryTarget.Force, targetStats.Combat, targetStats.Defense)) break;
        }

        var action = OriginalAiObjectiveFamilyRules.SelectContestedObjectiveResult(
            selected.HasValue, gang.Force, effectiveHeal);
        return action == GangAction.Attack
            ? new PreparedObjectiveChoice(action, new AiActionTarget(
                checked((byte)selected!.Value.Gang.Owner.Value),
                checked((byte)selected.Value.Slot)))
            : new PreparedObjectiveChoice(action, AiActionTarget.None);
    }

    private static GangAction PrepareOwnedObjectiveEquipmentOrInfluence(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot)
    {
        var player = state.FindPlayer(playerId)!;
        if (OriginalAiEquipmentRules.SelectObjectiveFamilyUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Equip,
                new AiActionTarget(checked((byte)upgrade.ItemId), 0));
            if (upgrade.Slot is EquipmentSlot.Weapon or EquipmentSlot.Armor)
                state.AiPlanning.SetEquipmentCooldown(
                    playerId, gangSlot, upgrade.Slot,
                    OriginalAiObjectiveFamilyRules.ObjectiveEquipmentCooldown);
            return GangAction.Equip;
        }

        var siteSlot = OriginalAiObjectiveFamilyRules
            .SelectHighestSupportUnfinishedSite(state, gang.SectorId);
        if (siteSlot is not { } slot)
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.None);
            return GangAction.None;
        }

        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Influence,
            new AiActionTarget(checked((byte)slot), 0));
        return GangAction.Influence;
    }

    private static int VisibleOpponentWeight(
        MatchState state,
        PlayerId observer,
        PlayerId opponent) =>
        state.FindPlayer(opponent)?.Setup.Controller == PlayerController.Human
        && state.AiStrategy.IsHostile(observer, opponent)
            ? 10
            : 1;

    private static IReadOnlyList<ObjectiveTarget> VisibleOpponentsInSector(
        MatchState state,
        PlayerId observer,
        int sectorId)
    {
        var result = new List<ObjectiveTarget>();
        for (var playerIndex = 0; playerIndex < MatchLimits.PlayerCount; playerIndex++)
        {
            var playerId = new PlayerId(playerIndex);
            if (playerId == observer || state.FindPlayer(playerId) is not { } player) continue;
            foreach (var candidate in player.Gangs.Select((gang, slot) => (gang, slot)))
                if (candidate.gang.IsActive
                    && candidate.gang.SectorId == sectorId
                    && state.CanPlayerDetectGang(observer, candidate.gang.Id))
                    result.Add(new ObjectiveTarget(candidate.gang, candidate.slot));
        }
        return result;
    }

    private static RecoveredFamilyChoice DesiredRecoveredFamilyChoice(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot)
    {
        if (state.AiPlanning.Family(player.Id, gangSlot) != 1)
            return new RecoveredFamilyChoice(GangAction.None);
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        return state.AiPlanning.PreviousAction(player.Id, gangSlot) switch
        {
            GangAction.None or GangAction.Chaos =>
                new RecoveredFamilyChoice(OriginalAiFamilyOneRules.SelectNoActionOrChaosContinuation(
                    gang.Force,
                    effectiveHeal,
                    state.Sectors[gang.SectorId].CrackdownActive,
                    state.AiPlanning.OlderAction(player.Id, gangSlot))),
            GangAction.Heal => new RecoveredFamilyChoice(OriginalAiFamilyOneRules.SelectHealContinuation(
                gang.Force,
                effectiveHeal,
                CanSoloControl(state, player.Id, gang))),
            GangAction.Control or GangAction.Equip or GangAction.Snitch =>
                SelectPostEquipmentChoice(state, player, gang, gangSlot),
            _ => new RecoveredFamilyChoice(GangAction.None)
        };
    }

    private static RecoveredFamilyChoice SelectPostEquipmentChoice(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot)
    {
        if (OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
            return new RecoveredFamilyChoice(
                GangAction.Equip, upgrade.ItemId, upgrade.Slot);

        var sector = state.Sectors[gang.SectorId];
        return new RecoveredFamilyChoice(
            OriginalAiFamilyOneRules.SelectPostEquipmentContinuation(
                player.Id,
                sector.Owner?.Value ?? -1,
                sector.Owner is { } owner
                    && state.FindPlayer(owner)?.Setup.Controller == PlayerController.Human,
                player.Cash,
                state.Setup.AiMentality,
                sector.Tolerance));
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

    private static int EstimatedCost(MatchState state, GameCommand command) => command.Action switch
    {
        GangAction.Bribe => ManualRules.OriginalBribeCost,
        GangAction.Equip => SpecialSiteRules.EquipmentCost(
            state, state.FindGang(command.Gang)!, state.Definitions.Items[command.Target.Id]),
        _ => 0
    };

    private static bool IsDetectableAttack(
        MatchState state,
        PlayerId player,
        GameCommand command) =>
        command.Action != GangAction.Attack
        || state.FindGang(new GangId(command.Target.Id)) is { } target
        && state.CanPlayerDetectGang(player, target.Id);

}
