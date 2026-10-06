using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Checks that the rebuild does what the mandatory deviations of DEVIATIONS.md say it does in place
/// of the original's behaviour. These tests compare the rebuild with the deviation log, not with the
/// original, so they are listed in each deviation's Tests item and in no parity row.
/// </summary>
public sealed partial class DeviationBehaviourTests
{
    // DEV-GFX-001: the window opens at the largest whole multiple of the 640-by-460 drawing area, up
    // to 2, that fits in nine tenths of the display, and at least 1.
    [Theory]
    [InlineData(3840, 2160, 1280, 920)]
    [InlineData(1920, 1080, 1280, 920)]
    [InlineData(1366, 768, 640, 460)]
    [InlineData(1280, 1024, 640, 460)]
    [InlineData(800, 600, 640, 460)]
    [InlineData(640, 480, 640, 460)]
    public void TheWindowOpensAtTheLargestWholeMultipleUpToTwo(int displayWidth, int displayHeight, int width, int height)
    {
        Assert.Equal((width, height), ShellWindow.OpeningSize(displayWidth, displayHeight));
    }

    [Fact]
    public void WithNoDisplayModeTheWindowOpensAtTwiceTheArea()
    {
        // DEV-GFX-001
        Assert.Equal((1280, 920), ShellWindow.OpeningSize(null, null));
    }

