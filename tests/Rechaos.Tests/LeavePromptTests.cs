using System.Reflection;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>RULE-UI-015: leaving a match that changed since it was last saved asks to save first.</summary>
public sealed class LeavePromptTests
{
    [Fact]
    public void ANewMatchIsUnsavedAndASaveOrLoadMarksItSaved()
    {
        var actions = new MatchActions(new MatchReplayRecorder(NativeSaveSerializerTests.CreateMatch()));
        Assert.False(actions.IsSaved);
        actions.MarkSaved();
        Assert.True(actions.IsSaved);
    }

    [Fact]
    public void ClosingAnUnsavedMatchOpensThePromptAndCancelReturnsToTheMatch()
    {
        var game = GameWith(saved: false);
        Assert.True(OriginalNewGameExperimentTests.ClosingIsCancelled(game));
        Assert.Equal(LeaveKind.Exit, Prompt(game));
        Assert.True((bool)DeviationBehaviourTests.Field("_gameMenuOpen").GetValue(game)!);

        DeviationBehaviourTests.Call(game, "AnswerLeavePrompt", LeaveAnswer.Cancel);
        Assert.Equal(LeaveKind.None, Prompt(game));
        Assert.False((bool)DeviationBehaviourTests.Field("_gameMenuOpen").GetValue(game)!);
    }

    [Fact]
    public void ClosingASavedMatchIsNotHeldBack()
    {
        var game = GameWith(saved: true);
        Assert.False(OriginalNewGameExperimentTests.ClosingIsCancelled(game));
        Assert.Equal(LeaveKind.None, Prompt(game));
    }

    [Fact]
    public void QuitToMainMenuAsksWhileTheMatchIsUnsaved()
    {
        var game = GameWith(saved: false);
        DeviationBehaviourTests.Call(game, "ActivateGameMenuAction", GameMenuAction.QuitToMainMenu);
        Assert.Equal(LeaveKind.End, Prompt(game));
    }

    [Fact]
    public void SaveFirstOpensTheSaveBrowserAndCancellingItReturnsToTheMatch()
    {
        var directory = Directory.CreateTempSubdirectory("rechaos-leave-prompt-");
        try
        {
            var game = GameWith(saved: false);
            DeviationBehaviourTests.Call(game, "RequestLeave", LeaveKind.End);
            var autoSavePath = Path.Combine(directory.FullName, "autosave.json");
            DeviationBehaviourTests.Field("_definitions").SetValue(game, ((MatchState)DeviationBehaviourTests.Field("_state").GetValue(game)!).Definitions);
            DeviationBehaviourTests.Field("_saveDirectory").SetValue(game, directory.FullName);
            DeviationBehaviourTests.Field("_autoSavePath").SetValue(game, autoSavePath);
            DeviationBehaviourTests.Field("_autoSave").SetValue(game, new RollingAutoSave(autoSavePath, (_, _) => { }));
            DeviationBehaviourTests.Field("_saveSlots").SetValue(game, new SaveSlotSummary?[SaveSlotCatalog.BrowserRowCount]);
            DeviationBehaviourTests.Call(game, "AnswerLeavePrompt", LeaveAnswer.SaveFirst);
            Assert.Equal(SaveBrowserMode.Save, (SaveBrowserMode)DeviationBehaviourTests.Field("_saveBrowserMode").GetValue(game)!);
            Assert.Equal(LeaveKind.End, (LeaveKind)DeviationBehaviourTests.Field("_leaveAfterSave").GetValue(game)!);

            DeviationBehaviourTests.Call(game, "CloseSaveBrowser");
            Assert.Equal(LeaveKind.None, (LeaveKind)DeviationBehaviourTests.Field("_leaveAfterSave").GetValue(game)!);
            Assert.False((bool)DeviationBehaviourTests.Field("_gameMenuOpen").GetValue(game)!);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ShutdownStillWritesTheAutosave()
    {
        // The autosave is written on the way out whether or not the player saved.
        var unload = typeof(ChaosGame).GetMethod("UnloadContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Contains(typeof(ChaosGame).GetMethod("FlushAutoSaves", BindingFlags.Instance | BindingFlags.NonPublic)!,
            DeviationBehaviourTests.Calls(unload));
    }

    private static ChaosGame GameWith(bool saved)
    {
        var game = DeviationBehaviourTests.HeadlessGame();
        var match = NativeSaveSerializerTests.CreateMatch();
        var actions = new MatchActions(new MatchReplayRecorder(match));
        if (saved) actions.MarkSaved();
        DeviationBehaviourTests.Field("_state").SetValue(game, match);
        DeviationBehaviourTests.Field("_actions").SetValue(game, actions);
        DeviationBehaviourTests.Field("_saveName").SetValue(game, new TextField("SAVE NAME", 48));
        return game;
    }

    private static LeaveKind Prompt(ChaosGame game) =>
        (LeaveKind)DeviationBehaviourTests.Field("_leavePrompt").GetValue(game)!;
}
