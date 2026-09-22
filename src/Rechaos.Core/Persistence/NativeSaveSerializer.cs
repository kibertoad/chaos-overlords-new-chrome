using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

/// <summary>Versioned recreation-native snapshots; this is not the original save format.</summary>
public static class NativeSaveSerializer
{
    // 27 moves with the state-fingerprint encoding, which now folds the definition set in as a
    // digest rather than inline (MatchStateHasher.FormatVersion 2), and drops every older format:
    // the fingerprint and the phase-hash history a save carries are written in the encoding of
    // their day, so a save from format 26 could only be restored on trust. There is no build in
    // players' hands whose saves this would strand.
    public const int CurrentFormatVersion = 27;
    public const int MaximumSaveBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    internal static JsonSerializerOptions CreateCompatibleJsonOptions() => new(JsonOptions);

    /// <summary>Writes a snapshot, refusing one the reader would later refuse.</summary>
    /// <remarks>
    /// Every save carries the whole event history and the whole phase-hash history, and neither is
    /// trimmed, so a long six-player match grows towards <see cref="MaximumSaveBytes"/>. The writer
    /// used to have no limit at all, so the first file over the line was written happily and then
    /// failed its own read-back, which is the worst moment to find out. Checking here means the
    /// player is told the match is too large to save while the match is still in memory.
    /// </remarks>
    /// <exception cref="InvalidDataException">The snapshot is over the size limit.</exception>
    public static void Save(Stream destination, MatchState state)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(state);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream is not writable.", nameof(destination));
        using var buffer = new MemoryStream();
        JsonSerializer.Serialize(buffer, Capture(state), JsonOptions);
        if (buffer.Length > MaximumSaveBytes)
        {
            throw new InvalidDataException(
                $"Native save is {buffer.Length} bytes, over the {MaximumSaveBytes} byte limit.");
        }
        buffer.Position = 0;
        buffer.CopyTo(destination);
    }

    public static MatchState Load(Stream source, OriginalData definitions) =>
        Load(source, definitions, verifyStateFingerprint: true);

    /// <summary>
    /// Restores a snapshot whose own state fingerprint is deliberately stale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one caller is <see cref="ReplayAnonymizer"/>, which rewrites the player names and Comlink
    /// text inside a snapshot before it leaves the machine. Those fields are part of the canonical
    /// hash, so a rewritten snapshot cannot carry the fingerprint it was saved with, and there is no
    /// way to compute the new one without first restoring the state it describes.
    /// </para>
    /// <para>
    /// This is not a weaker <see cref="Load"/>: the definition fingerprint and every structural
    /// check still run, and only the snapshot's agreement with itself is left to the caller. The
    /// anonymizer immediately re-serializes what comes out, which is what writes the correct
    /// fingerprint back. Nothing that reads a file somebody else wrote may use this.
    /// </para>
    /// </remarks>
    internal static MatchState LoadRewritten(Stream source, OriginalData definitions) =>
        Load(source, definitions, verifyStateFingerprint: false);

    private static MatchState Load(
        Stream source, OriginalData definitions, bool verifyStateFingerprint)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!source.CanRead) throw new ArgumentException("Source stream is not readable.", nameof(source));
        using var bounded = ReadBounded(
            source, MaximumSaveBytes, "Native save exceeds the size limit.");
        // The version has to be read before the members are bound: JsonOptions refuses unmapped
        // members, so a save from a newer build fails as "JSON is invalid" on the very field the
        // newer build added, and the caller would have no way to tell it from real damage.
        if (DeclaredFormatVersion(bounded) is { } declared && declared != CurrentFormatVersion)
            throw UnsupportedFormat(declared);
        NativeSaveDocument document;
        try
        {
            document = JsonSerializer.Deserialize<NativeSaveDocument>(bounded, JsonOptions)
                ?? throw new InvalidDataException("Native save is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Native save JSON is invalid.", exception);
        }
        try
        {
            return RestoreDocument(document, definitions, verifyStateFingerprint);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException
            or NullReferenceException)
        {
            throw new InvalidDataException("Native save state is invalid.", exception);
        }
    }

    /// <summary>The envelope's <c>formatVersion</c>, or null when the bytes are not readable JSON.</summary>
    /// <remarks>Leaves the stream rewound for the real deserialization pass.</remarks>
    private static int? DeclaredFormatVersion(MemoryStream bounded)
    {
        try
        {
            using var envelope = JsonDocument.Parse(bounded, new JsonDocumentOptions { MaxDepth = 64 });
            return envelope.RootElement.ValueKind == JsonValueKind.Object
                && envelope.RootElement.TryGetProperty("formatVersion", out var version)
                && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var value)
                    ? value
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            bounded.Position = 0;
        }
    }

    private static MatchState RestoreDocument(
        NativeSaveDocument document, OriginalData definitions, bool verifyStateFingerprint)
    {
        if (document.FormatVersion != CurrentFormatVersion)
            throw UnsupportedFormat(document.FormatVersion);
        if (!CryptographicOperations.FixedTimeEquals(
                DecodeSha256(document.DefinitionsSha256, "definition fingerprint"),
                DecodeSha256(DefinitionFingerprint(definitions), "current definition fingerprint")))
            throw IncompatibleSave.Create(
                IncompatibleSaveReason.DifferentDefinitions,
                "Native save gameplay definitions do not match this installation.");

        var setup = new MatchSetup(
            document.Setup.Scenario,
            document.Setup.Duration,
            document.Setup.InitialSeed,
            document.Setup.Players.Select(player => new MatchPlayerSetup(
                new PlayerId(player.Id), player.Name, player.Controller, player.PortraitId)).ToArray(),
            document.Setup.AiMentality,
            aiPolicy: document.Setup.AiPolicy
                ?? throw new InvalidDataException("Native save AI policy is missing."));
        var players = document.Players
            .Select(player => RestorePlayer(setup, player))
            .ToArray();
        var sectors = document.Sectors.Select(RestoreSector).ToArray();
        var notifications = document.Runtime.Notifications.ToDictionary(
            entry => new PlayerId(entry.Player),
            entry => (IReadOnlyList<GameNotification>)entry.Items);
        var notificationSequences = document.Runtime.Notifications.ToDictionary(
            entry => new PlayerId(entry.Player), entry => entry.NextSequence);
        var comlinkInboxes = (document.Runtime.Comlink
            ?? throw new InvalidDataException("Native save Comlink state is missing."))
            .ToDictionary(
                entry => new PlayerId(entry.Player),
                entry => new ComlinkInboxRestore(
                    entry.Items,
                    entry.NextSequence,
                    entry.ReadSequences
                        ?? throw new InvalidDataException(
                            "Native save Comlink read flags are missing.")));
        var aiStrategy = document.Runtime.AiStrategy is { } savedStrategy
            ? AiStrategicState.Restore(savedStrategy.Reactions, savedStrategy.Attitudes)
            : throw new InvalidDataException("Native save AI strategic state is missing.");
        var aiPlanning = document.Runtime.AiPlanning is { } savedPlanning
            ? AiPlanningState.Restore(
                savedPlanning.CurrentHireRoles,
                savedPlanning.PreviousHireRoles,
                savedPlanning.Families,
                savedPlanning.SectorAnchors
                    ?? throw new InvalidDataException("Native save AI sector anchors are missing."),
                savedPlanning.OlderActions
                    ?? throw new InvalidDataException("Native save older AI actions are missing."),
                savedPlanning.PreviousActions
                    ?? throw new InvalidDataException("Native save previous AI actions are missing."),
                savedPlanning.PlannedActions
                    ?? throw new InvalidDataException("Native save planned AI actions are missing."),
                savedPlanning.OlderTargets
                    ?? throw new InvalidDataException("Native save older AI targets are missing."),
                savedPlanning.PreviousTargets
                    ?? throw new InvalidDataException("Native save previous AI targets are missing."),
                savedPlanning.PlannedTargets
                    ?? throw new InvalidDataException("Native save planned AI targets are missing."),
                savedPlanning.HasPlanned
                    ?? throw new InvalidDataException("Native save AI first-planning flags are missing."),
                savedPlanning.WeaponCooldowns
                    ?? throw new InvalidDataException("Native save AI weapon cooldowns are missing."),
                savedPlanning.ArmorCooldowns
                    ?? throw new InvalidDataException("Native save AI armor cooldowns are missing."),
                savedPlanning.FormationSectors
                    ?? throw new InvalidDataException("Native save AI formation sectors are missing."),
                savedPlanning.CoverageSectors
                    ?? throw new InvalidDataException("Native save AI coverage sectors are missing."))
            : throw new InvalidDataException("Native save AI planning state is missing.");
        var runtime = new MatchRuntimeRestore(
            document.Runtime.Turn,
            document.Runtime.Phase,
            document.Runtime.ExecutionPhase,
            document.Runtime.ActivePlayer is { } active ? new PlayerId(active) : null,
            document.Runtime.RandomState,
            document.Runtime.RandomConsumptionCount,
            document.Runtime.Commands,
            document.Runtime.NextCommandSequence,
            document.Runtime.Events,
            document.Runtime.NextEventSequence,
            notifications,
            notificationSequences,
            comlinkInboxes,
            document.Runtime.PhaseHashes,
            document.Runtime.Outcome,
            aiStrategy,
            aiPlanning);
        var state = new MatchState(definitions, setup, players, sectors, runtime);
        if (verifyStateFingerprint
            && !string.Equals(
                document.StateFingerprint, MatchStateHasher.ComputeFingerprint(state),
                StringComparison.Ordinal))
            throw new InvalidDataException("Native save state fingerprint does not match its contents.");
        return state;
    }

    /// <summary>
    /// A save this build does not read, marked as intact rather than damaged either way.
    /// </summary>
    /// <remarks>
    /// Older formats are refused rather than migrated. Their state fingerprint, and the whole
    /// phase-hash history inside them, were written under an encoding this build has retired, so
    /// it could restore one only by taking it on trust. See
    /// <see cref="MatchStateHasher.FormatVersion"/> for the coupling that keeps the two moving
    /// together.
    /// </remarks>
    private static InvalidDataException UnsupportedFormat(int declared) =>
        IncompatibleSave.Create(
            declared > CurrentFormatVersion
                ? IncompatibleSaveReason.NewerFormat
                : IncompatibleSaveReason.OlderFormat,
            $"Unsupported native save format {declared}.");

    private static NativeSaveDocument Capture(MatchState state) => new(
        CurrentFormatVersion,
        DefinitionFingerprint(state.Definitions),
        MatchStateHasher.ComputeFingerprint(state),
        new MatchSetupDocument(
            state.Setup.Scenario,
            state.Setup.Duration,
            state.Setup.InitialSeed,
            state.Setup.Players.Select(player => new PlayerSetupDocument(
                player.Id.Value, player.Name, player.Controller, player.PortraitId)).ToArray(),
            state.Setup.AiMentality,
            state.Setup.AiPolicy),
        state.Players.Select(CapturePlayer).ToArray(),
        state.Sectors.Select(CaptureSector).ToArray(),
        new RuntimeDocument(
            state.Coordinator.Turn,
            state.Coordinator.Phase,
            state.Coordinator.ExecutionPhase,
            state.Coordinator.ActivePlayer?.Value,
            state.Random.State,
            state.Random.ConsumptionCount,
            state.Commands.ExecutionPlan().ToArray(),
            state.Commands.NextSequence,
            state.Events.ToArray(),
            state.NextEventSequence,
            state.Players.Select(player => new PlayerNotificationsDocument(
                player.Id.Value,
                state.NextNotificationSequence(player.Id),
                state.NotificationsFor(player.Id).ToArray())).ToArray(),
            state.PhaseHashes.ToArray(),
            state.Outcome,
            new AiStrategyDocument(
                state.AiStrategy.CaptureReactions(),
                state.AiStrategy.CaptureAttitudes()),
            new AiPlanningDocument(
                state.AiPlanning.CaptureCurrentHireRoles(),
                state.AiPlanning.CapturePreviousHireRoles(),
                state.AiPlanning.CaptureFamilies(),
                state.AiPlanning.CaptureSectorAnchors(),
                state.AiPlanning.CaptureOlderActions(),
                state.AiPlanning.CapturePreviousActions(),
                state.AiPlanning.CapturePlannedActions(),
                state.AiPlanning.CaptureOlderTargets(),
                state.AiPlanning.CapturePreviousTargets(),
                state.AiPlanning.CapturePlannedTargets(),
                state.AiPlanning.CaptureHasPlanned(),
                state.AiPlanning.CaptureWeaponCooldowns(),
                state.AiPlanning.CaptureArmorCooldowns(),
                state.AiPlanning.CaptureFormationSectors(),
                state.AiPlanning.CaptureCoverageSectors()),
            state.Players.Select(player => new PlayerComlinkDocument(
                player.Id.Value,
                state.ComlinkFor(player.Id).NextSequence,
                state.ComlinkFor(player.Id).LegacyReadThroughSequence,
                state.ComlinkFor(player.Id).Messages,
                state.ComlinkFor(player.Id).ReadSequences)).ToArray()));

    private static PlayerDocument CapturePlayer(MatchPlayerState player) => new(
        player.Id.Value,
        player.Cash,
        player.Support,
        player.BigManPoints,
        player.Status,
        player.Gangs.Select(gang => new GangDocument(
            gang.Id.Value, gang.DefinitionId, gang.SectorId, gang.Force,
            gang.Hidden, gang.HiredThisTurn,
            gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId)).ToArray(),
        player.HirePool.ToArray(),
        player.PendingHires.ToArray(),
        player.ResearchProgress.OrderBy(entry => entry.Key).ToDictionary(),
        player.ResearchedItems.Order().ToArray(),
        player.Inventory.OrderBy(entry => entry.Key).ToDictionary(),
        new StatisticsDocument(
            player.Statistics.CashEarned,
            player.Statistics.CashSpent,
            player.Statistics.DamageInflicted,
            player.Statistics.Casualties,
            player.Statistics.Overthrows,
            player.Statistics.TimesHidden),
        player.SnubbedHireOffer,
        player.HireOfferSlots.ToArray(),
        player.SnubbedHireOfferSlot,
        player.UsesMaximumHireForce);

    private static MatchPlayerState RestorePlayer(
        MatchSetup setup,
        PlayerDocument player)
    {
        if (player.Id < 0 || player.Id >= setup.Players.Count)
            throw new InvalidDataException("Native save contains an invalid player identifier.");
        var playerId = new PlayerId(player.Id);
        var gangs = player.Gangs.Select(gang =>
        {
            var restored = new MatchGangState(
                new GangId(gang.Id), playerId, gang.DefinitionId, gang.SectorId, gang.Force,
                gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId)
            {
                Hidden = gang.Hidden,
                HiredThisTurn = gang.HiredThisTurn
            };
            return restored;
        }).ToArray();
        var statistics = new MatchStatistics(
            player.Statistics.CashEarned,
            player.Statistics.CashSpent,
            player.Statistics.DamageInflicted,
            player.Statistics.Casualties,
            player.Statistics.Overthrows,
            player.Statistics.TimesHidden);
        var hireOfferSlots = player.HireOfferSlots
            ?? throw new InvalidDataException("Native save hire-offer slots are missing.");
        return new MatchPlayerState(
            setup.Players[player.Id], player.Cash, gangs, player.HirePool,
            player.PendingHires, player.ResearchProgress, player.ResearchedItems.ToHashSet(),
            player.Inventory, player.Support, player.BigManPoints, player.Status,
            statistics, player.SnubbedHireOffer,
            hireOfferSlots, player.SnubbedHireOfferSlot,
            usesMaximumHireForce: player.UsesMaximumHireForce);
    }

    private static SectorDocument CaptureSector(MatchSectorState sector) => new(
        sector.Id,
        sector.Owner?.Value,
        sector.Tolerance,
        sector.CrackdownActive,
        sector.IsImportant,
        sector.Sites.Select(site => new SiteDocument(
            site.Slot, site.DefinitionId, site.Resistance, site.InfluencedBy?.Value)).ToArray(),
        sector.Income,
        sector.CrackdownTurnsRemaining,
        sector.CrackdownHistory.ToArray(),
        Chaos: null);

    private static MatchSectorState RestoreSector(SectorDocument sector) => new(
        sector.Id,
        sector.Sites.Select(site => new MatchSiteState(
            site.Slot,
            site.DefinitionId,
            site.Resistance,
            site.InfluencedBy is { } influencedBy ? new PlayerId(influencedBy) : null)).ToArray(),
        sector.Owner is { } owner ? new PlayerId(owner) : null,
        sector.Tolerance,
        0,
        sector.CrackdownActive,
        sector.IsImportant,
        sector.Income ?? throw new InvalidDataException("Native save sector income is missing."),
        sector.CrackdownTurnsRemaining
            ?? throw new InvalidDataException("Native save crackdown duration is missing."),
        sector.CrackdownHistory
            ?? throw new InvalidDataException("Native save crackdown history is missing."));

    /// <summary>
    /// Copies <paramref name="source"/> into memory and rewinds the copy, throwing
    /// <see cref="InvalidDataException"/> with <paramref name="overLimitMessage"/> once more than
    /// <paramref name="maximumBytes"/> have been read.
    /// </summary>
    internal static MemoryStream ReadBounded(Stream source, int maximumBytes, string overLimitMessage)
    {
        if (source.CanSeek && source.Length - source.Position > maximumBytes)
            throw new InvalidDataException(overLimitMessage);
        var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (memory.Length + read > maximumBytes)
                throw new InvalidDataException(overLimitMessage);
            memory.Write(buffer, 0, read);
        }
        memory.Position = 0;
        return memory;
    }

    /// <summary>
    /// Fingerprints the bundled definitions, once per <see cref="OriginalData"/> instance.
    /// </summary>
    /// <remarks>
    /// Serialising and hashing the whole definition set costs the same every time for a given
    /// instance, and a single autosave paid it three times while opening the save browser paid it
    /// up to nine times on the update thread. The entries are weak, so nothing is pinned.
    /// </remarks>
    private static readonly ConditionalWeakTable<OriginalData, string> DefinitionFingerprints = [];

    private static string DefinitionFingerprint(OriginalData definitions) =>
        DefinitionFingerprints.GetValue(definitions, static value => Convert.ToHexStringLower(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions))));

    /// <summary>
    /// The definition fingerprint this build writes into every save and demands of every save it
    /// reads, as <see cref="Capture"/> and <see cref="RestoreDocument"/> use it.
    /// </summary>
    /// <remarks>
    /// The save browser's sidecar records this alongside <see cref="CurrentFormatVersion"/> so a
    /// row can be drawn without deserializing the match and still know whether that match would
    /// load here at all. A save the definitions or the format have moved on from is byte-identical
    /// on disk, so nothing about the file itself tells the two apart.
    /// </remarks>
    public static string DefinitionsFingerprint(OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return DefinitionFingerprint(definitions);
    }

    private static byte[] DecodeSha256(string value, string field)
    {
        try
        {
            var bytes = Convert.FromHexString(value);
            if (bytes.Length is not (16 or 32)) throw new FormatException();
            return bytes;
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"Native save {field} is invalid.", exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            // Without this a hand-edited `"setup": null` deserializes to a document whose members
            // are null and then raises NullReferenceException deep inside the restore, past every
            // handler on the load path. With it the same input is a JsonException, which Load
            // already translates into InvalidDataException.
            RespectNullableAnnotations = true,
            MaxDepth = 64
        };
        options.Converters.Add(new PlayerIdJsonConverter());
        options.Converters.Add(new GangIdJsonConverter());
        options.Converters.Add(new CommandTargetJsonConverter());
        return options;
    }
}

