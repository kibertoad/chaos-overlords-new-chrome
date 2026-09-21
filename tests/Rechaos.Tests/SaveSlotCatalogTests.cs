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
        var directory = Path.Combine(Path.GetTempPath(), $"rechaos-slots-{Guid.NewGuid():N}");
        try
        {
            var definitions = BundledOriginalData.Load();
            var setup = new MatchSetup(
                ScenarioId.Power, GameDuration.SixMonths, 1996,
                [
                    new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human),
                    new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Human)
                ], aiPolicy: AiPolicyMode.Advanced);
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
            [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)]));

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
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)]));
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
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)]));
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
                [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)]));
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
}
