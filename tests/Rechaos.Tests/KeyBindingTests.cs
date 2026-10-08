using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>DEV-UI-024: the rebindable single-key shortcuts and their local store.</summary>
public sealed class KeyBindingTests : IDisposable
{
    private readonly DirectoryInfo _directory =
        Directory.CreateTempSubdirectory("rechaos-keybindings-");

    private string StorePath => Path.Combine(_directory.FullName, "keybindings.json");

    /// <summary>
    /// The shortcuts every released format version lists. A version's list never changes once
    /// released, because files written under it are read with it; a change to the shortcut list
    /// is a new version, added here and in <see cref="KeyBindingFormat"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, Keys[]> PinnedVersions = new Dictionary<int, Keys[]>
    {
        [1] =
        [
            Keys.A, Keys.B, Keys.Back, Keys.C, Keys.D, Keys.D1, Keys.D2, Keys.D3,
            Keys.Delete, Keys.Down, Keys.E, Keys.End, Keys.Enter, Keys.Escape,
            Keys.Execute, Keys.F, Keys.F1, Keys.F5, Keys.F6, Keys.F9, Keys.F10,
            Keys.F11, Keys.F12, Keys.G, Keys.H, Keys.Home, Keys.I, Keys.J,
            Keys.K, Keys.L, Keys.Left, Keys.M, Keys.N, Keys.O, Keys.OemMinus,
            Keys.OemPlus, Keys.PageDown, Keys.PageUp, Keys.R, Keys.Right,
            Keys.S, Keys.Space, Keys.T, Keys.Tab, Keys.Up, Keys.V, Keys.W,
            Keys.X
        ]
    };

    [Fact]
    public void ReassigningOccupiedKeySwapsShortcutsWithoutCreatingDuplicateTriggers()
    {
        var map = KeyBindingMap.Default();

        Assert.True(map.Assign(Keys.F1, Keys.F5));

        Assert.Equal(Keys.F5, map.Physical(Keys.F1));
        Assert.Equal(Keys.F1, map.Physical(Keys.F5));
        AssertOneKeyPerShortcut(map);
        Assert.False(map.Assign(Keys.Enter, Keys.LeftControl));
        Assert.Equal(Keys.Enter, map.Physical(Keys.Enter));
    }

    [Fact]
    public void EveryFormatVersionKeepsItsPinnedShortcutList()
    {
        // #318: the shortcut list is the current version's list, so it cannot change without a
        // new version, and a released version's list cannot change at all.
        Assert.Equal(Enumerable.Range(1, KeyBindingFormat.CurrentVersion), KeyBindingFormat.Versions);
        Assert.Equal(PinnedVersions.Keys.Order(), KeyBindingFormat.Versions);
        foreach (var (version, keys) in PinnedVersions)
            Assert.Equal(keys, KeyBindingFormat.ShortcutsAt(version));
        Assert.Equal(KeyBindingFormat.ShortcutsAt(KeyBindingFormat.CurrentVersion), KeyBindingMap.LogicalKeys);
        Assert.Equal(KeyBindingMap.LogicalKeys.Count, KeyBindingMap.LogicalKeys.Distinct().Count());
        Assert.Equal(KeyBindingFormat.CurrentVersion, StoredKeyBindings.CurrentFormatVersion);
    }

