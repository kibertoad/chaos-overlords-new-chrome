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
                ]);
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

    [Fact]
    public void BackupOnlySlotIsListedAndRepairsItsPrimary()
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
            var backupHash = MatchStateHasher.ComputeSha256(NativeSaveStore.Load(
                path + NativeSaveStore.BackupSuffix, definitions));
            File.Delete(path);

            var summary = Assert.IsType<SaveSlotSummary>(
                SaveSlotCatalog.Read(directory, 4, definitions));

            Assert.True(summary.RecoveredFromBackup);
            Assert.True(summary.PrimaryRepaired);
            Assert.Contains("RECOVERED", summary.Details);
            Assert.True(File.Exists(path));
            Assert.Equal(backupHash, MatchStateHasher.ComputeSha256(
                NativeSaveStore.Load(path, definitions)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
