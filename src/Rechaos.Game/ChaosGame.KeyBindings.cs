using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class KeyBindingsLayout
{
    public const int VisibleRows = 11;
    /// <summary>The most screens a shortcut's description lists, one line each.</summary>
    public const int DetailLines = 8;
    /// <summary>The most characters of a row's description, after the two key columns.</summary>
    public const int SummaryCharacters = 40;
    /// <summary>The most characters of a key name in a row's key columns.</summary>
    public const int KeyNameCharacters = 10;
    /// <summary>The most characters of one line of the selected shortcut's description.</summary>
    public const int DetailCharacters = 74;
    public static Rectangle Panel => OptionsLayout.Panel;
    public static Rectangle Reset => new(120, 400, 112, 28);
    public static Rectangle Back => new(264, 400, 112, 28);
    public static Rectangle Capture => new(408, 400, 112, 28);
    public static Rectangle Row(int visibleRow) => new(120, 65 + visibleRow * 19, 400, 18);
    public static Point Detail(int line) => new(98, 281 + line * 11);

    /// <summary>
    /// The line under the selected shortcut's description. A capture that refused a key stays
    /// open, so its reason leads the capture prompt in place of "PRESS A KEY" until a key is bound
    /// or the capture stops.
    /// </summary>
    public static string StatusLine(bool capturing, string status) => capturing
        ? (string.IsNullOrEmpty(status) ? "PRESS A KEY" : status) + "   ESC, RIGHT-CLICK OR CANCEL STOPS"
        : string.IsNullOrEmpty(status)
            ? "UP/DOWN SELECT   ENTER CHANGES   ESC BACK"
            : status;
}

public sealed partial class ChaosGame
{
    private bool _editingKeyBindings;
    private bool _capturingKeyBinding;
    private int _keyBindingCursor;
    private int _keyBindingOffset;
    private string _keyBindingStatus = string.Empty;
    private KeyBindingSource _keyBindingSource;

    /// <summary>
    /// Whether the Keys panel is on screen. The flag alone outlives a forced exit from Options, such
    /// as the planning clock running out, and would keep F11 and F12 switched off elsewhere.
    /// </summary>
    private bool EditingKeyBindings =>
        _editingKeyBindings && _screens.Current == ClientScreen.Options;

    private void OpenKeyBindings()
    {
        _editingKeyBindings = true;
        _capturingKeyBinding = false;
        _keyBindingCursor = 0;
        _keyBindingOffset = 0;
        _keyBindingStatus = _keyBindingSource == KeyBindingSource.NewerBuild
            ? "KEYS FROM A NEWER VERSION: CHANGES ARE NOT SAVED"
            : string.Empty;
    }

    private void CloseKeyBindings()
    {
        _capturingKeyBinding = false;
        _editingKeyBindings = false;
        _keyBindingStatus = string.Empty;
    }

    private void CancelKeyCapture()
    {
        _capturingKeyBinding = false;
        _keyBindingStatus = "CHANGE CANCELLED";
    }

    private void UpdateKeyBindings(KeyboardState keyboard)
    {
        if (_capturingKeyBinding)
        {
            var pressed = keyboard.GetPressedKeys().Where(key => RawPressed(keyboard, key)).ToArray();
            switch (KeyCapture.Read(pressed))
            {
                case KeyCaptureResult.Waiting:
                    return;
                case KeyCaptureResult.Cancel:
                    CancelKeyCapture();
                    return;
                case KeyCaptureResult.TooManyKeys:
                    _keyBindingStatus = "PRESS ONE KEY";
                    return;
            }
            if (!_keyBindings.Assign(KeyBindingMap.LogicalKeys[_keyBindingCursor], pressed[0]))
            {
                _keyBindingStatus = "KEY UNAVAILABLE";
                return;
            }
            _capturingKeyBinding = false;
            _keyBindingStatus = KeyBindingStore.TrySave(_keyBindingsPath, _keyBindings)
                ? "KEY SAVED" : KeysNotSavedStatus();
            return;
        }

        if (RawPressed(keyboard, Keys.Escape) || RawPressed(keyboard, Keys.Back))
        {
            CloseKeyBindings();
            return;
        }
        if (RawPressed(keyboard, Keys.Up)) MoveKeyBindingCursor(-1);
        if (RawPressed(keyboard, Keys.Down)) MoveKeyBindingCursor(1);
        if (RawPressed(keyboard, Keys.PageUp)) MoveKeyBindingCursor(-KeyBindingsLayout.VisibleRows);
        if (RawPressed(keyboard, Keys.PageDown)) MoveKeyBindingCursor(KeyBindingsLayout.VisibleRows);
        if (RawPressed(keyboard, Keys.Home)) MoveKeyBindingCursor(-KeyBindingMap.LogicalKeys.Count);
        if (RawPressed(keyboard, Keys.End)) MoveKeyBindingCursor(KeyBindingMap.LogicalKeys.Count);
        if (RawPressed(keyboard, Keys.Enter)) StartKeyCapture();
    }

