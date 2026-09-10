using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyThreeTurnPlannerTests
{
    [Fact]
    public void FirstContinuationInfluencesHighestCashSiteAndReplays()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10, ownsSource: true);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(3, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Influence, command.Action);
        Assert.Equal(CommandTarget.Site(2), command.Target);
        Assert.Equal(new AiActionTarget(2, 0),
            match.AiPlanning.PlannedTarget(player, 0));

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void FirstContinuationHealsOnlyBelowRecoveredForceBoundary()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 0, force: 7, ownsSource: true);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void FailedLocalContinuationUsesCashSiteModeEightRoute()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10,
            ownsSource: false, ownedCashSector: 18);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(9), command.Target);
    }

    [Fact]
    public void PriorInfluenceRetainsItsUnfinishedSiteBeforeComparingCash()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10, ownsSource: true);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 3);
        match.AiPlanning.SetPlannedAction(
            player, 0, GangAction.Influence, new AiActionTarget(0, 0));
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Influence,
            match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(0, 0),
            match.AiPlanning.PlannedTarget(player, 0));
    }

    private static void BeginFamilyThreeTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 2);
    }

    private static MatchState CreateMatch(
        OriginalData data,
        short definitionId,
        int force,
        bool ownsSource,
        int? ownedCashSector = null)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, definitionId, 0, force)],
                researchedItems: new HashSet<short>()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 14, 14)
            ], owner: ownsSource && id == 0 || id == ownedCashSector
                ? setups[0].Id
                : null,
                crackdownActive: !ownsSource && id == 0,
                income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 41, setups), players, sectors);
    }
}
