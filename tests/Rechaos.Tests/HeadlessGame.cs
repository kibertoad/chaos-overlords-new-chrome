using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;

namespace Rechaos.Tests;

/// <summary>
/// A <see cref="ChaosGame"/> driven the way its window drives it, without a window, a graphics
/// device or an audio device.
/// </summary>
/// <remarks>
/// <para>
/// The game runs its own <c>Update</c> on every tick, with the keyboard and the pointer this harness
/// holds, at the time of this harness's clock, which moves one frame per tick unless a test moves it
/// further. Nothing is called behind the game's back: a test presses keys and buttons, moves the
/// clock and reads what the game shows through <c>ChaosGame.Observation.cs</c>, so a change that
/// stops the game from making a call the rules require fails here even when the helper it calls
/// still passes its own tests.
/// </para>
/// <para>
/// The game loads the original's data as it does at start, and no asset pack: it draws nothing and
/// plays no sound, and the effects it asks for are recorded in <see cref="Sounds"/>. Its user data
/// folder is a temporary one, written with the preferences the test starts from, and the run's
/// random sequence starts where the test says, so a match it begins is the same on every run. An
/// online test hands it the transport its calls go through.
/// </para>
/// </remarks>
internal sealed class HeadlessGame : IDisposable
{
    /// <summary>The time one tick moves the clock by, the window's fixed frame time.</summary>
    public static TimeSpan Frame => ShellWindow.FrameTime;

    /// <summary>The preferences a test starts from unless it says otherwise: the original's options, intro already seen.</summary>
    public static GamePreferences DefaultPreferences { get; } =
        GamePreferences.Default with { IntroMoviesSeen = true };

    private readonly Shell _shell = new();
    private readonly string _root;
    private bool _disposed;

    public HeadlessGame(
        GamePreferences? preferences = null,
        uint runRandomState = 1,
        HttpMessageHandler? multiplayerTransport = null)
    {
        _root = Path.Combine(Path.GetTempPath(), $"rechaos-headless-{Guid.NewGuid():N}");
        var assets = Directory.CreateDirectory(Path.Combine(_root, "assets")).FullName;
        UserData = Directory.CreateDirectory(Path.Combine(_root, "user")).FullName;
        if (!GamePreferencesStore.TrySave(
                Path.Combine(UserData, "preferences.json"), preferences ?? DefaultPreferences))
            throw new InvalidOperationException("The preferences could not be written.");
        Game = new ChaosGame(assets, new ChaosGameServices
        {
            UserDataDirectory = UserData,
            SoundEffects = Sounds,
            MultiplayerTransport = multiplayerTransport,
            RunRandomState = runRandomState,
        }, MatchDeviations.Original);
        Game.Attach(_shell);
        Game.LoadGameData();
    }

    public ChaosGame Game { get; }

    /// <summary>Every effect the game has asked to play, in order.</summary>
    public RecordingSoundEffects Sounds { get; } = new();

    /// <summary>The temporary user data folder: preferences, saves and recovery files.</summary>
    public string UserData { get; }

    /// <summary>The time the last tick ran at.</summary>
    public TimeSpan Now { get; private set; }

    /// <summary>Whether the game has asked the program to close.</summary>
    public bool ExitRequested => _shell.ExitRequested;

    /// <summary>
    /// Begins a local match from the title screen with Enter (New Game) and Enter (Begin), the
    /// setup it opens with: one human in slot 0 and the scenario and limit the preferences hold.
    /// </summary>
    public MatchState StartLocalMatch()
    {
        if (Game.CurrentScreen != ClientScreen.Title)
            throw new InvalidOperationException($"A local match starts from the title screen, not {Game.CurrentScreen}.");
        Press(Keys.Enter);
        if (Game.CurrentScreen != ClientScreen.Setup)
            throw new InvalidOperationException($"New Game opened {Game.CurrentScreen}, not the setup.");
        Press(Keys.Enter);
        return Game.Match ?? throw new InvalidOperationException("Begin started no match.");
    }

