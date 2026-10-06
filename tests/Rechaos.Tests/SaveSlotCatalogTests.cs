using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SaveSlotCatalogTests
{
    [Fact]
    public void NineSlotsPersistEditableNamesAndMatchDetails()
    {
        // DEV-UI-011: saving and loading use nine named slots.
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var setup = new MatchSetup(
                ScenarioId.Power, GameDuration.SixMonths, 1996,
                [
                    new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human),
                    new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Human)
                ], MatchDeviations.Original with { AiPolicy = AiPolicyMode.Advanced });
            var state = OriginalMatchFactory.Create(definitions, setup);

            var saved = SaveSlotCatalog.Save(directory, 8, "Friday campaign", state, online: false);
            var read = Assert.IsType<SaveSlotSummary>(SaveSlotCatalog.Read(directory, 8, definitions));

            Assert.Equal("Friday campaign", saved.Name);
            Assert.Equal(saved.Name, read.Name);
            Assert.Equal(ScenarioId.Power, read.Scenario);
            Assert.Equal(2, read.HumanPlayers);
            Assert.Equal(4, read.AiPlayers);
            Assert.Equal("HOT SEAT", read.MatchType);
            Assert.Contains("H2 A4", read.Details);
            Assert.Equal(AiPolicyMode.Advanced, read.AiPolicy);
            Assert.Contains("ADVANCED AI", read.Details);
            Assert.NotNull(SaveSlotCatalog.Load(directory, 8, definitions));
            Assert.Null(SaveSlotCatalog.Read(directory, 0, definitions));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SuggestedNameIncludesScenarioAndTurn()
    {
        var definitions = BundledOriginalData.Load();
        var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996,
            [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));

        Assert.Equal("GREED - TURN 1", SaveSlotCatalog.SuggestedName(state));
    }

    /// <summary>
    /// Listing a backup-only slot shows it and leaves the disk alone; loading it repairs.
    /// </summary>
    /// <remarks>
    /// The browser reads all nine slots every time it opens, so a repair from the listing pass ran
    /// without the player choosing anything.
    /// </remarks>
    [Fact]
    public void BackupOnlySlotIsListedWithoutRepairAndRepairsWhenLoaded()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.BigMan, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));
            var path = SaveSlotCatalog.SavePath(directory, 4);
            SaveSlotCatalog.Save(directory, 4, "Recovery test", state, online: false);
            state.FinishUpkeep();
            SaveSlotCatalog.Save(directory, 4, "Recovery test", state, online: false);
            var backupHash = MatchStateHasher.ComputeFingerprint(NativeSaveStore.Load(
                path + NativeSaveStore.BackupSuffix, definitions));
            File.Delete(path);

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 4, definitions));

            Assert.True(summary.RecoveredFromBackup);
            Assert.False(summary.PrimaryRepaired);
            Assert.Contains("BACKUP ONLY", summary.Details);
            Assert.False(File.Exists(path));

            var loaded = SaveSlotCatalog.Load(directory, 4, definitions);

            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(loaded));
            Assert.True(File.Exists(path));
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path, definitions)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A save this build cannot read is never replaced from its backup and never drawn as empty.
    /// </summary>
    [Fact]
    public void ASaveFromANewerBuildIsReportedIncompatibleAndLeftAlone()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Greed, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));
            var path = SaveSlotCatalog.SavePath(directory, 2);
            SaveSlotCatalog.Save(directory, 2, "Older build", state, online: false);
            state.FinishUpkeep();
            SaveSlotCatalog.Save(directory, 2, "Newer build", state, online: false);
            // Stand in for a save written by a build whose format version this one does not know.
            var newer = File.ReadAllText(path).Replace(
                $"\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion}",
                $"\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion + 1}",
                StringComparison.Ordinal);
            File.WriteAllText(path, newer);

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 2, definitions));

            Assert.Equal(SaveSlotStatus.Incompatible, summary.Status);
            Assert.False(summary.IsPlayable);
            Assert.Equal(newer, File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A save from another build is incompatible even when its sidecar is current for it.
    /// </summary>
    /// <remarks>
    /// The sidecar's length and write time only prove the file has not been edited since the
    /// sidecar was written; a build upgrade moves neither. Drawing such a row from the sidecar
    /// alone reported a save this build cannot read as playable, with match details taken from the
    /// build that wrote it, and the player who chose it got a generic load failure instead of
    /// being told the save belongs elsewhere. The sidecar now records the build that wrote it, and
    /// anything else falls back to the load that classifies it.
    /// </remarks>
    [Fact]
    public void ASaveFromANewerBuildIsIncompatibleEvenWhenItsSidecarIsCurrent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Greed, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));
            var path = SaveSlotCatalog.SavePath(directory, 5);
            var sidecar = path + ".json";
            SaveSlotCatalog.Save(directory, 5, "Newer build", state, online: false);
            var length = new FileInfo(path).Length;
            var written = File.GetLastWriteTimeUtc(path);
            // Stand in for the pair a build with a format version this one does not know would have
            // left: the save declares that version, the sidecar was written by that same build, and
            // the write it takes fits in the digits the current version already occupies, so the
            // length and write time the sidecar records are exactly the file's own.
            File.WriteAllText(path, File.ReadAllText(path).Replace(
                $"\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion}",
                $"\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion + 1}",
                StringComparison.Ordinal));
            File.SetLastWriteTimeUtc(path, written);
            File.WriteAllText(sidecar, File.ReadAllText(sidecar).Replace(
                $"\"FormatVersion\":{NativeSaveSerializer.CurrentFormatVersion}",
                $"\"FormatVersion\":{NativeSaveSerializer.CurrentFormatVersion + 1}",
                StringComparison.Ordinal));
            Assert.Equal(length, new FileInfo(path).Length);
            Assert.Equal(written, File.GetLastWriteTimeUtc(path));
            Assert.Contains(
                $"\"FormatVersion\":{NativeSaveSerializer.CurrentFormatVersion + 1}",
                File.ReadAllText(sidecar), StringComparison.Ordinal);

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 5, definitions));

            Assert.Equal(SaveSlotStatus.Incompatible, summary.Status);
            Assert.False(summary.IsPlayable);
            Assert.Contains("ANOTHER BUILD", summary.Details);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A sidecar written against other definitions describes a row this build must not draw.</summary>
    [Fact]
    public void ASidecarFromAnotherDefinitionSetIsNotBelieved()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Power, GameDuration.SixMonths, 1996,
                [
                    new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human),
                    new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Human)
                ], MatchDeviations.Original with { AiPolicy = AiPolicyMode.Advanced }));
            var sidecar = SaveSlotCatalog.SavePath(directory, 6) + ".json";
            SaveSlotCatalog.Save(directory, 6, "Other definitions", state, online: false);
            // The match type is the sidecar's alone: on the fallback path it is derived from the
            // match that was actually read, so believing this sidecar is visible in the row.
            File.WriteAllText(sidecar, File.ReadAllText(sidecar)
                .Replace(
                    $"\"DefinitionsSha256\":\"{NativeSaveSerializer.DefinitionsFingerprint(definitions)}\"",
                    "\"DefinitionsSha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"",
                    StringComparison.Ordinal)
                .Replace("\"MatchType\":\"HOT SEAT\"", "\"MatchType\":\"ONLINE\"",
                    StringComparison.Ordinal));

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 6, definitions));

            Assert.True(summary.IsPlayable);
            Assert.Equal("HOT SEAT", summary.MatchType);
            Assert.Equal(2, summary.HumanPlayers);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A save whose sidecar cannot be written is still a save.
    /// </summary>
    /// <remarks>
    /// The sidecar is browser data beside the match, and building it asks the file system about
    /// the save. A failure there used to escape after the match was already durable, so the player
    /// was told the save had failed and the previous match's journal was left paired with it.
    /// </remarks>
    [Fact]
    public void ASaveWhoseSidecarCannotBeWrittenStillSucceeds()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Greed, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));
            var path = SaveSlotCatalog.SavePath(directory, 7);
            Directory.CreateDirectory(path + ".json");

            var saved = SaveSlotCatalog.Save(directory, 7, "No sidecar", state, online: false);

            Assert.Equal("No sidecar", saved.Name);
            Assert.True(File.Exists(path));
            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 7, definitions));
            Assert.True(summary.IsPlayable);
            Assert.Equal(SaveSlotCatalog.SuggestedName(state), summary.Name);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A damaged display-name sidecar must not hide the save it labels.</summary>
    [Fact]
    public void ADamagedNameSidecarFallsBackToTheSuggestedName()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Greed, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));
            SaveSlotCatalog.Save(directory, 3, "Named", state, online: false);
            File.WriteAllText(SaveSlotCatalog.SavePath(directory, 3) + ".json", string.Empty);

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 3, definitions));

            Assert.True(summary.IsPlayable);
            Assert.Equal(SaveSlotCatalog.SuggestedName(state), summary.Name);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The crash-recovery save is offered on the automatic row when it is the newer loadable file.
    /// </summary>
    [Fact]
    public void TheAutomaticRowOffersTheNewerOfAutosaveAndCrashRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Greed, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)], MatchDeviations.Original));
            var autoSave = Path.Combine(directory, "autosave.rchsave");
            var recovery = SaveSlotCatalog.CrashRecoveryPath(directory);

            Assert.Null(SaveSlotCatalog.ReadAutomatic(autoSave, recovery, definitions));

            NativeSaveStore.SaveAtomic(recovery, state);
            var only = Assert.IsType<(SaveSlotSummary Row, string Path)>(
                SaveSlotCatalog.ReadAutomatic(autoSave, recovery, definitions));
            Assert.Equal(recovery, only.Path);
            Assert.Equal("CRASH RECOVERY", only.Row.Name);
            Assert.Equal(SaveSlotCatalog.AutoSaveRow, only.Row.Slot);
            Assert.True(only.Row.IsPlayable);

            NativeSaveStore.SaveAtomic(autoSave, state);
            File.SetLastWriteTimeUtc(autoSave, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(recovery, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(recovery, SaveSlotCatalog.ReadAutomatic(autoSave, recovery, definitions)?.Path);

            File.SetLastWriteTimeUtc(autoSave, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            var newer = SaveSlotCatalog.ReadAutomatic(autoSave, recovery, definitions);
            Assert.Equal(autoSave, newer?.Path);
            Assert.Equal("AUTOSAVE", newer?.Row.Name);

            // A newer recovery file that cannot be loaded never hides a good autosave.
            File.WriteAllText(recovery, "damaged");
            File.SetLastWriteTimeUtc(recovery, new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(autoSave, SaveSlotCatalog.ReadAutomatic(autoSave, recovery, definitions)?.Path);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>An out-of-range enum in a sidecar is not believed, so the row can still be drawn.</summary>
    [Theory]
    [InlineData("\"AiPolicy\":1", "\"AiPolicy\":99")]
    [InlineData("\"Scenario\":", "\"Scenario\":99,\"Ignored\":")]
    public void ASidecarWithAnOutOfRangeValueIsNotBelieved(string original, string replacement)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = OriginalMatchFactory.Create(definitions, new MatchSetup(
                ScenarioId.Power, GameDuration.SixMonths, 1996,
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)],
                MatchDeviations.Original with { AiPolicy = AiPolicyMode.Advanced }));
            var sidecar = SaveSlotCatalog.SavePath(directory, 2) + ".json";
            SaveSlotCatalog.Save(directory, 2, "Tampered", state, online: false);
            var text = File.ReadAllText(sidecar);
            Assert.Contains(original, text);
            File.WriteAllText(sidecar, text.Replace(original, replacement, StringComparison.Ordinal));

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 2, definitions));

            Assert.Equal(AiPolicyMode.Advanced, summary.AiPolicy);
            Assert.Equal(ScenarioId.Power, summary.Scenario);
            Assert.Contains("ADVANCED AI", summary.Details);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
