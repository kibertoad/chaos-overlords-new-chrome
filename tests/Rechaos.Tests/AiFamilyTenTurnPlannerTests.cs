using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyTenTurnPlannerTests
{
    [Fact]
    public void ArmorUpgradeHasPriorityUsesFixedCooldownAndReplays()
    {
        var data = BundledOriginalData.Load();
        var researched = Enumerable.Range(24, 10).Select(value => (short)value);
        var match = CreateMatch(data, cash: 100, force: 10, researched: researched);
        var player = new PlayerId(0);
        BeginFamilyTenTurn(match, player);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(10, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(33), command.Target);
        Assert.Equal(OriginalAiFamilyTenRules.ArmorCooldown,
            match.AiPlanning.ArmorCooldown(player, 0));

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();
        Assert.Equal((short)33, match.Players[0].Gangs[0].ArmorItemId);

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void ResearchedSmokeBombsFillEmptyMiscellaneousSlot()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 100, force: 10, researched: [44]);
        var player = new PlayerId(0);
        BeginFamilyTenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(44), command.Target);
    }

    [Fact]
    public void HealRequiresNoVisibleLocalOpponent()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 9, researched: []);
        var player = new PlayerId(0);
        BeginFamilyTenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void ModeNineProbesThenRedrawsEqualMaximumDestination()
    {
        var data = BundledOriginalData.Load();
        var seed = Enumerable.Range(1, 1_000).First(candidate =>
        {
            var random = new DeterministicRandom(candidate);
            return random.NextInclusive(2) == 1
                && random.NextInclusive(2) == 2;
        });
        var match = CreateMatch(data, cash: 20, force: 10, researched: [],
            seed: seed, completedStealthSectors: new HashSet<int> { 1, 8 });
        match.Sectors[1].Owner = new PlayerId(0);
        match.Sectors[8].Owner = new PlayerId(0);
        var player = new PlayerId(0);
        BeginFamilyTenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(8), command.Target);
        Assert.Equal(6, match.Random.ConsumptionCount);
    }

    [Fact]
    public void NoStealthImprovementChoosesChaosWithoutPriorLocalChaos()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 10, researched: []);
        var player = new PlayerId(0);
        BeginFamilyTenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Chaos,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void PriorLocalChaosChangesStationaryActionToHide()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 10, researched: []);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 10);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Chaos);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Hide,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    private static void BeginFamilyTenTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 0);
    }

    private static MatchState CreateMatch(
        OriginalData data,
        int cash,
        int force,
        IEnumerable<short> researched,
        int seed = 41,
        IReadOnlySet<int>? completedStealthSectors = null)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], cash,
                [new MatchGangState(new GangId(10), setups[0].Id, 0, 0, force)],
                researchedItems: researched.ToHashSet()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
                completedStealthSectors?.Contains(id) == true
                    ?
                    [
                        new MatchSiteState(0, 9, 0),
                        new MatchSiteState(1, 11, 0),
                        new MatchSiteState(2, 14, 0)
                    ]
                    :
                    [
                        new MatchSiteState(0, 0, 7),
                        new MatchSiteState(1, 3, 13),
                        new MatchSiteState(2, 5, 15)
                    ],
                owner: id == 0 ? setups[0].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Siege, GameDuration.SixMonths, seed, setups), players, sectors);
    }
}