internal sealed record NativeSaveDocument(
    int FormatVersion,
    string DefinitionsSha256,
    string StateFingerprint,
    MatchSetupDocument Setup,
    IReadOnlyList<PlayerDocument> Players,
    IReadOnlyList<SectorDocument> Sectors,
    RuntimeDocument Runtime);

internal sealed record MatchSetupDocument(
    ScenarioId Scenario,
    GameDuration Duration,
    int InitialSeed,
    IReadOnlyList<PlayerSetupDocument> Players,
    AiDifficulty AiMentality = AiDifficulty.Criminal,
    AiPolicyMode? AiPolicy = null);

internal sealed record PlayerSetupDocument(
    int Id,
    string Name,
    PlayerController Controller,
    short PortraitId = 0);

internal sealed record PlayerDocument(
    int Id,
    int Cash,
    int Support,
    int BigManPoints,
    PlayerStatus Status,
    IReadOnlyList<GangDocument> Gangs,
    IReadOnlyList<short> HirePool,
    IReadOnlyList<PendingHireState> PendingHires,
    IReadOnlyDictionary<short, int> ResearchProgress,
    IReadOnlyList<short> ResearchedItems,
    IReadOnlyDictionary<short, int> Inventory,
    StatisticsDocument Statistics,
    short? SnubbedHireOffer,
    IReadOnlyList<HireOfferSlotState>? HireOfferSlots = null,
    int? SnubbedHireOfferSlot = null,
    bool UsesMaximumHireForce = false);

