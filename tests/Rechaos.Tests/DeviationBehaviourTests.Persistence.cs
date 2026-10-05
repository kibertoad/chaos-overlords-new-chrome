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
        // DEV-SAVE-001: the rebuild's save is a document of its own with a version number. The
        // selected sectors are kept beside it, in the save browser's file, so the match document
        // and its fingerprint hold none.
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

    [Fact]
    public void AnOriginalSaveIsNotRead()
    {
        // DEV-SAVE-001: the rebuild never reads the original's save files. A file in their binary
        // form is refused, here a buffer that opens with binary fields and zeros.
        var original = new byte[4096];
        original[0] = 0x01;
        original[2] = 0x06;
        var definitions = NativeSaveSerializerTests.CreateMatch().Definitions;
        Assert.ThrowsAny<Exception>(() => NativeSaveSerializer.Load(new MemoryStream(original), definitions));
    }

    [Fact]
    public void ASlotSaveKeepsEverySelectedSectorAndALoadRestoresThem()
    {
        // DEV-SAVE-001, FND-SAVE-003: the selected sectors go into the save browser's file beside
        // the save, and a load restores them. A save whose file no longer matches loads with every
        // player on the sector of its roster slot 0.
        var match = NativeSaveSerializerTests.CreateMatch();
        var memory = new PlanningSelectionMemory();
        memory.Reset(match);
        memory.Store(match.Players[0].Id, 40);
        memory.Store(match.Players[1].Id, 41);
        var directory = Directory.CreateTempSubdirectory("rechaos-dev-save-");
        try
        {
            SaveSlotCatalog.Save(directory.FullName, 0, "SELECTION", match, online: false,
                selectedSectors: memory.Snapshot());
            var path = SaveSlotCatalog.SavePath(directory.FullName, 0);
            var restored = new PlanningSelectionMemory();
            restored.Restore(match, SaveSlotCatalog.ReadSelectedSectors(path));
            Assert.Equal(40, restored.For(match.Players[0].Id, -1));
            Assert.Equal(41, restored.For(match.Players[1].Id, -1));

            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
            Assert.Null(SaveSlotCatalog.ReadSelectedSectors(path));
            restored.Restore(match, null);
            Assert.Equal(match.Players[0].Gangs[0].SectorId, restored.For(match.Players[0].Id, -1));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AnOnlineMatchTakenUpStartsEveryPlayerOnItsSlotZeroSector()
    {
        // DEV-SAVE-001: an online match the client takes up starts each player's planning on the
        // sector of its roster slot 0, since the server keeps no selection.
        var match = NativeSaveSerializerTests.CreateMatch();
        var memory = new PlanningSelectionMemory();
        memory.Store(match.Players[0].Id, 40);
        memory.Reset(match);
        Assert.Equal(match.Players[0].Gangs[0].SectorId, memory.For(match.Players[0].Id, -1));
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
    public void AFileThatLacksAFieldGivesTheDefaultsAndANewerFieldItsDefault()
    {
        // DEV-OPTIONS-001: a file that lacks one of the fields its version has is read as the
        // defaults as a whole, so no option takes a value from another. A field a later version
        // added, which an older file lacks, takes its own default.
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

            var older = full.DeepClone().AsObject();
            older.Remove(nameof(GamePreferences.IntroOnlyOnce));
            File.WriteAllText(path, older.ToJsonString());
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
    public void ASecondCopyRuns()
    {
        // DEV-UI-015: nothing stops a second copy from starting, since the rebuild takes no named
        // mutex or window search. A save named on the command line is still opened (RULE-UI-013).
        foreach (var assembly in RebuildAssemblies())
        {
            var references = References(assembly);
            Assert.DoesNotContain("System.Threading.Mutex", references);
            Assert.DoesNotContain("System.Diagnostics.Process::GetProcessesByName", references);
        }
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
