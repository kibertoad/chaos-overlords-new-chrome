using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiTournamentTests
{
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

    private static MatchReplayRecorder DriveMatch(
        ScenarioId scenario,
        int seed,
        GameDuration duration = GameDuration.SixMonths,
        int? throughTurn = null)
    {
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
        var boundaryLimit = (throughTurn ?? ScenarioCatalog.Turns(duration) + 1) * 32;
        while (recorder.State.Outcome is null
               && (throughTurn is null || recorder.State.Coordinator.Turn <= throughTurn)
               && boundaries++ < boundaryLimit)
        {
            var state = recorder.State;
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
            "AI match exceeded the phase-boundary safety limit.");
        return recorder;
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
