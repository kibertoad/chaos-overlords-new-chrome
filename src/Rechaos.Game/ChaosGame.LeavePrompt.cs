using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>What the player asked to leave: the match (File, End) or the program (File, Exit).</summary>
internal enum LeaveKind
{
    None,
    End,
    Exit
}

/// <summary>The three answers of the save-first prompt, in the order the original numbers them.</summary>
internal enum LeaveAnswer
{
    SaveFirst,
    Cancel,
    LeaveWithoutSaving
}

public sealed partial class ChaosGame
{
    /// <summary>The leave the save-first prompt showing is for, or None when it is closed.</summary>
    private LeaveKind _leavePrompt;

    /// <summary>The leave that follows the save the prompt's Save First opened.</summary>
    private LeaveKind _leaveAfterSave;

    /// <summary>Set once the program may close without asking again.</summary>
    private bool _exitConfirmed;

    /// <summary>
    /// RULE-UI-015: a local match is in play and has changed since it was last saved or loaded.
    /// A finished match, whose end evaluation has run, is no longer in play.
    /// </summary>
    private bool MatchUnsaved =>
        _session is null && _state is { Outcome: null } && _actions is { IsSaved: false };

    /// <summary>
    /// RULE-UI-014, RULE-UI-015: closing the window is File, Exit, so it asks to save first while
    /// the match is unsaved and closes at once otherwise. The autosave is still written on the
    /// way out (UnloadContent).
    /// </summary>
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        // DEV-UI-026: the question is about the live match, and the save-first prompt is drawn
        // over it, so a replay shown at the time closes first.
        CloseReplayPlayback();
        if (!_exitConfirmed && MatchUnsaved)
        {
            args.Cancel = true;
            RequestLeave(LeaveKind.Exit);
            return;
        }
        base.OnExiting(sender, args);
    }

    /// <summary>
    /// RULE-UI-015: File, End (the menu's Quit to Main Menu) or File, Exit. While the match is
    /// unsaved the save-first prompt opens; otherwise the leave happens at once.
    /// </summary>
    private void RequestLeave(LeaveKind kind)
    {
        if (!MatchUnsaved)
        {
            Leave(kind);
            return;
        }
        if (!_gameMenuOpen) OpenGameMenu();
        _bugReportOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _quitToMainMenuConfirmationOpen = false;
        _leavePrompt = kind;
        _gameMenuCursor = (int)LeaveAnswer.SaveFirst;
        _message = string.Empty;
    }

    private void Leave(LeaveKind kind)
    {
        _leavePrompt = LeaveKind.None;
        _leaveAfterSave = LeaveKind.None;
        if (kind == LeaveKind.End)
        {
            QuitToMainMenu();
            return;
        }
        _exitConfirmed = true;
        Exit();
    }

    /// <summary>
    /// RULE-UI-015, FND-UI-058: Save First opens the save browser and leaves once a save is
    /// written; cancelling the browser returns to the match. Cancel returns to the match. The third
    /// answer leaves without saving.
    /// </summary>
    private void AnswerLeavePrompt(LeaveAnswer answer)
    {
        var kind = _leavePrompt;
        _leavePrompt = LeaveKind.None;
        switch (answer)
        {
            case LeaveAnswer.SaveFirst:
                OpenSaveBrowser(saving: true);
                _leaveAfterSave = kind;
                break;
            case LeaveAnswer.Cancel:
                CloseGameMenu();
                break;
            default:
                Leave(kind);
                break;
        }
    }

    /// <summary>Leaves after the save Save First asked for was written.</summary>
    private void LeaveAfterSave()
    {
        if (_leaveAfterSave != LeaveKind.None) Leave(_leaveAfterSave);
    }

    /// <summary>
    /// Whether closing the save browser ended a Save First, which returns to the match as a save
    /// the player cancels does in the original.
    /// </summary>
    private bool CancelLeaveAfterSave()
    {
        if (_leaveAfterSave == LeaveKind.None) return false;
        _leaveAfterSave = LeaveKind.None;
        CloseGameMenu();
        return true;
    }

    private void UpdateLeavePrompt(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left)) _gameMenuCursor = Math.Max(0, _gameMenuCursor - 1);
        if (Pressed(keyboard, Keys.Right)) _gameMenuCursor = Math.Min(2, _gameMenuCursor + 1);
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back))
            AnswerLeavePrompt(LeaveAnswer.Cancel);
        else if (Pressed(keyboard, Keys.Enter))
            AnswerLeavePrompt((LeaveAnswer)Math.Clamp(_gameMenuCursor, 0, 2));
    }

    private void HandleLeavePromptClick(Point point)
    {
        if (GameMenuLayout.SaveFirst.Contains(point)) AnswerLeavePrompt(LeaveAnswer.SaveFirst);
        else if (GameMenuLayout.CancelLeave.Contains(point)) AnswerLeavePrompt(LeaveAnswer.Cancel);
        else if (GameMenuLayout.LeaveWithoutSaving.Contains(point))
            AnswerLeavePrompt(LeaveAnswer.LeaveWithoutSaving);
    }

    private void DrawLeavePrompt(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var panel = GameMenuLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 20, 20, 250));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "GAME NOT SAVED", 132, Color.Gold, 2);
        DrawCentered(font, batch, "SAVE IT BEFORE YOU LEAVE?", 198, Color.White, 1);
        DrawButton(batch, pixel, font, GameMenuLayout.SaveFirst, "SAVE",
            _gameMenuCursor == (int)LeaveAnswer.SaveFirst);
        DrawButton(batch, pixel, font, GameMenuLayout.CancelLeave, "CANCEL",
            _gameMenuCursor == (int)LeaveAnswer.Cancel);
        DrawButton(batch, pixel, font, GameMenuLayout.LeaveWithoutSaving, "DON'T SAVE",
            _gameMenuCursor == (int)LeaveAnswer.LeaveWithoutSaving);
    }
}
