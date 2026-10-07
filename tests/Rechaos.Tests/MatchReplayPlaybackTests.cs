using System.Text;
using System.Text.Json.Nodes;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The read-only replay cursor F10's viewer plays (DEV-UI-026): it verifies the whole journal
/// before showing a frame, every seek lands on a state the journal vouches for, and a journal it
/// cannot play is reported as missing, incompatible, diverged or damaged, without touching the
/// files.
/// </summary>
public sealed class MatchReplayPlaybackTests
{
    private static readonly Lazy<MatchReplayRecorder> ComputerMatch = new(() => RecordComputerMatch(8));

    [Fact]
    public void OpeningVerifiesTheJournalAndShowsTheOpeningState()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        var opening = MatchStateHasher.ComputeFingerprint(recorder.State);
        recorder.FinishUpkeep();
        recorder.FinishCommand(new PlayerId(0));
        recorder.FinishCommand(new PlayerId(1));

        var playback = MatchReplaySerializer.OpenPlayback(
            new MemoryStream(Journal(recorder)), recorder.State.Definitions);

        Assert.Equal(0, playback.Position);
        Assert.Null(playback.CurrentStep);
        Assert.Equal(recorder.StepCount, playback.StepCount);
        Assert.Equal(opening, MatchStateHasher.ComputeFingerprint(playback.State));
        playback.Seek(playback.StepCount);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(playback.State));
        Assert.False(playback.MoveNext());
    }

    [Fact]
    public void EverySeekLandsOnTheRecordedFingerprintFromAnyStartingPosition()
    {
        var recorder = ComputerMatch.Value;
        Assert.True(recorder.StepCount > 3 * MatchReplayPlayback.MinimumCheckpointInterval,
            $"The match recorded only {recorder.StepCount} steps, too few to cross a checkpoint.");
        var playback = MatchReplaySerializer.OpenPlayback(
            new MemoryStream(Journal(recorder)), recorder.State.Definitions);
        var steps = recorder.Steps;

        // Forward one at a time, then a fixed scatter of jumps both ways across the checkpoints.
        while (playback.MoveNext())
            Assert.Equal(steps[playback.Position - 1].ResultingStateFingerprint,
                MatchStateHasher.ComputeFingerprint(playback.State));
        var random = new Random(141);
        for (var jump = 0; jump < 40; jump++)
        {
            var target = random.Next(playback.StepCount + 1);
            playback.Seek(target);
            Assert.Equal(target, playback.Position);
            Assert.Equal(FingerprintAt(recorder, target), MatchStateHasher.ComputeFingerprint(playback.State));
        }
    }

    [Fact]
    public void AStateTheViewerChangedIsRebuiltInsteadOfReportedAsDivergence()
    {
        var recorder = ComputerMatch.Value;
        var playback = MatchReplaySerializer.OpenPlayback(
            new MemoryStream(Journal(recorder)), recorder.State.Definitions);
        playback.Seek(MatchReplayPlayback.MinimumCheckpointInterval + 5);

        playback.State.Players[0].Cash += 1000;
        Assert.True(playback.MoveNext());

        Assert.Equal(FingerprintAt(recorder, playback.Position),
            MatchStateHasher.ComputeFingerprint(playback.State));
    }

    [Fact]
    public void TurnNavigationStopsAtTheFirstStepOfEachTurn()
    {
        var recorder = ComputerMatch.Value;
        var playback = MatchReplaySerializer.OpenPlayback(
            new MemoryStream(Journal(recorder)), recorder.State.Definitions);
        var starts = Enumerable.Range(0, playback.StepCount + 1)
            .Where(position => position == 0 || playback.TurnAt(position) != playback.TurnAt(position - 1))
            .ToArray();
        Assert.True(starts.Length > 3);

        var visited = new List<int> { playback.Position };
        while (playback.Position < playback.StepCount)
        {
            playback.Seek(playback.NextTurnStart());
            visited.Add(playback.Position);
        }
        Assert.Equal(starts.Append(playback.StepCount).Distinct().ToArray(), visited.Distinct().ToArray());

        // Back from inside a turn goes to its start; from a start, to the previous turn's.
        playback.Seek(starts[2] + 1);
        playback.Seek(playback.PreviousTurnStart());
        Assert.Equal(starts[2], playback.Position);
        playback.Seek(playback.PreviousTurnStart());
        Assert.Equal(starts[1], playback.Position);
        playback.Seek(0);
        Assert.Equal(0, playback.PreviousTurnStart());
    }

    [Fact]
    public void ADivergedStepIsReportedWithItsNumberBeforeAnyFrameIsShown()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        var opening = MatchStateHasher.ComputeFingerprint(recorder.State);
        recorder.FinishUpkeep();
        recorder.FinishCommand(new PlayerId(0));
        var journal = JsonNode.Parse(Journal(recorder))!.AsObject();
        journal["steps"]![1]!["resultingStateFingerprint"] = opening;

        var exception = Assert.Throws<InvalidDataException>(() => MatchReplaySerializer.OpenPlayback(
            new MemoryStream(Encoding.UTF8.GetBytes(journal.ToJsonString())), recorder.State.Definitions));

        Assert.Equal(new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: 1), ReplayFailure.Of(exception));
    }

    [Fact]
    public void ADifferentRecordedResultIsADivergence()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        recorder.FinishUpkeep();
        var gang = recorder.State.Players[0].Gangs[0];
        recorder.Cancel(new PlayerId(0), gang.Id);
        var journal = JsonNode.Parse(Journal(recorder))!.AsObject();
        var step = journal["steps"]![1]!;
        step["accepted"] = !step["accepted"]!.GetValue<bool>();

        var exception = Assert.Throws<InvalidDataException>(() => MatchReplaySerializer.OpenPlayback(
            new MemoryStream(Encoding.UTF8.GetBytes(journal.ToJsonString())), recorder.State.Definitions));

        Assert.Equal(ReplayFailureKind.Diverged, ReplayFailure.Of(exception).Kind);
        Assert.Equal(1, ReplayFailure.Of(exception).DivergedStep);
    }

    [Fact]
    public void FailuresAreClassifiedByWhatThePlayerCanDoAboutThem()
    {
        Assert.Equal(ReplayFailureKind.Missing, ReplayFailure.Of(new FileNotFoundException()).Kind);
        Assert.Equal(ReplayFailureKind.Missing, ReplayFailure.Of(new DirectoryNotFoundException()).Kind);
        Assert.Equal(ReplayFailureKind.Unreadable, ReplayFailure.Of(new UnauthorizedAccessException()).Kind);
        Assert.Equal(ReplayFailureKind.Unreadable, ReplayFailure.Of(new IOException()).Kind);
        Assert.Equal(ReplayFailureKind.Damaged, ReplayFailure.Of(new InvalidDataException()).Kind);
        Assert.Equal(
            new ReplayFailure(ReplayFailureKind.Incompatible, IncompatibleSaveReason.OlderFormat),
            ReplayFailure.Of(IncompatibleSave.Create(IncompatibleSaveReason.OlderFormat, "older")));
    }

    [Fact]
    public void AnIncompatiblePrimaryIsReportedAndNeitherFileIsTouched()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "match.rchreplay");
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        recorder.FinishUpkeep();
        MatchReplayStore.SaveAtomic(path, recorder);
        recorder.FinishCommand(new PlayerId(0));
        MatchReplayStore.SaveAtomic(path, recorder);
        var journal = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        journal["formatVersion"] = MatchReplaySerializer.CurrentFormatVersion + 1;
        File.WriteAllText(path, journal.ToJsonString());
        var before = directory.Snapshot();

        var exception = Assert.ThrowsAny<InvalidDataException>(() =>
            MatchReplayStore.OpenPlaybackRecoveringBackup(path, recorder.State.Definitions));

        Assert.Equal(
            new ReplayFailure(ReplayFailureKind.Incompatible, IncompatibleSaveReason.NewerFormat),
            ReplayFailure.Of(exception));
        Assert.Equal(before, directory.Snapshot());
    }

    [Fact]
    public void ADamagedPrimaryPlaysTheBackupAndSaysWhyWithoutRepairingAnything()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "match.rchreplay");
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        recorder.FinishUpkeep();
        MatchReplayStore.SaveAtomic(path, recorder);
        var backupEnd = MatchStateHasher.ComputeFingerprint(recorder.State);
        recorder.FinishCommand(new PlayerId(0));
        MatchReplayStore.SaveAtomic(path, recorder);
        File.WriteAllText(path, "damaged");
        var before = directory.Snapshot();

        var opened = MatchReplayStore.OpenPlaybackRecoveringBackup(path, recorder.State.Definitions);

        Assert.True(opened.OpenedBackup);
        Assert.Equal(ReplayFailureKind.Damaged, opened.PrimaryFailure!.Kind);
        opened.Playback.Seek(opened.Playback.StepCount);
        Assert.Equal(backupEnd, MatchStateHasher.ComputeFingerprint(opened.Playback.State));
        Assert.Equal(before, directory.Snapshot());
    }

    [Fact]
    public void WhenTheBackupFailsTooThePrimarysOwnFailureIsReported()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "match.rchreplay");
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        var opening = MatchStateHasher.ComputeFingerprint(recorder.State);
        recorder.FinishUpkeep();
        recorder.FinishCommand(new PlayerId(0));
        var journal = JsonNode.Parse(Journal(recorder))!.AsObject();
        journal["steps"]![1]!["resultingStateFingerprint"] = opening;
        File.WriteAllText(path, journal.ToJsonString());
        File.WriteAllText(path + MatchReplayStore.BackupSuffix, "damaged");

        var exception = Assert.ThrowsAny<InvalidDataException>(() =>
            MatchReplayStore.OpenPlaybackRecoveringBackup(path, recorder.State.Definitions));

        Assert.Equal(new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: 1), ReplayFailure.Of(exception));
    }

    [Fact]
    public void AMissingReplayIsReportedAsMissing()
    {
        using var directory = new TemporaryDirectory();
        var exception = Assert.ThrowsAny<IOException>(() => MatchReplayStore.OpenPlaybackRecoveringBackup(
            Path.Combine(directory.Path, "none.rchreplay"), BundledOriginalData.Load()));
        Assert.Equal(ReplayFailureKind.Missing, ReplayFailure.Of(exception).Kind);
    }

    /// <summary>
    /// A replay saved after a load carries the history from before the save, the moves the load
    /// records, and the play after it, and plays through all of it.
    /// </summary>
    [Fact]
    public void PlaybackCrossesASaveAndLoadAsTheGameRecordsThem()
    {
        using var directory = new TemporaryDirectory();
        var replayPath = Path.Combine(directory.Path, "last-match.rchreplay");
        var data = BundledOriginalData.Load();
        var recorder = new MatchReplayRecorder(ComputerMatchStart(data));
        AdvanceTurns(recorder, 2);
        using var save = new MemoryStream();
        using var companion = new MemoryStream();
        NativeSaveSerializer.Save(save, recorder.State);
        MatchReplaySerializer.Save(companion, recorder);
        var savedAt = recorder.StepCount;
        var savedFingerprint = MatchStateHasher.ComputeFingerprint(recorder.State);

        save.Position = 0;
        companion.Position = 0;
        var loaded = NativeSaveSerializer.Load(save, data);
        var resumed = MatchReplaySerializer.TryResumeOnto(companion, loaded);
        Assert.NotNull(resumed);
        // What ChaosGame.AdoptMatch records on every load (RULE-RNG-001, RULE-COMLINK-004, RULE-AI-003).
        resumed.ContinueRandomStream(0x2468ACE1);
        resumed.EmptyComlinkInboxes();
        resumed.RefreshAiSectorRecords();
        AdvanceTurns(resumed, 2);
        MatchReplayStore.SaveAtomic(replayPath, resumed);

        var opened = MatchReplayStore.OpenPlaybackRecoveringBackup(replayPath, data);

        Assert.Null(opened.PrimaryFailure);
        var playback = opened.Playback;
        Assert.Equal(resumed.StepCount, playback.StepCount);
        playback.Seek(savedAt);
        Assert.Equal(savedFingerprint, MatchStateHasher.ComputeFingerprint(playback.State));
        playback.Seek(savedAt + 1);
        Assert.Equal(ReplayOperationKind.ContinueRandomStream, playback.CurrentStep!.Kind);
        playback.Seek(playback.StepCount);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(resumed.State),
            MatchStateHasher.ComputeFingerprint(playback.State));
    }

    internal static MatchReplayRecorder RecordComputerMatch(int turns)
    {
        var recorder = new MatchReplayRecorder(ComputerMatchStart(BundledOriginalData.Load()));
        AdvanceTurns(recorder, turns);
        return recorder;
    }

    internal static byte[] Journal(MatchReplayRecorder recorder)
    {
        using var stream = new MemoryStream();
        MatchReplaySerializer.Save(stream, recorder);
        return stream.ToArray();
    }

    private static MatchState ComputerMatchStart(OriginalData data) => OriginalMatchFactory.Create(data,
        new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 141,
        [
            new MatchPlayerSetup(new PlayerId(0), "CPU 1", PlayerController.Computer),
            new MatchPlayerSetup(new PlayerId(1), "CPU 2", PlayerController.Computer)
        ], MatchDeviations.Original));

    private static void AdvanceTurns(MatchReplayRecorder recorder, int turns)
    {
        var through = recorder.State.Coordinator.Turn + turns;
        while (recorder.State.Outcome is null && recorder.State.Coordinator.Turn < through)
            HeadlessMatchRunner.Advance(recorder);
    }

    private static string FingerprintAt(MatchReplayRecorder recorder, int position) => position == 0
        ? JsonNode.Parse(Journal(recorder))!["initialStateFingerprint"]!.GetValue<string>()
        : recorder.Steps[position - 1].ResultingStateFingerprint;

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "rechaos-playback-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        /// <summary>Every file in the directory with its bytes, to show nothing was rewritten.</summary>
        public string Snapshot() => string.Join("\n", Directory.EnumerateFiles(Path).Order(StringComparer.Ordinal)
            .Select(file => $"{System.IO.Path.GetFileName(file)}:{Convert.ToHexString(File.ReadAllBytes(file))}"));

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