internal sealed record GangDocument(
    int Id,
    short DefinitionId,
    int SectorId,
    int Force,
    bool Hidden,
    bool HiredThisTurn,
    short? WeaponItemId,
    short? ArmorItemId,
    short? MiscellaneousItemId);

internal sealed record StatisticsDocument(
    long CashEarned,
    long CashSpent,
    int DamageInflicted,
    int Casualties,
    int Overthrows,
    int TimesHidden);

internal sealed record SectorDocument(
    int Id,
    int? Owner,
    int Tolerance,
    bool CrackdownActive,
    bool IsImportant,
    IReadOnlyList<SiteDocument> Sites,
    int? Income = null,
    int? CrackdownTurnsRemaining = null,
    IReadOnlyList<int>? CrackdownHistory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Chaos = null);

internal sealed record SiteDocument(int Slot, short DefinitionId, int Resistance, int? InfluencedBy);

internal sealed record RuntimeDocument(
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    int? ActivePlayer,
    uint RandomState,
    long RandomConsumptionCount,
    IReadOnlyList<QueuedCommand> Commands,
    long NextCommandSequence,
    IReadOnlyList<GameEvent> Events,
    long NextEventSequence,
    IReadOnlyList<PlayerNotificationsDocument> Notifications,
    IReadOnlyList<PhaseBoundaryHash> PhaseHashes,
    MatchOutcome? Outcome,
    AiStrategyDocument? AiStrategy = null,
    AiPlanningDocument? AiPlanning = null,
    IReadOnlyList<PlayerComlinkDocument>? Comlink = null);

