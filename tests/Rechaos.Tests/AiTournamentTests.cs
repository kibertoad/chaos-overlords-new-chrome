using System.Diagnostics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiTournamentTests
{
    private readonly ITestOutputHelper _output;

    public AiTournamentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly ScenarioId[] ObjectiveScenarios =
    [
        ScenarioId.KillEmAll,
        ScenarioId.Big40,
        ScenarioId.Eliminate,
        ScenarioId.Siege,
        ScenarioId.BigMan,
        ScenarioId.Armageddon
    ];

    public static TheoryData<ScenarioId, int> ObjectiveStressCases
    {
        get
        {
            var data = new TheoryData<ScenarioId, int>();
            foreach (var seed in new[] { 1977, 4093, 12289, 32771, 65521, 104729 })
            foreach (var scenario in ObjectiveScenarios)
                data.Add(scenario, seed);
            return data;
        }
    }

    [Theory]
    [InlineData(ScenarioId.Greed)]
    [InlineData(ScenarioId.Power)]
    [InlineData(ScenarioId.Acceptance)]
    [InlineData(ScenarioId.Dominance)]
    public void SixComputerTimedMatchCompletesDeterministicallyAndReplays(
        ScenarioId scenario)
    {
        var first = DriveMatch(scenario, 1984);
        var second = DriveMatch(scenario, 1984);

        Assert.NotNull(first.State.Outcome);
        Assert.Equal(MatchEndReason.TimeLimit, first.State.Outcome!.Reason);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(first.State),
            MatchStateHasher.ComputeSha256(second.State));
        AssertReplayMatches(first);
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll)]
    [InlineData(ScenarioId.Big40)]
    [InlineData(ScenarioId.Eliminate)]
    [InlineData(ScenarioId.Siege)]
    [InlineData(ScenarioId.BigMan)]
    [InlineData(ScenarioId.Armageddon)]
    public void SixComputerObjectiveMatchRunsDeterministicReplayWindow(
        ScenarioId scenario)
    {
        var first = DriveMatch(scenario, 2112, throughTurn: 20);
        var second = DriveMatch(scenario, 2112, throughTurn: 20);

        Assert.True(first.State.Outcome is not null || first.State.Coordinator.Turn > 20);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(first.State),
            MatchStateHasher.ComputeSha256(second.State));
        AssertReplayMatches(first);
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll)]
    [InlineData(ScenarioId.Big40)]
    [InlineData(ScenarioId.Eliminate)]
    [InlineData(ScenarioId.Siege)]
    [InlineData(ScenarioId.BigMan)]
    [InlineData(ScenarioId.Armageddon)]
    public void SixComputerObjectiveCampaignMakesProgressWithLiveHiring(
        ScenarioId scenario)
    {
        const int horizon = 40;
        var campaign = DriveMatch(
            scenario, 7717, GameDuration.FourYears, throughTurn: horizon);

        Assert.True(campaign.State.Outcome is not null
            || campaign.State.Coordinator.Turn > horizon);
        Assert.True(campaign.State.Sectors.Count(sector => sector.Owner is not null)
            > MatchLimits.PlayerCount);
        Assert.Contains(campaign.State.Events,
            gameEvent => gameEvent.Kind == GameEventKind.HireResolved);
        AssertObjectiveProgress(campaign.State, scenario, 7717);
        AssertReplayMatches(campaign);
    }

    [Fact]
    public void SixComputerBigManCampaignCompletesNaturallyAndReplays()
    {
        var campaign = DriveMatch(
            ScenarioId.BigMan, 7717, GameDuration.FourYears, throughTurn: 60);

        Assert.NotNull(campaign.State.Outcome);
        Assert.Equal(MatchEndReason.ObjectiveCompleted, campaign.State.Outcome!.Reason);
        AssertReplayMatches(campaign);
    }

    [Theory]
    [MemberData(nameof(ObjectiveStressCases))]
    public void SixComputerObjectiveCampaignRemainsLiveAcrossAdditionalSeeds(
        ScenarioId scenario,
        int seed)
    {
        const int horizon = 40;
        var campaign = DriveMatch(
            scenario, seed, GameDuration.FourYears, throughTurn: horizon);

        Assert.True(campaign.State.Outcome is not null
            || campaign.State.Coordinator.Turn > horizon);
        Assert.Contains(campaign.State.Events,
            gameEvent => gameEvent.Kind == GameEventKind.HireResolved);
        Assert.True(campaign.State.Sectors.Count(sector => sector.Owner is not null)
            > MatchLimits.PlayerCount);
        AssertObjectiveProgress(campaign.State, scenario, seed);
        AssertReplayMatches(campaign);
    }

    private static void AssertReplayMatches(MatchReplayRecorder recorder)
    {
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(
            replay, recorder.State.Definitions);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(recorder.State),
            MatchStateHasher.ComputeSha256(replayed));
    }

    private static void AssertObjectiveProgress(MatchState state, ScenarioId scenario, int seed)
    {
        var attackEvents = state.Events
            .Where(gameEvent => gameEvent.Action == GangAction.Attack)
            .ToArray();
        var negativeAttitudes = state.Players.Sum(observer => state.Players.Count(other =>
            observer.Id != other.Id && state.AiStrategy.IsHostile(observer.Id, other.Id)));
        var message = $"{scenario} seed {seed} did not make scenario-specific progress: "
            + $"{attackEvents.Length} attack events, "
            + $"{attackEvents.Count(gameEvent =>
                gameEvent.Kind == GameEventKind.CommandResolved)} resolved, "
            + $"{negativeAttitudes} hostile player pairs.";
        if (scenario == ScenarioId.KillEmAll)
        {
            Assert.True(attackEvents.Length > 0 || negativeAttitudes == 0, message);
            return;
        }
        if (scenario == ScenarioId.Eliminate)
        {
            Assert.True(attackEvents.Any(gameEvent =>
                gameEvent.Kind == GameEventKind.CommandResolved
                && gameEvent.Resolution?.Code == CommandResolutionCode.Resolved
                && (gameEvent.Resolution.Damage > 0
                    || gameEvent.Resolution.RetaliationDamage > 0)), message);
            return;
        }

        var maximumControlled = state.Players
            .Where(player => player.Status == PlayerStatus.Active)
            .Max(player => scenario == ScenarioId.Siege
                ? state.Sectors.Count(sector => sector.IsImportant && sector.Owner == player.Id)
                : state.Sectors.Count(sector => sector.Owner == player.Id));
        var resolvedAttack = attackEvents.Any(
            gameEvent => gameEvent.Kind == GameEventKind.CommandResolved);
        Assert.True(maximumControlled >= 2
                    || scenario == ScenarioId.Siege && resolvedAttack,
            message);
    }

    private MatchReplayRecorder DriveMatch(
        ScenarioId scenario,
        int seed,
        GameDuration duration = GameDuration.SixMonths,
        int? throughTurn = null)
    {
        var elapsed = Stopwatch.StartNew();
        Trace(
            "AI tournament start: scenario={0}, seed={1}, duration={2}, horizon={3}.",
            scenario, seed, duration, throughTurn?.ToString() ?? "completion");
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU ONE", PlayerController.Computer),
            new(new PlayerId(1), "CPU TWO", PlayerController.Computer)
        ];
        var recorder = new MatchReplayRecorder(OriginalMatchFactory.Create(
            data, new MatchSetup(scenario, duration, seed, setups)));
        Assert.Equal(MatchLimits.PlayerCount, recorder.State.Players.Count);
        Assert.All(
            recorder.State.Players,
            player => Assert.Equal(PlayerController.Computer, player.Setup.Controller));
        var boundaries = 0;
        var lastReportedTurn = 0;
        var boundaryLimit = (throughTurn ?? ScenarioCatalog.Turns(duration) + 1) * 32;
        while (recorder.State.Outcome is null
               && (throughTurn is null || recorder.State.Coordinator.Turn <= throughTurn)
               && boundaries++ < boundaryLimit)
        {
            var state = recorder.State;
            if (state.Coordinator.Phase == TurnPhase.Upkeep
                && state.Coordinator.Turn % 10 == 0
                && state.Coordinator.Turn != lastReportedTurn)
            {
                lastReportedTurn = state.Coordinator.Turn;
                Trace(
                    "AI tournament progress: scenario={0}, seed={1}, turn={2}, boundaries={3}, events={4}, elapsed={5}.",
                    scenario, seed, state.Coordinator.Turn, boundaries,
                    state.Events.Count, elapsed.Elapsed);
            }
            switch (state.Coordinator.Phase)
            {
                case TurnPhase.Upkeep:
                    recorder.FinishUpkeep();
                    break;
                case TurnPhase.Command:
                    var player = state.Coordinator.ActivePlayer!.Value;
                    if (state.FindPlayer(player)?.Status == PlayerStatus.Active)
                        PlanComputerTurn(recorder, player);
                    else
                        recorder.FinishCommand(player);
                    break;
                case TurnPhase.Execution:
                    recorder.FinishExecutionPhase();
                    break;
                case TurnPhase.Hire:
                    recorder.FinishHire(state.Coordinator.ActivePlayer!.Value);
                    break;
                case TurnPhase.PlayerElimination:
                    recorder.FinishPlayerElimination();
                    break;
            }
        }
        Assert.True(
            boundaries < boundaryLimit,
            $"AI match exceeded the phase-boundary safety limit: scenario={scenario}, "
            + $"seed={seed}, turn={recorder.State.Coordinator.Turn}, boundaries={boundaries}, "
            + $"events={recorder.State.Events.Count}, elapsed={elapsed.Elapsed}.");
        Trace(
            "AI tournament complete: scenario={0}, seed={1}, turn={2}, boundaries={3}, events={4}, outcome={5}, elapsed={6}.",
            scenario, seed, recorder.State.Coordinator.Turn, boundaries,
            recorder.State.Events.Count, recorder.State.Outcome?.Reason.ToString() ?? "window-complete",
            elapsed.Elapsed);
        return recorder;
    }

    private void Trace(string format, params object[] values)
    {
        _output.WriteLine(format, values);
        TestContext.Current.SendDiagnosticMessage(format, values);
    }

    private static void PlanComputerTurn(
        MatchReplayRecorder recorder,
        PlayerId player)
    {
        recorder.PrepareAiPlanning(player);
        foreach (var command in AiTurnPlanner.Plan(recorder.State, player))
            Assert.True(recorder.Submit(command).Accepted);
        recorder.PrepareHireOffers(player);
        var hiring = recorder.PrepareAiHiring(player);
        if (hiring.Choice is { } choice)
        {
            Assert.True(recorder.QueueHire(
                player, choice.GangDefinitionId, choice.SectorId).Accepted);
        }
        else if (hiring.RejectedGangDefinitionId is { } rejectedOffer)
        {
            Assert.True(recorder.SnubHireOffer(player, rejectedOffer).Accepted);
        }
        recorder.FinishCommand(player);
    }
}
