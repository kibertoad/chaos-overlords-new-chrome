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
            var backupHash = MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path + NativeSaveStore.BackupSuffix, match.Definitions));
            File.Delete(path);

            var recovered = NativeSaveStore.LoadRecoveringBackup(path, match.Definitions);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(recovered.State));
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(
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
            var backupHash = MatchStateHasher.ComputeFingerprint(MatchReplayStore.LoadAndReplay(
                path + MatchReplayStore.BackupSuffix, recorder.State.Definitions));
            File.Delete(path);

            var recovered = MatchReplayStore.LoadAndReplayRecoveringBackup(
                path, recorder.State.Definitions);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(recovered.State));
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(
                MatchReplayStore.LoadAndReplay(path, recorder.State.Definitions)));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NativeStoreRepairKeepsTheDamagedPrimaryAsEvidence()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            var match = CreateMatch();
            NativeSaveStore.SaveAtomic(path, match);
            match.FinishUpkeep();
            NativeSaveStore.SaveAtomic(path, match);
            var backup = File.ReadAllBytes(path + NativeSaveStore.BackupSuffix);
            byte[] damaged = [1, 2, 3, 4];
            File.WriteAllBytes(path, damaged);

            var recovered = NativeSaveStore.LoadRecoveringBackup(path, match.Definitions);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(backup, File.ReadAllBytes(path));
            Assert.Equal(backup, File.ReadAllBytes(path + NativeSaveStore.BackupSuffix));
            Assert.Equal(damaged, File.ReadAllBytes(path + AtomicGenerationRecovery.RejectedSuffix));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrowsingLeavesADamagedPrimaryAndRepairOnlyWhenAsked(bool repairPrimary)
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            File.WriteAllText(path, "damaged");
            File.WriteAllText(path + ".bak", "good");

            var loaded = AtomicGenerationRecovery.LoadRecoveringBackup(
                path, ".bak", repairPrimary, LoadGood);

            Assert.Equal("good", loaded.Value);
            Assert.True(loaded.RecoveredFromBackup);
            Assert.Equal(repairPrimary, loaded.PrimaryRepaired);
            Assert.Equal(repairPrimary ? "good" : "damaged", File.ReadAllText(path));
            Assert.Equal(repairPrimary, File.Exists(path + AtomicGenerationRecovery.RejectedSuffix));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WhenNoGenerationLoadsThePrimaryFailureIsRethrown(bool backupExists)
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            File.WriteAllText(path, "damaged");
            if (backupExists) File.WriteAllText(path + ".bak", "damaged");

            var failure = Assert.Throws<InvalidDataException>(() =>
                AtomicGenerationRecovery.LoadRecoveringBackup(path, ".bak", true, LoadGood));

            Assert.Equal("primary " + Path.GetFullPath(path), failure.Message);
            Assert.False(File.Exists(path + AtomicGenerationRecovery.RejectedSuffix));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AnIncompatiblePrimaryIsRethrownWithoutTouchingTheBackup()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            File.WriteAllText(path, "newer");
            File.WriteAllText(path + ".bak", "good");

            var failure = Assert.Throws<InvalidDataException>(() =>
                AtomicGenerationRecovery.LoadRecoveringBackup(path, ".bak", true, candidate =>
                    File.ReadAllText(candidate) == "newer"
                        ? throw IncompatibleSave.Create(IncompatibleSaveReason.NewerFormat, "newer")
                        : LoadGood(candidate)));

            Assert.True(IncompatibleSave.IsIncompatible(failure));
            Assert.Equal("newer", File.ReadAllText(path));
            Assert.Equal("good", File.ReadAllText(path + ".bak"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Reads a file that holds "good", naming which generation failed otherwise.</summary>
    private static string LoadGood(string candidate)
    {
        var text = File.ReadAllText(candidate);
        if (text == "good") return text;
        var generation = Path.GetExtension(candidate) == ".bak" ? "backup" : "primary";
        throw new InvalidDataException($"{generation} {Path.GetFullPath(candidate)}");
    }

    [Fact]
    public void TrustedExistingPrimarySkipsRedundantValidationBeforeReplacement()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        var validated = new List<string>();
        try
        {
            File.WriteAllText(path, "previous generation");

            AtomicGenerationRecovery.SaveAtomic(
                path, ".bak", "unreadable",
                stream => stream.Write("next generation"u8),
                candidate => validated.Add(Path.GetFullPath(candidate)),
                trustExistingPrimary: true);

            Assert.Single(validated);
            Assert.NotEqual(Path.GetFullPath(path), validated[0]);
            Assert.Equal("next generation", File.ReadAllText(path));
            Assert.Equal("previous generation", File.ReadAllText(path + ".bak"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CapturedSnapshotCanBeDurablyWrittenAfterTheStateAdvances()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            var match = CreateMatch();
            var snapshot = NativeSaveStore.Serialize(match);
            var expectedHash = MatchStateHasher.ComputeFingerprint(match);
            match.FinishUpkeep();

            NativeSaveStore.SaveAtomic(path, snapshot, match.Definitions, trustExistingPrimary: false);

            Assert.Equal(expectedHash, MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path, match.Definitions)));
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
            ], MatchDeviations.Original));
    }
}
