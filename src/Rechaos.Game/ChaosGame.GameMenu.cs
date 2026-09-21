using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class GameMenuLayout
{
    public static Rectangle Panel => PanelWith(lines: 0, columns: 0);

    /// <summary>
    /// The menu panel, grown to hold the session lines drawn under the buttons.
    /// </summary>
    /// <remarks>
    /// It grows around the longest line rather than the line being cut short, because the point of
    /// putting a join code and a password on screen is that somebody reads them out, and half a
    /// password read out confidently is worse than none at all. Growth is symmetrical about the
    /// interface's centre line, which is where the panel and the buttons in it already sit.
    /// </remarks>
    public static Rectangle PanelWith(int lines, int columns)
    {
        var width = Math.Max(288, Math.Max(0, columns) * 6 + 32);
        return new Rectangle(
            (VirtualInput.Width - width) / 2, 68, width, 310 + Math.Max(0, lines) * SessionLineHeight);
    }

    /// <summary>The top of the nth session line, below the last button.</summary>
    public static int SessionLine(int index) => 364 + Math.Max(0, index) * SessionLineHeight;

    private const int SessionLineHeight = 16;

    public static Rectangle Resume => new(226, 124, 188, 32);
    public static Rectangle Save => new(226, 162, 188, 32);
    public static Rectangle Load => new(226, 200, 188, 32);
    public static Rectangle Options => new(226, 238, 188, 32);
    public static Rectangle ReportBug => new(226, 276, 188, 32);
    public static Rectangle QuitToMainMenu => new(226, 314, 188, 42);

    /// <summary>Entries in the order they are drawn, which is the order the cursor walks.</summary>
    public const int EntryCount = 6;

    /// <summary>The Options entry's position in keyboard navigation.</summary>
    public const int OptionsIndex = 3;

    /// <summary>Where the cursor rests when the bug report panel closes.</summary>
    public const int ReportBugIndex = 4;

    public static Rectangle ConfirmQuit => new(206, 284, 108, 34);
    public static Rectangle CancelQuit => new(326, 284, 108, 34);
    public static Rectangle BrowserPanel => new(36, 15, 568, 430);
    public static Rectangle UseSlot => new(374, 397, 92, 30);
    public static Rectangle CancelBrowser => new(478, 397, 92, 30);
    public static Rectangle NameField => new(116, 202, 408, 32);
    public static Rectangle ConfirmSave => new(206, 272, 108, 34);
    public static Rectangle CancelSave => new(326, 272, 108, 34);

    /// <summary>
    /// One browser row: nine manual slots and then the autosave.
    /// </summary>
    /// <remarks>
    /// The rows were pitched at 37 for nine of them. The autosave has to be reachable from the same
    /// browser (it was written every turn and nothing could load it), and ten rows at that pitch run
    /// into the buttons, so the pitch is 34 and the row 32 tall. The last row now ends at 386,
    /// clear of the button strip at 397.
    /// </remarks>
    public static Rectangle SlotRow(int slot)
    {
        if (slot is < 0 or >= SaveSlotCatalog.BrowserRowCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(58, 48 + slot * 34, 524, 32);
    }
}

internal enum SaveBrowserMode
{
    None,
    Save,
    Load
}

internal enum GameMenuAction
{
    Resume,
    Save,
    Load,
    Options,
    ReportBug,
    QuitToMainMenu
}

public sealed partial class ChaosGame
{
    private bool _gameMenuOpen;
    private bool _quitToMainMenuConfirmationOpen;
    private int _gameMenuCursor;
    private SaveBrowserMode _saveBrowserMode;
    private bool _saveBrowserFromTitle;
    private int _saveSlotCursor;
    /// <summary>The nine manual slots, then the autosave in <see cref="SaveSlotCatalog.AutoSaveRow"/>.</summary>
    private SaveSlotSummary?[] _saveSlots = new SaveSlotSummary?[SaveSlotCatalog.BrowserRowCount];
    private readonly TextField _saveName = new("SAVE NAME", 48);
    private bool _editingSaveName;

    private void OpenGameMenu()
    {
        CancelCurrentInteraction();
        _gameMenuOpen = true;
        _bugReportOpen = false;
        _quitToMainMenuConfirmationOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        _gameMenuCursor = 0;
        _planningTimer.Pause(_inputTime);
        _message = string.Empty;
    }

    private void CloseGameMenu()
    {
        _gameMenuOpen = false;
        _bugReportOpen = false;
        _quitToMainMenuConfirmationOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _planningTimer.Resume(_inputTime);
        _message = string.Empty;
    }

    private void OpenSaveBrowser(bool saving, bool fromTitle = false)
    {
        // The server owns the durable history of an online match.  A local snapshot could neither
        // be resumed by the other players nor safely become authoritative again.
        if (_session is not null) return;
        if (_definitions is null || saving && _state is null) return;
        _gameMenuOpen = true;
        _bugReportOpen = false;
        _saveBrowserFromTitle = fromTitle;
        _saveBrowserMode = saving ? SaveBrowserMode.Save : SaveBrowserMode.Load;
        _quitToMainMenuConfirmationOpen = false;
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _planningTimer.Pause(_inputTime);
        RefreshSaveSlots();
        _saveSlotCursor = saving ? 0
            : Math.Max(0, Array.FindIndex(_saveSlots, slot => slot is { IsPlayable: true }));
        _message = string.Empty;
    }

    private void RefreshSaveSlots()
    {
        if (_definitions is null) return;
        for (var slot = 0; slot < SaveSlotCatalog.SlotCount; slot++)
            _saveSlots[slot] = SaveSlotCatalog.Read(_saveDirectory, slot, _definitions);
        _saveSlots[SaveSlotCatalog.AutoSaveRow] =
            SaveSlotCatalog.ReadAutoSave(_autoSavePath, _definitions);
    }

    private void UpdateGameMenu(KeyboardState keyboard)
    {
        if (_bugReportOpen)
        {
            UpdateBugReport(keyboard);
            return;
        }
        if (_saveBrowserMode != SaveBrowserMode.None)
        {
            UpdateSaveBrowser(keyboard);
            return;
        }
        if (_quitToMainMenuConfirmationOpen)
        {
            UpdateQuitConfirmation(keyboard);
            return;
        }
        if (Pressed(keyboard, Keys.Up)) _gameMenuCursor = Math.Max(0, _gameMenuCursor - 1);
        if (Pressed(keyboard, Keys.Down))
            _gameMenuCursor = Math.Min(GameMenuButtons().Length - 1, _gameMenuCursor + 1);
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)) CloseGameMenu();
        else if (Pressed(keyboard, Keys.Enter)) ActivateGameMenuSelection();
    }

    private void UpdateSaveBrowser(KeyboardState keyboard)
    {
        if (_editingSaveName)
        {
            if (Pressed(keyboard, Keys.Escape)) CancelSaveName();
            else if (Pressed(keyboard, Keys.Enter)) SaveSelectedSlot();
            return;
        }
        if (Pressed(keyboard, Keys.Up)) _saveSlotCursor = Math.Max(0, _saveSlotCursor - 1);
        if (Pressed(keyboard, Keys.Down))
            _saveSlotCursor = Math.Min(LastSelectableSlotRow, _saveSlotCursor + 1);
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)) CloseSaveBrowser();
        else if (Pressed(keyboard, Keys.Enter)) UseSelectedSlot();
    }

    private void UpdateQuitConfirmation(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Right))
            _gameMenuCursor = _gameMenuCursor == 0 ? 1 : 0;
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back))
            CancelQuitToMainMenu();
        else if (Pressed(keyboard, Keys.Enter))
        {
            if (_gameMenuCursor == 0) QuitToMainMenu();
            else CancelQuitToMainMenu();
        }
    }

    private void ActivateGameMenuSelection()
    {
        ActivateGameMenuAction(GameMenuButtons()[_gameMenuCursor].Action);
    }

    private void ActivateGameMenuAction(GameMenuAction action)
    {
        switch (action)
        {
            case GameMenuAction.Resume: CloseGameMenu(); break;
            case GameMenuAction.Save: OpenSaveBrowser(saving: true); break;
            case GameMenuAction.Load: OpenSaveBrowser(saving: false); break;
            case GameMenuAction.Options: OpenOptionsFromGameMenu(); break;
            case GameMenuAction.ReportBug: OpenBugReport(); break;
            case GameMenuAction.QuitToMainMenu: OpenQuitToMainMenuConfirmation(); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    /// <summary>The autosave row is read-only, so Save mode stops at the ninth slot.</summary>
    private int LastSelectableSlotRow => _saveBrowserMode == SaveBrowserMode.Save
        ? SaveSlotCatalog.SlotCount - 1
        : SaveSlotCatalog.BrowserRowCount - 1;

    private void UseSelectedSlot()
    {
        if (_saveBrowserMode == SaveBrowserMode.Save)
        {
            if (_saveSlotCursor >= SaveSlotCatalog.SlotCount)
            {
                _message = "THE AUTOSAVE CANNOT BE WRITTEN BY HAND";
                return;
            }
            _saveName.Set(_state is null ? string.Empty : SaveSlotCatalog.SuggestedName(_state));
            _saveName.IsFocused = true;
            _editingSaveName = true;
            _message = string.Empty;
            return;
        }
        switch (_saveSlots[_saveSlotCursor])
        {
            case null:
                _message = _saveSlotCursor == SaveSlotCatalog.AutoSaveRow
                    ? "NO AUTOSAVE YET"
                    : "EMPTY SLOT";
                return;
            case { Status: SaveSlotStatus.Incompatible }:
                _message = "SAVED BY ANOTHER BUILD";
                return;
            case { Status: SaveSlotStatus.Unreadable }:
                _message = "SAVE CANNOT BE READ";
                return;
        }
        var loaded = _saveSlotCursor == SaveSlotCatalog.AutoSaveRow
            ? LoadGameFromAutoSave()
            : LoadGameFromSlot(_saveSlotCursor);
        if (loaded) CloseSaveBrowserAfterLoad();
    }

    private void SaveSelectedSlot()
    {
        if (SaveGameToSlot(_saveSlotCursor, _saveName.Value) is not { } summary) return;
        _saveSlots[_saveSlotCursor] = summary;
        _editingSaveName = false;
        _saveName.IsFocused = false;
    }

    private void CancelSaveName()
    {
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _message = string.Empty;
    }

    private void CloseSaveBrowser()
    {
        _saveBrowserMode = SaveBrowserMode.None;
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _message = string.Empty;
        if (_saveBrowserFromTitle) _gameMenuOpen = false;
    }

    private void CloseSaveBrowserAfterLoad()
    {
        _gameMenuOpen = false;
        _bugReportOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        _editingSaveName = false;
        _saveName.IsFocused = false;
    }

    private void HandleGameMenuClick(Point point)
    {
        if (_bugReportOpen)
        {
            HandleBugReportClick(point);
            return;
        }
        if (_saveBrowserMode != SaveBrowserMode.None)
        {
            HandleSaveBrowserClick(point);
            return;
        }
        if (_quitToMainMenuConfirmationOpen)
        {
            if (GameMenuLayout.ConfirmQuit.Contains(point)) QuitToMainMenu();
            else if (GameMenuLayout.CancelQuit.Contains(point)) CancelQuitToMainMenu();
            return;
        }
        foreach (var button in GameMenuButtons())
            if (button.Bounds.Contains(point))
            {
                ActivateGameMenuAction(button.Action);
                return;
            }
    }

    private void HandleSaveBrowserClick(Point point)
    {
        if (_editingSaveName)
        {
            if (GameMenuLayout.ConfirmSave.Contains(point)) SaveSelectedSlot();
            else if (GameMenuLayout.CancelSave.Contains(point)) CancelSaveName();
            return;
        }
        var slot = HitTest.IndexAt(LastSelectableSlotRow + 1, GameMenuLayout.SlotRow, point);
        if (slot >= 0) _saveSlotCursor = slot;
        else if (GameMenuLayout.UseSlot.Contains(point)) UseSelectedSlot();
        else if (GameMenuLayout.CancelBrowser.Contains(point)) CloseSaveBrowser();
    }

    private void OpenQuitToMainMenuConfirmation()
    {
        _quitToMainMenuConfirmationOpen = true;
        _gameMenuCursor = 1;
        _message = string.Empty;
    }

    private void CancelQuitToMainMenu()
    {
        _quitToMainMenuConfirmationOpen = false;
        _gameMenuCursor = GameMenuButtons().Length - 1;
        _message = string.Empty;
    }

    private void QuitToMainMenu()
    {
        _gameMenuOpen = false;
        _bugReportOpen = false;
        _quitToMainMenuConfirmationOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        StopPlanningTimer();
        ResetTransientMatchUi();
        if (_session is not null)
        {
            LeaveOnlineMatch();
            return;
        }
        _state = null;
        _actions = null;
        _message = string.Empty;
        _screens.Show(ClientScreen.Title);
    }

    private void DrawGameMenu(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (!_gameMenuOpen) return;
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height),
            new Color(0, 0, 0, 185));
        if (_bugReportOpen)
        {
            DrawBugReport(batch, pixel, font);
            return;
        }
        if (_saveBrowserMode != SaveBrowserMode.None)
        {
            DrawSaveBrowser(batch, pixel, font);
            return;
        }
        IReadOnlyList<string> session =
            _quitToMainMenuConfirmationOpen ? [] : OnlineSessionLines();
        var panel = GameMenuLayout.PanelWith(
            session.Count, session.Count == 0 ? 0 : session.Max(line => line.Length));
        batch.Draw(pixel, panel, new Color(12, 20, 20, 250));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        if (_quitToMainMenuConfirmationOpen)
        {
            DrawCentered(font, batch, "QUIT TO MAIN MENU?", 132, Color.Gold, 2);
            if (_session is null)
            {
                DrawCentered(font, batch, "YOUR CURRENT GAME WILL BE LOST", 190, Color.White, 1);
                DrawCentered(font, batch, "IF NOT SAVED.", 207, Color.White, 1);
            }
            else
            {
                DrawCentered(font, batch, "SERVER SAVES EACH TURN.", 178, Color.White, 1);
                DrawCentered(font, batch, "RESUME IT LATER FROM ONLINE.", 195, Color.White, 1);
                DrawCentered(font, batch, "AVAILABLE UNTIL COMPLETED OR EXPIRED.", 212, Color.White, 1);
            }
            DrawButton(batch, pixel, font, GameMenuLayout.ConfirmQuit, "QUIT", _gameMenuCursor == 0);
            DrawButton(batch, pixel, font, GameMenuLayout.CancelQuit, "CANCEL", _gameMenuCursor == 1);
            return;
        }
        DrawCentered(font, batch, "GAME MENU", 82, Color.Gold, 2);
        var buttons = GameMenuButtons();
        for (var index = 0; index < buttons.Length; index++)
            DrawButton(batch, pixel, font, buttons[index].Bounds, buttons[index].Label,
                _gameMenuCursor == index);
        for (var index = 0; index < session.Count; index++)
            DrawCentered(font, batch, session[index], GameMenuLayout.SessionLine(index),
                Color.Gold, 1);
    }

    /// <summary>
    /// What an online match is reached by, for the player holding the menu open.
    /// </summary>
    /// <remarks>
    /// The lobby screen is where the join code was read out, and the match replaces it, so a host
    /// asked for the code on turn nine had nowhere left to look it up and no way to invite a late
    /// joiner. The password is here for the same reason and under the same rule: what is shown is
    /// what this client was actually seated with, so a session opened without one shows none rather
    /// than whatever the connect screen was last left holding.
    /// </remarks>
    private List<string> OnlineSessionLines()
    {
        var lines = new List<string>(2);
        if (_session is null) return lines;
        if (_online.JoinCodeShown.Length > 0) lines.Add($"JOIN CODE  {_online.JoinCodeShown}");
        if (_online.PasswordShown.Length > 0) lines.Add($"PASSWORD  {_online.PasswordShown}");
        return lines;
    }

    private (Rectangle Bounds, string Label, GameMenuAction Action)[] GameMenuButtons() =>
        _session is null
            ?
            [
                (GameMenuLayout.Resume, "RESUME", GameMenuAction.Resume),
                (GameMenuLayout.Save, "SAVE GAME", GameMenuAction.Save),
                (GameMenuLayout.Load, "LOAD GAME", GameMenuAction.Load),
                (GameMenuLayout.Options, "OPTIONS", GameMenuAction.Options),
                (GameMenuLayout.ReportBug, "REPORT BUG", GameMenuAction.ReportBug),
                (GameMenuLayout.QuitToMainMenu, "QUIT TO MAIN MENU", GameMenuAction.QuitToMainMenu)
            ]
            :
            [
                (GameMenuLayout.Resume, "RESUME", GameMenuAction.Resume),
                (GameMenuLayout.Options, "OPTIONS", GameMenuAction.Options),
                (GameMenuLayout.ReportBug, "REPORT BUG", GameMenuAction.ReportBug),
                (GameMenuLayout.QuitToMainMenu, "QUIT TO MAIN MENU", GameMenuAction.QuitToMainMenu)
            ];

    private void DrawSaveBrowser(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_editingSaveName)
        {
            DrawSaveNameEditor(batch, pixel, font);
            return;
        }
        var panel = GameMenuLayout.BrowserPanel;
        batch.Draw(pixel, panel, new Color(12, 20, 20, 252));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch,
            _saveBrowserMode == SaveBrowserMode.Save ? "SAVE GAME" : "LOAD GAME",
            27, Color.Gold, 2);
        for (var slot = 0; slot <= LastSelectableSlotRow; slot++)
        {
            var row = GameMenuLayout.SlotRow(slot);
            batch.Draw(pixel, row, slot == _saveSlotCursor
                ? new Color(72, 54, 18, 235) : new Color(24, 37, 39, 235));
            DrawBorder(batch, pixel, row, slot == _saveSlotCursor ? Color.Gold : Color.Gray, 1);
            var summary = _saveSlots[slot];
            var label = slot == SaveSlotCatalog.AutoSaveRow ? "A." : $"{slot + 1}.";
            font.Draw(batch, summary is null ? $"{label} EMPTY" : $"{label} {summary.Name}",
                new Vector2(row.X + 7, row.Y + 3), Color.White, 1);
            if (summary is not null)
                font.Draw(batch, summary.Details, new Vector2(row.X + 22, row.Y + 18),
                    summary.IsPlayable ? Color.LightGray : Color.Orange, 1);
        }
        DrawButton(batch, pixel, font, GameMenuLayout.UseSlot,
            _saveBrowserMode == SaveBrowserMode.Save ? "SELECT" : "LOAD", true);
        DrawButton(batch, pixel, font, GameMenuLayout.CancelBrowser, "CANCEL", false);
        if (!string.IsNullOrEmpty(_message))
            font.Draw(batch, _message, new Vector2(68, 407), Color.White, 1);
    }

    private void DrawSaveNameEditor(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var panel = GameMenuLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 20, 20, 252));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, $"SAVE IN SLOT {_saveSlotCursor + 1}", 118, Color.Gold, 2);
        DrawCentered(font, batch, "EDIT THE SUGGESTED NAME", 170, Color.White, 1);
        batch.Draw(pixel, GameMenuLayout.NameField, Color.Black);
        DrawBorder(batch, pixel, GameMenuLayout.NameField, Color.Gold, 1);
        font.Draw(batch, _saveName.Display,
            new Vector2(GameMenuLayout.NameField.X + 7, GameMenuLayout.NameField.Y + 12),
            Color.White, 1);
        DrawButton(batch, pixel, font, GameMenuLayout.ConfirmSave, "SAVE", true);
        DrawButton(batch, pixel, font, GameMenuLayout.CancelSave, "CANCEL", false);
        if (_saveSlots[_saveSlotCursor] is not null)
            DrawCentered(font, batch, "THIS WILL OVERWRITE THE EXISTING SAVE.", 246, Color.Orange, 1);
        if (!string.IsNullOrEmpty(_message))
            DrawCentered(font, batch, _message, 330, Color.White, 1);
    }
}
