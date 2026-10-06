using System.Reflection;
using Microsoft.Xna.Framework;
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
        Assert.True(ClosingIsCancelled(game));
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
        Assert.False(ClosingIsCancelled(game));
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
            var match = NativeSaveSerializerTests.CreateMatch();
            var game = GameFor(match, new MatchActions(new MatchReplayRecorder(match)), directory.FullName);
            DeviationBehaviourTests.Call(game, "RequestLeave", LeaveKind.End);
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
        var match = NativeSaveSerializerTests.CreateMatch();
        var actions = new MatchActions(new MatchReplayRecorder(match));
        if (saved) actions.MarkSaved();
        return GameFor(match, actions, null);
    }

    /// <summary>A headless game on <paramref name="match"/>, with a save directory when one is given.</summary>
    internal static ChaosGame GameFor(MatchState match, MatchActions actions, string? saveDirectory)
    {
        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_state").SetValue(game, match);
        DeviationBehaviourTests.Field("_actions").SetValue(game, actions);
        DeviationBehaviourTests.Field("_saveName").SetValue(game, new TextField("SAVE NAME", 48));
        if (saveDirectory is null) return game;
        var autoSavePath = Path.Combine(saveDirectory, "autosave.json");
        DeviationBehaviourTests.Field("_definitions").SetValue(game, match.Definitions);
        DeviationBehaviourTests.Field("_saveDirectory").SetValue(game, saveDirectory);
        DeviationBehaviourTests.Field("_autoSavePath").SetValue(game, autoSavePath);
        DeviationBehaviourTests.Field("_autoSave").SetValue(game, new RollingAutoSave(autoSavePath, (_, _) => { }));
        DeviationBehaviourTests.Field("_saveSlots").SetValue(game, new SaveSlotSummary?[SaveSlotCatalog.BrowserRowCount]);
        return game;
    }

    /// <summary>Closes the window of a headless game and says whether the close was held back.</summary>
    internal static bool ClosingIsCancelled(ChaosGame game)
    {
        var args = new ExitingEventArgs();
        typeof(ChaosGame).GetMethod("OnExiting", BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(object), typeof(ExitingEventArgs)])!
            .Invoke(game, [game, args]);
        return args.Cancel;
    }

    /// <summary>Gives the prompt's answer; a leave the game takes reaches Exit, which a headless game cannot run.</summary>
    internal static void Answer(ChaosGame game, LeaveAnswer answer) =>
        IgnoringExit(() => DeviationBehaviourTests.Call(game, "AnswerLeavePrompt", answer));

    /// <summary>Reports the save Save First opened as written.</summary>
    internal static void SaveWritten(ChaosGame game) =>
        IgnoringExit(() => DeviationBehaviourTests.Call(game, "LeaveAfterSave"));

    /// <summary>Whether the game decided to close, which it marks before it calls Exit.</summary>
    internal static bool Left(ChaosGame game) =>
        (bool)DeviationBehaviourTests.Field("_exitConfirmed").GetValue(game)!;

    private static void IgnoringExit(Action action)
    {
        try
        {
            action();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is NullReferenceException)
        {
        }
    }

    private static LeaveKind Prompt(ChaosGame game) =>
        (LeaveKind)DeviationBehaviourTests.Field("_leavePrompt").GetValue(game)!;
}
