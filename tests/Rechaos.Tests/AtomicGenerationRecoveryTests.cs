using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AtomicGenerationRecoveryTests
{
    [Fact]
    public void NativeStoreRepairsMissingPrimaryFromValidBackup()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            var match = CreateMatch();
            NativeSaveStore.SaveAtomic(path, match);
            match.FinishUpkeep();
            NativeSaveStore.SaveAtomic(path, match);
            var backupHash = MatchStateHasher.ComputeSha256(
                NativeSaveStore.Load(path + NativeSaveStore.BackupSuffix, match.Definitions));
            File.Delete(path);

            var recovered = NativeSaveStore.LoadRecoveringBackup(path, match.Definitions);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(backupHash, MatchStateHasher.ComputeSha256(recovered.State));
            Assert.Equal(backupHash, MatchStateHasher.ComputeSha256(
                NativeSaveStore.Load(path, match.Definitions)));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStoreRepairsMissingPrimaryFromValidBackup()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchreplay");
        try
        {
            var recorder = new MatchReplayRecorder(CreateMatch());
            recorder.FinishUpkeep();
            MatchReplayStore.SaveAtomic(path, recorder);
            recorder.FinishCommand(new PlayerId(0));
            MatchReplayStore.SaveAtomic(path, recorder);
            var backupHash = MatchStateHasher.ComputeSha256(MatchReplayStore.LoadAndReplay(
                path + MatchReplayStore.BackupSuffix, recorder.State.Definitions));
            File.Delete(path);

            var recovered = MatchReplayStore.LoadAndReplayRecoveringBackup(
                path, recorder.State.Definitions);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(backupHash, MatchStateHasher.ComputeSha256(recovered.State));
            Assert.Equal(backupHash, MatchStateHasher.ComputeSha256(
                MatchReplayStore.LoadAndReplay(path, recorder.State.Definitions)));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(), "rechaos-generation-recovery", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static MatchState CreateMatch()
    {
        var definitions = BundledOriginalData.Load();
        return OriginalMatchFactory.Create(definitions, new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996,
            [
                new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human),
                new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Computer)
            ]));
    }
}
