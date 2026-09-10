namespace Rechaos.Core.GameModel;

internal static class AiPlanningPreparation
{
    public static void ApplyFamilyAssignments(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var gangs = state.FindPlayer(player)?.Gangs
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        var firstPlanningPass = state.AiPlanning.BeginPlanning(player);
        if (!firstPlanningPass)
            state.AiPlanning.RollActiveGangActions(player, gangs);
        state.AiPlanning.CleanupDuplicatePreviousActions(player, gangs);
        for (var gangSlot = 0; gangSlot < gangs.Count; gangSlot++)
        {
            if (!gangs[gangSlot].IsActive) continue;
            var selection = OriginalAiFamilyRules.Select(
                state.Setup.Scenario,
                state.AiPlanning.CurrentHireRole(player),
                state.AiPlanning.Family(player, gangSlot));
            state.AiPlanning.SetFamily(player, gangSlot, selection.Family);
        }
    }

    public static OriginalAiHireRoleSelection? SelectHireRole(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var playerState = state.FindPlayer(player)
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        var activeGangCount = playerState.Gangs.Count(gang => gang.IsActive);
        var ownedSectorCount = state.Sectors.Count(sector => sector.Owner == player);
        var hasNeutralSector = state.Sectors.Any(sector =>
            sector.Owner is null && !sector.CrackdownActive);
        var hireGangLimit = OriginalAiHireRoleRules.CalculateHireGangLimit(
            state.Setup.Scenario, activeGangCount, ownedSectorCount,
            playerState.Cash, hasNeutralSector);
        var elapsedTurns = state.Coordinator.Turn - 1;
        var turnsRemaining = Math.Max(0, ScenarioCatalog.Turns(state.Setup.Duration) - elapsedTurns);
        if (!OriginalAiHireRoleRules.ShouldAttemptHire(
                state.Setup.Scenario, activeGangCount, hireGangLimit,
                turnsRemaining, state.Setup.Duration))
            return null;

        var firstHostileSector = FirstVisibleHostileSector(state, player);
        var inputs = new OriginalAiHireAdjustmentInputs(
            turnsRemaining,
            playerState.Cash,
            state.Players.Any(other =>
                other.Status == PlayerStatus.Active && other.Cash > playerState.Cash),
            firstHostileSector.HasValue,
            firstHostileSector is { } sectorId
                && HasFamilySixCoverage(state, player, playerState, sectorId),
            CountFamilies(state, player, playerState, 5),
            CountFamilies(state, player, playerState, 7),
            state.AiPlanning.PreviousHireRole(player),
            CountFamilies(state, player, playerState, 2),
            CountFamilies(state, player, playerState, 3),
            CountFamilies(state, player, playerState, 6, 12),
            CountFamilies(state, player, playerState, 0, 4),
            state.Setup.Duration);
        return OriginalAiHireRoleRules.SelectAdjusted(
            state.Setup.Scenario, elapsedTurns, inputs);
    }

    private static int CountFamilies(
        MatchState state,
        PlayerId player,
        MatchPlayerState playerState,
        params int[] families) => playerState.Gangs
        .Select((gang, slot) => (gang, slot))
        .Count(entry => entry.gang.IsActive
            && families.Contains(state.AiPlanning.Family(player, entry.slot)));

    public static int PrepareHirePlacementMode(
        MatchState state,
        PlayerId player,
        int adjustedRole)
    {
        ArgumentNullException.ThrowIfNull(state);
        var playerState = state.FindPlayer(player)
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        var owners = state.Sectors.Select(sector => sector.Owner?.Value ?? -1)
            // Sector index 64 aliases player 0 gang slot 0's owner byte. In a
            // fresh game that byte is initialized to zero and remains stale at
            // zero if the Right Hands slot later becomes inactive.
            .Append(0)
            .ToArray();
        var availability = state.Sectors
            .Select(sector => checked((byte)sector.CrackdownTurnsRemaining))
            // The aliased retaliation byte is irrelevant while owner[64] != -1.
            .Append((byte)0)
            .ToArray();
        var anchorSector = state.AiPlanning.SectorAnchor(player)
            - AiPlanningState.SectorAnchorOffset;
        var activeGangCount = (int sectorId) => playerState.Gangs.Count(gang =>
            gang.IsActive && gang.SectorId == sectorId);
        var priorChaosCount = (int sectorId) => playerState.Gangs
            .Select((gang, slot) => (gang, slot))
            .Count(entry => entry.gang.IsActive
                && entry.gang.SectorId == sectorId
                && state.AiPlanning.PreviousAction(player, entry.slot) == GangAction.Chaos);
        var retainsAnchor = state.Setup.Scenario != ScenarioId.BigMan
            && anchorSector is >= 0 and < MatchLimits.SectorCount
            && owners[anchorSector] == player.Value
            && activeGangCount(anchorSector) < MatchLimits.FriendlyGangsPerSector
            && OriginalAiHireAnchorRules.CountAvailableNeutralNeighbors(
                player, anchorSector, owners, availability) > 0;
        if (!retainsAnchor)
        {
            anchorSector = OriginalAiHireAnchorRules.Select(
                player, state.Setup.Scenario, anchorSector, owners, availability,
                activeGangCount, priorChaosCount);
            state.AiPlanning.SetSectorAnchor(
                player, checked(anchorSector + AiPlanningState.SectorAnchorOffset));
        }

        var firstHostileSector = FirstVisibleHostileSector(state, player)
            ?? OriginalAiHirePlacementRules.InactiveGangSector;
        var gangSlotZeroSector = playerState.Gangs.Count > 0 && playerState.Gangs[0].IsActive
            ? playerState.Gangs[0].SectorId
            : OriginalAiHirePlacementRules.InactiveGangSector;
        return OriginalAiHirePlacementModeRules.Select(
            state.Setup.Scenario, adjustedRole,
            state.AiPlanning.SectorAnchor(player), firstHostileSector,
            gangSlotZeroSector);
    }

    private static int? FirstVisibleHostileSector(MatchState state, PlayerId observer) =>
        state.Sectors
            .Where(sector => state.Players.Any(owner =>
                owner.Id != observer
                && owner.Status == PlayerStatus.Active
                && state.AiStrategy.IsHostile(observer, owner.Id)
                && owner.Gangs.Any(gang => gang.IsActive
                    && gang.SectorId == sector.Id
                    && state.CanPlayerDetectGang(observer, gang.Id))))
            .Select(sector => (int?)sector.Id)
            .FirstOrDefault();

    private static bool HasFamilySixCoverage(
        MatchState state,
        PlayerId player,
        MatchPlayerState playerState,
        int sectorId) => playerState.Gangs
        .Select((gang, slot) => (gang, slot))
        .Any(entry => entry.gang.IsActive
            && state.AiPlanning.Family(player, entry.slot) == 6
            && (entry.gang.QueuedCommand?.Command is
                    { Action: GangAction.Move, Target.Kind: CommandTargetKind.Sector } move
                ? move.Target.Id
                : entry.gang.SectorId) == sectorId);
}
