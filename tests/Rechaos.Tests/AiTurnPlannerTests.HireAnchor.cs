using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class AiTurnPlannerTests
{
    // RULE-AI-013: the planning pass replaces a full anchor, and the hire is placed there.
    [Fact]
    public void PlanningRefreshesAFullAnchorAndTheHireUsesIt()
    {
        short[] offers = [1, 2, 3];
        var match = CreateMatch(cash: 100, hirePool: offers);
        var playerId = new PlayerId(0);
        match.Sectors[10].Owner = playerId;
        for (var index = 0; index < MatchLimits.FriendlyGangsPerSector - 1; index++)
            match.Players[0].AddGang(new MatchGangState(
                new GangId(30 + index), playerId, 1, sectorId: 0, force: 5));
        match.FinishUpkeep();
        match.PrepareAiPlanning(playerId);

        var preparation = AiTurnPlanner.PrepareHire(
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 0),
            match.AiPlanning.SectorAnchor(playerId));

        Assert.NotNull(preparation.Choice);
        Assert.Equal(10, preparation.Choice.SectorId);
        Assert.Equal(10 + AiPlanningState.SectorAnchorOffset,
            match.AiPlanning.SectorAnchor(playerId));
    }

    // RULE-AI-013: the hire preview refreshes a full anchor against the current state without
    // storing it, so a Hire phase reached without a planning pass does not place at a stale one.
    [Fact]
    public void HirePreviewRefreshesAStaleAnchorWithoutStoringIt()
    {
        var match = CreateMatch();
        var playerId = new PlayerId(0);
        match.Sectors[10].Owner = playerId;
        for (var index = 0; index < MatchLimits.FriendlyGangsPerSector - 1; index++)
            match.Players[0].AddGang(new MatchGangState(
                new GangId(30 + index), playerId, 1, sectorId: 0, force: 5));
        var storedAnchor = match.AiPlanning.SectorAnchor(playerId);

        Assert.Equal(0 + AiPlanningState.SectorAnchorOffset, storedAnchor);
        Assert.Equal(10 + AiPlanningState.SectorAnchorOffset,
            AiPlanningPreparation.ResolveHireAnchor(match, playerId));
        Assert.Equal(storedAnchor, match.AiPlanning.SectorAnchor(playerId));
    }

    // RULE-AI-010, FND-AI-050: with more than six sectors and more hunters than a quarter of
    // them, the first hunter goes back to family 0, unless the gang in the slot numbered by the
    // sector count plans an Attack.
    [Theory]
    [InlineData(7, GangAction.None, 0)]
    [InlineData(7, GangAction.Attack, 6)]
    [InlineData(6, GangAction.None, 6)]
    [InlineData(8, GangAction.None, 6)]
    public void SurplusHunterRevertsToFamilyZero(
        int ownedSectors,
        GangAction slotPlannedAction,
        int expectedFamily)
    {
        var match = CreateMatch();
        var playerId = new PlayerId(0);
        for (var sectorId = 0; sectorId < ownedSectors; sectorId++)
            match.Sectors[sectorId].Owner = playerId;
        match.Players[0].AddGang(new MatchGangState(new GangId(31), playerId, 1, 0, 5));
        match.Players[0].AddGang(new MatchGangState(new GangId(32), playerId, 1, 0, 5));
        match.AiPlanning.BeginPlanning(playerId);
        match.AiPlanning.SetFamily(playerId, 1, 6);
        match.AiPlanning.SetFamily(playerId, 2, 12);
        match.AiPlanning.SetPlannedAction(playerId, ownedSectors, slotPlannedAction);
        match.FinishUpkeep();

        match.PrepareAiHiring(playerId);

        Assert.Equal(expectedFamily, match.AiPlanning.Family(playerId, 1));
        Assert.Equal(12, match.AiPlanning.Family(playerId, 2));
    }

    // RULE-AI-013, FND-AI-051: player 0 keeps a failed anchor while sector 0, 6, 7 or 8 is free
    // land, even after gaining a sector the scan would take, and a hire placed there is dropped.
    [Fact]
    public void PlayerZeroKeepsAFailedAnchorAndDropsItsHire()
    {
        short[] offers = [1, 2, 3];
        var match = CreateMatch(cash: 100, hirePool: offers);
        var playerId = new PlayerId(0);
        match.AiPlanning.SetSectorAnchor(playerId, AiPlanningState.SectorAnchorOffset - 1);
        match.FinishUpkeep();
        match.PrepareAiPlanning(playerId);

        var preparation = AiTurnPlanner.PrepareHire(
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 0),
            match.AiPlanning.SectorAnchor(playerId));

        Assert.Equal(AiPlanningState.SectorAnchorOffset - 1,
            match.AiPlanning.SectorAnchor(playerId));
        Assert.Null(preparation.Choice);
        Assert.Null(preparation.RejectedGangDefinitionId);
    }
}
