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
            match, new PlayerId(0), selection, match.AiPlanning.SectorAnchor(new PlayerId(0)));

        Assert.Null(preparation.Choice);
        Assert.Equal(offers[rejectedIndex].Id, preparation.RejectedGangDefinitionId);
        Assert.True(match.SnubHireOffer(
            new PlayerId(0), preparation.RejectedGangDefinitionId!.Value).Accepted);
    }

    // RULE-AI-010, RULE-AI-004: role 4 places at the first sector the pass weighted 10, which only
    // a visible gang of a hostile human earns. A hostile computer's gang weighs 1, so with no other
    // hostile human in sight first_hostile is 100, the placement writes no destination and nothing
    // is hired or snubbed.
    [Theory]
    [InlineData(PlayerController.Human, 10, 1, 1)]
    [InlineData(PlayerController.Computer, 1, OriginalAiHirePlacementRules.InactiveGangSector, null)]
    public void RoleFourPlacementUsesFirstSectorWeightedTen(
        PlayerController rivalController, int expectedWeight, int expectedFirstHostile,
        int? expectedHireSector)
    {
        var data = BundledOriginalData.Load();
        var observerDefinition = data.Gangs.MaxBy(gang => gang.Stats.Detect)!.Id;
        var targetDefinition = data.Gangs.MinBy(gang => gang.Stats.Stealth)!.Id;
        short[] offers = [1, 2, 3];
        var match = CreateMatch(
            data: data, definitionId: observerDefinition, rivalDefinitionId: targetDefinition,
            cash: 100, hirePool: offers, rivalController: rivalController);
        var playerId = new PlayerId(0);
        match.Sectors[1].Owner = playerId;
        match.Players[0].AddGang(new MatchGangState(
            new GangId(30), playerId, observerDefinition, sectorId: 1, force: 5));
        match.AiStrategy.RecordCombat(new PlayerId(1), playerId, openingDamage: 1);
        match.FinishUpkeep();
        AiTurnPlanner.CachedSectorWeights.Cache(match, playerId);
        var rivalSector = match.Players[1].Gangs[0].SectorId;
        var anchor = match.AiPlanning.SectorAnchor(playerId);

        Assert.True(match.AiStrategy.IsHostile(playerId, new PlayerId(1)));
        Assert.Equal(expectedWeight, match.AiPlanning.SectorWeight(playerId, rivalSector));
        Assert.Equal(expectedFirstHostile + AiPlanningState.SectorAnchorOffset,
            AiPlanningPreparation.PrepareHirePlacementMode(match, playerId, 4, anchor));
        var preparation = AiTurnPlanner.PrepareHire(
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 4), anchor);

        Assert.Equal(expectedHireSector, preparation.Choice?.SectorId);
        Assert.Null(preparation.RejectedGangDefinitionId);
    }
}
