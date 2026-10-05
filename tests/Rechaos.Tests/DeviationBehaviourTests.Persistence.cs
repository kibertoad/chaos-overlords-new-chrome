using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class DeviationBehaviourTests
{
    [Fact]
    public void TheRebuildsSaveIsItsOwnVersionedFormatWithNoSelectedSector()
    {
        // DEV-SAVE-001: the rebuild's save is a document of its own with a version number, and it
        // keeps no player's selected sector.
        var match = NativeSaveSerializerTests.CreateMatch();
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        var document = JsonNode.Parse(stream.ToArray())!;
        Assert.NotNull(document["FormatVersion"] ?? document["formatVersion"]);
        foreach (var name in PropertyNames(document))
        {
            Assert.DoesNotContain("cursor", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("S40W", 45_305)]
    [InlineData("N40W", 45_329)]
    [InlineData("M10W", 16)]
    public void AnOriginalSaveIsNotRead(string marker, int size)
    {
        // DEV-SAVE-001: the rebuild never reads the original's save files. A file with the marker
        // and the size of each form of FMT-SAVE-001 and FMT-SAVE-002 is refused as data that is
        // not the rebuild's document, before any of its fields is read.
        var original = new byte[size];
        Encoding.ASCII.GetBytes(marker).CopyTo(original, 0);
        if (size > 16) Encoding.ASCII.GetBytes(marker).CopyTo(original, size - 4);
        var definitions = NativeSaveSerializerTests.CreateMatch().Definitions;
        var refused = Assert.Throws<InvalidDataException>(
            () => NativeSaveSerializer.Load(new MemoryStream(original), definitions));
        Assert.Equal("Native save JSON is invalid.", refused.Message);
    }

    [Fact]
    public void ALoadedMatchStartsEveryPlayerOnItsSlotZeroSector()
    {
        // DEV-SAVE-001: a loaded match, or an online match taken up, forgets every selection and starts
        // each player's planning on the sector of its roster slot 0, as a new match does.
        var match = NativeSaveSerializerTests.CreateMatch();
        var memory = new PlanningSelectionMemory();
        memory.Store(match.Players[0].Id, 40);
        memory.Store(match.Players[1].Id, 41);
        memory.Reset(match);
        Assert.Equal(match.Players[0].Gangs[0].SectorId, memory.For(match.Players[0].Id, -1));
        Assert.Equal(match.Players[1].Gangs[0].SectorId, memory.For(match.Players[1].Id, -1));
        var reset = typeof(PlanningSelectionMemory).GetMethod(nameof(PlanningSelectionMemory.Reset))!;
        Assert.Contains(reset, Calls(typeof(ChaosGame).GetMethod("ResetMatchPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!));
    }

    [Fact]
    public void OptionsAreWrittenToAPerUserFileInOneStep()
    {
        // DEV-OPTIONS-001: the options go to a file of the player's own, written whole and read
        // back, where the original never saves them.
        var directory = Directory.CreateTempSubdirectory("rechaos-dev-options-");
        try
        {
            var path = Path.Combine(directory.FullName, "preferences.json");
            var changed = GamePreferences.Default with { MusicVolumeLevel = 2, DetailedCombat = true };
            Assert.True(GamePreferencesStore.TrySave(path, changed));
            Assert.Equal(changed, GamePreferencesStore.LoadOrDefault(path));
            Assert.Equal([path], Directory.GetFiles(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AFileThatLacksAFieldGivesTheDefaultsAndAnOptionalFieldItsDefault()
    {
        // DEV-OPTIONS-001: a file that lacks one of the fields its version requires is read as the
        // defaults as a whole, so no option takes a value from another. Intro only once, which a
        // file of the current version may leave out, takes its own default and the rest is kept.
        var directory = Directory.CreateTempSubdirectory("rechaos-dev-options-");
        try
        {
            var path = Path.Combine(directory.FullName, "preferences.json");
            var changed = GamePreferences.Default with { MusicVolumeLevel = 2, IntroOnlyOnce = !GamePreferences.Default.IntroOnlyOnce };
            Assert.True(GamePreferencesStore.TrySave(path, changed));
            var full = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

            var lacking = full.DeepClone().AsObject();
            lacking.Remove(nameof(GamePreferences.SoundEffectVolumeLevel));
            File.WriteAllText(path, lacking.ToJsonString());
            Assert.Equal(GamePreferences.Default, GamePreferencesStore.LoadOrDefault(path));

            var optional = full.DeepClone().AsObject();
            optional.Remove(nameof(GamePreferences.IntroOnlyOnce));
            File.WriteAllText(path, optional.ToJsonString());
            var read = GamePreferencesStore.LoadOrDefault(path);
            Assert.Equal(2, read.MusicVolumeLevel);
            Assert.Equal(GamePreferences.Default.IntroOnlyOnce, read.IntroOnlyOnce);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ClosingTheWindowWritesTheAutosaveAndAsksNothing()
    {
        // DEV-UI-017: closing the window ends the program. Shutdown writes the rolling autosave
        // first, and nothing intercepts the close to offer a save.
        var unload = typeof(ChaosGame).GetMethod("UnloadContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Contains(typeof(ChaosGame).GetMethod("FlushAutoSaves", BindingFlags.Instance | BindingFlags.NonPublic)!, Calls(unload));
        foreach (var name in new[] { "OnExiting", "EndRun" })
            Assert.Null(typeof(ChaosGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        Assert.DoesNotContain("Microsoft.Xna.Framework.Game::add_Exiting", References(typeof(ChaosGame).Assembly));
    }

    [Fact]
    public void ASecondCopyRunsAndACommandLineFileIsNotOpened()
    {
        // DEV-UI-015: nothing stops a second copy from starting, since the rebuild takes no named
        // mutex or window search, and a save named on the command line is not taken as one.
        foreach (var assembly in RebuildAssemblies())
        {
            var references = References(assembly);
            Assert.DoesNotContain("System.Threading.Mutex", references);
            Assert.DoesNotContain("System.Diagnostics.Process::GetProcessesByName", references);
        }
        Assert.Null(ReferenceFrameRequest.ParseArguments([@"C:\Games\SAVE1.GAM"]));
    }

    [Fact]
    public void OnlinePlayUsesTheCoordinationServerAndNoSocketOfItsOwn()
    {
        // DEV-NET-001: online play talks to the coordination server over HTTP. The rebuild opens no
        // socket, modem line or serial port of its own.
        foreach (var assembly in RebuildAssemblies())
        {
            var references = References(assembly);
            // AddressFamily, which only tells an IPv6 address from an IPv4 one, is the namespace's
            // one type the rebuild names.
            foreach (var type in new[] { "Socket", "TcpClient", "TcpListener", "UdpClient", "NetworkStream" })
                Assert.DoesNotContain($"System.Net.Sockets.{type}", references);
            Assert.DoesNotContain(references, name => name.StartsWith("System.IO.Ports.", StringComparison.Ordinal));
        }
        Assert.Contains("System.Net.Http.HttpClient", References(typeof(Rechaos.Multiplayer.Protocol.MultiplayerProtocolVersion).Assembly)
            .Concat(References(typeof(ChaosGame).Assembly)));
    }

    [Fact]
    public void AnOnlineMatchThatEndsOpensTheAwardsWithNoFinalView()
    {
        // DEV-NET-001: an online match that ends goes straight to the awards. The original shows each
        // player a final view of the city first, as the rebuild's local matches do.
        var game = HeadlessGame();
        var final = NativeSaveSerializerTests.CreateMatch();
        Call(game, "ConcludeOnlineMatch", final);
        Assert.Equal(ClientScreen.Endgame, ((ScreenRouter)Field("_screens").GetValue(game)!).Current);
        Assert.Null(Field("_finalViewPlayer").GetValue(game));
        Assert.Empty((Queue<PlayerId>)Field("_pendingFinalViews").GetValue(game)!);
        Assert.Same(final, Field("_state").GetValue(game));
    }

    private static IEnumerable<string> PropertyNames(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var (name, child) in value)
                {
                    yield return name;
                    foreach (var nested in PropertyNames(child)) yield return nested;
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                foreach (var nested in PropertyNames(child))
                    yield return nested;
                break;
        }
    }
}
