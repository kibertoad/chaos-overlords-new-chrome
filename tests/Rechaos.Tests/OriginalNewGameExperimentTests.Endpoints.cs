using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // RULE-OBJECTIVE-005: -2 stops before the card at the human's own slot; -1
    // has dismissed it and lets the remaining slots and resolution finish.
    private static void AdvanceToRecordedEndpoint(MatchReplayRecorder recorder, PlayerId human, int controller)
    {
        var match = recorder.State;
        while (match.Outcome is null
               && !(match.Coordinator.Phase == TurnPhase.Command && match.Coordinator.ActivePlayer == human))
            HeadlessMatchRunner.Advance(recorder);
        if (match.Outcome is not null || IsActive(match, human) || controller != -1) return;

        do
        {
            HeadlessMatchRunner.Advance(recorder);
        } while (match.Outcome is null && match.Coordinator.Phase != TurnPhase.Upkeep);
    }

    // RULE-SETUP-008, RULE-OBJECTIVE-005: the rest of a hot-seat round after the first human's
    // Done. Each later human still playing draws its offers at its planning entry and presses Done
    // with no orders; an eliminated one's slot is crossed, its card being presentation only. The
    // round ends before the first human's next planning entry, where the probe stops at the
    // hand-off card before the offers are drawn, or at the match's outcome.
    private static void AdvanceHotSeatRound(MatchReplayRecorder recorder, PlayerId first)
    {
        var match = recorder.State;
        while (match.Outcome is null)
        {
            if (match.Coordinator.Phase == TurnPhase.Command
                && match.Coordinator.ActivePlayer is { } player
                && match.FindPlayer(player)!.Setup.Controller == PlayerController.Human)
            {
                if (player == first && IsActive(match, first)) return;
                if (IsActive(match, player))
                {
                    recorder.PrepareHireOffers(player);
                    recorder.FinishCommand(player);
                    continue;
                }
            }
            HeadlessMatchRunner.Advance(recorder);
        }
    }

    private static void AssertReplayEndpoint(MatchState match, PlayerId human, int controller)
    {
        if (controller != 0)
            Assert.Equal(PlayerStatus.Eliminated, match.FindPlayer(human)!.Status);
        else if (match.Outcome is null)
        {
            Assert.Equal(TurnPhase.Command, match.Coordinator.Phase);
            Assert.Equal(human, match.Coordinator.ActivePlayer);
        }
        else
        {
            Assert.Equal(PlayerStatus.Active, match.FindPlayer(human)!.Status);
            Assert.Equal(TurnPhase.Upkeep, match.Coordinator.Phase);
        }
    }
    // Synthetic harness cases, not recordings from the original: RULE-OBJECTIVE-005.
    [Theory]
    [InlineData(0, -2)]
    [InlineData(2, -2)]
    [InlineData(2, -1)]
    [InlineData(5, -1)]
    public void EliminatedHumanEndpointIncludesTheCorrectComputerPlanning(int slot, int controller)
    {
        MatchState Create()
        {
            var state = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
                ScenarioId.Power, GameDuration.OneYear, 12345,
                [new MatchPlayerSetup(new PlayerId(slot), "PROBE", PlayerController.Human, 0)],
                MatchDeviations.Original,
                AiDifficulty.Goon, allowSparsePlayerIds: true));
            state.FindPlayer(new PlayerId(slot))!.Status = PlayerStatus.Eliminated;
            foreach (var gang in state.FindPlayer(new PlayerId(slot))!.Gangs) gang.Force = 0;
            state.FinishUpkeep();
            return state;
        }

        var expected = Create();
        var expectedRecorder = new MatchReplayRecorder(expected);
        for (var earlier = 0; earlier < slot; earlier++) HeadlessMatchRunner.Advance(expectedRecorder);
        if (controller == -1)
        {
            for (var remaining = slot; remaining < 6; remaining++) HeadlessMatchRunner.Advance(expectedRecorder);
            while (expected.Coordinator.Phase != TurnPhase.Upkeep) HeadlessMatchRunner.Advance(expectedRecorder);
        }

        var actual = Create();
        AdvanceToRecordedEndpoint(new MatchReplayRecorder(actual), new PlayerId(slot), controller);
        AssertReplayEndpoint(actual, new PlayerId(slot), controller);
        Assert.Equal(expected.Random.ConsumptionCount, actual.Random.ConsumptionCount);
        Assert.Equal(expected.Random.State, actual.Random.State);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(expected), MatchStateHasher.ComputeFingerprint(actual));
        Assert.Equal(controller == -2 ? TurnPhase.Command : TurnPhase.Upkeep, actual.Coordinator.Phase);
        if (controller == -2) Assert.Equal(new PlayerId(slot), actual.Coordinator.ActivePlayer);
        else Assert.Equal(MatchEndReason.NoHumansLeft, actual.Outcome!.Reason);
    }

    // RULE-OBJECTIVE-001: a surviving human's decided match has no next planning entry.
    [Fact]
    public void SurvivingHumanOutcomeDoesNotRequireAnActivePlanningSlot()
    {
        var human = new PlayerId(2);
        var match = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
            ScenarioId.Power, GameDuration.OneYear, 12345,
            [new MatchPlayerSetup(human, "PROBE", PlayerController.Human, 0)], MatchDeviations.Original,
            AiDifficulty.Goon, allowSparsePlayerIds: true));
        foreach (var player in match.Players.Where(player => player.Id != human))
        {
            player.Status = PlayerStatus.Eliminated;
            foreach (var gang in player.Gangs) gang.Force = 0;
        }
        var recorder = new MatchReplayRecorder(match);
        while (match.Coordinator.Phase != TurnPhase.PlayerElimination)
        {
            if (match.Coordinator.Phase == TurnPhase.Command)
                recorder.FinishCommand(match.Coordinator.ActivePlayer!.Value);
            else HeadlessMatchRunner.Advance(recorder);
        }
        AdvanceToRecordedEndpoint(recorder, human, 0);
        Assert.NotNull(match.Outcome);
        Assert.Null(match.Coordinator.ActivePlayer);
        AssertReplayEndpoint(match, human, 0);
    }
}