    /// <summary>Runs one update, <paramref name="elapsed"/> (one frame by default) after the last.</summary>
    public void Tick(TimeSpan? elapsed = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var step = elapsed ?? Frame;
        if (step < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        Now += step;
        Game.Update(new GameTime(Now, step));
    }

    /// <summary>Ticks frame by frame until <paramref name="duration"/> has passed.</summary>
    public void Advance(TimeSpan duration)
    {
        var until = Now + duration;
        while (Now < until) Tick(Frame < until - Now ? Frame : until - Now);
    }

    /// <summary>Moves the clock by <paramref name="duration"/> in a single tick, as a stalled frame would.</summary>
    public void Jump(TimeSpan duration) => Tick(duration);

    /// <summary>A key going down and, on the next tick, up again.</summary>
    public void Press(Keys key)
    {
        HoldKey(key);
        ReleaseKey(key);
    }

    public void Press(params Keys[] keys)
    {
        foreach (var key in keys) Press(key);
    }

    public void HoldKey(Keys key)
    {
        _shell.Keys.Add(key);
        Tick();
    }

    public void ReleaseKey(Keys key)
    {
        _shell.Keys.Remove(key);
        Tick();
    }

    /// <summary>Types text into whichever field has the caret, as the window's text events do.</summary>
    public void Type(string text)
    {
        foreach (var character in text) Game.HandleTextInput(character);
        Tick();
    }

    /// <summary>Moves the pointer to <paramref name="point"/> on the 640-by-460 virtual screen.</summary>
    public void MoveTo(Point point)
    {
        _shell.Pointer = point;
        Tick();
    }

    /// <summary>Moves the pointer to <paramref name="point"/> and presses the left button there.</summary>
    public void PressLeft(Point point)
    {
        _shell.Pointer = point;
        _shell.Left = ButtonState.Pressed;
        Tick();
    }

    /// <summary>Lets the left button come up where the pointer is.</summary>
    public void ReleaseLeft()
    {
        _shell.Left = ButtonState.Released;
        Tick();
    }

    public void Click(Point point)
    {
        PressLeft(point);
        ReleaseLeft();
    }

    public void PressRight(Point point)
    {
        _shell.Pointer = point;
        _shell.Right = ButtonState.Pressed;
        Tick();
    }

    public void ReleaseRight()
    {
        _shell.Right = ButtonState.Released;
        Tick();
    }

    /// <summary>
    /// Ticks until <paramref name="condition"/> holds, sleeping between ticks so work the game hands
    /// to background tasks (the online sessions) can arrive, or fails after <paramref name="patience"/>
    /// of wall-clock time, with what <paramref name="onTimeout"/> says about where the game stands.
    /// </summary>
    public void TickUntil(
        Func<bool> condition, string what, TimeSpan? patience = null, Func<string>? onTimeout = null)
    {
        var deadline = DateTime.UtcNow + (patience ?? TimeSpan.FromSeconds(30));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"{what} did not happen in time"
                    + (onTimeout is null ? string.Empty : $": {onTimeout()}"));
            Thread.Sleep(10);
            Tick();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            Game.UnloadContent();
        }
        finally
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The window the game believes it runs in: input the test holds, a full-size viewport.</summary>
    private sealed class Shell : IGameShell
    {
        public HashSet<Keys> Keys { get; } = [];
        public Point Pointer { get; set; } = new(-1, -1);
        public ButtonState Left { get; set; }
        public ButtonState Right { get; set; }
        public bool ExitRequested { get; private set; }

        public GraphicsDevice GraphicsDevice =>
            throw new InvalidOperationException("A headless game has no graphics device; nothing on an update may draw.");

        public Viewport Viewport => new(0, 0, VirtualInput.Width, VirtualInput.Height);

        public bool IsActive => true;

        public KeyboardState ReadKeyboard() => new([.. Keys]);

        public MouseState ReadMouse() => new(
            Pointer.X, Pointer.Y, 0, Left, ButtonState.Released, Right, ButtonState.Released,
            ButtonState.Released);

        public void ShowPointer(PointerShape shape)
        {
        }

        public void SetTitle(string title)
        {
        }

        public bool ToggleFullScreen() => false;

        public void Exit() => ExitRequested = true;
    }
}

/// <summary>The effects a <see cref="HeadlessGame"/> has asked for.</summary>
internal sealed class RecordingSoundEffects : ISoundEffectOutput
{
    private readonly List<PlayedSound> _played = [];

    public IReadOnlyList<PlayedSound> Played => _played;

    /// <summary>The general slots asked for, in order.</summary>
    public IEnumerable<int> GeneralSlots =>
        _played.Where(sound => sound.Cue.Bank == SoundEffectBank.General).Select(sound => sound.Cue.Index);

    /// <summary>Forgets what has been played so far, so a test can look at what one step plays.</summary>
    public void Clear() => _played.Clear();

    public void Play(SoundEffectCue cue, float volume) => _played.Add(new PlayedSound(cue, volume));

    public void SetVolume(float volume)
    {
    }

    public void Stop()
    {
    }
}

internal readonly record struct PlayedSound(SoundEffectCue Cue, float Volume);
