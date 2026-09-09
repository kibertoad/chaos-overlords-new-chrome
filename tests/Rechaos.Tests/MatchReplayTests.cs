using System.Text;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class MatchReplayTests
{
    [Fact]
    public void ReplaysAcceptedRejectedAndPhaseOperationsToIdenticalState()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        Assert.True(recorder.TryDismissNotification(new PlayerId(0), out var notification));
        Assert.Equal(GameNotificationKind.Economy, notification!.Kind);
        Assert.True(recorder.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Hide, CommandTarget.None)).Accepted);
        Assert.False(recorder.Submit(new GameCommand(
            new PlayerId(0), new GangId(1), GangAction.Hide, CommandTarget.None)).Accepted);
        recorder.FinishCommand(new PlayerId(0));
        recorder.FinishCommand(new PlayerId(1));
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();
        Assert.True(recorder.QueueHire(new PlayerId(0), 2, 0).Accepted);
        Assert.True(recorder.SnubHireOffer(new PlayerId(0), 3).Accepted);
        recorder.FinishHire(new PlayerId(0));
        recorder.FinishHire(new PlayerId(1));
        recorder.FinishPlayerElimination();

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, recorder.State.Definitions);

        Assert.Equal(MatchStateHasher.ComputeSha256(recorder.State), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(recorder.State.Events, restored.Events);
        Assert.Equal(recorder.State.PhaseHashes, restored.PhaseHashes);
        Assert.Equal(17, recorder.Steps.Count);
    }

    [Fact]
    public void RejectsReplayWhoseExpectedStepHashWasModified()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var json = Encoding.UTF8.GetString(replay.ToArray());
        var hash = recorder.Steps[0].ResultingStateSha256;
        var replacement = new string(hash[0] == '0' ? '1' : '0', 1) + hash[1..];
        var changed = json.Replace(hash, replacement, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => MatchReplaySerializer.LoadAndReplay(
            new MemoryStream(Encoding.UTF8.GetBytes(changed)), recorder.State.Definitions));
    }

    [Fact]
    public void RecorderRejectsOutOfBandStateMutation()
    {
        var match = CreateMatch();
        var recorder = new MatchReplayRecorder(match);
        match.FinishUpkeep();

        Assert.Throws<InvalidOperationException>(() => recorder.FinishCommand(new PlayerId(0)));
        Assert.Empty(recorder.Steps);
    }

    [Fact]
    public void SerializerRejectsOutOfBandMutationAfterLastRecordedStep()
    {
        var match = CreateMatch();
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));

        Assert.Throws<InvalidOperationException>(() =>
            MatchReplaySerializer.Save(new MemoryStream(), recorder));
    }

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id is 0 or 63 ? MatchBootstrap.HeadquartersDefinitionId : (short)0,
                    id is 0 or 63 ? 0 : 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return MatchBootstrap.Create(data, setup, sectors,
        [
            new MatchPlayerStart(new PlayerId(0), 0, 10, 500, [2, 3, 4]),
            new MatchPlayerStart(new PlayerId(1), 63, 9, 500, [5, 6, 7])
        ]);
    }
}
