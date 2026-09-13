using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The history that survives a save, a quit and a load.
/// </summary>
/// <remarks>
/// Without it a bug report filed after loading a save reproduces only what happened since, which is
/// exactly the part nobody needs — the turns that produced the bug are the ones before.
/// </remarks>
public sealed class MatchJournalStoreTests
{
    [Fact]
    public void ResumesAJournalAndKeepsAppendingToTheSameHistory()
    {
        var directory = NewDirectory();
        try
        {
            var definitions = BundledOriginalData.Load();
            var recorder = new MatchReplayRecorder(TestMatches.Create());
            recorder.FinishUpkeep();
            foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);
            var stepsBeforeSave = recorder.StepCount;

            SaveSlotCatalog.Save(directory, 0, "with history", recorder.State, false, recorder);
            var loaded = SaveSlotCatalog.Load(directory, 0, definitions);
            var resumed = SaveSlotCatalog.LoadJournal(directory, 0, definitions, loaded);

            Assert.NotNull(resumed);
            Assert.Equal(stepsBeforeSave, resumed.StepCount);
            Assert.Equal(
                MatchStateHasher.ComputeSha256(recorder.State),
                MatchStateHasher.ComputeSha256(resumed.State));

            // Playing on appends to the same journal, so the whole session still replays.
            while (resumed.State.Coordinator.Phase == TurnPhase.Execution)
                resumed.FinishExecutionPhase();
            Assert.True(resumed.StepCount > stepsBeforeSave);
            using var journal = new MemoryStream();
            MatchReplaySerializer.Save(journal, resumed);
            journal.Position = 0;
            Assert.Equal(
                MatchStateHasher.ComputeSha256(resumed.State),
                MatchStateHasher.ComputeSha256(
                    MatchReplaySerializer.LoadAndReplay(journal, definitions)));
        }
        finally
        {
            Delete(directory);
        }
    }

    /// <summary>A slot saved without a journal must not inherit the last game's.</summary>
    [Fact]
    public void SavingWithoutAJournalRemovesTheOneAlreadyThere()
    {
        var directory = NewDirectory();
        try
        {
            var definitions = BundledOriginalData.Load();
            var recorder = new MatchReplayRecorder(TestMatches.Create());
            recorder.FinishUpkeep();
            SaveSlotCatalog.Save(directory, 2, "first", recorder.State, false, recorder);
            Assert.True(File.Exists(SaveSlotCatalog.JournalPath(directory, 2)));

            var other = TestMatches.Create("SOMEBODY ELSE");
            SaveSlotCatalog.Save(directory, 2, "second", other, online: true);

            Assert.False(File.Exists(SaveSlotCatalog.JournalPath(directory, 2)));
            Assert.Null(SaveSlotCatalog.LoadJournal(
                directory, 2, definitions, SaveSlotCatalog.Load(directory, 2, definitions)));
        }
        finally
        {
            Delete(directory);
        }
    }

    /// <summary>
    /// A journal that replays to a different state belongs to a different game and is ignored.
    /// </summary>
    /// <remarks>
    /// Resuming it would graft this match's turns onto that one's history, which replays to a state
    /// nobody was ever in — worse than having no history at all.
    /// </remarks>
    [Fact]
    public void IgnoresAJournalThatDoesNotMatchTheSave()
    {
        var directory = NewDirectory();
        try
        {
            var definitions = BundledOriginalData.Load();
            var recorder = new MatchReplayRecorder(TestMatches.Create());
            recorder.FinishUpkeep();
            SaveSlotCatalog.Save(directory, 3, "mismatch", recorder.State, false, recorder);

            // A save from a different game, in the same slot's shape.
            var somebodyElses = TestMatches.Create("SOMEBODY ELSE");

            Assert.Null(SaveSlotCatalog.LoadJournal(directory, 3, definitions, somebodyElses));
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void IgnoresACorruptJournalRatherThanFailingTheLoad()
    {
        var directory = NewDirectory();
        try
        {
            var definitions = BundledOriginalData.Load();
            var recorder = new MatchReplayRecorder(TestMatches.Create());
            recorder.FinishUpkeep();
            SaveSlotCatalog.Save(directory, 4, "corrupt", recorder.State, false, recorder);
            File.WriteAllText(SaveSlotCatalog.JournalPath(directory, 4), "not a journal");

            var loaded = SaveSlotCatalog.Load(directory, 4, definitions);

            Assert.NotNull(loaded);
            Assert.Null(SaveSlotCatalog.LoadJournal(directory, 4, definitions, loaded));
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void AnswersNullWhenTheSlotHasNoJournal()
    {
        var directory = NewDirectory();
        try
        {
            var definitions = BundledOriginalData.Load();
            var state = TestMatches.Create();
            SaveSlotCatalog.Save(directory, 5, "no journal", state, online: false);

            Assert.Null(SaveSlotCatalog.LoadJournal(
                directory, 5, definitions, SaveSlotCatalog.Load(directory, 5, definitions)));
        }
        finally
        {
            Delete(directory);
        }
    }

    private static string NewDirectory() =>
        Path.Combine(Path.GetTempPath(), $"rechaos-journal-{Guid.NewGuid():N}");

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