    private void StartKeyCapture()
    {
        _capturingKeyBinding = true;
        _keyBindingStatus = string.Empty;
    }

    private string KeysNotSavedStatus() => KeyBindingStore.IsFromNewerBuild(_keyBindingsPath)
        ? "NOT SAVED: A NEWER VERSION OWNS THE KEYS FILE"
        : "KEYS COULD NOT BE SAVED";

    private void MoveKeyBindingCursor(int delta)
    {
        _keyBindingCursor = Math.Clamp(_keyBindingCursor + delta,
            0, KeyBindingMap.LogicalKeys.Count - 1);
        if (_keyBindingCursor < _keyBindingOffset) _keyBindingOffset = _keyBindingCursor;
        if (_keyBindingCursor >= _keyBindingOffset + KeyBindingsLayout.VisibleRows)
            _keyBindingOffset = _keyBindingCursor - KeyBindingsLayout.VisibleRows + 1;
    }

    private void ScrollKeyBindings(int wheelDelta)
    {
        if (_capturingKeyBinding) return;
        _keyBindingOffset = Math.Clamp(
            _keyBindingOffset - HelpLayout.WheelSteps(wheelDelta) * 3,
            0, Math.Max(0, KeyBindingMap.LogicalKeys.Count - KeyBindingsLayout.VisibleRows));
        _keyBindingCursor = Math.Clamp(_keyBindingCursor, _keyBindingOffset,
            _keyBindingOffset + KeyBindingsLayout.VisibleRows - 1);
    }

    private void HandleKeyBindingsClick(Point point)
    {
        if (KeyBindingsLayout.Back.Contains(point))
        {
            CloseKeyBindings();
            return;
        }
        if (KeyBindingsLayout.Reset.Contains(point))
        {
            _keyBindings = KeyBindingMap.Default();
            _capturingKeyBinding = false;
            _keyBindingStatus = KeyBindingStore.TrySave(_keyBindingsPath, _keyBindings)
                ? "DEFAULT KEYS RESTORED" : KeysNotSavedStatus();
            return;
        }
        if (KeyBindingsLayout.Capture.Contains(point))
        {
            if (_capturingKeyBinding) CancelKeyCapture();
            else StartKeyCapture();
            return;
        }
        if (_capturingKeyBinding) return;
        for (var row = 0; row < KeyBindingsLayout.VisibleRows; row++)
        {
            if (_keyBindingOffset + row >= KeyBindingMap.LogicalKeys.Count) break;
            if (!KeyBindingsLayout.Row(row).Contains(point)) continue;
            _keyBindingCursor = _keyBindingOffset + row;
            _keyBindingStatus = string.Empty;
            break;
        }
    }

