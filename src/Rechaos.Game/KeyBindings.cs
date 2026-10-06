using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>One shortcut, named by its default key, and the key that triggers it.</summary>
public sealed record KeyBinding(Keys Logical, Keys Physical);

/// <summary>
/// Which shortcuts each version of <c>keybindings.json</c> lists (DEV-UI-024). The list of the
/// current version is <see cref="KeyBindingMap.LogicalKeys"/>, so the shortcut list cannot change
/// without a new version, and a file of an older version is migrated rather than reset.
/// </summary>
/// <remarks>
/// The history is append-only. To add or remove a shortcut, add a version with the keys it adds
/// and removes and raise <see cref="CurrentVersion"/> to it; never edit a version that has been
/// released, because files written under it are read with its list. <c>KeyBindingTests</c> pins
/// every version's list.
/// </remarks>
public static class KeyBindingFormat
{
    /// <summary>The version this build writes.</summary>
    public const int CurrentVersion = 1;

    private sealed record Revision(int Version, Keys[] Added, Keys[] Removed);

    private static readonly Revision[] History =
    [
        new(1,
        [
            Keys.A, Keys.B, Keys.Back, Keys.C, Keys.D, Keys.D1, Keys.D2, Keys.D3,
            Keys.Delete, Keys.Down, Keys.E, Keys.End, Keys.Enter, Keys.Escape,
            Keys.Execute, Keys.F, Keys.F1, Keys.F5, Keys.F6, Keys.F9, Keys.F10,
            Keys.F11, Keys.F12, Keys.G, Keys.H, Keys.Home, Keys.I, Keys.J,
            Keys.K, Keys.L, Keys.Left, Keys.M, Keys.N, Keys.O, Keys.OemMinus,
            Keys.OemPlus, Keys.PageDown, Keys.PageUp, Keys.R, Keys.Right,
            Keys.S, Keys.Space, Keys.T, Keys.Tab, Keys.Up, Keys.V, Keys.W,
            Keys.X
        ], [])
    ];

    /// <summary>The versions in the history, oldest first.</summary>
    public static IReadOnlyList<int> Versions { get; } =
        History.Select(revision => revision.Version).ToArray();

    /// <summary>
    /// The shortcuts a file of <paramref name="version"/> lists, in the order the editor shows
    /// them: those of the version before, less the removed ones, then the added ones.
    /// </summary>
    public static IReadOnlyList<Keys> ShortcutsAt(int version)
    {
        if (version < 1 || version > CurrentVersion)
            throw new ArgumentOutOfRangeException(
                nameof(version), version, "Not a key binding version of this build.");
        var keys = new List<Keys>();
        foreach (var revision in History.TakeWhile(revision => revision.Version <= version))
        {
            keys.RemoveAll(revision.Removed.Contains);
            keys.AddRange(revision.Added);
        }
        return keys;
    }
}

/// <summary>Single-key shortcuts used by the game's Pressed input path (DEV-UI-024).</summary>
public sealed class KeyBindingMap
{
    /// <summary>The shortcuts of this build, each named by its default key.</summary>
    public static IReadOnlyList<Keys> LogicalKeys { get; } =
        KeyBindingFormat.ShortcutsAt(KeyBindingFormat.CurrentVersion);

    private readonly Dictionary<Keys, Keys> _physical;

    private KeyBindingMap(Dictionary<Keys, Keys> physical) => _physical = physical;

    public static KeyBindingMap Default() =>
        new(LogicalKeys.ToDictionary(key => key, key => key));

    public Keys Physical(Keys logical) => _physical.GetValueOrDefault(logical, logical);

    public IReadOnlyList<KeyBinding> Entries() =>
        LogicalKeys.Select(key => new KeyBinding(key, Physical(key))).ToArray();

    /// <summary>
    /// Binds <paramref name="logical"/> to <paramref name="physical"/>. The shortcut that held
    /// <paramref name="physical"/> takes the key <paramref name="logical"/> had, so no key ever
    /// triggers two shortcuts.
    /// </summary>
    public bool Assign(Keys logical, Keys physical)
    {
        if (!_physical.ContainsKey(logical) || !CanAssign(physical)) return false;
        var old = _physical[logical];
        var occupied = _physical.FirstOrDefault(pair => pair.Value == physical);
        if (occupied.Value == physical && occupied.Key != logical)
            _physical[occupied.Key] = old;
        _physical[logical] = physical;
        return true;
    }

