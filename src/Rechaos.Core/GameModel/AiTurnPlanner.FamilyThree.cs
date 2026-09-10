namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyThreeCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var player = state.FindPlayer(playerId)!;
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;

        if (OriginalAiFamilyThreeRules.UsesCashSiteContinuation(previousAction))
        {
            if (OriginalAiFamilyThreeRules.ShouldHeal(gang.Force, effectiveHeal))
            {
                state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
                return true;
            }

            if (state.Sectors[gang.SectorId].Owner == playerId
                && OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(
                    state, gang.SectorId) is { } siteSlot)
            {
                SetFamilyThreeInfluence(state, playerId, gangSlot, siteSlot);
                return true;
            }

            if (CanSoloControl(state, playerId, gang))
            {
                state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Control);
                return true;
            }

            PrepareFamilyThreeMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
            return true;
        }

        if (previousAction == GangAction.Influence)
        {
            if (OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                    state, player, gang, gangSlot) is { } upgrade)
            {
                state.AiPlanning.SetPlannedAction(
                    playerId, gangSlot, GangAction.Equip,
                    new AiActionTarget(checked((byte)upgrade.ItemId), 0));
                state.AiPlanning.SetEquipmentCooldown(
                    playerId, gangSlot, upgrade.Slot,
                    OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                        state.Definitions.Items[upgrade.ItemId].Cost));
                return true;
            }

            if (OriginalAiFamilyThreeRules.ShouldHeal(gang.Force, effectiveHeal))
            {
                state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
                return true;
            }

            if (state.Sectors[gang.SectorId].Owner == playerId)
            {
                var previousSiteSlot = state.AiPlanning
                    .PreviousTarget(playerId, gangSlot).First;
                if (previousSiteSlot < MatchLimits.SitesPerSector
                    && state.Sectors[gang.SectorId].Sites[previousSiteSlot].Resistance > 0)
                {
                    SetFamilyThreeInfluence(
                        state, playerId, gangSlot, previousSiteSlot);
                    return true;
                }

                if (OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(
                        state, gang.SectorId) is { } siteSlot)
                {
                    SetFamilyThreeInfluence(state, playerId, gangSlot, siteSlot);
                    return true;
                }
            }

            PrepareFamilyThreeMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
            return true;
        }

        if (previousAction != GangAction.Snitch) return false;
        PrepareFamilyThreeMove(
            state, playerId, gang, gangSlot,
            sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
        return true;
    }

    private static void SetFamilyThreeInfluence(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        int siteSlot) =>
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Influence,
            new AiActionTarget(checked((byte)siteSlot), 0));

    private static void PrepareFamilyThreeMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 8,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 3,
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
            unfinishedSiteScore: sectorId =>
                OriginalAiFamilyThreeRules.UnfinishedCashScore(state, sectorId));
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Move,
            new AiActionTarget(checked((byte)target), 0));
    }
}
