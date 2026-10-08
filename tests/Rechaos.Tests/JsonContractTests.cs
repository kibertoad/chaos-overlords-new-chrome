using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// The source-generated JSON contracts write the bytes the reflection-based serializer wrote, so
/// saves, replays and the definition fingerprint they are checked against stay as they were.
/// </summary>
/// <remarks>
/// The resolver's WebAssembly bundle is trimmed (docs/MULTIPLAYER.md, "Resolving turns on the
/// server"), and the trimmer removes what reflection-based System.Text.Json binds constructor
/// parameters by, so <c>Rechaos.Core</c> and <c>Rechaos.Multiplayer</c> read and write through
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>s. These tests hold those
/// contracts to the reflection-based ones with the same options.
/// </remarks>
public sealed class JsonContractTests
{
    /// <summary>
    /// SHA-256 of the bundled definitions as the save options serialize them. Every save carries
    /// it and every load demands it, so a change here makes every save on every machine unreadable;
    /// it may move only with the definitions themselves.
    /// </summary>
    private const string PinnedDefinitionsFingerprint =
        "120c4165be2f08bb7ef17ca16bcd7b802be0e4e664deb1374a824dec2d433671";

    private static readonly OriginalData Definitions = BundledOriginalData.Load();

    private static JsonSerializerOptions Reflection(JsonSerializerOptions options) =>
        new(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    private static JsonSerializerOptions SaveOptionsByReflection { get; } =
        Reflection(NativeSaveSerializer.CreateCompatibleJsonOptions());

    [Fact]
    public void TheDefinitionFingerprintIsPinned()
    {
        Assert.Equal(PinnedDefinitionsFingerprint, NativeSaveSerializer.DefinitionsFingerprint(Definitions));
        var byReflection = Convert.ToHexStringLower(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Definitions, SaveOptionsByReflection)));
        Assert.Equal(PinnedDefinitionsFingerprint, byReflection);
    }

    [Fact]
    public void TheBundledDataReadsAsReflectionReadsIt()
    {
        using var stream = typeof(BundledOriginalData).Assembly.GetManifestResourceStream(
            "Rechaos.Core.GameData.original-data.json")!;
        var byReflection = JsonSerializer.Deserialize<OriginalData>(
            stream, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() })!;
        Assert.Equal(
            JsonSerializer.SerializeToUtf8Bytes(byReflection, SaveOptionsByReflection),
            JsonSerializer.SerializeToUtf8Bytes(Definitions, SaveOptionsByReflection));
    }

    [Fact]
    public void ASaveIsWrittenAndReadAsReflectionWritesAndReadsIt()
    {
        var replay = PlayedMatch(turns: 6);
        using var written = new MemoryStream();
        NativeSaveSerializer.Save(written, replay.State);
        var bytes = written.ToArray();

        var document = JsonSerializer.Deserialize<NativeSaveDocument>(bytes, SaveOptionsByReflection)!;
        Assert.Equal(bytes, JsonSerializer.SerializeToUtf8Bytes(document, SaveOptionsByReflection));

        using var read = new MemoryStream(bytes);
        var restored = NativeSaveSerializer.Load(read, Definitions);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(replay.State), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void AReplayIsWrittenAndReadAsReflectionWritesAndReadsIt()
    {
        var replay = PlayedMatch(turns: 3);
        using var written = new MemoryStream();
        MatchReplaySerializer.Save(written, replay);
        var bytes = written.ToArray();

        var document = JsonSerializer.Deserialize<ReplayDocument>(bytes, SaveOptionsByReflection)!;
        Assert.NotEmpty(document.Steps);
        Assert.Equal(bytes, JsonSerializer.SerializeToUtf8Bytes(document, SaveOptionsByReflection));

        using var read = new MemoryStream(bytes);
        var replayed = MatchReplaySerializer.LoadAndReplay(read, Definitions);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(replay.State), MatchStateHasher.ComputeFingerprint(replayed));
    }

    [Fact]
    public void EveryWireTypeHasASourceGeneratedContract()
    {
        // Every record and enum the codegen writes; the static route tables are not payloads.
        var missing = typeof(PlayerView).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PlayerView).Namespace
                && type.IsPublic
                && !(type.IsAbstract && type.IsSealed))
            .Where(type => WireJson.Options.TypeInfoResolver!.GetTypeInfo(type, WireJson.Options) is null)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(missing);
    }

    [Fact]
    public void TheGeneratedDiscriminatorsAreEveryUnionsTag()
    {
        // The codegen reads these off the C# text; reflection over the attributes is the reference.
        // Declared attributes only: a union's members inherit the attribute from their base.
        var declared = typeof(PlayerView).Assembly.GetTypes()
            .Select(type => type.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false))
            .Select(attribute => attribute?.TypeDiscriminatorPropertyName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        Assert.Equal(declared, WireJsonContext.TypeDiscriminators.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AWirePayloadIsWrittenAsReflectionWritesIt()
    {
        var replay = PlayedMatch(turns: 2);
        var sealedOrders = Seal(replay.State);
        var wireByReflection = Reflection(WireJson.Options);
        Assert.Equal(
            JsonSerializer.Serialize(sealedOrders, wireByReflection),
            WireJson.Write(sealedOrders));
        Assert.Equal(
            JsonSerializer.Serialize(Roster, wireByReflection),
            WireJson.Write(Roster));
        // The settings record goes through its own camelCase options, so compare the blob with that
        // record written by reflection rather than the finished dictionary with itself.
        var settingsByReflection = new JsonSerializerOptions(wireByReflection)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        Assert.Equal(
            JsonSerializer.Serialize(
                new MultiplayerGameSettings.Wire(
                    (int)Settings.Scenario, (int)Settings.Duration, (int)Settings.AiMentality,
                    [.. Settings.Portraits], (int)Settings.AiPolicy, Settings.AllowLateJoin),
                settingsByReflection),
            JsonSerializer.Serialize(Settings.ToWire(), WireJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(Settings.ToWire(), WireJson.Options),
            JsonSerializer.Serialize(MultiplayerGameSettings.FromWire(Settings.ToWire()).ToWire(), WireJson.Options));
        var reread = WireJson.ReadExact<SealedOrdersView>(WireJson.Write(sealedOrders));
        Assert.Equal(WireJson.Write(sealedOrders), WireJson.Write(reread));
    }

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly PlayerView[] Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
    ];

    /// <summary>A two-human match whose humans order nothing, played for a few turns.</summary>
    private static MatchReplayRecorder PlayedMatch(int turns)
    {
        var replay = new MatchReplayRecorder(MatchBootstrapFactory.Create(Definitions, 1996, Settings, Roster));
        CommandPhase.Enter(replay);
        for (var turn = 0; turn < turns; turn++) SealedTurnApplier.Apply(replay, Seal(replay.State));
        return replay;
    }

    private static SealedOrdersView Seal(MatchState state)
    {
        var entries = Roster
            .Select(player =>
            {
                var document = new OrderDocumentBuilder(new PlayerId(player.Slot)).Build();
                return new SealedPlayerOrders(player.Id, player.Slot, document, OrderDigest.OfDocument(document));
            })
            .ToList();
        return new SealedOrdersView(state.Coordinator.Turn, OrderDigest.OfSet(entries), entries);
    }
}
