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
        state.AiPlanning.FlagInactiveSlots(player, gangs);
        state.AiPlanning.RefreshEquipmentCooldowns(player, gangs);
        // RULE-AI-003: a gang flagged for a family loses both auxiliary values.
        for (var gangSlot = 0; gangSlot < gangs.Count; gangSlot++)
        {
            if (!gangs[gangSlot].IsActive || !state.AiPlanning.NeedsFamily(player, gangSlot)) continue;
            state.AiPlanning.SetFocusValue(player, gangSlot, AiPlanningState.InactiveFocusValue);
            state.AiPlanning.SetCoverageSector(player, gangSlot, AiPlanningState.InactiveCoverageSector);
        }
        state.AiPlanning.CleanupDuplicatePreviousActions(player, gangs);
    }

    /// <summary>
    /// RULE-AI-002: a gang flagged for a family has its record wiped and takes the family its
    /// player's hire role stands for in this scenario; every other gang keeps its family. In Big
    /// Man the first turn forces hire role 1.
    /// </summary>
    public static void AssignFamilyIfNeeded(MatchState state, PlayerId player, int gangSlot)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.AiPlanning.NeedsFamily(player, gangSlot)) return;
        state.AiPlanning.ResetForNewFamily(player, gangSlot);
        if (state.Setup.Scenario == ScenarioId.BigMan && state.Coordinator.Turn == 1)
            state.AiPlanning.SetCurrentHireRole(player, 1);
        var role = state.AiPlanning.CurrentHireRole(player);
        if (OriginalAiFamilyRules.FamilyFor(state.Setup.Scenario, role) is not { } family) return;
        state.AiPlanning.SetFamily(player, gangSlot, family);
        if (role == 4)
            state.AiPlanning.SetCoverageSector(
                player, gangSlot, state.FindPlayer(player)!.Gangs[gangSlot].SectorId);
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

    /// <summary>
    /// RULE-AI-010, FND-AI-050: after the hire choice, whether or not the player may hire, a player
    /// owning more than six sectors whose hunters (families 6 and 12) outnumber a quarter of them
    /// sends the first hunter in slot order back to family 0. The Attack test that spares it reads
    /// the planning record of the slot numbered by the sector count, whichever hunter is visited.
    /// </summary>
    public static void RevertSurplusHunter(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var playerState = state.FindPlayer(player)
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        var ownedSectorCount = state.Sectors.Count(sector => sector.Owner == player);
        if (ownedSectorCount / 4 >= CountFamilies(state, player, playerState, 6, 12)
            || ownedSectorCount <= 6)
            return;
        if (state.AiPlanning.PlannedAction(player, ownedSectorCount) == GangAction.Attack) return;
        for (var gangSlot = 0; gangSlot < playerState.Gangs.Count; gangSlot++)
        {
            if (!playerState.Gangs[gangSlot].IsActive
                || state.AiPlanning.Family(player, gangSlot) is not (6 or 12))
                continue;
            state.AiPlanning.SetFamily(player, gangSlot, 0);
            return;
        }
    }

    private static int CountFamilies(
        MatchState state,
        PlayerId player,
        MatchPlayerState playerState,
        params int[] families) => playerState.Gangs
        .Select((gang, slot) => (gang, slot))
        .Count(entry => entry.gang.IsActive
            && families.Contains(state.AiPlanning.Family(player, entry.slot)));

    /// <summary>
    /// RULE-AI-013: after every gang has been dispatched and before the gang limit, the placement
    /// anchor is kept or replaced by the fixed scans.
    /// </summary>
    public static void RefreshHireAnchor(MatchState state, PlayerId player)
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
            // Clamped, not checked. The original held this counter in a byte; here it is an int
            // that Trigger adds three to five to and FinishCombat takes one off, so a sector that
            // is crackdown-triggered most turns crosses 255 in a long game. Everything downstream
            // reads it as "how long this sector stays shut", and 255 turns is already forever.
            .Select(sector => (byte)Math.Clamp(sector.CrackdownTurnsRemaining, 0, byte.MaxValue))
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
        if (OriginalAiHireAnchorRules.KeepsAnchor(
                player, state.Setup.Scenario, anchorSector, owners, availability, activeGangCount))
            return;
        anchorSector = OriginalAiHireAnchorRules.Select(
            player, state.Setup.Scenario, anchorSector, owners, availability,
            activeGangCount, priorChaosCount);
        state.AiPlanning.SetSectorAnchor(
            player, checked(anchorSector + AiPlanningState.SectorAnchorOffset));
    }

    public static int PrepareHirePlacementMode(
        MatchState state,
        PlayerId player,
        int adjustedRole)
    {
        ArgumentNullException.ThrowIfNull(state);
        var playerState = state.FindPlayer(player)
            ?? throw new ArgumentOutOfRangeException(nameof(player));
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
            && (state.AiPlanning.FocusValue(player, entry.slot)
                    != AiPlanningState.InactiveFocusValue
                ? entry.gang.SectorId
                : state.AiPlanning.CoverageSector(player, entry.slot)) == sectorId);
}
