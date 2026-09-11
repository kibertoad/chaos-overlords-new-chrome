using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ResearchResolutionTests
{
    [Fact]
    public void ResearchUsesForceAndEffectiveSkillAndRecordsProgress()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchableItem(data);
        var match = CreateMatch(researchProgress: new Dictionary<short, int> { [item] = 100 });
        QueueAndEnterExecution(match, item);

        match.FinishExecutionPhase();

        var gang = match.FindGang(new GangId(10))!;
        var resolution = match.Events[^1].Resolution!;
        var expectedDice = ManualRules.ResearchDiceCount(
            gang.Force,
            EffectiveStatisticsCalculator.ForGang(match, gang).Research);
        Assert.Equal(expectedDice, resolution.Rolls.Count);
        Assert.All(resolution.Rolls, roll => Assert.InRange(roll, 1, 6));
        Assert.Equal(
            OriginalResolutionRules.CountSuccesses(resolution.Rolls, 6),
            resolution.Successes);
        Assert.Equal(100, resolution.PreviousValue);
        Assert.Equal(
            ManualRules.ApplyResearchProgress(resolution.PreviousValue!.Value, resolution.Successes),
            resolution.ResultValue);
        Assert.Equal(resolution.ResultValue, match.Players[0].RemainingResearch(match.Definitions, item));
        Assert.Equal(resolution.ResultValue, match.Players[0].ResearchProgress[item]);
        Assert.DoesNotContain(item, match.Players[0].ResearchedItems);
        Assert.Equal(resolution.Rolls.Count * 3, match.Random.ConsumptionCount);
        var notification = Assert.Single(
            match.NotificationsFor(new PlayerId(0)),
            value => value.Kind == GameNotificationKind.Research);
        Assert.Equal(match.Events[^1].Sequence, notification.RelatedEventSequence);
    }

    [Fact]
    public void ResearchCompletionMovesItemToCompletedSet()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchableItem(data);
        var match = CreateMatch(researchProgress: new Dictionary<short, int> { [item] = 1 });
        QueueAndEnterExecution(match, item);

        match.FinishExecutionPhase();

        var resolution = match.Events[^1].Resolution!;
        Assert.True(resolution.Successes > 0);
        Assert.Equal(0, resolution.ResultValue);
        Assert.Contains(item, match.Players[0].ResearchedItems);
        Assert.DoesNotContain(item, match.Players[0].ResearchProgress.Keys);
    }

    [Fact]
    public void CompletedItemCannotBeQueuedForResearchAgain()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchableItem(data);
        var match = CreateMatch(researchedItems: new HashSet<short> { item });
        match.FinishUpkeep();
        var eventCount = match.Events.Count;

        var result = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Research, CommandTarget.Item(item)));

        Assert.False(result.Accepted);
        Assert.Equal(CommandValidationCode.ItemAlreadyResearched, result.Validation.Code);
        Assert.Equal(eventCount, match.Events.Count);
        Assert.False(match.Commands.TryGet(new GangId(10), out _));
    }

    [Fact]
    public void ResearchTechRequiresGangAndLocalSpecialSiteCapacity()
    {
        var data = BundledOriginalData.Load();
        var techEightItem = data.Items.First(item =>
            item.Type != 99 && item.ResearchDifficulty > 0 && item.TechLevel == 8);
        var techTenItem = data.Items.First(item =>
            item.Type != 99 && item.ResearchDifficulty > 0 && item.TechLevel == 10);
        var techTenGang = data.Gangs.First(gang => gang.TechLevel == 10);

        var unsupported = CreateMatch(gangDefinitionId: techTenGang.Id);
        unsupported.FinishUpkeep();
        Assert.Equal(CommandValidationCode.ResearchTechLevelUnavailable,
            unsupported.Submit(new GameCommand(new PlayerId(0), new GangId(10), GangAction.Research,
                CommandTarget.Item(techEightItem.Id))).Validation.Code);

        var science = CreateMatch(gangDefinitionId: techTenGang.Id, specialSiteDefinition: 8);
        science.FinishUpkeep();
        Assert.True(science.Submit(new GameCommand(new PlayerId(0), new GangId(10), GangAction.Research,
            CommandTarget.Item(techEightItem.Id))).Accepted);
        Assert.Equal(SpecialSiteRules.ScienceCenterTechLimit,
            SpecialSiteRules.ResearchTechLimit(science, science.FindGang(new GangId(10))!));

        var lab = CreateMatch(gangDefinitionId: techTenGang.Id, specialSiteDefinition: 4);
        lab.FinishUpkeep();
        Assert.True(lab.Submit(new GameCommand(new PlayerId(0), new GangId(10), GangAction.Research,
            CommandTarget.Item(techTenItem.Id))).Accepted);
        Assert.Equal(SpecialSiteRules.ResearchLabTechLimit,
            SpecialSiteRules.ResearchTechLimit(lab, lab.FindGang(new GangId(10))!));
    }

    [Fact]
    public void NewlyInfluencedScienceCenterDoesNotChangeSameInstantResearchPool()
    {
        var data = BundledOriginalData.Load();
        var item = data.Items.First(value =>
            value.Type != 99 && value.ResearchDifficulty > 1 && value.TechLevel <= 5).Id;
        var match = CreateMatch(
            researchProgress: new Dictionary<short, int> { [item] = 100 },
            specialSiteDefinition: 8,
            specialSiteInfluenced: false,
            includeSecondResearchGang: true);
        var researchGang = match.FindGang(new GangId(11))!;
        var statisticsBefore = EffectiveStatisticsCalculator.ForGang(match, researchGang);
        match.FinishUpkeep();

        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Influence,
            CommandTarget.Site(1))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), researchGang.Id, GangAction.Research,
            CommandTarget.Item(item))).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(0), match.FindSite(1)!.InfluencedBy);
        Assert.Equal(statisticsBefore.Research + 2,
            EffectiveStatisticsCalculator.ForGang(match, researchGang).Research);
        var research = match.LastPhaseResolutions.Single(result =>
            result.Command.Action == GangAction.Research).Event!.Resolution!;
        Assert.Equal(
            ManualRules.ResearchDiceCount(researchGang.Force, statisticsBefore.Research),
            research.Rolls.Count);
    }

    [Fact]
    public void EquivalentResearchRunsProduceIdenticalStateAndPhaseHash()
    {
        var first = CreateMatch();
        var second = CreateMatch();
        var item = ResearchableItem(first.Definitions);
        QueueAndEnterExecution(first, item);
        QueueAndEnterExecution(second, item);

        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        var firstResolution = first.Events[^1].Resolution!;
        var secondResolution = second.Events[^1].Resolution!;
        Assert.Equal(firstResolution.Rolls, secondResolution.Rolls);
        Assert.Equal(firstResolution.Successes, secondResolution.Successes);
        Assert.Equal(firstResolution.PreviousValue, secondResolution.PreviousValue);
        Assert.Equal(firstResolution.ResultValue, secondResolution.ResultValue);
        Assert.Equal(
            first.Players[0].ResearchProgress.OrderBy(value => value.Key),
            second.Players[0].ResearchProgress.OrderBy(value => value.Key));
        Assert.Equal(
            first.Players[0].ResearchedItems.Order(),
            second.Players[0].ResearchedItems.Order());
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    [Fact]
    public void GoonComputerResearchLosesOneFifthOfItsDicePool()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchableItem(data);
        var goon = CreateMatch(
            researchProgress: new Dictionary<short, int> { [item] = 100 },
            playerZeroController: PlayerController.Computer,
            difficulty: AiDifficulty.Goon);
        var standard = CreateMatch(
            researchProgress: new Dictionary<short, int> { [item] = 100 },
            playerZeroController: PlayerController.Computer,
            difficulty: AiDifficulty.Criminal);
        QueueAndEnterExecution(goon, item);
        QueueAndEnterExecution(standard, item);

        goon.FinishExecutionPhase();
        standard.FinishExecutionPhase();

        var standardPool = standard.Events[^1].Resolution!.Rolls.Count;
        Assert.Equal(standardPool - standardPool / 5, goon.Events[^1].Resolution!.Rolls.Count);
        Assert.Equal(OriginalResolutionRules.CountSuccesses(
            goon.Events[^1].Resolution!.Rolls, 6), goon.Events[^1].Resolution!.Successes);
    }

    [Fact]
    public void MatchRejectsOverlappingActiveAndCompletedResearch()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchableItem(data);

        Assert.Throws<ArgumentException>(() => CreateMatch(
            researchProgress: new Dictionary<short, int> { [item] = 1 },
            researchedItems: new HashSet<short> { item }));
    }

    private static void QueueAndEnterExecution(MatchState match, short item)
    {
        match.FinishUpkeep();
        var player = new PlayerId(0);
        Assert.True(match.Submit(new GameCommand(
            player, new GangId(10), GangAction.Research, CommandTarget.Item(item))).Accepted);
        match.FinishCommand(player);
        match.FinishCommand(new PlayerId(1));
    }

    private static short ResearchableItem(OriginalData data) => checked((short)Enumerable.Range(0, data.Items.Count)
        .First(index => data.Items[index].Type != 99 && data.Items[index].ResearchDifficulty > 1));

    private static MatchState CreateMatch(
        IReadOnlyDictionary<short, int>? researchProgress = null,
        IReadOnlySet<short>? researchedItems = null,
        short? gangDefinitionId = null,
        short? specialSiteDefinition = null,
        bool specialSiteInfluenced = true,
        bool includeSecondResearchGang = false,
        PlayerController playerZeroController = PlayerController.Human,
        AiDifficulty difficulty = AiDifficulty.Criminal)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "ONE", playerZeroController),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups, difficulty);
        var researchGang = gangDefinitionId is { } definitionId
            ? data.Gangs.Single(gang => gang.Id == definitionId)
            : data.Gangs.OrderByDescending(gang => gang.Stats.Research).First();
        var playerZeroGangs = new List<MatchGangState>
        {
            new(new GangId(10), new PlayerId(0), researchGang.Id, 0, 10)
        };
        if (includeSecondResearchGang)
            playerZeroGangs.Add(new MatchGangState(
                new GangId(11), new PlayerId(0), researchGang.Id, 0, 10));
        MatchPlayerState[] players =
        [
            new(setup.Players[0], 500, playerZeroGangs,
                researchProgress: researchProgress,
                researchedItems: researchedItems),
            new(setup.Players[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), data.Gangs[0].Id, 0, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, specialSiteDefinition ?? 1,
                    id == 0 && specialSiteDefinition.HasValue && !specialSiteInfluenced ? 0 : 5,
                    id == 0 && specialSiteDefinition.HasValue && specialSiteInfluenced
                        ? new PlayerId(0) : null),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 && specialSiteDefinition.HasValue ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