internal sealed record AiStrategyDocument(
    IReadOnlyList<int> Reactions,
    IReadOnlyList<int> Attitudes);

internal sealed record AiPlanningDocument(
    IReadOnlyList<int> CurrentHireRoles,
    IReadOnlyList<int> PreviousHireRoles,
    IReadOnlyList<int> Families,
    IReadOnlyList<int>? SectorAnchors = null,
    IReadOnlyList<GangAction>? OlderActions = null,
    IReadOnlyList<GangAction>? PreviousActions = null,
    IReadOnlyList<GangAction>? PlannedActions = null,
    IReadOnlyList<AiActionTarget>? OlderTargets = null,
    IReadOnlyList<AiActionTarget>? PreviousTargets = null,
    IReadOnlyList<AiActionTarget>? PlannedTargets = null,
    IReadOnlyList<bool>? HasPlanned = null,
    IReadOnlyList<short>? WeaponCooldowns = null,
    IReadOnlyList<short>? ArmorCooldowns = null,
    IReadOnlyList<short>? FormationSectors = null,
    IReadOnlyList<short>? CoverageSectors = null);

internal sealed record PlayerNotificationsDocument(
    int Player,
    long NextSequence,
    IReadOnlyList<GameNotification> Items);

internal sealed record PlayerComlinkDocument(
    int Player,
    long NextSequence,
    long ReadThroughSequence,
    IReadOnlyList<ComlinkMessage> Items,
    IReadOnlyList<long>? ReadSequences = null);

