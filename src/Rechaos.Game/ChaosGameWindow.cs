using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>
/// The MonoGame window <see cref="ChaosGame"/> runs in: it owns the graphics device, polls the
/// input devices and hands each tick, draw and window event to the game.
/// </summary>
public sealed class ChaosGameWindow : Microsoft.Xna.Framework.Game, IGameShell
{
    public const string Title = "Chaos Overlords: New Chrome";

    private readonly ChaosGame _game;
    private readonly GraphicsDeviceManager _graphics;

    public ChaosGameWindow(ChaosGame game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        // Two virtual pixels per device pixel is the size the interface was drawn for, but a
        // 1366x768 laptop or a 1080p panel at 150% scaling cannot show a 920-pixel-tall window, and
        // the DONE button ends up below the screen edge. Draw and input already letterbox from the
        // viewport, so any size works; the window just has to fit on the display it opens on.
        var (backBufferWidth, backBufferHeight) = game.DrawsReferenceFrame
            ? (VirtualInput.Width, VirtualInput.Height)
            : ShellWindow.OpeningSize(
                GraphicsAdapter.DefaultAdapter?.CurrentDisplayMode?.Width,
                GraphicsAdapter.DefaultAdapter?.CurrentDisplayMode?.Height);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = backBufferWidth,
            PreferredBackBufferHeight = backBufferHeight,
            SynchronizeWithVerticalRetrace = true,
            HardwareModeSwitch = ShellWindow.SwitchesDisplayMode,
            IsFullScreen = game.StartsFullScreen
        };
        // Ticking on without focus is what keeps background online notices, the planning timer and
        // the autosave serviced; the redraw is the expensive part, and BeginDraw spaces that out
        // instead. The sleep between inactive ticks doubles as the input poll gap, because every
        // edge comes from comparing consecutive polled snapshots, so it stays far shorter than a
        // click: a press and release that both landed inside one sleep would never be seen, and
        // the click that raises the window would be swallowed.
        InactiveSleepTime = ShellWindow.InactiveSleepTime;
        IsFixedTimeStep = true;
        TargetElapsedTime = ShellWindow.FrameTime;
        IsMouseVisible = true;
        Window.AllowUserResizing = ShellWindow.AllowsResizing;
        Window.Title = Title;
        // The only text the game takes: a server address, a name and a join code. The platform has
        // already decoded the keystroke, so a non-US layout types what it should.
        Window.TextInput += (_, args) => game.HandleTextInput(args.Character);
        game.Attach(this);
    }

    Viewport IGameShell.Viewport => GraphicsDevice.Viewport;

    KeyboardState IGameShell.ReadKeyboard() => Keyboard.GetState();

    MouseState IGameShell.ReadMouse() => Mouse.GetState();

    void IGameShell.ShowPointer(PointerShape shape) =>
        Mouse.SetCursor(shape == PointerShape.Hourglass ? MouseCursor.Wait : MouseCursor.Arrow);

    void IGameShell.SetTitle(string title) => Window.Title = title;

    bool IGameShell.ToggleFullScreen()
    {
        _graphics.ToggleFullScreen();
        return _graphics.IsFullScreen;
    }

    protected override void LoadContent()
    {
        _game.LoadContent();
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        _game.Update(gameTime);
        base.Update(gameTime);
    }

    protected override bool BeginDraw() => _game.ShouldDraw() && base.BeginDraw();

    protected override void Draw(GameTime gameTime)
    {
        _game.Draw(gameTime);
        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (!_game.ConfirmExit())
        {
            args.Cancel = true;
            return;
        }
        base.OnExiting(sender, args);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        base.OnActivated(sender, args);
        _game.Activated();
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        _game.Deactivated();
        base.OnDeactivated(sender, args);
    }

    protected override void UnloadContent()
    {
        _game.UnloadContent();
        base.UnloadContent();
    }
}

/// <summary>
/// The shell of a game no window runs yet: no device, no input, the focus held.
/// </summary>
internal sealed class DetachedShell : IGameShell
{
    public static DetachedShell Instance { get; } = new();

    private DetachedShell()
    {
    }

    public GraphicsDevice GraphicsDevice =>
        throw new InvalidOperationException("The game has no window to draw in.");

    public Viewport Viewport => new(0, 0, VirtualInput.Width, VirtualInput.Height);

    public bool IsActive => true;

    public KeyboardState ReadKeyboard() => default;

    public MouseState ReadMouse() => default;

    public void ShowPointer(PointerShape shape)
    {
    }

    public void SetTitle(string title)
    {
    }

    public bool ToggleFullScreen() => false;

    public void Exit()
    {
    }
}
