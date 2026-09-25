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
        IReadOnlyList<int> PlayerOrder)
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
            Enumerable.Range(0, MatchLimits.PlayerCount).ToArray());
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
            // RULE-AI-001: a player whose seat the computer took over plans every gang as a
            // raider.
            if (state.AiPlanning.RaiderMode(playerId))
                state.AiPlanning.SetRaiderFamily(playerId, entry.slot);
            // RULE-AI-002: the family is settled just before the gang's own handler, so an earlier
            // gang's handler sees a later new gang's record as it stood.
            AiPlanningPreparation.AssignFamilyIfNeeded(state, playerId, entry.slot);
            var family = state.AiPlanning.Family(playerId, entry.slot);
            switch (family)
            {
                case 0:
                    PrepareFamilyZeroCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 1:
                    PrepareFamilyOneCommand(
                        state, player, entry.gang, entry.slot, snapshot);
                    break;
                case 2:
                    PrepareFamilyTwoCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 3:
                    PrepareFamilyThreeCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 4:
                    PrepareFamilyFourCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 5:
                    PrepareFamilyFiveCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 6:
                    PrepareFamilySixCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 7:
                    PrepareFamilySevenCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 9:
                    PrepareFamilyNineCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 10:
                    PrepareFamilyTenCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 11:
                    PrepareFamilyElevenCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 12:
                    PrepareFamilyTwelveCommand(
                        state, playerId, entry.gang, entry.slot, snapshot);
                    break;
                case 13:
                case 14:
                    PrepareObjectiveFamilyCommand(
                        state, playerId, entry.gang, entry.slot, family, snapshot);
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
        PrepareFamilyOneAction(state, player, gang, gangSlot, snapshot);
        // FND-AI-057: after the switch, the Greed Terminate of FND-AI-042.
        TerminateForGreed(state, player.Id, gangSlot);
    }

    private static void PrepareFamilyOneAction(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        if (state.AiPlanning.PreviousAction(player.Id, gangSlot)
            is GangAction.Attack or GangAction.Hide or GangAction.Move)
        {
            PrepareFamilyOneAfterAttackHideOrMove(state, player, gang, gangSlot, snapshot);
            return;
        }
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

        SetRecoveredMoveAction(
            state, player.Id, gangSlot, SelectFamilyOneMove(state, player, gang, snapshot));
    }

    /// <summary>
    /// RULE-AI-020, FND-AI-057: after previous Attack, Hide or Move. At weight 10 one draw is
    /// made; a passing comparison attacks. Otherwise a gang in a sector the owner query gives to
    /// its player moves through mode 5, and any other gang heals, takes the sector, snitches or
    /// moves. Every action but the Attack clears the focus.
    /// </summary>
    private static void PrepareFamilyOneAfterAttackHideOrMove(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var visible = VisibleOpponentsInSector(state, player.Id, gang.SectorId);
        var visibleWeight = FirstVisibleOpponentWeight(state, player.Id, visible);
        if (visibleWeight == 10)
        {
            var draw = DrawHumanWeightedAttackTarget(
                state, player.Id, gang, visible, visibleWeight);
            if (draw.Accepted)
            {
                SetRecoveredFocusedAttack(state, player.Id, gang, gangSlot, draw.Selected);
                return;
            }
        }
        else if (OwnerQuery(state, gang.SectorId) == player.Id.Value)
        {
            SetRecoveredFocusedMoveAction(
                state, player.Id, gangSlot, SelectFamilyOneMove(state, player, gang, snapshot));
            return;
        }

        var action = OriginalAiFamilyOneRules.SelectAfterAttackHideOrMove(
            gang.Force,
            EffectiveStatisticsCalculator.ForGang(state, gang).Heal,
            CanSoloControl(state, player.Id, gang),
            OwnerIsHuman(state, gang.SectorId),
            IsHostileOwner(state, player.Id, gang.SectorId),
            state.Setup.AiMentality,
            player.Cash);
        if (action == GangAction.Move)
            SetRecoveredFocusedMoveAction(
                state, player.Id, gangSlot, SelectFamilyOneMove(state, player, gang, snapshot));
        else
            SetRecoveredActionClearingFocus(state, player.Id, gangSlot, action);
    }

    private static int SelectFamilyOneMove(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        FamilyPlanningSnapshot snapshot)
    {
        return OriginalAiSectorSelectionRules.Select(
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
    }
}
