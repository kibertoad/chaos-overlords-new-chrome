using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class KeyBindingTests : IDisposable
{
    private readonly DirectoryInfo _directory =
        Directory.CreateTempSubdirectory("rechaos-keybindings-");

    [Fact]
    public void ReassigningOccupiedKeySwapsShortcutsWithoutCreatingDuplicateTriggers()
    {
        var map = KeyBindingMap.Default();

        Assert.True(map.Assign(Keys.F1, Keys.F5));

        Assert.Equal(Keys.F5, map.Physical(Keys.F1));
        Assert.Equal(Keys.F1, map.Physical(Keys.F5));
        Assert.Equal(map.Entries().Count,
            map.Entries().Select(entry => entry.Physical).Distinct().Count());
        Assert.False(map.Assign(Keys.Enter, Keys.LeftControl));
        Assert.Equal(Keys.Enter, map.Physical(Keys.Enter));
    }

    [Fact]
    public void FullMapRoundTripsAndMalformedOrFutureFilesRestoreDefaults()
    {
        var path = Path.Combine(_directory.FullName, "keybindings.json");
        var map = KeyBindingMap.Default();
        Assert.True(map.Assign(Keys.F1, Keys.F5));
        Assert.True(KeyBindingStore.TrySave(path, map));
        Assert.Equal(Keys.F5, KeyBindingStore.LoadOrDefault(path).Physical(Keys.F1));

        File.WriteAllText(path, "{\"FormatVersion\":2,\"Entries\":[]}");
        Assert.Equal(Keys.F1, KeyBindingStore.LoadOrDefault(path).Physical(Keys.F1));
        File.WriteAllText(path, "{\"FormatVersion\":1,\"Entries\":[]}");
        Assert.Equal(Keys.F1, KeyBindingStore.LoadOrDefault(path).Physical(Keys.F1));
        var duplicate = map.Entries().Select(entry => entry with { }).ToArray();
        duplicate[1] = duplicate[1] with { Physical = duplicate[0].Physical };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
            new StoredKeyBindings(1, duplicate)));
        Assert.Equal(Keys.F1, KeyBindingStore.LoadOrDefault(path).Physical(Keys.F1));
        File.WriteAllText(path, "invalid json");
        Assert.Equal(Keys.F1, KeyBindingStore.LoadOrDefault(path).Physical(Keys.F1));
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

    public void Dispose() => _directory.Delete(recursive: true);
}
