namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyTenCommand(
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
            return true;
        }

        if (OriginalAiFamilyTenRules.ShouldEquipSmokeBombs(player, gang))
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Equip,
                new AiActionTarget(
                    checked((byte)OriginalAiFamilyTenRules.SmokeBombItemId), 0));
            return true;
        }

        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        var hasVisibleOpponent = VisibleOpponentsInSector(
            state, playerId, gang.SectorId).Count > 0;
        if (OriginalAiFamilyTenRules.ShouldHeal(
                gang.Force, effectiveHeal, hasVisibleOpponent))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return true;
        }

        var selected = SelectFamilyTenStealthSector(
            state, playerId, gang, sectorOwners, sectorDisabled,
            sectorGangCounts, playerOrder);
        if (OriginalAiFamilyTenRules.ShouldMoveToStealthierSector(
                OriginalAiFamilyTenRules.CompletedStealthScore(state, gang.SectorId),
                OriginalAiFamilyTenRules.CompletedStealthScore(state, selected)))
        {
            var target = SelectFamilyTenStealthSector(
                state, playerId, gang, sectorOwners, sectorDisabled,
                sectorGangCounts, playerOrder);
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Move,
                new AiActionTarget(checked((byte)target), 0));
            return true;
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
        return true;
    }

    private static int SelectFamilyTenStealthSector(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder) =>
        OriginalAiSectorSelectionRules.Select(
            mode: 9,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 10,
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
            completedSiteScore: sectorId =>
                OriginalAiFamilyTenRules.CompletedStealthScore(state, sectorId));
}
