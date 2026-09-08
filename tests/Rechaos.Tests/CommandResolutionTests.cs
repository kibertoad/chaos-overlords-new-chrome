using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class CommandResolutionTests
{
    [Fact]
    public void BribeSpendsThreeAndAddsFiveTolerance()
    {
        var match = CreateMatch(cash: 10);
        QueueAndEnterExecution(match, GangAction.Bribe);

        match.FinishExecutionPhase();

        Assert.Equal(7, match.Players[0].Cash);
        Assert.Equal(3, match.Players[0].Statistics.CashSpent);
        Assert.Equal(5, match.Sectors[0].Tolerance);
        Assert.Equal(CommandResolutionCode.Resolved, Assert.Single(match.LastPhaseResolutions).Code);
        Assert.Equal(GameEventKind.CommandResolved, match.Events[^1].Kind);
        Assert.Equal(CommandResolutionCode.Resolved, match.Events[^1].ResolutionCode);
        Assert.Equal(match.Events[^1].Sequence, Assert.Single(match.NotificationsFor(new PlayerId(0))).RelatedEventSequence);
        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);
    }

    [Fact]
    public void BribeFailureIsOrderedAndDoesNotMutateCashOrTolerance()
    {
        var match = CreateMatch(cash: 2);
        QueueAndEnterExecution(match, GangAction.Bribe);

        match.FinishExecutionPhase();

        Assert.Equal(2, match.Players[0].Cash);
        Assert.Equal(0, match.Sectors[0].Tolerance);
        Assert.Equal(CommandResolutionCode.InsufficientCash, Assert.Single(match.LastPhaseResolutions).Code);
        Assert.Equal(GameEventKind.CommandFailed, match.Events[^1].Kind);
        Assert.Equal(CommandResolutionCode.InsufficientCash, match.Events[^1].ResolutionCode);
    }

    [Fact]
    public void SnitchFloorsToleranceAtZeroWithoutCost()
    {
        var match = CreateMatch(cash: 10);
        QueueAndEnterExecution(match, GangAction.Snitch);

        match.FinishExecutionPhase();

        Assert.Equal(10, match.Players[0].Cash);
        Assert.Equal(0, match.Sectors[0].Tolerance);
        Assert.Equal(CommandResolutionCode.Resolved, Assert.Single(match.LastPhaseResolutions).Code);
    }

    [Fact]
    public void UnsupportedActionBlocksSubphaseBeforeAnyResolution()
    {
        var match = CreateMatch(cash: 10);
        match.FinishUpkeep();
        var player = new PlayerId(0);
        Assert.True(match.Submit(new GameCommand(player, new GangId(10), GangAction.Bribe, CommandTarget.None)).Accepted);
        Assert.True(match.Submit(new GameCommand(player, new GangId(11), GangAction.Heal, CommandTarget.None)).Accepted);
        match.FinishCommand(player);
        match.FinishCommand(new PlayerId(1));

        Assert.Throws<NotSupportedException>(() => match.FinishExecutionPhase());

        Assert.Equal(TurnPhase.Execution, match.Coordinator.Phase);
        Assert.Equal(ExecutionPhase.Instant, match.Coordinator.ExecutionPhase);
        Assert.Equal(10, match.Players[0].Cash);
        Assert.Equal(0, match.Sectors[0].Tolerance);
        Assert.Empty(match.LastPhaseResolutions);
        Assert.DoesNotContain(match.Events, gameEvent => gameEvent.Kind is GameEventKind.CommandResolved or GameEventKind.CommandFailed);
    }

    private static void QueueAndEnterExecution(MatchState match, GangAction action)
    {
        match.FinishUpkeep();
        var player = new PlayerId(0);
        Assert.True(match.Submit(new GameCommand(player, new GangId(10), action, CommandTarget.None)).Accepted);
        match.FinishCommand(player);
        match.FinishCommand(new PlayerId(1));
    }

    private static MatchState CreateMatch(int cash)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups);
        MatchPlayerState[] players =
        [
            new(setup.Players[0], cash,
            [
                new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 5),
                new MatchGangState(new GangId(11), new PlayerId(0), 2, 0, 5)
            ]),
            new(setup.Players[1], 500, [new MatchGangState(new GangId(20), new PlayerId(1), 3, 0, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
