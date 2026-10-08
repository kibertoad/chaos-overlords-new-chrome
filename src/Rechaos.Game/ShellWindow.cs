using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>A key the rebuild's shell answers in place of the original's menu bar.</summary>
public enum ShellShortcut
{
    None,
    Help,
    Credits,
    Options,
    Escape
}

/// <summary>
/// The window the rebuild draws into and the keys it answers outside any screen, which stand in for
/// the original's window, menu bar and accelerators (DEV-GFX-001, DEV-UI-018, DEV-UI-019).
/// </summary>
public static class ShellWindow
{
    /// <summary>
    /// DEV-UI-018: input is read once per frame, 60 times a second. A second is not a whole number
    /// of ticks at 60 frames, so the frame is rounded to the nearest tick, 166667, which is also
    /// MonoGame's own default.
    /// </summary>
    public static readonly TimeSpan FrameTime = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 30) / 60);

    /// <summary>DEV-GFX-001: the window can be resized; the drawing area is scaled to fit it.</summary>
    public static readonly bool AllowsResizing = true;

    /// <summary>
    /// DEV-GFX-001: full screen is a borderless window at the desktop's mode; the display mode is
    /// never switched.
    /// </summary>
    public static readonly bool SwitchesDisplayMode = false;

    /// <summary>
    /// DEV-GFX-001: the window keeps ticking when it loses focus, sleeping this long between
    /// ticks. The original minimizes itself instead.
    /// </summary>
    public static readonly TimeSpan InactiveSleepTime = TimeSpan.FromMilliseconds(20);

    private const int PreferredScale = 2;

    /// <summary>
    /// DEV-GFX-001: the size the window opens at, the largest whole multiple of the 640-by-460
    /// drawing area, up to 2, that fits in nine tenths of the display, and at least 1.
    /// </summary>
    /// <remarks>
    /// Whole multiples keep the pixel art on exact pixel boundaries. Nine tenths of the display
    /// keeps the window clear of the taskbar and the title bar. With no display mode known the
    /// window opens at twice the area.
    /// </remarks>
    public static (int Width, int Height) OpeningSize(int? displayWidth, int? displayHeight)
    {
        if (displayWidth is not { } width || displayHeight is not { } height)
            return (VirtualInput.Width * PreferredScale, VirtualInput.Height * PreferredScale);
        var scale = Math.Min(width * 9 / 10 / VirtualInput.Width, height * 9 / 10 / VirtualInput.Height);
        scale = Math.Clamp(scale, 1, PreferredScale);
        return (VirtualInput.Width * scale, VirtualInput.Height * scale);
    }

    /// <summary>
    /// DEV-UI-019, DEV-HELP-001, DEV-UI-011: the key a frame answers outside the text fields.
    /// Shift+F1 opens the credits, F1 the help, O the Options screen and Escape the pause menu or
    /// whatever the screen showing gives it.
    /// </summary>
    /// <param name="keyboard">The keys held this frame.</param>
    /// <param name="previous">The keys held the frame before.</param>
    /// <param name="bindings">
    /// The player's key bindings (DEV-UI-024); F1, O and Escape are read on the keys they are bound
    /// to, and Shift is a chord on whichever key F1 is. Without a map the defaults apply.
    /// </param>
    public static ShellShortcut ShortcutFor(
        KeyboardState keyboard, KeyboardState previous, KeyBindingMap? bindings = null)
    {
        if (Pressed(keyboard, previous, Bound(bindings, Keys.F1)))
            return keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
                ? ShellShortcut.Credits
                : ShellShortcut.Help;
        if (Pressed(keyboard, previous, Bound(bindings, Keys.O))) return ShellShortcut.Options;
        if (Pressed(keyboard, previous, Bound(bindings, Keys.Escape))) return ShellShortcut.Escape;
        return ShellShortcut.None;
    }

    /// <summary>DEV-OPTIONS-003, DEV-UI-019: Alt+Enter went down this frame.</summary>
    public static bool AltEnter(KeyboardState keyboard, KeyboardState previous) =>
        Pressed(keyboard, previous, Keys.Enter)
        && (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));

    /// <summary>DEV-OPTIONS-003, DEV-UI-019: F11 or Alt+Enter switches full screen.</summary>
    /// <remarks>
    /// F11 is read on the key it is bound to (DEV-UI-024). Alt+Enter is a chord and stays on the
    /// physical Enter.
    /// </remarks>
    public static bool TogglesFullscreen(
        KeyboardState keyboard, KeyboardState previous, KeyBindingMap? bindings = null) =>
        Pressed(keyboard, previous, Bound(bindings, Keys.F11)) || AltEnter(keyboard, previous);

    private static Keys Bound(KeyBindingMap? bindings, Keys key) => bindings?.Physical(key) ?? key;

    private static bool Pressed(KeyboardState keyboard, KeyboardState previous, Keys key) =>
        keyboard.IsKeyDown(key) && !previous.IsKeyDown(key);
}
