using System.Text;
using System.Text.Json.Nodes;
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
        Assert.Equal(16, recorder.Steps.Count);
    }

    [Fact]
    public void ReplaysCrackdownTriggerCountdownAndFollowingPoliceCombat()
    {
        var recorder = new MatchReplayRecorder(CreateMatch(negativeTolerance: true));
        recorder.FinishUpkeep();
        FinishCommands(recorder);
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();
        Assert.True(recorder.State.Sectors[0].CrackdownActive);
        Assert.Equal([1], recorder.State.Sectors[0].CrackdownHistory);
        var initialDuration = recorder.State.Sectors[0].CrackdownTurnsRemaining;
        FinishHireAndElimination(recorder);

        recorder.FinishUpkeep();
        Assert.Equal(initialDuration, recorder.State.Sectors[0].CrackdownTurnsRemaining);
        FinishCommands(recorder);
        while (recorder.State.Coordinator.ExecutionPhase != ExecutionPhase.Combat)
            recorder.FinishExecutionPhase();
        recorder.FinishExecutionPhase();
        Assert.NotEmpty(recorder.State.LastPoliceAttackResolutions);
        Assert.Equal(initialDuration - 1, recorder.State.Sectors[0].CrackdownTurnsRemaining);

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, recorder.State.Definitions);

        Assert.Equal(MatchStateHasher.ComputeSha256(recorder.State), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(recorder.State.Sectors[0].CrackdownTurnsRemaining,
            restored.Sectors[0].CrackdownTurnsRemaining);
        Assert.Equal(recorder.State.Sectors[0].CrackdownHistory, restored.Sectors[0].CrackdownHistory);
        Assert.Equal(recorder.State.Events, restored.Events);
    }

    [Fact]
    public void ReplaysPlanningTimeHireOfferGeneration()
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var recorder = new MatchReplayRecorder(OriginalMatchFactory.Create(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer])));
        recorder.FinishUpkeep();

        var offers = recorder.PrepareHireOffers(setupPlayer.Id).ToArray();

        Assert.Equal(MatchLimits.HireOffersPerPlayer, offers.Length);
        Assert.Equal(ReplayOperationKind.PrepareHireOffers, recorder.Steps[^1].Kind);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(offers, restored.Players[0].HirePool);
        Assert.Equal(MatchStateHasher.ComputeSha256(recorder.State), MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void ReplaysAiHiringRolePreparation()
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(
            new PlayerId(0), "CPU", PlayerController.Computer);
        var recorder = new MatchReplayRecorder(OriginalMatchFactory.Create(data,
            new MatchSetup(ScenarioId.BigMan, GameDuration.SixMonths, 1996, [setupPlayer])));
        recorder.FinishUpkeep();
        recorder.PrepareAiPlanning(setupPlayer.Id);

        recorder.PrepareAiHiring(setupPlayer.Id);

        Assert.Equal(ReplayOperationKind.PrepareAiHiring, recorder.Steps[^1].Kind);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(recorder.State.AiPlanning.CurrentHireRole(setupPlayer.Id),
            restored.AiPlanning.CurrentHireRole(setupPlayer.Id));
        Assert.Equal(MatchStateHasher.ComputeSha256(recorder.State),
            MatchStateHasher.ComputeSha256(restored));
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

    [Fact]
    public void VersionFourReplayUsesVersionFiveStateHashesAfterStrategyMigration()
    {
        var initial = CreateMatch();
        var oldInitialHash = MatchStateHasher.ComputeVersionFiveSha256(initial);
        var recorder = new MatchReplayRecorder(initial);
        recorder.FinishUpkeep();
        var oldResultHash = MatchStateHasher.ComputeVersionFiveSha256(initial);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var document = JsonNode.Parse(replay.ToArray())!.AsObject();
        document["formatVersion"] = 4;
        document["initialStateSha256"] = oldInitialHash;
        document["steps"]![0]!["resultingStateSha256"] = oldResultHash;

        var snapshotBytes = Convert.FromBase64String(
            document["initialSnapshot"]!.GetValue<string>());
        var snapshot = JsonNode.Parse(snapshotBytes)!.AsObject();
        snapshot["formatVersion"] = 5;
        snapshot["stateSha256"] = oldInitialHash;
        snapshot["runtime"]!.AsObject().Remove("aiStrategy");
        document["initialSnapshot"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(snapshot.ToJsonString()));

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = MatchReplaySerializer.LoadAndReplay(legacy, initial.Definitions);

        Assert.Equal(MatchStateHasher.ComputeSha256(initial), MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void VersionSixReplayUsesVersionSixHashesAfterPlanningMigration()
    {
        var initial = CreateMatch();
        var oldInitialHash = MatchStateHasher.ComputeVersionSixSha256(initial);
        var recorder = new MatchReplayRecorder(initial);
        recorder.FinishUpkeep();
        var oldResultHash = MatchStateHasher.ComputeVersionSixSha256(initial);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var document = JsonNode.Parse(replay.ToArray())!.AsObject();
        document["formatVersion"] = 6;
        document["initialStateSha256"] = oldInitialHash;
        document["steps"]![0]!["resultingStateSha256"] = oldResultHash;

        var snapshotBytes = Convert.FromBase64String(
            document["initialSnapshot"]!.GetValue<string>());
        var snapshot = JsonNode.Parse(snapshotBytes)!.AsObject();
        snapshot["formatVersion"] = 6;
        snapshot["stateSha256"] = oldInitialHash;
        snapshot["runtime"]!.AsObject().Remove("aiPlanning");
        document["initialSnapshot"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(snapshot.ToJsonString()));

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = MatchReplaySerializer.LoadAndReplay(legacy, initial.Definitions);

        Assert.Equal(MatchStateHasher.ComputeSha256(initial), MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void VersionEightReplayMigratesVersionSevenInitialHireSlots()
    {
        var initial = CreateMatch();
        var oldInitialHash = MatchStateHasher.ComputeVersionTenSha256(initial);
        var recorder = new MatchReplayRecorder(initial);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var document = JsonNode.Parse(replay.ToArray())!.AsObject();
        document["formatVersion"] = 8;
        document["initialStateSha256"] = oldInitialHash;
        var snapshotBytes = Convert.FromBase64String(
            document["initialSnapshot"]!.GetValue<string>());
        var snapshot = JsonNode.Parse(snapshotBytes)!.AsObject();
        snapshot["formatVersion"] = 7;
        snapshot["stateSha256"] = oldInitialHash;
        foreach (var player in snapshot["players"]!.AsArray())
        {
            player!.AsObject().Remove("hireOfferSlots");
            player.AsObject().Remove("snubbedHireOfferSlot");
        }
        document["initialSnapshot"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(snapshot.ToJsonString()));

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = MatchReplaySerializer.LoadAndReplay(legacy, initial.Definitions);

        Assert.Equal(initial.Players[0].HirePool, restored.Players[0].HirePool);
        Assert.Equal(initial.Players[0].HireOfferSlots, restored.Players[0].HireOfferSlots);
    }

    [Fact]
    public void AtomicReplayStoreWritesAndReplaysAFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rechaos-replay-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "match.rchreplay");
        try
        {
            var recorder = new MatchReplayRecorder(CreateMatch());
            recorder.FinishUpkeep();
            MatchReplayStore.SaveAtomic(path, recorder);

            var replayed = MatchReplayStore.LoadAndReplay(path, recorder.State.Definitions);

            Assert.Equal(MatchStateHasher.ComputeSha256(recorder.State), MatchStateHasher.ComputeSha256(replayed));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void FinishCommands(MatchReplayRecorder recorder)
    {
        foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);
    }

    private static void FinishHireAndElimination(MatchReplayRecorder recorder)
    {
        foreach (var player in recorder.State.Players) recorder.FinishHire(player.Id);
        recorder.FinishPlayerElimination();
    }

    private static MatchState CreateMatch(bool negativeTolerance = false)
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
            ], tolerance: id == 0 && negativeTolerance ? -2 : ManualRules.MinimumTolerance))
            .ToArray();
        return MatchBootstrap.Create(data, setup, sectors,
        [
            new MatchPlayerStart(new PlayerId(0), 0, 10, 500, [2, 3, 4]),
            new MatchPlayerStart(new PlayerId(1), 63, 9, 500, [5, 6, 7])
        ]);
    }
}
