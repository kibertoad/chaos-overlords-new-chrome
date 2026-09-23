using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class KeyBindingsLayout
{
    public const int VisibleRows = 16;
    public static Rectangle Panel => OptionsLayout.Panel;
    public static Rectangle Reset => new(120, 400, 112, 28);
    public static Rectangle Back => new(264, 400, 112, 28);
    public static Rectangle Capture => new(408, 400, 112, 28);
    public static Rectangle Row(int visibleRow) => new(120, 65 + visibleRow * 19, 400, 18);
}

public sealed partial class ChaosGame
{
    private bool _editingKeyBindings;
    private bool _capturingKeyBinding;
    private int _keyBindingCursor;
    private int _keyBindingOffset;
    private string _keyBindingStatus = string.Empty;

    private void OpenKeyBindings()
    {
        _editingKeyBindings = true;
        _capturingKeyBinding = false;
        _keyBindingCursor = 0;
        _keyBindingOffset = 0;
        _keyBindingStatus = string.Empty;
    }

    private void CloseKeyBindings()
    {
        _capturingKeyBinding = false;
        _editingKeyBindings = false;
        _keyBindingStatus = string.Empty;
    }

    private void UpdateKeyBindings(KeyboardState keyboard)
    {
        if (_capturingKeyBinding)
        {
            var pressed = keyboard.GetPressedKeys().Where(key => RawPressed(keyboard, key)).ToArray();
            if (pressed.Length == 0) return;
            if (pressed.Length > 1)
            {
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
                ? "KEY SAVED" : "KEY COULD NOT BE SAVED";
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
        if (RawPressed(keyboard, Keys.Enter)) _capturingKeyBinding = true;
    }

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
                ? "DEFAULT KEYS RESTORED" : "KEYS COULD NOT BE SAVED";
            return;
        }
        if (KeyBindingsLayout.Capture.Contains(point))
        {
            _capturingKeyBinding = !_capturingKeyBinding;
            _keyBindingStatus = string.Empty;
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
            if (index == _keyBindingCursor)
                batch.Draw(pixel, rectangle, new Color(72, 54, 18, 245));
            var logical = KeyBindingMap.LogicalKeys[index];
            var physical = _keyBindings.Physical(logical);
            font.Draw(batch, $"{logical,-15} {physical}".ToUpperInvariant(),
                new Vector2(rectangle.X + 8, rectangle.Y + 5),
                index == _keyBindingCursor ? Color.Gold : Color.White, 1);
        }
        var status = _capturingKeyBinding
            ? "PRESS ONE KEY   RIGHT-CLICK OR CANCEL TO STOP"
            : string.IsNullOrEmpty(_keyBindingStatus)
                ? "UP/DOWN SELECT   ENTER CHANGES   ESC BACK"
                : _keyBindingStatus;
        DrawCentered(font, batch, status, 378, Color.White, 1);
        DrawButton(batch, pixel, font, KeyBindingsLayout.Reset, "RESET", true);
        DrawButton(batch, pixel, font, KeyBindingsLayout.Back, "BACK", true);
        DrawButton(batch, pixel, font, KeyBindingsLayout.Capture,
            _capturingKeyBinding ? "CANCEL" : "CHANGE", true);
    }
}
