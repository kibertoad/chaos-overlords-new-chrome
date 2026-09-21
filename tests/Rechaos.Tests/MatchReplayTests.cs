using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class MatchReplayTests
{
    /// <summary>
    /// The operation kind is serialized as its number, so a stored replay reads whatever member
    /// happens to sit at that ordinal today. Adding one is safe; moving one silently reinterprets
    /// every replay already on disk, which is why the whole order is pinned rather than the count.
    /// </summary>
    [Fact]
    public void OperationKindOrdinalsAreStableOnTheWire()
    {
        Assert.Equal(
            new[]
            {
                (ReplayOperationKind.SubmitCommand, 0),
                (ReplayOperationKind.CancelCommand, 1),
                (ReplayOperationKind.QueueHire, 2),
                (ReplayOperationKind.SnubHireOffer, 3),
                (ReplayOperationKind.FinishUpkeep, 4),
                (ReplayOperationKind.FinishCommand, 5),
                (ReplayOperationKind.FinishExecutionPhase, 6),
                (ReplayOperationKind.FinishHire, 7),
                (ReplayOperationKind.FinishPlayerElimination, 8),
                (ReplayOperationKind.DismissNotification, 9),
                (ReplayOperationKind.PrepareHireOffers, 10),
                (ReplayOperationKind.PrepareAiPlanning, 11),
                (ReplayOperationKind.PrepareAiHiring, 12),
                (ReplayOperationKind.SendComlinkMessage, 13),
                (ReplayOperationKind.MarkComlinkRead, 14),
                (ReplayOperationKind.PrepareSimultaneousHireOffers, 15),
                (ReplayOperationKind.TransferPlayerToComputer, 16),
                (ReplayOperationKind.TransferPlayerToHuman, 17)
            },
            Enum.GetValues<ReplayOperationKind>().Select(kind => (kind, (int)kind)));
    }

    [Fact]
    public void ReplaysAcceptedRejectedAndPhaseOperationsToIdenticalState()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        FinishCommands(recorder);
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();
        FinishHireAndElimination(recorder);
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

        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(
            JsonSerializer.Serialize(recorder.State.Events),
            JsonSerializer.Serialize(restored.Events));
        Assert.Equal(recorder.State.PhaseHashes, restored.PhaseHashes);
        Assert.Equal(28, recorder.Steps.Count);
    }

    [Fact]
    public void ReplayPreservesDelayedSiteActivationAcrossTurnBoundary()
    {
        var initial = CreateMatch();
        initial.FindSite(1)!.Resistance = 1;
        var recorder = new MatchReplayRecorder(initial);
        recorder.FinishUpkeep();
        Assert.True(recorder.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Influence,
            CommandTarget.Site(1))).Accepted);
        FinishCommands(recorder);

        recorder.FinishExecutionPhase();
        Assert.Null(recorder.State.FindSite(1)!.InfluencedBy);
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();
        FinishHireAndElimination(recorder);
        recorder.FinishUpkeep();

        Assert.Equal(new PlayerId(0), recorder.State.FindSite(1)!.InfluencedBy);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(
            replay, recorder.State.Definitions);

        Assert.Equal(recorder.State.FindSite(1)!.InfluencedBy,
            restored.FindSite(1)!.InfluencedBy);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(JsonSerializer.Serialize(recorder.State.Events),
            JsonSerializer.Serialize(restored.Events));
    }

    [Fact]
    public void ReplaysComlinkDeliveryAndReadState()
    {
        var recorder = new MatchReplayRecorder(CreateMatch(secondPlayerHuman: true));
        recorder.FinishUpkeep();
        Assert.True(recorder.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "TRUCE?").Accepted);
        Assert.True(recorder.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "ANSWER ME").Accepted);
        var inbox = recorder.State.ComlinkFor(new PlayerId(1));
        Assert.True(recorder.MarkComlinkRead(new PlayerId(1), inbox.Messages[1].Sequence));

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, recorder.State.Definitions);

        Assert.Equal(recorder.State.ComlinkFor(new PlayerId(1)).Messages,
            restored.ComlinkFor(new PlayerId(1)).Messages);
        Assert.True(restored.ComlinkFor(new PlayerId(1)).HasUnread);
        Assert.Equal([inbox.Messages[1].Sequence],
            restored.ComlinkFor(new PlayerId(1)).ReadSequences);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State), MatchStateHasher.ComputeFingerprint(restored));
        var recipients = Assert.IsAssignableFrom<IList<PlayerId>>(
            recorder.Steps.First(step => step.Kind == ReplayOperationKind.SendComlinkMessage).Recipients!);
        Assert.True(recipients.IsReadOnly);
    }

    [Fact]
    public void CurrentReplayRequiresComlinkSequenceForReadOperation()
    {
        var recorder = new MatchReplayRecorder(CreateMatch(secondPlayerHuman: true));
        recorder.FinishUpkeep();
        Assert.True(recorder.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "TRUCE?").Accepted);
        var sequence = recorder.State.ComlinkFor(new PlayerId(1)).Messages[0].Sequence;
        Assert.True(recorder.MarkComlinkRead(new PlayerId(1), sequence));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var document = JsonNode.Parse(replay.ToArray())!.AsObject();
        document["steps"]![2]!.AsObject().Remove("comlinkSequence");
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            MatchReplaySerializer.LoadAndReplay(modified, recorder.State.Definitions));

        Assert.Contains("invalid payload", exception.Message);
    }


    [Fact]
    public void ReplayRejectsFieldsThatDoNotBelongToOperation()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var document = JsonNode.Parse(replay.ToArray())!.AsObject();
        document["steps"]![0]!["player"] = 0;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            MatchReplaySerializer.LoadAndReplay(modified, recorder.State.Definitions));

        Assert.Contains("invalid payload", exception.Message);
    }

    [Fact]
    public void ReplaysCrackdownTriggerCountdownAndFollowingPoliceCombat()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        Assert.True(recorder.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Chaos, CommandTarget.None)).Accepted);
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

        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(recorder.State.Sectors[0].CrackdownTurnsRemaining,
            restored.Sectors[0].CrackdownTurnsRemaining);
        Assert.Equal(recorder.State.Sectors[0].CrackdownHistory, restored.Sectors[0].CrackdownHistory);
        Assert.Equal(
            JsonSerializer.Serialize(recorder.State.Events),
            JsonSerializer.Serialize(restored.Events));
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void CurrentReplayReplaysSimultaneousHireOfferGeneration()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();

        var drawn = recorder.PrepareSimultaneousHireOffers();

        Assert.Empty(drawn);
        Assert.Equal(
            ReplayOperationKind.PrepareSimultaneousHireOffers,
            recorder.Steps[^1].Kind);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var document = JsonNode.Parse(replay.ToArray())!.AsObject();
        Assert.Equal(MatchReplaySerializer.CurrentFormatVersion,
            document["formatVersion"]!.GetValue<int>());
        replay.Position = 0;

        var restored = MatchReplaySerializer.LoadAndReplay(
            replay, recorder.State.Definitions);

        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restored));
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void ReplaysComputerPlannedActionHistory()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        recorder.FinishCommand(new PlayerId(0));
        recorder.PrepareAiPlanning(new PlayerId(1));
        Assert.True(recorder.Submit(new GameCommand(
            new PlayerId(1), new GangId(1), GangAction.Move, CommandTarget.Sector(62))).Accepted);

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, recorder.State.Definitions);

        Assert.Equal(GangAction.Move,
            restored.AiPlanning.PlannedAction(new PlayerId(1), 0));
        Assert.Equal(new AiActionTarget(62, 0),
            restored.AiPlanning.PlannedTarget(new PlayerId(1), 0));
        Assert.True(restored.AiPlanning.HasPlanned(new PlayerId(1)));
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void EncodesOriginalCommandDependentAiTargetBytes()
    {
        var state = CreateMatch();
        var computer = state.Players[1];
        computer.AddGang(new MatchGangState(
            new GangId(50), computer.Id, computer.Gangs[0].DefinitionId, 63, 5));
        var weapon = state.Definitions.Items.First(item => item.Type is >= 0 and <= 2).Id;
        var armor = state.Definitions.Items.First(item => item.Type == 3).Id;
        var miscellaneous = state.Definitions.Items.First(item => item.Type == 4).Id;

        Assert.Equal(new AiActionTarget(0, 0), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Attack,
                CommandTarget.Gang(state.Players[0].Gangs[0].Id))));
        Assert.Equal(new AiActionTarget(62, 0), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Move,
                CommandTarget.Sector(62))));
        Assert.Equal(new AiActionTarget(2, 0), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Influence,
                CommandTarget.Site(63 * MatchLimits.SitesPerSector + 2))));
        Assert.Equal(new AiActionTarget(checked((byte)armor), 0),
            OriginalAiActionTargetEncoding.Encode(state,
                new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Research,
                    CommandTarget.Item(armor))));
        Assert.Equal(new AiActionTarget(1, 1), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Give,
                CommandTarget.Gang(new GangId(50)), SecondaryTarget: CommandTarget.Item(weapon))));
        Assert.Equal(new AiActionTarget(7, 1), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Give,
                CommandTarget.Gang(new GangId(50)), SecondaryTarget: CommandTarget.Item(weapon),
                TertiaryTarget: CommandTarget.Item(armor),
                QuaternaryTarget: CommandTarget.Item(miscellaneous))));
        Assert.Equal(new AiActionTarget(2, 0), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Sell,
                CommandTarget.Item(armor))));
        Assert.Equal(new AiActionTarget(4, 0), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Sell,
                CommandTarget.Item(miscellaneous))));
        Assert.Equal(new AiActionTarget(7, 0), OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Sell,
                CommandTarget.Item(weapon), SecondaryTarget: CommandTarget.Item(armor),
                TertiaryTarget: CommandTarget.Item(miscellaneous))));
        Assert.Equal(AiActionTarget.None, OriginalAiActionTargetEncoding.Encode(state,
            new GameCommand(computer.Id, computer.Gangs[0].Id, GangAction.Hide, CommandTarget.None)));
    }

    [Fact]
    public void RejectsReplayWhoseExpectedStepHashWasModified()
    {
        var recorder = new MatchReplayRecorder(CreateMatch());
        recorder.FinishUpkeep();
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        var json = Encoding.UTF8.GetString(replay.ToArray());
        var hash = recorder.Steps[0].ResultingStateFingerprint;
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
    public void CurrentReplayPreservesMaximumHireForceFlag()
    {
        var recorder = new MatchReplayRecorder(CreateMatch(firstPlayerName: "SMGMILK"));
        Assert.True(recorder.State.Players[0].UsesMaximumHireForce);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;

        var restored = MatchReplaySerializer.LoadAndReplay(
            replay, recorder.State.Definitions);

        Assert.True(restored.Players[0].UsesMaximumHireForce);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restored));
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

            Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State), MatchStateHasher.ComputeFingerprint(replayed));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AtomicReplayStoreRecoversPreviousValidGeneration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rechaos-replay-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "match.rchreplay");
        try
        {
            var recorder = new MatchReplayRecorder(CreateMatch());
            recorder.FinishUpkeep();
            MatchReplayStore.SaveAtomic(path, recorder);
            var previousHash = MatchStateHasher.ComputeFingerprint(recorder.State);
            recorder.FinishCommand(new PlayerId(0));
            MatchReplayStore.SaveAtomic(path, recorder);
            File.WriteAllText(path, "corrupt");

            var recovered = MatchReplayStore.LoadAndReplayRecoveringBackup(
                path, recorder.State.Definitions);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(previousHash, MatchStateHasher.ComputeFingerprint(recovered.State));
            Assert.Equal(previousHash, MatchStateHasher.ComputeFingerprint(
                MatchReplayStore.LoadAndReplay(path, recorder.State.Definitions)));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AtomicReplayStorePreservesGoodBackupWhenCurrentReplayIsCorrupt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rechaos-replay-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "match.rchreplay");
        try
        {
            var recorder = new MatchReplayRecorder(CreateMatch());
            recorder.FinishUpkeep();
            MatchReplayStore.SaveAtomic(path, recorder);
            recorder.FinishCommand(new PlayerId(0));
            MatchReplayStore.SaveAtomic(path, recorder);
            var backupHash = MatchStateHasher.ComputeFingerprint(MatchReplayStore.LoadAndReplay(
                path + MatchReplayStore.BackupSuffix, recorder.State.Definitions));
            File.WriteAllText(path, "corrupt");

            recorder.FinishCommand(new PlayerId(1));
            MatchReplayStore.SaveAtomic(path, recorder);

            Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State), MatchStateHasher.ComputeFingerprint(
                MatchReplayStore.LoadAndReplay(path, recorder.State.Definitions)));
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(MatchReplayStore.LoadAndReplay(
                path + MatchReplayStore.BackupSuffix, recorder.State.Definitions)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void AdvanceToHire(MatchState state)
    {
        state.FinishUpkeep();
        state.FinishCommand(new PlayerId(0));
        state.FinishCommand(new PlayerId(1));
        while (state.Coordinator.Phase == TurnPhase.Execution)
            state.FinishExecutionPhase();
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

    private static MatchState CreateMatch(
        string firstPlayerName = "ONE",
        bool secondPlayerHuman = false) =>
        TestMatches.Create(firstPlayerName, secondPlayerHuman);
}
