using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

/// <summary>Versioned recreation-native snapshots; this is not the original save format.</summary>
public static class NativeSaveSerializer
{
    public const int CurrentFormatVersion = 18;
    public const int MaximumSaveBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    internal static JsonSerializerOptions CreateCompatibleJsonOptions() => new(JsonOptions);

    public static void Save(Stream destination, MatchState state)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(state);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream is not writable.", nameof(destination));
        JsonSerializer.Serialize(destination, Capture(state), JsonOptions);
    }

    public static MatchState Load(Stream source, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!source.CanRead) throw new ArgumentException("Source stream is not readable.", nameof(source));
        using var bounded = ReadBounded(source);
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
            return RestoreDocument(document, definitions);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            throw new InvalidDataException("Native save state is invalid.", exception);
        }
    }

    private static MatchState RestoreDocument(NativeSaveDocument document, OriginalData definitions)
    {
        if (document.FormatVersion is < 1 or > CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported native save format {document.FormatVersion}.");
        if (!CryptographicOperations.FixedTimeEquals(
                DecodeSha256(document.DefinitionsSha256, "definition fingerprint"),
                DecodeSha256(DefinitionFingerprint(definitions), "current definition fingerprint")))
            throw new InvalidDataException("Native save gameplay definitions do not match this installation.");

        var setup = new MatchSetup(
            document.Setup.Scenario,
            document.Setup.Duration,
            document.Setup.InitialSeed,
            document.Setup.Players.Select(player => new MatchPlayerSetup(
                new PlayerId(player.Id), player.Name, player.Controller,
                document.FormatVersion >= 5 ? player.PortraitId : checked((short)player.Id))).ToArray(),
            document.FormatVersion >= 5 ? document.Setup.AiMentality : AiDifficulty.Criminal);
        var players = document.Players
            .Select(player => RestorePlayer(setup, player, document.FormatVersion))
            .ToArray();
        var sectors = document.Sectors.Select(sector => RestoreSector(
            sector, definitions, document.FormatVersion)).ToArray();
        var notifications = document.Runtime.Notifications.ToDictionary(
            entry => new PlayerId(entry.Player),
            entry => (IReadOnlyList<GameNotification>)entry.Items);
        var notificationSequences = document.Runtime.Notifications.ToDictionary(
            entry => new PlayerId(entry.Player), entry => entry.NextSequence);
        var comlinkInboxes = document.FormatVersion >= 17
            ? (document.Runtime.Comlink
                ?? throw new InvalidDataException("Native save Comlink state is missing."))
                .ToDictionary(
                    entry => new PlayerId(entry.Player),
                    entry => new ComlinkInboxRestore(
                        entry.Items, entry.NextSequence, entry.ReadThroughSequence))
            : setup.Players.ToDictionary(
                player => player.Id,
                _ => new ComlinkInboxRestore([], 0, -1));
        var aiStrategy = document.FormatVersion >= 6
            ? document.Runtime.AiStrategy is { } savedStrategy
                ? AiStrategicState.Restore(savedStrategy.Reactions, savedStrategy.Attitudes)
                : throw new InvalidDataException("Native save AI strategic state is missing.")
            : AiStrategicState.MigrateLegacy(setup);
        var aiPlanning = document.FormatVersion >= 7
            ? document.Runtime.AiPlanning is { } savedPlanning
                ? AiPlanningState.Restore(
                    savedPlanning.CurrentHireRoles,
                    savedPlanning.PreviousHireRoles,
                    savedPlanning.Families,
                    document.FormatVersion >= 11
                        ? savedPlanning.SectorAnchors
                            ?? throw new InvalidDataException("Native save AI sector anchors are missing.")
                        : AiPlanningState.Initialize(players).CaptureSectorAnchors(),
                    document.FormatVersion >= 12
                        ? savedPlanning.OlderActions
                            ?? throw new InvalidDataException("Native save older AI actions are missing.")
                        : EmptyAiActions(),
                    document.FormatVersion >= 12
                        ? savedPlanning.PreviousActions
                            ?? throw new InvalidDataException("Native save previous AI actions are missing.")
                        : EmptyAiActions(),
                    document.FormatVersion >= 12
                        ? savedPlanning.PlannedActions
                            ?? throw new InvalidDataException("Native save planned AI actions are missing.")
                        : EmptyAiActions(),
                    document.FormatVersion >= 13
                        ? savedPlanning.OlderTargets
                            ?? throw new InvalidDataException("Native save older AI targets are missing.")
                        : EmptyAiTargets(),
                    document.FormatVersion >= 13
                        ? savedPlanning.PreviousTargets
                            ?? throw new InvalidDataException("Native save previous AI targets are missing.")
                        : EmptyAiTargets(),
                    document.FormatVersion >= 13
                        ? savedPlanning.PlannedTargets
                            ?? throw new InvalidDataException("Native save planned AI targets are missing.")
                        : EmptyAiTargets(),
                    document.FormatVersion >= 13
                        ? savedPlanning.HasPlanned
                            ?? throw new InvalidDataException("Native save AI first-planning flags are missing.")
                        : InferLegacyHasPlanned(savedPlanning),
                    document.FormatVersion >= 14
                        ? savedPlanning.WeaponCooldowns
                            ?? throw new InvalidDataException("Native save AI weapon cooldowns are missing.")
                        : EmptyAiCooldowns(),
                    document.FormatVersion >= 14
                        ? savedPlanning.ArmorCooldowns
                            ?? throw new InvalidDataException("Native save AI armor cooldowns are missing.")
                        : EmptyAiCooldowns(),
                    document.FormatVersion >= 15
                        ? savedPlanning.FormationSectors
                            ?? throw new InvalidDataException("Native save AI formation sectors are missing.")
                        : InferLegacyFormationSectors(savedPlanning, players),
                    document.FormatVersion >= 16
                        ? savedPlanning.CoverageSectors
                            ?? throw new InvalidDataException("Native save AI coverage sectors are missing.")
                        : EmptyAiCoverageSectors())
                : throw new InvalidDataException("Native save AI planning state is missing.")
            : AiPlanningState.Initialize(players);
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
        var restoredHash = document.FormatVersion switch
        {
            1 => MatchStateHasher.ComputeLegacySha256(state),
            2 => MatchStateHasher.ComputeVersionTwoSha256(state),
            3 => MatchStateHasher.ComputeVersionThreeSha256(state),
            4 => MatchStateHasher.ComputeVersionFourSha256(state),
            5 => MatchStateHasher.ComputeVersionFiveSha256(state),
            6 => MatchStateHasher.ComputeVersionSixSha256(state),
            7 => MatchStateHasher.ComputeVersionTenSha256(state),
            8 => MatchStateHasher.ComputeVersionElevenSha256(state),
            9 => MatchStateHasher.ComputeVersionTwelveSha256(state),
            10 => MatchStateHasher.ComputeVersionThirteenSha256(state),
            11 => MatchStateHasher.ComputeVersionFourteenSha256(state),
            12 => MatchStateHasher.ComputeVersionFifteenSha256(state),
            13 => MatchStateHasher.ComputeVersionSixteenSha256(state),
            14 => MatchStateHasher.ComputeVersionSeventeenSha256(state),
            15 => MatchStateHasher.ComputeVersionEighteenSha256(state),
            16 => MatchStateHasher.ComputeVersionNineteenSha256(state),
            17 => MatchStateHasher.ComputeVersionTwentySha256(state),
            _ => MatchStateHasher.ComputeSha256(state)
        };
        if (!CryptographicOperations.FixedTimeEquals(
                DecodeSha256(document.StateSha256, "state fingerprint"),
                DecodeSha256(restoredHash, "restored state fingerprint")))
            throw new InvalidDataException("Native save state fingerprint does not match its contents.");
        return state;
    }

    private static NativeSaveDocument Capture(MatchState state) => new(
        CurrentFormatVersion,
        DefinitionFingerprint(state.Definitions),
        MatchStateHasher.ComputeSha256(state),
        new MatchSetupDocument(
            state.Setup.Scenario,
            state.Setup.Duration,
            state.Setup.InitialSeed,
            state.Setup.Players.Select(player => new PlayerSetupDocument(
                player.Id.Value, player.Name, player.Controller, player.PortraitId)).ToArray(),
            state.Setup.AiMentality),
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
                state.ComlinkFor(player.Id).ReadThroughSequence,
                state.ComlinkFor(player.Id).Messages)).ToArray()));

    private static IReadOnlyList<GangAction> EmptyAiActions() =>
        new GangAction[MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer];

    private static IReadOnlyList<AiActionTarget> EmptyAiTargets() =>
        new AiActionTarget[MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer];

    private static IReadOnlyList<short> EmptyAiCooldowns() =>
        new short[MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer];

    private static IReadOnlyList<short> EmptyAiCoverageSectors() =>
        Enumerable.Repeat(
            checked((short)AiPlanningState.InactiveCoverageSector),
            MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer).ToArray();

    private static IReadOnlyList<short> InferLegacyFormationSectors(
        AiPlanningDocument planning,
        IReadOnlyList<MatchPlayerState> players)
    {
        var result = Enumerable.Repeat(
            checked((short)AiPlanningState.InactiveFormationSector),
            MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer).ToArray();
        foreach (var player in players)
        {
            for (var gangSlot = 0; gangSlot < player.Gangs.Count; gangSlot++)
            {
                var gang = player.Gangs[gangSlot];
                if (!gang.IsActive) continue;
                result[player.Id.Value * AiPlanningState.GangSlotsPerPlayer + gangSlot] =
                    checked((short)gang.SectorId);
            }
        }
        return result;
    }

    private static IReadOnlyList<bool> InferLegacyHasPlanned(AiPlanningDocument planning)
    {
        var result = new bool[MatchLimits.PlayerCount];
        for (var player = 0; player < MatchLimits.PlayerCount; player++)
        {
            var start = player * AiPlanningState.GangSlotsPerPlayer;
            result[player] = planning.Families
                .Skip(start).Take(AiPlanningState.GangSlotsPerPlayer)
                .Any(family => family != AiPlanningState.UnusedFamily)
                || (planning.OlderActions?.Skip(start).Take(AiPlanningState.GangSlotsPerPlayer)
                    .Any(action => action != GangAction.None) ?? false)
                || (planning.PreviousActions?.Skip(start).Take(AiPlanningState.GangSlotsPerPlayer)
                    .Any(action => action != GangAction.None) ?? false)
                || (planning.PlannedActions?.Skip(start).Take(AiPlanningState.GangSlotsPerPlayer)
                    .Any(action => action != GangAction.None) ?? false);
        }
        return result;
    }

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
        PlayerDocument player,
        int formatVersion)
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
        var hireState = formatVersion >= 8
            ? (Slots: player.HireOfferSlots
                    ?? throw new InvalidDataException("Native save hire-offer slots are missing."),
                Pending: player.PendingHires,
                SnubSlot: player.SnubbedHireOfferSlot)
            : MigrateLegacyHireState(player);
        var pendingHires = formatVersion >= 9
            ? hireState.Pending
            : hireState.Pending.Select(pending => pending with { InitialCostPaid = true }).ToArray();
        return new MatchPlayerState(
            setup.Players[player.Id], player.Cash, gangs, player.HirePool,
            pendingHires, player.ResearchProgress, player.ResearchedItems.ToHashSet(),
            player.Inventory, player.Support, player.BigManPoints, player.Status,
            statistics, player.SnubbedHireOffer,
            hireState.Slots, hireState.SnubSlot,
            usesMaximumHireForce: formatVersion >= 10 && player.UsesMaximumHireForce);
    }

    private static (
        IReadOnlyList<HireOfferSlotState> Slots,
        IReadOnlyList<PendingHireState> Pending,
        int? SnubSlot) MigrateLegacyHireState(PlayerDocument player)
    {
        var actionCount = player.PendingHires.Count + (player.SnubbedHireOffer.HasValue ? 1 : 0);
        if (actionCount == 0)
        {
            var ordinary = Enumerable.Range(0, MatchLimits.HireOffersPerPlayer)
                .Select(slot => slot < player.HirePool.Count
                    ? HireOfferSlotState.Available(player.HirePool[slot])
                    : HireOfferSlotState.Uninitialized)
                .ToArray();
            return (ordinary, player.PendingHires, null);
        }

        var survivorCount = Math.Max(0, MatchLimits.HireOffersPerPlayer - actionCount);
        var visibleSurvivors = player.HirePool.Take(survivorCount).ToArray();
        var prefetched = player.HirePool.Skip(visibleSurvivors.Length).ToArray();
        var slots = visibleSurvivors.Select(HireOfferSlotState.Available).ToList();
        var migratedPending = new List<PendingHireState>(player.PendingHires.Count);
        var prefetchIndex = 0;
        foreach (var pending in player.PendingHires)
        {
            var slot = slots.Count;
            var replacement = prefetchIndex < prefetched.Length
                ? prefetched[prefetchIndex++]
                : (short?)null;
            slots.Add(new HireOfferSlotState(
                pending.GangDefinitionId, null, replacement));
            migratedPending.Add(pending with { OfferSlot = slot });
        }

        int? snubSlot = null;
        if (player.SnubbedHireOffer is { } snubbed)
        {
            snubSlot = slots.Count;
            var replacement = prefetchIndex < prefetched.Length
                ? prefetched[prefetchIndex]
                : (short?)null;
            slots.Add(new HireOfferSlotState(snubbed, null, replacement));
        }
        while (slots.Count < MatchLimits.HireOffersPerPlayer)
            slots.Add(HireOfferSlotState.Uninitialized);
        if (slots.Count != MatchLimits.HireOffersPerPlayer)
            throw new InvalidDataException("Legacy hire state cannot be mapped to three fixed slots.");
        return (slots, migratedPending, snubSlot);
    }

    private static SectorDocument CaptureSector(MatchSectorState sector) => new(
        sector.Id,
        sector.Owner?.Value,
        sector.Tolerance,
        sector.Chaos,
        sector.CrackdownActive,
        sector.IsImportant,
        sector.Sites.Select(site => new SiteDocument(
            site.Slot, site.DefinitionId, site.Resistance, site.InfluencedBy?.Value)).ToArray(),
        sector.Income,
        sector.CrackdownTurnsRemaining,
        sector.CrackdownHistory.ToArray());

    private static MatchSectorState RestoreSector(
        SectorDocument sector,
        OriginalData definitions,
        int formatVersion) => new(
        sector.Id,
        sector.Sites.Select(site => new MatchSiteState(
            site.Slot,
            site.DefinitionId,
            site.Resistance,
            site.InfluencedBy is { } influencedBy ? new PlayerId(influencedBy) : null)).ToArray(),
        sector.Owner is { } owner ? new PlayerId(owner) : null,
        sector.Tolerance,
        sector.Chaos,
        sector.CrackdownActive,
        sector.IsImportant,
        formatVersion == 1
            ? sector.Sites.Sum(site => definitions.Sites.Single(
                definition => definition.Id == site.DefinitionId).Cash)
            : sector.Income ?? throw new InvalidDataException("Native save sector income is missing."),
        formatVersion < 3
            ? sector.CrackdownActive ? ManualRules.MinimumCrackdownTurns : 0
            : sector.CrackdownTurnsRemaining
                ?? throw new InvalidDataException("Native save crackdown duration is missing."),
        formatVersion < 4
            ? []
            : sector.CrackdownHistory
                ?? throw new InvalidDataException("Native save crackdown history is missing."));

    private static MemoryStream ReadBounded(Stream source)
    {
        if (source.CanSeek && source.Length - source.Position > MaximumSaveBytes)
            throw new InvalidDataException("Native save exceeds the size limit.");
        var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (memory.Length + read > MaximumSaveBytes)
                throw new InvalidDataException("Native save exceeds the size limit.");
            memory.Write(buffer, 0, read);
        }
        memory.Position = 0;
        return memory;
    }

    private static string DefinitionFingerprint(OriginalData definitions) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(definitions, JsonOptions)));

    private static byte[] DecodeSha256(string value, string field)
    {
        try
        {
            var bytes = Convert.FromHexString(value);
            if (bytes.Length != 32) throw new FormatException();
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
    string StateSha256,
    MatchSetupDocument Setup,
    IReadOnlyList<PlayerDocument> Players,
    IReadOnlyList<SectorDocument> Sectors,
    RuntimeDocument Runtime);

internal sealed record MatchSetupDocument(
    ScenarioId Scenario,
    GameDuration Duration,
    int InitialSeed,
    IReadOnlyList<PlayerSetupDocument> Players,
    AiDifficulty AiMentality = AiDifficulty.Criminal);

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
    int Chaos,
    bool CrackdownActive,
    bool IsImportant,
    IReadOnlyList<SiteDocument> Sites,
    int? Income = null,
    int? CrackdownTurnsRemaining = null,
    IReadOnlyList<int>? CrackdownHistory = null);

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
    IReadOnlyList<ComlinkMessage> Items);

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
