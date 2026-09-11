using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class InstantResolutionTests
{
    [Fact]
    public void HideMarksGangHiddenAndRecordsTransition()
    {
        var match = CreateMatch(siteResistance: 100);
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Hide, CommandTarget.None)).Accepted);
        EnterExecution(match);

        match.FinishExecutionPhase();

        Assert.True(match.FindGang(new GangId(10))!.Hidden);
        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(0, resolution.PreviousValue);
        Assert.Equal(1, resolution.ResultValue);
        Assert.Empty(resolution.Rolls);
        Assert.Equal(1, match.Players[0].Statistics.TimesHidden);
    }

    [Fact]
    public void RepeatingHideCountsAgainAfterNextUpkeepRevealsGang()
    {
        var match = CreateMatch(siteResistance: 100);
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Hide,
            CommandTarget.None, Repeat: true)).Accepted);
        EnterExecution(match);

        match.FinishExecutionPhase();
        Assert.True(match.FindGang(new GangId(10))!.Hidden);
        Assert.Equal(1, match.Players[0].Statistics.TimesHidden);

        while (match.Coordinator.Phase == TurnPhase.Execution)
            match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        EnterCommand(match);
        EnterExecution(match);

        match.FinishExecutionPhase();

        Assert.True(match.FindGang(new GangId(10))!.Hidden);
        Assert.Equal(2, match.Players[0].Statistics.TimesHidden);
        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(0, resolution.PreviousValue);
        Assert.Equal(1, resolution.ResultValue);
    }

    [Fact]
    public void FriendlyInfluenceCommandsRollSeparatelyAndAccumulateProgress()
    {
        var match = CreateMatch(siteResistance: 100);
        QueueInfluencePair(match);

        match.FinishExecutionPhase();

        var gangs = new[] { match.FindGang(new GangId(10))!, match.FindGang(new GangId(11))! };
        Assert.Equal(2, match.LastPhaseResolutions.Count);
        var first = match.LastPhaseResolutions[0].Event!.Resolution!;
        var second = match.LastPhaseResolutions[1].Event!.Resolution!;
        var expectedDice = gangs.Select(gang => ManualRules.InfluenceDiceCount(
            [(gang.Force, EffectiveStatisticsCalculator.ForGang(match, gang).Influence)])).ToArray();
        Assert.Equal(expectedDice[0], first.Rolls.Count);
        Assert.Equal(expectedDice[1], second.Rolls.Count);
        Assert.Equal(100 - first.Successes, second.PreviousValue);
        Assert.Equal(100 - first.Successes - second.Successes, match.FindSite(0)!.Resistance);
        Assert.Equal(expectedDice.Sum() * 3, match.Random.ConsumptionCount);
        Assert.Equal(2, match.NotificationsFor(new PlayerId(0))
            .Count(value => value.Kind == GameNotificationKind.Influence));
    }

    [Fact]
    public void InfluenceCompletionClaimsSiteAndAppliesSupport()
    {
        var match = CreateMatch(siteResistance: 1, siteDefinitionId: 6);
        var siteDefinition = match.Definitions.Sites.Single(value => value.Id == match.FindSite(0)!.DefinitionId);
        QueueInfluencePair(match);
        var toleranceBefore = match.Sectors[0].Tolerance;

        match.FinishExecutionPhase();

        Assert.Equal(0, match.FindSite(0)!.Resistance);
        Assert.Equal(new PlayerId(0), match.FindSite(0)!.InfluencedBy);
        Assert.Equal(siteDefinition.Support, match.Players[0].Support);
        Assert.Equal(toleranceBefore + siteDefinition.Tolerance, match.Sectors[0].Tolerance);
    }

    [Fact]
    public void LaterInfluenceSkipsRollAfterEarlierRosterSlotCompletesSite()
    {
        var match = CreateMatch(siteResistance: 1, siteDefinitionId: 6);
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Influence, CommandTarget.Site(0))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Influence, CommandTarget.Site(0))).Accepted);
        EnterExecution(match);

        match.FinishExecutionPhase();

        Assert.Equal([new GangId(10), new GangId(11)],
            match.LastPhaseResolutions.Select(result => result.Command.Gang).ToArray());
        var first = match.LastPhaseResolutions[0].Event!.Resolution!;
        var second = match.LastPhaseResolutions[1].Event!.Resolution!;
        Assert.True(first.Successes > 0);
        Assert.NotEmpty(first.Rolls);
        Assert.Empty(second.Rolls);
        Assert.Equal(0, second.Successes);
        Assert.Equal(0, second.PreviousValue);
        Assert.Equal(0, second.ResultValue);
    }

    [Fact]
    public void AlreadyInfluencedSiteRejectsAnotherInfluenceCommand()
    {
        var match = CreateMatch(siteResistance: 0, influencedBy: new PlayerId(0));
        EnterCommand(match);

        var result = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Influence, CommandTarget.Site(0)));

        Assert.False(result.Accepted);
        Assert.Equal(CommandValidationCode.SiteAlreadyInfluenced, result.Validation.Code);
        Assert.False(match.Commands.TryGet(new GangId(10), out _));
    }

    [Fact]
    public void InfluenceRequiresPlayerToControlTargetSector()
    {
        var match = CreateMatch(siteResistance: 7, playerControlsSector: false);
        EnterCommand(match);

        var result = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Influence, CommandTarget.Site(0)));

        Assert.False(result.Accepted);
        Assert.Equal(CommandValidationCode.SectorNotControlled, result.Validation.Code);
    }

    [Fact]
    public void EquivalentInfluenceRunsProduceIdenticalEventsAndHash()
    {
        var first = CreateMatch(siteResistance: 100);
        var second = CreateMatch(siteResistance: 100);
        QueueInfluencePair(first);
        QueueInfluencePair(second);

        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        Assert.Equal(
            first.LastPhaseResolutions.Select(value => value.Event!.Resolution!.Rolls),
            second.LastPhaseResolutions.Select(value => value.Event!.Resolution!.Rolls));
        Assert.Equal(first.FindSite(0)!.Resistance, second.FindSite(0)!.Resistance);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    private static void QueueInfluencePair(MatchState match)
    {
        EnterCommand(match);
        var player = new PlayerId(0);
        Assert.True(match.Submit(new GameCommand(
            player, new GangId(10), GangAction.Influence, CommandTarget.Site(0))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            player, new GangId(11), GangAction.Influence, CommandTarget.Site(0))).Accepted);
        EnterExecution(match);
    }

    private static void EnterCommand(MatchState match) => match.FinishUpkeep();

    private static void EnterExecution(MatchState match)
    {
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
    }

    private static MatchState CreateMatch(
        int siteResistance,
        PlayerId? influencedBy = null,
        bool playerControlsSector = true,
        short siteDefinitionId = 0)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups);
        var influencer = data.Gangs.OrderByDescending(gang => gang.Stats.Influence).First();
        MatchPlayerState[] players =
        [
            new(setup.Players[0], 500,
            [
                new MatchGangState(new GangId(10), new PlayerId(0), influencer.Id, 0, 7),
                new MatchGangState(new GangId(11), new PlayerId(0), influencer.Id, 0, 6)
            ]),
            new(setup.Players[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), data.Gangs[0].Id, 0, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id == 0 ? siteDefinitionId : (short)0,
                    id == 0 ? siteResistance : 7, id == 0 ? influencedBy : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 && playerControlsSector ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
