namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilyTenCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        if (OriginalAiFamilyTenRules.SelectArmorUpgrade(
                state, player, gang) is { } armorId
            && OriginalAiFamilyTenRules.CanEquipArmor(
                state.AiPlanning.ArmorCooldown(playerId, gangSlot),
                state.Definitions.Items[armorId].Cost,
                player.Cash))
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Equip,
                new AiActionTarget(checked((byte)armorId), 0));
            state.AiPlanning.SetEquipmentCooldown(
                playerId, gangSlot, EquipmentSlot.Armor,
                OriginalAiFamilyTenRules.ArmorCooldown);
            return;
        }

        if (OriginalAiFamilyTenRules.ShouldEquipSmokeBombs(player, gang))
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Equip,
                new AiActionTarget(
                    checked((byte)OriginalAiFamilyTenRules.SmokeBombItemId), 0));
            return;
        }

        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        var hasVisibleOpponent = VisibleOpponentsInSector(
            state, playerId, gang.SectorId).Count > 0;
        if (OriginalAiFamilyTenRules.ShouldHeal(
                gang.Force, effectiveHeal, hasVisibleOpponent))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        var selected = SelectFamilyTenStealthSector(
            state, playerId, gang, snapshot);
        if (OriginalAiFamilyTenRules.ShouldMoveToStealthierSector(
                OriginalAiFamilyTenRules.CompletedStealthScore(state, gang.SectorId),
                OriginalAiFamilyTenRules.CompletedStealthScore(state, selected)))
        {
            var target = SelectFamilyTenStealthSector(
                state, playerId, gang, snapshot);
            SetRecoveredMoveAction(state, playerId, gangSlot, target);
            return;
        }

        var priorChaosCount = player.Gangs
            .Select((candidate, slot) => (candidate, slot))
            .Count(entry => entry.candidate.IsActive
                && entry.candidate.SectorId == gang.SectorId
                && state.AiPlanning.PreviousAction(playerId, entry.slot)
                    == GangAction.Chaos);
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot,
            OriginalAiFamilyTenRules.SelectStationaryAction(priorChaosCount));
    }

    private static int SelectFamilyTenStealthSector(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        FamilyPlanningSnapshot snapshot) =>
        OriginalAiSectorSelectionRules.Select(
            mode: 9,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 10,
            snapshot.SectorOwners,
            snapshot.SectorDisabled,
            snapshot.SectorGangCounts,
            canSoloControl: _ => true,
            hasPriorChaos: _ => false,
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            snapshot.PlayerOrder,
            state.Random,
            completedSiteScore: sectorId =>
                OriginalAiFamilyTenRules.CompletedStealthScore(state, sectorId));
}
