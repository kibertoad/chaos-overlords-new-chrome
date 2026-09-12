using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class GameMenuLayout
{
    public static Rectangle Panel => new(176, 92, 288, 276);
    public static Rectangle Resume => new(226, 132, 188, 32);
    public static Rectangle Save => new(226, 176, 188, 32);
    public static Rectangle Load => new(226, 220, 188, 32);
    public static Rectangle QuitToMainMenu => new(226, 280, 188, 42);
    public static Rectangle ConfirmQuit => new(206, 284, 108, 34);
    public static Rectangle CancelQuit => new(326, 284, 108, 34);
    public static Rectangle BrowserPanel => new(36, 15, 568, 430);
    public static Rectangle UseSlot => new(374, 397, 92, 30);
    public static Rectangle CancelBrowser => new(478, 397, 92, 30);
    public static Rectangle NameField => new(116, 202, 408, 32);
    public static Rectangle ConfirmSave => new(206, 272, 108, 34);
    public static Rectangle CancelSave => new(326, 272, 108, 34);

    public static Rectangle SlotRow(int slot)
    {
        if (slot is < 0 or >= SaveSlotCatalog.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(58, 48 + slot * 37, 524, 35);
    }
}

internal enum SaveBrowserMode
{
    None,
    Save,
    Load
}

public sealed partial class ChaosGame
{
    private bool _gameMenuOpen;
    private bool _quitToMainMenuConfirmationOpen;
    private int _gameMenuCursor;
    private SaveBrowserMode _saveBrowserMode;
    private bool _saveBrowserFromTitle;
    private int _saveSlotCursor;
    private SaveSlotSummary?[] _saveSlots = new SaveSlotSummary?[SaveSlotCatalog.SlotCount];
    private readonly TextField _saveName = new("SAVE NAME", 48);
    private bool _editingSaveName;

    private void OpenGameMenu()
    {
        CancelCurrentInteraction();
        _gameMenuOpen = true;
        _quitToMainMenuConfirmationOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        _gameMenuCursor = 0;
        _planningTimer.Pause(_inputTime);
        _message = string.Empty;
    }

    private void CloseGameMenu()
    {
        _gameMenuOpen = false;
        _quitToMainMenuConfirmationOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _planningTimer.Resume(_inputTime);
        _message = string.Empty;
    }

    private void OpenSaveBrowser(bool saving, bool fromTitle = false)
    {
        if (_definitions is null || saving && _state is null) return;
        _gameMenuOpen = true;
        _saveBrowserFromTitle = fromTitle;
        _saveBrowserMode = saving ? SaveBrowserMode.Save : SaveBrowserMode.Load;
        _quitToMainMenuConfirmationOpen = false;
        _editingSaveName = false;
        _saveName.IsFocused = false;
        _planningTimer.Pause(_inputTime);
        RefreshSaveSlots();
        _saveSlotCursor = saving ? 0
            : Math.Max(0, Array.FindIndex(_saveSlots, slot => slot is not null));
        _message = string.Empty;
    }

    private void RefreshSaveSlots()
    {
        if (_definitions is null) return;
        for (var slot = 0; slot < SaveSlotCatalog.SlotCount; slot++)
            _saveSlots[slot] = SaveSlotCatalog.Read(_saveDirectory, slot, _definitions);
    }

    private void UpdateGameMenu(KeyboardState keyboard)
    {
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
        if (Pressed(keyboard, Keys.Down)) _gameMenuCursor = Math.Min(3, _gameMenuCursor + 1);
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
            _saveSlotCursor = Math.Min(SaveSlotCatalog.SlotCount - 1, _saveSlotCursor + 1);
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
        switch (_gameMenuCursor)
        {
            case 0: CloseGameMenu(); break;
            case 1: OpenSaveBrowser(saving: true); break;
            case 2: OpenSaveBrowser(saving: false); break;
            case 3: OpenQuitToMainMenuConfirmation(); break;
        }
    }

    private void UseSelectedSlot()
    {
        if (_saveBrowserMode == SaveBrowserMode.Save)
        {
            _saveName.Set(_state is null ? string.Empty : SaveSlotCatalog.SuggestedName(_state));
            _saveName.IsFocused = true;
            _editingSaveName = true;
            _message = string.Empty;
        }
        else if (_saveSlots[_saveSlotCursor] is null) _message = "EMPTY SLOT";
        else if (LoadGameFromSlot(_saveSlotCursor)) CloseSaveBrowserAfterLoad();
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
        _saveBrowserMode = SaveBrowserMode.None;
        _editingSaveName = false;
        _saveName.IsFocused = false;
    }

    private void HandleGameMenuClick(Point point)
    {
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
        if (GameMenuLayout.Resume.Contains(point)) CloseGameMenu();
        else if (GameMenuLayout.Save.Contains(point)) OpenSaveBrowser(saving: true);
        else if (GameMenuLayout.Load.Contains(point)) OpenSaveBrowser(saving: false);
        else if (GameMenuLayout.QuitToMainMenu.Contains(point)) OpenQuitToMainMenuConfirmation();
    }

    private void HandleSaveBrowserClick(Point point)
    {
        if (_editingSaveName)
        {
            if (GameMenuLayout.ConfirmSave.Contains(point)) SaveSelectedSlot();
            else if (GameMenuLayout.CancelSave.Contains(point)) CancelSaveName();
            return;
        }
        var slot = Enumerable.Range(0, SaveSlotCatalog.SlotCount)
            .FirstOrDefault(index => GameMenuLayout.SlotRow(index).Contains(point), -1);
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
        _gameMenuCursor = 3;
        _message = string.Empty;
    }

    private void QuitToMainMenu()
    {
        _gameMenuOpen = false;
        _quitToMainMenuConfirmationOpen = false;
        _saveBrowserMode = SaveBrowserMode.None;
        StopPlanningTimer();
        _combatAnimationPlayer.Clear();
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
        if (_saveBrowserMode != SaveBrowserMode.None)
        {
            DrawSaveBrowser(batch, pixel, font);
            return;
        }
        var panel = GameMenuLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 20, 20, 250));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        if (_quitToMainMenuConfirmationOpen)
        {
            DrawCentered(font, batch, "QUIT TO MAIN MENU?", 132, Color.Gold, 2);
            DrawCentered(font, batch, "YOUR CURRENT GAME WILL BE LOST", 190, Color.White, 1);
            DrawCentered(font, batch, "IF NOT SAVED.", 207, Color.White, 1);
            DrawButton(batch, pixel, font, GameMenuLayout.ConfirmQuit, "QUIT", _gameMenuCursor == 0);
            DrawButton(batch, pixel, font, GameMenuLayout.CancelQuit, "CANCEL", _gameMenuCursor == 1);
            return;
        }
        DrawCentered(font, batch, "GAME MENU", 106, Color.Gold, 2);
        var buttons = new[]
        {
            (GameMenuLayout.Resume, "RESUME"), (GameMenuLayout.Save, "SAVE GAME"),
            (GameMenuLayout.Load, "LOAD GAME"),
            (GameMenuLayout.QuitToMainMenu, "QUIT TO MAIN MENU")
        };
        for (var index = 0; index < buttons.Length; index++)
            DrawButton(batch, pixel, font, buttons[index].Item1, buttons[index].Item2,
                _gameMenuCursor == index);
    }

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
        for (var slot = 0; slot < SaveSlotCatalog.SlotCount; slot++)
        {
            var row = GameMenuLayout.SlotRow(slot);
            batch.Draw(pixel, row, slot == _saveSlotCursor
                ? new Color(72, 54, 18, 235) : new Color(24, 37, 39, 235));
            DrawBorder(batch, pixel, row, slot == _saveSlotCursor ? Color.Gold : Color.Gray, 1);
            var summary = _saveSlots[slot];
            font.Draw(batch, summary is null ? $"{slot + 1}. EMPTY" : $"{slot + 1}. {summary.Name}",
                new Vector2(row.X + 7, row.Y + 4), Color.White, 1);
            if (summary is not null)
                font.Draw(batch, summary.Details, new Vector2(row.X + 22, row.Y + 20),
                    Color.LightGray, 1);
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
