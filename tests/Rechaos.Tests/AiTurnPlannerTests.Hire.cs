using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class AiTurnPlannerTests
{
    [Fact]
    public void HirePlannerSelectsOnlyAnAffordableValidOfferWithoutMutation()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        var before = MatchStateHasher.ComputeFingerprint(match);

        var choice = AiTurnPlanner.ChooseHire(match, new PlayerId(0));

        Assert.Equal(before, MatchStateHasher.ComputeFingerprint(match));
        if (choice is not null)
            Assert.True(HireRules.Validate(match, new PlayerId(0),
                choice.GangDefinitionId, choice.SectorId).IsValid);
    }

    [Fact]
    public void HiringPreparationUsesZeroBasedOriginalTurnAndAdvancesCurrentRole()
    {
        var match = CreateMatch(scenario: ScenarioId.BigMan);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        match.PrepareAiHiring(player);

        Assert.Equal(6, match.AiPlanning.PreviousHireRole(player));
        Assert.Equal(0, match.AiPlanning.CurrentHireRole(player));
    }

    [Fact]
    public void HiringPreparationLeavesRoleUnchangedWhenOriginalAttemptGateFails()
    {
        var match = CreateMatch(
            scenario: ScenarioId.Greed,
            ownsStartingSector: false);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        match.PrepareAiHiring(player);

        Assert.Equal(6, match.AiPlanning.CurrentHireRole(player));
    }

    [Fact]
    public void HirePlannerUsesOriginalRoleRankingInsteadOfRecreationScalar()
    {
        var data = BundledOriginalData.Load();
        var affordable = data.Gangs.Where(gang => gang.Id != 0 && gang.Force <= 100).ToArray();
        var offers = affordable
            .SelectMany(first => affordable.Where(second => second.Id != first.Id)
                .Select(second => (first, second)))
            .SelectMany(pair => affordable.Where(third =>
                    third.Id != pair.first.Id && third.Id != pair.second.Id)
                .Select(third => new[] { pair.first, pair.second, third }))
            .First(candidate =>
            {
                var original = OriginalAiHireRules.SelectOfferIndex(
                    candidate, ScenarioId.Power, requestedMode: 0, availableCash: 100);
                var recreationScalar = candidate
                    .Select((gang, index) => (index,
                        score: gang.Force * 20 + gang.TechLevel * 10
                            - gang.Upkeep * 15 - HireRules.InitialCost(gang)))
                    .OrderByDescending(entry => entry.score)
                    .ThenBy(entry => candidate[entry.index].Id)
                    .First().index;
                return original.HasValue && original.Value != recreationScalar;
            });
        var match = CreateMatch(data: data, cash: 100,
            hirePool: offers.Select(gang => gang.Id).ToArray());
        match.FinishUpkeep();
        match.PrepareAiPlanning(new PlayerId(0));
        var expectedIndex = OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 0,
            availableCash: match.Players[0].Cash)!.Value;

        var choice = match.PrepareAiHiring(new PlayerId(0)).Choice;

        Assert.NotNull(choice);
        Assert.Equal(offers[expectedIndex].Id, choice.GangDefinitionId);
    }

    [Fact]
    public void PreparedHireUsesOriginalFailedRankingRejection()
    {
        var data = BundledOriginalData.Load();
        var offers = data.Gangs
            .Where(gang => gang.Id != 0 && gang.Force > 0)
            .Take(MatchLimits.HireOffersPerPlayer)
            .ToArray();
        var match = CreateMatch(data: data, cash: -100,
            hirePool: offers.Select(gang => gang.Id).ToArray());
        match.FinishUpkeep();
        var selection = new OriginalAiHireRoleSelection(RankingMode: 3, Role: 4);
        var rejectedIndex = OriginalAiHireRules.SelectRejectedOfferIndex(
            offers, ScenarioId.Power);

        var preparation = AiTurnPlanner.PrepareHire(
            match, new PlayerId(0), selection);

        Assert.Null(preparation.Choice);
        Assert.Equal(offers[rejectedIndex].Id, preparation.RejectedGangDefinitionId);
        Assert.True(match.SnubHireOffer(
            new PlayerId(0), preparation.RejectedGangDefinitionId!.Value).Accepted);
    }

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
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 0));

        Assert.NotNull(preparation.Choice);
        Assert.Equal(10, preparation.Choice.SectorId);
        Assert.Equal(10 + AiPlanningState.SectorAnchorOffset,
            match.AiPlanning.SectorAnchor(playerId));
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
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 0));

        Assert.Equal(AiPlanningState.SectorAnchorOffset - 1,
            match.AiPlanning.SectorAnchor(playerId));
        Assert.Null(preparation.Choice);
        Assert.Null(preparation.RejectedGangDefinitionId);
    }

    [Fact]
    public void RoleFourPlacementUsesFirstVisibleHostileSectorRegardlessOfController()
    {
        var data = BundledOriginalData.Load();
        var observerDefinition = data.Gangs.MaxBy(gang => gang.Stats.Detect)!.Id;
        var targetDefinition = data.Gangs.MinBy(gang => gang.Stats.Stealth)!.Id;
        short[] offers = [1, 2, 3];
        var match = CreateMatch(
            data: data, definitionId: observerDefinition, rivalDefinitionId: targetDefinition,
            cash: 100, hirePool: offers, rivalController: PlayerController.Computer);
        var playerId = new PlayerId(0);
        match.Sectors[1].Owner = playerId;
        match.Players[0].AddGang(new MatchGangState(
            new GangId(30), playerId, observerDefinition, sectorId: 1, force: 5));
        match.AiStrategy.RecordCombat(new PlayerId(1), playerId, openingDamage: 1);
        match.FinishUpkeep();

        var preparation = AiTurnPlanner.PrepareHire(
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 4));

        Assert.NotNull(preparation.Choice);
        Assert.Equal(1, preparation.Choice.SectorId);
    }
}
