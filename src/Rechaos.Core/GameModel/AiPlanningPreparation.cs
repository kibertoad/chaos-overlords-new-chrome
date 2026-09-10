namespace Rechaos.Core.GameModel;

internal static class AiPlanningPreparation
{
    public static void ApplyFamilyAssignments(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var gangs = state.FindPlayer(player)?.Gangs
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        state.AiPlanning.BeginPlanning(player);
        state.AiPlanning.RollActiveGangActions(player, gangs);
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

        var firstHostileSector = FirstVisibleHostileHumanSector(state, player);
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

    private static int? FirstVisibleHostileHumanSector(MatchState state, PlayerId observer) =>
        state.Sectors
            .Where(sector => state.Players.Any(owner =>
                owner.Id != observer
                && owner.Setup.Controller == PlayerController.Human
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
