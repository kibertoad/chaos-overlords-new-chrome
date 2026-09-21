namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    /// <summary>Family 5 runs the family 3 site-influence planner with its own site selector and move.</summary>
    private static void PrepareFamilyFiveCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot) =>
        PrepareSiteInfluenceFamilyCommand(
            state, playerId, gang, gangSlot, snapshot, FamilyFiveSiteInfluence);

    private static void PrepareFamilyFiveMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 7,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 5,
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
            unfinishedSiteScore: sectorId =>
                OriginalAiFamilyFiveRules.UnfinishedSupportScore(state, sectorId),
            hasPriorInfluence: sectorId => player.Gangs
                .Select((candidate, slot) => (candidate, slot))
                .Any(entry => entry.candidate.IsActive
                    && entry.candidate.SectorId == sectorId
                    && state.AiPlanning.PreviousAction(playerId, entry.slot)
                        == GangAction.Influence));
        SetRecoveredMoveAction(state, playerId, gangSlot, target);
    }
}