internal sealed class PlayerIdJsonConverter : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed class GangIdJsonConverter : JsonConverter<GangId>
{
    public override GangId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, GangId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed class CommandTargetJsonConverter : JsonConverter<CommandTarget>
{
    public override CommandTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Command target must be an object.");
        CommandTargetKind? kind = null;
        int? id = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
            var property = reader.GetString();
            reader.Read();
            switch (property)
            {
                case "kind": kind = (CommandTargetKind)reader.GetByte(); break;
                case "id": id = reader.GetInt32(); break;
                default: throw new JsonException($"Unknown command-target property '{property}'.");
            }
        }
        if (kind is null || id is null) throw new JsonException("Command target is incomplete.");
        return kind.Value switch
        {
            CommandTargetKind.None when id == -1 => CommandTarget.None,
            CommandTargetKind.Gang => CommandTarget.Gang(new GangId(id.Value)),
            CommandTargetKind.Sector => CommandTarget.Sector(id.Value),
            CommandTargetKind.Site => CommandTarget.Site(id.Value),
            CommandTargetKind.Item => CommandTarget.Item(id.Value),
            _ => throw new JsonException("Command target kind or identifier is invalid.")
        };
    }

    public override void Write(Utf8JsonWriter writer, CommandTarget value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("kind", (byte)value.Kind);
        writer.WriteNumber("id", value.Id);
        writer.WriteEndObject();
    }
}
