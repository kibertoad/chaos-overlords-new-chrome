namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    /// <summary>
    /// Immutable board projections shared by every family during one planning
    /// pass. Keeping their capture here makes it explicit that later gang
    /// decisions see the same sector snapshot while authoritative planning
    /// records may change in gang order.
    /// </summary>
    private sealed record FamilyPlanningSnapshot(
        IReadOnlyList<int> SectorOwners,
        IReadOnlyList<bool> SectorDisabled,
        IReadOnlyList<int> SectorGangCounts,
        IReadOnlyList<int> PlayerOrder,
        IReadOnlyList<int> FamilySlots)
    {
        public static FamilyPlanningSnapshot Capture(
            MatchState state,
            MatchPlayerState player) => new(
            state.Sectors.Select(sector => sector.Owner?.Value ?? -1).ToArray(),
            state.Sectors.Select(sector => sector.CrackdownActive).ToArray(),
            Enumerable.Range(0, MatchLimits.SectorCount)
                .Select(sectorId => player.Gangs.Count(gang =>
                    gang.IsActive && gang.SectorId == sectorId))
                .ToArray(),
            Enumerable.Range(0, MatchLimits.PlayerCount).ToArray(),
            Enumerable.Range(0, AiPlanningState.GangSlotsPerPlayer)
                .Select(slot => state.AiPlanning.Family(player.Id, slot))
                .ToArray());
    }

    internal static void PrepareRecoveredFamilyCommands(
        MatchState state,
        PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var player = state.FindPlayer(playerId)
            ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        var snapshot = FamilyPlanningSnapshot.Capture(state, player);

        foreach (var entry in player.Gangs.Select((gang, slot) => (gang, slot)))
        {
            if (!entry.gang.IsActive) continue;
            var family = state.AiPlanning.Family(playerId, entry.slot);
            switch (family)
            {
                case 0:
                    PrepareFamilyZeroCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 1:
                    PrepareFamilyOneCommand(
                        state, player, entry.gang, entry.slot, snapshot);
                    break;
                case 2:
                    PrepareFamilyTwoCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 3:
                    PrepareFamilyThreeCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 4:
                    PrepareFamilyFourCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 5:
                    PrepareFamilyFiveCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 6:
                    PrepareFamilySixCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 7:
                    PrepareFamilySevenCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 9:
                    PrepareFamilyNineCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 10:
                    PrepareFamilyTenCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 11:
                    PrepareFamilyElevenCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder,
                        snapshot.FamilySlots);
                    break;
                case 12:
                    PrepareFamilyTwelveCommand(
                        state, playerId, entry.gang, entry.slot,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
                case 13:
                case 14:
                    PrepareObjectiveFamilyCommand(
                        state, playerId, entry.gang, entry.slot, family,
                        snapshot.SectorOwners, snapshot.SectorDisabled,
                        snapshot.SectorGangCounts, snapshot.PlayerOrder);
                    break;
            }
        }
    }

    private static void PrepareFamilyOneCommand(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var choice = DesiredRecoveredFamilyChoice(state, player, gang, gangSlot);
        if (choice.Action == GangAction.None) return;
        if (choice.Action == GangAction.Equip)
        {
            var itemId = checked((short)choice.TargetId!.Value);
            SetRecoveredReplacementEquipmentAction(
                state, player.Id, gangSlot, itemId, choice.EquipmentSlot!.Value);
            return;
        }
        if (choice.Action != GangAction.Move)
        {
            state.AiPlanning.SetPlannedAction(player.Id, gangSlot, choice.Action);
            return;
        }

        var target = OriginalAiSectorSelectionRules.Select(
            mode: 5,
            sourceSectorId: gang.SectorId,
            player: player.Id,
            family: 1,
            snapshot.SectorOwners,
            snapshot.SectorDisabled,
            snapshot.SectorGangCounts,
            canSoloControl: sectorId => CanSoloControl(state, player.Id, gang, sectorId),
            hasPriorChaos: sectorId => player.Gangs
                .Select((candidate, slot) => (candidate, slot))
                .Any(entry => entry.candidate.IsActive
                    && entry.candidate.SectorId == sectorId
                    && state.AiPlanning.PreviousAction(player.Id, entry.slot)
                        == GangAction.Chaos),
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(player.Id, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            snapshot.PlayerOrder,
            state.Random);
        SetRecoveredMoveAction(state, player.Id, gangSlot, target);
    }
}