    private void DrawKeyBindings(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var background = ReturnScreenBackground(_optionsReturnScreen);
        if (background is not null)
            batch.Draw(background, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 190));
        batch.Draw(pixel, KeyBindingsLayout.Panel, new Color(15, 25, 28, 245));
        DrawBorder(batch, pixel, KeyBindingsLayout.Panel, new Color(80, 180, 130), 2);
        DrawCentered(font, batch, "KEY BINDINGS", 38, Color.Gold, 2);
        for (var row = 0; row < KeyBindingsLayout.VisibleRows; row++)
        {
            var index = _keyBindingOffset + row;
            if (index >= KeyBindingMap.LogicalKeys.Count) break;
            var rectangle = KeyBindingsLayout.Row(row);
            var selected = index == _keyBindingCursor;
            if (selected)
                batch.Draw(pixel, rectangle, new Color(72, 54, 18, 245));
            var logical = KeyBindingMap.LogicalKeys[index];
            var physical = _keyBindings.Physical(logical);
            var text = selected ? Color.Gold : Color.White;
            var x = rectangle.X + 8;
            var y = rectangle.Y + 5;
            font.Draw(batch, RowKeyName(logical), new Vector2(x, y), text, 1);
            // A key moved off its default stands out, so the player sees what was changed.
            font.Draw(batch, RowKeyName(physical), new Vector2(x + 66, y),
                physical == logical ? text : Color.LightGreen, 1);
            font.Draw(batch, KeyBindingDescriptions.Of(logical)?.Summary ?? string.Empty,
                new Vector2(x + 132, y), text, 1);
        }
        batch.Draw(pixel, new Rectangle(98, 276, 444, 1), new Color(80, 180, 130));
        var uses = KeyBindingDescriptions.Of(KeyBindingMap.LogicalKeys[_keyBindingCursor])?.Uses ?? [];
        for (var line = 0; line < uses.Count && line < KeyBindingsLayout.DetailLines; line++)
            font.Draw(batch, uses[line].ToString(), KeyBindingsLayout.Detail(line).ToVector2(),
                Color.LightGray, 1);
        DrawCentered(font, batch,
            KeyBindingsLayout.StatusLine(_capturingKeyBinding, _keyBindingStatus), 378, Color.White, 1);
        DrawButton(batch, pixel, font, KeyBindingsLayout.Reset, "RESET", true);
        DrawButton(batch, pixel, font, KeyBindingsLayout.Back, "BACK", true);
        DrawButton(batch, pixel, font, KeyBindingsLayout.Capture,
            _capturingKeyBinding ? "CANCEL" : "CHANGE", true);
    }

    private static string RowKeyName(Keys key)
    {
        var name = KeyBindingDescriptions.KeyName(key);
        return name.Length <= KeyBindingsLayout.KeyNameCharacters
            ? name
            : name[..KeyBindingsLayout.KeyNameCharacters];
    }

    /// <summary>Whether the shortcut <paramref name="key"/> went down this frame (DEV-UI-024).</summary>
    /// <remarks>
    /// A text editor reads typed characters from the physical keys, so while one has focus its
    /// editing keys are physical too. Otherwise a shortcut rebound to a letter would type that
    /// letter and also confirm, erase or move the caret.
    /// </remarks>
    private bool Pressed(KeyboardState current, Keys key) =>
        RawPressed(current, TextInputHasFocus() ? key : _keyBindings.Physical(key));

    private bool RawPressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}

/// <summary>What the keys that went down while the Keys editor waits for a key mean.</summary>
public enum KeyCaptureResult
{
    Waiting,
    /// <summary>Escape went down: the capture stops and nothing changes (DEV-UI-024).</summary>
    Cancel,
    TooManyKeys,
    /// <summary>One key other than Escape went down, and becomes the binding.</summary>
    Bind
}

public static class KeyCapture
{
    /// <summary>
    /// Escape cancels the capture rather than being bound, because it is the key players press to
    /// back out. The physical Escape can still be given to any shortcut: bind the shortcut that
    /// holds Escape to that shortcut's key, and the two swap.
    /// </summary>
    public static KeyCaptureResult Read(IReadOnlyCollection<Keys> pressed)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        if (pressed.Contains(Keys.Escape)) return KeyCaptureResult.Cancel;
        return pressed.Count switch
        {
            0 => KeyCaptureResult.Waiting,
            1 => KeyCaptureResult.Bind,
            _ => KeyCaptureResult.TooManyKeys
        };
    }
}
