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
        IReadOnlyList<int> SectorGangCounts)
    {
        public static FamilyPlanningSnapshot Capture(
            MatchState state,
            MatchPlayerState player) => new(
            state.Sectors.Select(sector => sector.Owner?.Value ?? -1).ToArray(),
            state.Sectors.Select(sector => sector.HasCrackdownTurns).ToArray(),
            Enumerable.Range(0, MatchLimits.SectorCount)
                .Select(sectorId => player.Gangs.Count(gang =>
                    gang.IsActive && gang.SectorId == sectorId))
                .ToArray());
    }

    /// <summary>
    /// RULE-AI-002: dispatches every active gang of the player to its family handler, on the
    /// sector weights its pass cached (RULE-AI-003).
    /// </summary>
    internal static void PrepareRecoveredFamilyCommands(
        MatchState state,
        CachedSectorWeights weights)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(weights);
        var playerId = weights.Player;
        var player = state.FindPlayer(playerId)
            ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        var snapshot = FamilyPlanningSnapshot.Capture(state, player);

        foreach (var entry in player.Gangs.Select((gang, slot) => (gang, slot)))
        {
            if (!entry.gang.IsActive) continue;
            // RULE-AI-001: a raider player's gangs are family 9 before each dispatch, so only a
            // flagged gang's first dispatch gives it the hire role's family, until the next pass.
            if (state.AiPlanning.RaiderMode(playerId))
                state.AiPlanning.SetFamily(playerId, entry.slot, AiPlanningState.RaiderFamily);
            // RULE-AI-002: the family is settled just before the gang's own handler, so an earlier
            // gang's handler sees a later new gang's record as it stood.
            AiPlanningPreparation.AssignFamilyIfNeeded(state, playerId, entry.gang, entry.slot);
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
        state.MarkAiPlanningPrepared(playerId);
    }

    private static void PrepareFamilyOneCommand(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        PrepareFamilyOneAction(state, player, gang, gangSlot, snapshot);
        // RULE-AI-020: every action the switch writes but the Attack clears the focus.
        if (state.AiPlanning.PlannedAction(player.Id, gangSlot)
            is not (GangAction.None or GangAction.Attack))
            state.AiPlanning.SetFocusValue(
                player.Id, gangSlot, AiPlanningState.InactiveFocusValue);
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
    /// moves. The Attack stores the gang's sector as the focus.
    /// </summary>
    private static void PrepareFamilyOneAfterAttackHideOrMove(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var visible = VisibleOpponentsInSector(state, player.Id, gang.SectorId);
        var visibleWeight = state.AiPlanning.SectorWeight(player.Id, gang.SectorId);
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
            SetRecoveredMoveAction(
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
            SetRecoveredMoveAction(
                state, player.Id, gangSlot, SelectFamilyOneMove(state, player, gang, snapshot));
        else
            state.AiPlanning.SetPlannedAction(player.Id, gangSlot, action);
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
            hasPriorChaos: sectorId =>
                CountPreviousChaosInSector(state, player.Id, sectorId) > 0,
            ownerIsHuman: sectorId => OwnerIsHuman(state, sectorId),
            multipliesByFive: sectorId => MultipliesSelectorScore(state, player.Id, sectorId),
            state.Random, planning: state.AiPlanning);
    }
}