    /// <summary>Builds the map a stored file of <paramref name="version"/> describes.</summary>
    /// <remarks>
    /// <para>
    /// A file of this build's version or an older one must list exactly the shortcuts of its
    /// version, each once, on distinct keys that can be bound; anything else is rejected. A newer
    /// build's file may list shortcuts this build does not have, which are skipped, and must put
    /// the ones it shares with this build on distinct keys that can be bound.
    /// </para>
    /// <para>
    /// The map starts from this build's defaults and binds each stored shortcut this build still
    /// has to its stored key, in the file's order, with the swap of <see cref="Assign"/>. The
    /// result is what the player would get by making each stored binding in the Keys editor after
    /// the update. Every stored binding is kept, because the stored keys are distinct. A shortcut
    /// the file does not list keeps its default key when no stored binding uses that key; when
    /// one does, it takes the key that stored shortcut had before its binding was made, which is
    /// that shortcut's default unless an earlier binding had already moved it. The keys of
    /// shortcuts this build no longer has are left free.
    /// </para>
    /// </remarks>
    public static bool TryCreate(int version, IReadOnlyList<KeyBinding>? entries, out KeyBindingMap map)
    {
        map = Default();
        if (entries is null || version < 1) return false;
        var listed = version <= KeyBindingFormat.CurrentVersion
            ? KeyBindingFormat.ShortcutsAt(version)
            : null;
        if (listed is not null && entries.Count != listed.Count) return false;
        var logical = new HashSet<Keys>();
        var physical = new HashSet<Keys>();
        var kept = new List<KeyBinding>();
        foreach (var entry in entries)
        {
            if (entry is null || !logical.Add(entry.Logical)) return false;
            if (listed is not null && !listed.Contains(entry.Logical)) return false;
            var shared = LogicalKeys.Contains(entry.Logical);
            // A newer build's shortcut this build does not have is skipped, key and all.
            if (listed is null && !shared) continue;
            if (!CanAssign(entry.Physical) || !physical.Add(entry.Physical)) return false;
            if (shared) kept.Add(entry);
        }
        map = Migrate(kept);
        return true;
    }

    /// <summary>
    /// This build's defaults with each of <paramref name="stored"/> bound in turn, as
    /// <see cref="TryCreate"/> describes; bindings of shortcuts this build does not have are
    /// skipped.
    /// </summary>
    public static KeyBindingMap Migrate(IEnumerable<KeyBinding> stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var map = Default();
        foreach (var entry in stored) map.Assign(entry.Logical, entry.Physical);
        return map;
    }

    public static bool CanAssign(Keys key) => Enum.IsDefined(key) && key is not (Keys.None
        or Keys.LeftAlt or Keys.RightAlt or Keys.LeftControl or Keys.RightControl
        or Keys.LeftShift or Keys.RightShift or Keys.LeftWindows or Keys.RightWindows);
}

public sealed record StoredKeyBindings(int FormatVersion, IReadOnlyList<KeyBinding> Entries)
{
    public const int CurrentFormatVersion = KeyBindingFormat.CurrentVersion;
}

/// <summary>Where the bindings a build started with came from.</summary>
public enum KeyBindingSource
{
    /// <summary>No file, or one that could not be read or did not describe a valid map.</summary>
    Defaults,
    /// <summary>A file of this build's version.</summary>
    Current,
    /// <summary>A file of an older version, migrated to this build's shortcuts.</summary>
    Migrated,
    /// <summary>A newer build's file, of which this build uses the shortcuts it shares.</summary>
    NewerBuild
}

public sealed record KeyBindingLoad(KeyBindingMap Map, KeyBindingSource Source);

public static class KeyBindingStore
{
    private const long MaximumBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static KeyBindingMap LoadOrDefault(string path) => Load(path).Map;

    /// <summary>
    /// Reads the bindings at <paramref name="path"/>, migrating an older version's file and taking
    /// the shared shortcuts from a newer build's (<see cref="KeyBindingMap.TryCreate"/>).
    /// </summary>
    public static KeyBindingLoad Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumBytes)
                return new(KeyBindingMap.Default(), KeyBindingSource.Defaults);
            var stored = JsonSerializer.Deserialize<StoredKeyBindings>(File.ReadAllText(path));
            if (stored is not null
                && KeyBindingMap.TryCreate(stored.FormatVersion, stored.Entries, out var map))
            {
                if (stored.FormatVersion > KeyBindingFormat.CurrentVersion)
                    return new(map, KeyBindingSource.NewerBuild);
                NewerBindings.NoteCurrent(path);
                return new(map, stored.FormatVersion == KeyBindingFormat.CurrentVersion
                    ? KeyBindingSource.Current
                    : KeyBindingSource.Migrated);
            }
        }
        catch (Exception error) when (error is IOException or JsonException
                                      or UnauthorizedAccessException or ArgumentException
                                      or NotSupportedException)
        {
        }
        return new(KeyBindingMap.Default(), KeyBindingSource.Defaults);
    }

    /// <summary>Writes the bindings atomically, unless a newer build owns the file.</summary>
    /// <remarks>
    /// A newer build's file may list shortcuts this build does not have, and writing this build's
    /// map over it would drop them. This build keeps its changes for the session instead.
    /// </remarks>
    public static bool TrySave(string path, KeyBindingMap map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(map);
        if (IsFromNewerBuild(path)) return false;
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(
                new StoredKeyBindings(StoredKeyBindings.CurrentFormatVersion, map.Entries()),
                JsonOptions));
            File.Move(temporary, path, overwrite: true);
            NewerBindings.NoteCurrent(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // A binding write failure must never interrupt the game.
            }
            return false;
        }
    }

    /// <summary>Whether the file at <paramref name="path"/> was written by a newer build.</summary>
    public static bool IsFromNewerBuild(string path) => NewerBindings.IsNewer(path, MaximumBytes);

    private static readonly NewerBuildFileGuard NewerBindings = new(
        nameof(StoredKeyBindings.FormatVersion), StoredKeyBindings.CurrentFormatVersion);
}
