using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>
/// What <see cref="ChaosGame"/> needs from the program it runs in: the window, the graphics
/// device, the input devices and the pointer.
/// </summary>
/// <remarks>
/// <see cref="ChaosGameWindow"/> is the shell the player gets, built on MonoGame. Tests drive the
/// same game through a shell of their own that hands it scripted input and has no window or
/// graphics device, so everything <see cref="ChaosGame"/> does on an update can be observed without
/// a display.
/// </remarks>
internal interface IGameShell
{
    /// <summary>The device the game draws and loads textures with. Only drawing and loading read it.</summary>
    GraphicsDevice GraphicsDevice { get; }

    /// <summary>The area the virtual screen is letterboxed into, which maps the pointer onto it.</summary>
    Viewport Viewport { get; }

    /// <summary>Whether the window has the focus.</summary>
    bool IsActive { get; }

    KeyboardState ReadKeyboard();

    MouseState ReadMouse();

    void ShowPointer(PointerShape shape);

    void SetTitle(string title);

    /// <summary>Switches between a window and the full screen; returns whether the full screen is on.</summary>
    bool ToggleFullScreen();

    /// <summary>Closes the program, after <see cref="ChaosGame.ConfirmExit"/> allows it.</summary>
    void Exit();
}

/// <summary>The services of the game that tests replace, and the defaults the program runs with.</summary>
internal sealed record ChaosGameServices
{
    /// <summary>The program's own: the player's user data folder, the audio device and the network.</summary>
    public static ChaosGameServices Desktop { get; } = new();

    /// <summary>
    /// Where preferences, saves and recovery files go; the player's local application data folder
    /// when null. A reference frame names its own.
    /// </summary>
    public string? UserDataDirectory { get; init; }

    /// <summary>Where the effects are played; the native player the assets load into when null.</summary>
    public ISoundEffectOutput? SoundEffects { get; init; }

    /// <summary>The transport every online call goes through; sockets when null.</summary>
    public HttpMessageHandler? MultiplayerTransport { get; init; }

    /// <summary>
    /// Where the run's random sequence starts (RULE-RNG-001, DEV-RNG-001); the clock when null.
    /// </summary>
    public uint? RunRandomState { get; init; }
}