    [Fact]
    public void TheScreensReadNoShortcutKeyTheMapDoesNotList()
    {
        // A key read through the bindings but missing from the map would still act on its
        // printed key, and could then trigger two shortcuts at once. A listed key nothing reads
        // would be a dead row in the editor.
        var source = Path.Combine(AppContext.BaseDirectory, "game-source");
        var calls = new Regex(
            @"(?<![A-Za-z])(?:Pressed\((?:keyboard|current), |Pressed\(|Bound\(bindings, )Keys\.([A-Za-z0-9]+)\)");
        var read = new HashSet<Keys>();
        foreach (var file in Directory.EnumerateFiles(source, "*.cs"))
            foreach (Match match in calls.Matches(File.ReadAllText(file)))
                read.Add(Enum.Parse<Keys>(match.Groups[1].Value));
        read.UnionWith(AttackCommandLayout.ConfirmKeys);
        read.UnionWith((Keys[])typeof(ChaosGame)
            .GetField("TitleShortcutKeys", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!);

        Assert.Equal(KeyBindingMap.LogicalKeys.Order(), read.Order());
    }

    [Fact]
    public void EveryShortcutSaysWhatItDoesInTheFontsCharacters()
    {
        // #316: each row of the Keys editor names what its shortcut does on every screen that
        // reads it, within the room the editor gives it.
        foreach (var key in KeyBindingMap.LogicalKeys)
        {
            var description = KeyBindingDescriptions.Of(key);
            Assert.NotNull(description);
            Assert.InRange(description.Summary.Length, 1, KeyBindingsLayout.SummaryCharacters);
            AssertPrintable(description.Summary);
            Assert.InRange(description.Uses.Count, 1, KeyBindingsLayout.DetailLines);
            foreach (var use in description.Uses)
            {
                Assert.InRange(use.ToString().Length, 1, KeyBindingsLayout.DetailCharacters);
                AssertPrintable(use.ToString());
            }
            var name = KeyBindingDescriptions.KeyName(key);
            Assert.InRange(name.Length, 1, KeyBindingsLayout.KeyNameCharacters);
            AssertPrintable(name);
        }
        Assert.Equal("-", KeyBindingDescriptions.KeyName(Keys.OemMinus));
        Assert.Equal("BACKSPACE", KeyBindingDescriptions.KeyName(Keys.Back));
        Assert.Equal("BACKSLASH", KeyBindingDescriptions.KeyName(Keys.OemPipe));
    }

    [Fact]
    public void EscapeCancelsAKeyCaptureAndEscapesShortcutStaysMovable()
    {
        // #317: Escape stops the capture instead of becoming the binding.
        Assert.Equal(KeyCaptureResult.Cancel, KeyCapture.Read([Keys.Escape]));
        Assert.Equal(KeyCaptureResult.Cancel, KeyCapture.Read([Keys.A, Keys.Escape]));
        Assert.Equal(KeyCaptureResult.Waiting, KeyCapture.Read([]));
        Assert.Equal(KeyCaptureResult.Bind, KeyCapture.Read([Keys.F5]));
        Assert.Equal(KeyCaptureResult.TooManyKeys, KeyCapture.Read([Keys.A, Keys.B]));

        // Escape's shortcut moves like any other, and the physical Escape then goes to the
        // shortcut that held the new key; binding that one back to F5 swaps Escape home.
        var map = KeyBindingMap.Default();
        Assert.True(map.Assign(Keys.Escape, Keys.F5));
        Assert.Equal(Keys.F5, map.Physical(Keys.Escape));
        Assert.Equal(Keys.Escape, map.Physical(Keys.F5));
        Assert.True(map.Assign(Keys.F5, Keys.F5));
        Assert.Equal(Keys.Escape, map.Physical(Keys.Escape));
    }

    [Fact]
    public void ACaptureShowsWhyItRefusedAKey()
    {
        // DEV-UI-024: a capture that refuses a modifier or two keys at once stays open, and the
        // status line says why while keeping the way out on screen.
        Assert.Equal("PRESS A KEY   ESC, RIGHT-CLICK OR CANCEL STOPS",
            KeyBindingsLayout.StatusLine(capturing: true, string.Empty));
        Assert.Equal("KEY UNAVAILABLE   ESC, RIGHT-CLICK OR CANCEL STOPS",
            KeyBindingsLayout.StatusLine(capturing: true, "KEY UNAVAILABLE"));
        Assert.Equal("PRESS ONE KEY   ESC, RIGHT-CLICK OR CANCEL STOPS",
            KeyBindingsLayout.StatusLine(capturing: true, "PRESS ONE KEY"));
        Assert.Equal("UP/DOWN SELECT   ENTER CHANGES   ESC BACK",
            KeyBindingsLayout.StatusLine(capturing: false, string.Empty));
        Assert.Equal("KEY SAVED", KeyBindingsLayout.StatusLine(capturing: false, "KEY SAVED"));
        AssertPrintable(KeyBindingsLayout.StatusLine(capturing: true, "KEY UNAVAILABLE"));
    }

    [Fact]
    public void FullMapRoundTripsAndMalformedFilesRestoreDefaults()
    {
        var map = KeyBindingMap.Default();
        Assert.True(map.Assign(Keys.F1, Keys.F5));
        Assert.True(KeyBindingStore.TrySave(StorePath, map));
        var loaded = KeyBindingStore.Load(StorePath);
        Assert.Equal(KeyBindingSource.Current, loaded.Source);
        Assert.Equal(Keys.F5, loaded.Map.Physical(Keys.F1));

        void AssertDefaults(string json)
        {
            File.WriteAllText(StorePath, json);
            var result = KeyBindingStore.Load(StorePath);
            Assert.Equal(KeyBindingSource.Defaults, result.Source);
            Assert.Equal(Keys.F1, result.Map.Physical(Keys.F1));
        }

        AssertDefaults("{\"FormatVersion\":1,\"Entries\":[]}");
        AssertDefaults("{\"FormatVersion\":0,\"Entries\":[]}");
        var duplicate = map.Entries().ToArray();
        duplicate[1] = duplicate[1] with { Physical = duplicate[0].Physical };
        AssertDefaults(JsonSerializer.Serialize(new StoredKeyBindings(1, duplicate)));
        var missing = map.Entries().Skip(1).Append(new KeyBinding(Keys.Q, Keys.Q)).ToArray();
        AssertDefaults(JsonSerializer.Serialize(new StoredKeyBindings(1, missing)));
        var modifier = map.Entries().ToArray();
        modifier[0] = modifier[0] with { Physical = Keys.LeftShift };
        AssertDefaults(JsonSerializer.Serialize(new StoredKeyBindings(1, modifier)));
        AssertDefaults("invalid json");
        Assert.Equal(KeyBindingSource.Defaults,
            KeyBindingStore.Load(Path.Combine(_directory.FullName, "absent.json")).Source);
    }

    [Fact]
    public void MigrationKeepsStoredBindingsAndPlacesUnlistedShortcutsByTheSwapRule()
    {
        // #318: an older file lists fewer shortcuts. Each stored binding is made in turn on this
        // build's defaults, as the editor would make it.
        var olderShortcuts = KeyBindingMap.LogicalKeys.Where(key => key != Keys.X).ToArray();

        // The new shortcut's default key is free: it keeps it.
        var untouched = KeyBindingMap.Migrate(olderShortcuts.Select(key => new KeyBinding(key, key)));
        Assert.Equal(Keys.X, untouched.Physical(Keys.X));

        // A stored binding holds the new shortcut's default key: the new shortcut takes the key
        // that stored shortcut had, its default.
        var clash = KeyBindingMap.Migrate(olderShortcuts.Select(key =>
            new KeyBinding(key, key == Keys.G ? Keys.X : key)));
        Assert.Equal(Keys.X, clash.Physical(Keys.G));
        Assert.Equal(Keys.G, clash.Physical(Keys.X));
        AssertOneKeyPerShortcut(clash);

        // A chain: H took G first, which moved G's shortcut onto H; G's later binding to X then
        // hands the new shortcut the key G held at that point, H.
        var stored = olderShortcuts.Select(key => key switch
        {
            Keys.H => new KeyBinding(Keys.H, Keys.G),
            Keys.G => new KeyBinding(Keys.G, Keys.X),
            _ => new KeyBinding(key, key)
        }).OrderBy(entry => entry.Logical == Keys.G ? 1 : 0).ToArray();
        var chain = KeyBindingMap.Migrate(stored);
        Assert.Equal(Keys.G, chain.Physical(Keys.H));
        Assert.Equal(Keys.X, chain.Physical(Keys.G));
        Assert.Equal(Keys.H, chain.Physical(Keys.X));
        AssertOneKeyPerShortcut(chain);

        // A shortcut this build no longer has is dropped and leaves its key free.
        var removed = KeyBindingMap.Migrate(KeyBindingMap.LogicalKeys
            .Select(key => new KeyBinding(key, key == Keys.T ? Keys.Q : key))
            .Append(new KeyBinding(Keys.Z, Keys.T)));
        Assert.Equal(Keys.Q, removed.Physical(Keys.T));
        Assert.DoesNotContain(removed.Entries(), entry => entry.Physical == Keys.T);
        AssertOneKeyPerShortcut(removed);
    }

    [Fact]
    public void ANewerBuildsFileLendsItsSharedBindingsAndIsNeverOverwritten()
    {
        var entries = KeyBindingMap.LogicalKeys
            .Select(key => new KeyBinding(key, key switch
            {
                Keys.F1 => Keys.F2,
                _ => key
            }))
            // A shortcut this build does not have, on a key this build's F2 does not clash with.
            .Append(new KeyBinding(Keys.Q, Keys.F1))
            .ToArray();
        var newer = JsonSerializer.Serialize(new StoredKeyBindings(KeyBindingFormat.CurrentVersion + 1, entries));
        File.WriteAllText(StorePath, newer);

        var loaded = KeyBindingStore.Load(StorePath);
        Assert.Equal(KeyBindingSource.NewerBuild, loaded.Source);
        Assert.Equal(Keys.F2, loaded.Map.Physical(Keys.F1));
        AssertOneKeyPerShortcut(loaded.Map);
        Assert.True(KeyBindingStore.IsFromNewerBuild(StorePath));

        Assert.True(loaded.Map.Assign(Keys.H, Keys.F3));
        Assert.False(KeyBindingStore.TrySave(StorePath, loaded.Map));
        Assert.Equal(newer, File.ReadAllText(StorePath));
    }

    [Fact]
    public void IdleGangConfirmationUsesTheReboundPhysicalKey()
    {
        var map = KeyBindingMap.Default();
        Assert.True(map.Assign(Keys.Enter, Keys.F5));

        Assert.Equal(IdleGangWarningChoice.Confirm,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(Keys.F5), default, map));
        Assert.Equal(IdleGangWarningChoice.None,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(Keys.Enter), default, map));
    }

    [Fact]
    public void ShellKeysFollowTheBindingsAndAltEnterStaysOnThePhysicalEnter()
    {
        var map = KeyBindingMap.Default();
        Assert.True(map.Assign(Keys.F1, Keys.F2));
        Assert.True(map.Assign(Keys.F11, Keys.F3));
        Assert.True(map.Assign(Keys.Enter, Keys.F4));

        Assert.Equal(ShellShortcut.Help, ShellWindow.ShortcutFor(new KeyboardState(Keys.F2), default, map));
        Assert.Equal(ShellShortcut.Credits,
            ShellWindow.ShortcutFor(new KeyboardState(Keys.F2, Keys.LeftShift), new KeyboardState(Keys.LeftShift), map));
        Assert.Equal(ShellShortcut.None, ShellWindow.ShortcutFor(new KeyboardState(Keys.F1), default, map));
        Assert.True(ShellWindow.TogglesFullscreen(new KeyboardState(Keys.F3), default, map));
        Assert.False(ShellWindow.TogglesFullscreen(new KeyboardState(Keys.F11), default, map));
        Assert.True(ShellWindow.TogglesFullscreen(
            new KeyboardState(Keys.Enter, Keys.LeftAlt), new KeyboardState(Keys.LeftAlt), map));
    }

    private static void AssertOneKeyPerShortcut(KeyBindingMap map)
    {
        var entries = map.Entries();
        Assert.Equal(KeyBindingMap.LogicalKeys, entries.Select(entry => entry.Logical));
        Assert.Equal(entries.Count, entries.Select(entry => entry.Physical).Distinct().Count());
        Assert.All(entries, entry => Assert.True(KeyBindingMap.CanAssign(entry.Physical)));
    }

    private static void AssertPrintable(string text) =>
        Assert.All(text, character => Assert.True(OriginalFontLayout.TryGlyph(character, out _),
            $"'{character}' in \"{text}\" has no glyph."));

    public void Dispose() => _directory.Delete(recursive: true);
}