    // DEV-GFX-001: a resized or full-screen window draws the area at the largest scale that fits,
    // whole or not, centred with bars on the two sides it does not fill.
    [Theory]
    [InlineData(1920, 1080, 1080f / 460, 1920, 1080)]
    [InlineData(1000, 1000, 1000f / 640, 1000, 1000)]
    [InlineData(1280, 920, 2f, 1280, 920)]
    public void AResizedWindowScalesTheAreaToFitAndLetterboxesIt(
        int viewportWidth, int viewportHeight, float scale, int expectedWidth, int expectedHeight)
    {
        var viewport = new Viewport(0, 0, viewportWidth, viewportHeight);
        var transform = VirtualInput.Transform(viewport);
        Assert.Equal(scale, transform.M11, 4);
        Assert.Equal(scale, transform.M22, 4);
        var drawn = VirtualInput.ToPhysical(viewport, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height));
        Assert.True(drawn.Width == expectedWidth || drawn.Height == expectedHeight);
        Assert.InRange(drawn.Width, 0, viewportWidth);
        Assert.InRange(drawn.Height, 0, viewportHeight);
        Assert.Equal(viewportWidth - drawn.Right, drawn.Left, 1d);
        Assert.Equal(viewportHeight - drawn.Bottom, drawn.Top, 1d);
        // A press on a bar is outside the area.
        if (drawn.Left > 0) Assert.False(VirtualInput.TryMap(viewport, new Point(drawn.Left - 1, viewportHeight / 2), out _));
        if (drawn.Top > 0) Assert.False(VirtualInput.TryMap(viewport, new Point(viewportWidth / 2, drawn.Top - 1), out _));
        Assert.True(VirtualInput.TryMap(viewport, drawn.Center, out var centre));
        Assert.Equal(new Point(320, 230), centre);
    }

    [Fact]
    public void TheWindowResizesSwitchesNoDisplayModeAndKeepsTickingWithoutFocus()
    {
        // DEV-GFX-001: the window can be resized, full screen keeps the desktop's mode, and losing
        // focus leaves the window open and ticking. The window's constructor hands these to MonoGame.
        Assert.True(ShellWindow.AllowsResizing);
        Assert.False(ShellWindow.SwitchesDisplayMode);
        Assert.Equal(TimeSpan.FromMilliseconds(20), ShellWindow.InactiveSleepTime);
        var read = ReadFields(Constructor());
        foreach (var name in new[]
                 {
                     nameof(ShellWindow.AllowsResizing), nameof(ShellWindow.SwitchesDisplayMode),
                     nameof(ShellWindow.InactiveSleepTime)
                 })
            Assert.Contains(typeof(ShellWindow).GetField(name)!, read);
        Assert.Contains(typeof(ShellWindow).GetMethod(nameof(ShellWindow.OpeningSize))!, Calls(Constructor()));
    }

    [Fact]
    public void FullScreenIsSwitchedWithF11OrAltEnter()
    {
        // DEV-GFX-001, DEV-UI-019: full screen is switched from the keyboard, not from a menu.
        Assert.True(ShellWindow.TogglesFullscreen(new KeyboardState(Keys.F11), new KeyboardState()));
        Assert.True(ShellWindow.TogglesFullscreen(new KeyboardState(Keys.Enter, Keys.LeftAlt), new KeyboardState(Keys.LeftAlt)));
        Assert.True(ShellWindow.AltEnter(new KeyboardState(Keys.Enter, Keys.RightAlt), new KeyboardState()));
        Assert.False(ShellWindow.TogglesFullscreen(new KeyboardState(Keys.Enter), new KeyboardState()));
        Assert.False(ShellWindow.TogglesFullscreen(new KeyboardState(Keys.F11), new KeyboardState(Keys.F11)));
        var update = Calls(typeof(ChaosGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!);
        Assert.Contains(typeof(ShellWindow).GetMethod(nameof(ShellWindow.TogglesFullscreen))!, update);
        Assert.Contains(typeof(ShellWindow).GetMethod(nameof(ShellWindow.ShortcutFor))!, update);
    }

    [Fact]
    public void TheGameDrawsOnlyThe16BitImageSet()
    {
        // DEV-UI-016: images come from the 16-bit set the extractor writes to images/. The 8-bit set
        // in images8/ and the palette in raw/data/CLT00002 are in the asset pack, but the game never
        // names either, and no preference picks the colour depth.
        foreach (var text in UserStrings(typeof(ChaosGame).Assembly))
        {
            Assert.DoesNotContain("images8", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CLT00002", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PX08", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("images", UserStrings(typeof(ChaosGame).Assembly));
        Assert.DoesNotContain(typeof(GamePreferences).GetProperties(), property =>
            property.Name.Contains("Colo", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Depth", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Bits", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Enum.GetNames<ChaosGame.TitleAction>(), name =>
            name.Contains("Colo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheMusicComesFromTheTrackFilesAndNoDriveIsLookedFor()
    {
        // DEV-AUDIO-001: the music is the GOG release's track files, and nothing asks for a CD
        // drive, a drive type or a disc.
        var root = Directory.CreateTempSubdirectory("rechaos-dev-audio-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "music"));
            File.WriteAllBytes(Path.Combine(root.FullName, "music", "track03.ogg"), []);
            Assert.Equal([Path.Combine(root.FullName, "music", "track03.ogg")],
                SoundtrackCatalog.FindAvailableTracks(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
        foreach (var assembly in RebuildAssemblies())
        {
            var references = References(assembly);
            Assert.DoesNotContain("System.IO.DriveInfo", references);
            Assert.DoesNotContain("System.Environment::GetLogicalDrives", references);
            Assert.DoesNotContain("System.IO.Directory::GetLogicalDrives", references);
        }
    }

    [Fact]
    public void F1OpensTheHelpAndShiftF1TheCredits()
    {
        // DEV-HELP-001, DEV-UI-019: F1 opens the rebuild's help viewer, Shift+F1 the credits.
        Assert.Equal(ShellShortcut.Help, ShellWindow.ShortcutFor(new KeyboardState(Keys.F1), new KeyboardState()));
        Assert.Equal(ShellShortcut.Credits,
            ShellWindow.ShortcutFor(new KeyboardState(Keys.F1, Keys.LeftShift), new KeyboardState(Keys.LeftShift)));
        Assert.Equal(ShellShortcut.Credits,
            ShellWindow.ShortcutFor(new KeyboardState(Keys.F1, Keys.RightShift), new KeyboardState(Keys.RightShift)));
        Assert.Contains(typeof(ChaosGame).GetMethod("OpenHelp", BindingFlags.Instance | BindingFlags.NonPublic)!,
            Calls(typeof(ChaosGame).GetMethod("RunTitleAction", BindingFlags.Instance | BindingFlags.NonPublic)!));
    }

    [Fact]
    public void TheTitleButtonsStandInForTheMenuBar()
    {
        // DEV-UI-019: the title screen's buttons stand in for the menu, and a press outside them
        // does nothing.
        Assert.Equal(ChaosGame.TitleAction.NewGame, ChaosGame.TitleActionAt(new Point(320, 300)));
        Assert.Equal(ChaosGame.TitleAction.LoadGame, ChaosGame.TitleActionAt(new Point(260, 350)));
        Assert.Equal(ChaosGame.TitleAction.Online, ChaosGame.TitleActionAt(new Point(370, 350)));
        Assert.Equal(ChaosGame.TitleAction.Options, ChaosGame.TitleActionAt(new Point(190, 390)));
        Assert.Equal(ChaosGame.TitleAction.Help, ChaosGame.TitleActionAt(new Point(270, 390)));
        Assert.Equal(ChaosGame.TitleAction.Intro, ChaosGame.TitleActionAt(new Point(360, 390)));
        Assert.Equal(ChaosGame.TitleAction.Quit, ChaosGame.TitleActionAt(new Point(440, 390)));
        Assert.Null(ChaosGame.TitleActionAt(new Point(20, 20)));
        Assert.Equal(ShellShortcut.Options, ShellWindow.ShortcutFor(new KeyboardState(Keys.O), new KeyboardState()));
    }

    [Fact]
    public void EscapeOpensThePauseMenuInAMatch()
    {
        // DEV-UI-011, DEV-UI-019: Escape is the shell's key for the pause menu, which holds saving,
        // loading and leaving the match.
        Assert.Equal(ShellShortcut.Escape, ShellWindow.ShortcutFor(new KeyboardState(Keys.Escape), new KeyboardState()));
        // A key held from the frame before is not pressed again.
        Assert.Equal(ShellShortcut.None,
            ShellWindow.ShortcutFor(new KeyboardState(Keys.Escape), new KeyboardState(Keys.Escape)));
        Assert.Contains(typeof(ChaosGame).GetMethod("OpenGameMenu", BindingFlags.Instance | BindingFlags.NonPublic)!,
            Calls(typeof(ChaosGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!));
        Assert.Equal(9, SaveSlotCatalog.SlotCount);
    }

    [Fact]
    public void InputIsReadSixtyTimesASecond()
    {
        // DEV-UI-018: the game ticks at a fixed 60 frames a second and reads the keyboard and the
        // mouse once each tick.
        // A sixtieth of a second rounded to the nearest tick, MonoGame's default frame time.
        Assert.Equal(TimeSpan.FromTicks(166_667), ShellWindow.FrameTime);
        Assert.Contains(typeof(ShellWindow).GetField(nameof(ShellWindow.FrameTime))!, ReadFields(Constructor()));
        var update = Calls(typeof(ChaosGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!);
        Assert.Contains(typeof(IGameShell).GetMethod(nameof(IGameShell.ReadKeyboard))!, update);
        Assert.Contains(typeof(IGameShell).GetMethod(nameof(IGameShell.ReadMouse))!, update);
        // The window reads the devices themselves.
        var window = typeof(ChaosGameWindow).GetInterfaceMap(typeof(IGameShell));
        MethodBase Implementation(string name) =>
            window.TargetMethods[Array.FindIndex(window.InterfaceMethods, method => method.Name == name)];
        Assert.Contains(typeof(Keyboard).GetMethod(nameof(Keyboard.GetState), Type.EmptyTypes)!,
            Calls(Implementation(nameof(IGameShell.ReadKeyboard))));
        Assert.Contains(typeof(Mouse).GetMethod(nameof(Mouse.GetState), Type.EmptyTypes)!,
            Calls(Implementation(nameof(IGameShell.ReadMouse))));
    }

    [Fact]
    public void ADoubleClickIsTwoPressesOnOneTargetWithin500Milliseconds()
    {
        // DEV-UI-018: two presses on the same target 500 ms apart or less make a double-click; a
        // press on another target, or a later one, starts over.
        Assert.Equal(TimeSpan.FromMilliseconds(500), CitySectorClickTracker.DoubleClickWindow);
        var clicks = new CitySectorClickTracker();
        Assert.False(clicks.Register(10, TimeSpan.FromMilliseconds(1000)));
        Assert.True(clicks.Register(10, TimeSpan.FromMilliseconds(1500)));
        Assert.False(clicks.Register(10, TimeSpan.FromMilliseconds(2000)));
        Assert.False(clicks.Register(10, TimeSpan.FromMilliseconds(2501)));
        Assert.False(clicks.Register(11, TimeSpan.FromMilliseconds(2600)));
        Assert.True(clicks.Register(11, TimeSpan.FromMilliseconds(2700)));
    }

    [Fact]
    public void OnlineTextFieldsTakeTheCharacterThePlatformDecoded()
    {
        // DEV-UI-018: an online text field takes the character the window's text event delivers,
        // so a key gives what the player's keyboard layout says, accents included.
        var field = new TextField("YOUR NAME", 32) { IsFocused = true };
        foreach (var character in "Zoë Ä")
            field.Type(character);
        Assert.Equal("Zoë Ä", field.Value);
    }

    /// <summary>The constructor of the window that hosts the game, which hands MonoGame its settings.</summary>
    private static ConstructorInfo Constructor() =>
        typeof(ChaosGameWindow).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderByDescending(constructor => constructor.GetParameters().Length).First();

    private static IEnumerable<Assembly> RebuildAssemblies() =>
    [
        typeof(ChaosGame).Assembly,
        typeof(MatchState).Assembly,
        typeof(Rechaos.Multiplayer.Protocol.MultiplayerProtocolVersion).Assembly
    ];

    /// <summary>The methods a method's body calls, resolved from its IL.</summary>
    internal static HashSet<MethodBase> Calls(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var calls = new HashSet<MethodBase>();
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] is not (0x28 or 0x6F or 0x73)) continue;
            try
            {
                if (method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)) is { } called) calls.Add(called);
            }
            catch (ArgumentException)
            {
            }
        }
        return calls;
    }

    /// <summary>The static fields a method's body reads, resolved from its IL.</summary>
    private static HashSet<FieldInfo> ReadFields(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var fields = new HashSet<FieldInfo>();
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x7E) continue;
            try
            {
                if (method.Module.ResolveField(BitConverter.ToInt32(il, i + 1)) is { } field) fields.Add(field);
            }
            catch (ArgumentException)
            {
            }
        }
        return fields;
    }

    /// <summary>The types and members an assembly references, as Namespace.Type and Namespace.Type::Member.</summary>
    internal static HashSet<string> References(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        string Name(TypeReferenceHandle handle)
        {
            var type = metadata.GetTypeReference(handle);
            return $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.TypeReferences) names.Add(Name(handle));
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind == HandleKind.TypeReference)
                names.Add($"{Name((TypeReferenceHandle)member.Parent)}::{metadata.GetString(member.Name)}");
        }
        return names;
    }

    /// <summary>The string literals an assembly's code holds.</summary>
    private static List<string> UserStrings(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var strings = new List<string>();
        for (var handle = MetadataTokens.UserStringHandle(1); !handle.IsNil; handle = metadata.GetNextHandle(handle))
            strings.Add(metadata.GetUserString(handle));
        return strings;
    }

    /// <summary>
    /// A game with no window, its collections and helper objects made and nothing loaded. It runs in
    /// the detached shell, whose Exit does nothing, and its effects go to a recorder.
    /// </summary>
    internal static ChaosGame HeadlessGame()
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        // No constructor or field initializer has run, so the interface-typed services are set here.
        Field("_shell").SetValue(game, DetachedShell.Instance);
        Field("_soundEffects").SetValue(game, new RecordingSoundEffects());
        foreach (var field in typeof(ChaosGame).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            var type = field.FieldType;
            if (type.IsValueType || type.IsAbstract || type.IsInterface || type == typeof(string)) continue;
            if (type.Namespace is not { } ns || !(ns.StartsWith("Rechaos", StringComparison.Ordinal)
                    || ns == "System.Collections.Generic")) continue;
            if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) is null)
                continue;
            try
            {
                field.SetValue(game, Activator.CreateInstance(type, nonPublic: true));
            }
            catch (TargetInvocationException)
            {
            }
        }
        return game;
    }

    internal static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ChaosGame), name);

    internal static object? Call(ChaosGame game, string name, params object?[] arguments) =>
        (typeof(ChaosGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(ChaosGame), name)).Invoke(game, arguments);
}
