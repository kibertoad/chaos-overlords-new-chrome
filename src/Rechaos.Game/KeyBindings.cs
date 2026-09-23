using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public sealed record KeyBinding(Keys Logical, Keys Physical);

/// <summary>Single-key shortcuts used by the game's Pressed input path.</summary>
public sealed class KeyBindingMap
{
    public static IReadOnlyList<Keys> LogicalKeys { get; } =
    [
        Keys.A, Keys.B, Keys.Back, Keys.C, Keys.D, Keys.D1, Keys.D2, Keys.D3,
        Keys.Delete, Keys.Down, Keys.E, Keys.End, Keys.Enter, Keys.Escape,
        Keys.Execute, Keys.F, Keys.F1, Keys.F5, Keys.F6, Keys.F9, Keys.F10,
        Keys.F11, Keys.F12, Keys.G, Keys.H, Keys.Home, Keys.I, Keys.J,
        Keys.K, Keys.L, Keys.Left, Keys.M, Keys.N, Keys.O, Keys.OemMinus,
        Keys.OemPlus, Keys.PageDown, Keys.PageUp, Keys.R, Keys.Right,
        Keys.S, Keys.Space, Keys.T, Keys.Tab, Keys.Up, Keys.V, Keys.W,
        Keys.X
    ];

    private readonly Dictionary<Keys, Keys> _physical;

    private KeyBindingMap(Dictionary<Keys, Keys> physical) => _physical = physical;

    public static KeyBindingMap Default() =>
        new(LogicalKeys.ToDictionary(key => key, key => key));

    public Keys Physical(Keys logical) => _physical.GetValueOrDefault(logical, logical);

    public IReadOnlyList<KeyBinding> Entries() =>
        LogicalKeys.Select(key => new KeyBinding(key, Physical(key))).ToArray();

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

    public static bool TryCreate(IReadOnlyList<KeyBinding>? entries, out KeyBindingMap map)
    {
        map = Default();
        if (entries is null || entries.Count != LogicalKeys.Count) return false;
        var logical = new HashSet<Keys>();
        var physical = new HashSet<Keys>();
        foreach (var entry in entries)
        {
            if (entry is null || !LogicalKeys.Contains(entry.Logical)
                || !logical.Add(entry.Logical)
                || !CanAssign(entry.Physical) || !physical.Add(entry.Physical)) return false;
            map._physical[entry.Logical] = entry.Physical;
        }
        return true;
    }

    public static bool CanAssign(Keys key) => Enum.IsDefined(key) && key is not (Keys.None
        or Keys.LeftAlt or Keys.RightAlt or Keys.LeftControl or Keys.RightControl
        or Keys.LeftShift or Keys.RightShift or Keys.LeftWindows or Keys.RightWindows);
}

public sealed record StoredKeyBindings(int FormatVersion, IReadOnlyList<KeyBinding> Entries)
{
    public const int CurrentFormatVersion = 1;
}

public static class KeyBindingStore
{
    private const long MaximumBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static KeyBindingMap LoadOrDefault(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumBytes) return KeyBindingMap.Default();
            var stored = JsonSerializer.Deserialize<StoredKeyBindings>(File.ReadAllText(path));
            if (stored is { FormatVersion: StoredKeyBindings.CurrentFormatVersion }
                && KeyBindingMap.TryCreate(stored.Entries, out var map)) return map;
        }
        catch (Exception error) when (error is IOException or JsonException
                                      or UnauthorizedAccessException or ArgumentException)
        {
        }
        return KeyBindingMap.Default();
    }

    public static bool TrySave(string path, KeyBindingMap map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(map);
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(
                new StoredKeyBindings(StoredKeyBindings.CurrentFormatVersion, map.Entries()),
                JsonOptions));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temporary); } catch (IOException) { }
            return false;
        }
    }
}
