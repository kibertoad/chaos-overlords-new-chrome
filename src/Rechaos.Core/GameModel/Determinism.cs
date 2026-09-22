using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Stable, serializable implementation of the original executable's statically
/// linked Visual C++ rand step and three-sample bounded-range wrapper.
/// </summary>
public sealed class DeterministicRandom
{
    private const uint Multiplier = 0x343fd;
    private const uint Addend = 0x269ec3;
    private const int SelectionThreshold = 0x3ffe;
    private uint _state;

    public DeterministicRandom(int seed)
    {
        _state = unchecked((uint)seed);
    }

    public DeterministicRandom(uint state, long consumptionCount)
    {
        if (consumptionCount < 0) throw new ArgumentOutOfRangeException(nameof(consumptionCount));
        _state = state;
        ConsumptionCount = consumptionCount;
    }

    public uint State => _state;
    public long ConsumptionCount { get; private set; }

    /// <summary>
    /// Reproduces the original process initializer's zero-extension of the low
    /// 16 bits returned by <c>timeGetTime</c> before it seeds the runtime stream.
    /// </summary>
    public static int SeedFromTimerMilliseconds(uint timerMilliseconds) =>
        checked((int)(timerMilliseconds & ushort.MaxValue));

    public int NextRaw()
    {
        _state = unchecked(_state * Multiplier + Addend);
        ConsumptionCount++;
        return (int)((_state >> 16) & 0x7fff);
    }

    public int NextInt(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        return NextInclusive(exclusiveMaximum) - 1;
    }

    public int NextInclusive(int maximum)
    {
        if (maximum < 1) maximum = 1;
        var first = NextRaw();
        var second = NextRaw();
        var selector = NextRaw();
        var selected = selector > SelectionThreshold ? first : second;
        return selected % maximum + 1;
    }
}

public sealed record PhaseBoundaryHash(
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    string Fingerprint)
{
    /// <summary>Writes this boundary in the encoding the state fingerprint chains it under.</summary>
    internal static void WriteCanonical(Stream stream, PhaseBoundaryHash boundary)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(boundary.Turn);
        writer.Write((byte)boundary.Phase);
        MatchStateHasher.WriteNullableByte(writer, boundary.ExecutionPhase is { } phase ? (byte)phase : null);
        MatchStateHasher.WriteString(writer, boundary.Fingerprint);
    }
}

/// <summary>
/// The canonical little-endian encoding of all authoritative match state, reduced to one
/// 128-bit fingerprint.
/// </summary>
/// <remarks>
/// <para>
/// The fingerprint answers "is this the same state?": every replay step, every save and every
/// online turn report carries one, and two clients compare theirs to detect a desync. It is a
/// checksum against divergence and corruption, not a defence against an adversary, so it is
/// XxHash128 rather than a cryptographic digest — the same collision odds for anything that is
/// not deliberately crafted, at a fraction of the cost, and a match fingerprints itself thousands
/// of times.
/// </para>
/// <para>
/// The two histories a match carries — its events and its phase boundaries — only ever grow, so
/// the state keeps a running digest of each, chained entry by entry as they are stored, and the
/// fingerprint folds in the digest rather than the history. That is what keeps the cost of a
/// fingerprint flat over a long match instead of growing with every turn played.
/// </para>
/// </remarks>
public static class MatchStateHasher
{
    /// <summary>Bumped whenever the encoding changes, so no two encodings share a fingerprint space.</summary>
    private const int FormatVersion = 2;

    /// <summary>The number of lowercase hex characters a fingerprint has.</summary>
    public const int FingerprintLength = 2 * DigestBytes;

    private const int DigestBytes = 16;

    /// <summary>The fingerprint of the whole state, as saves, journals and turn reports record it.</summary>
    public static string ComputeFingerprint(MatchState state) =>
        ComputeFingerprint(state, includePhaseHistory: true);

    /// <summary>
    /// The fingerprint a phase boundary records: the whole state except the boundary history it
    /// is about to join, which would otherwise have to contain itself.
    /// </summary>
    internal static string ComputePhaseBoundaryFingerprint(MatchState state) =>
        ComputeFingerprint(state, includePhaseHistory: false);

    /// <summary>Whether <paramref name="value"/> has the shape of a fingerprint.</summary>
    public static bool IsFingerprint(string? value) =>
        value?.Length == FingerprintLength && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>Extends a running history digest by one canonically encoded entry.</summary>
    internal static UInt128 Chain(UInt128 previous, ReadOnlySpan<byte> entry)
    {
        var hash = new XxHash128();
        Span<byte> link = stackalloc byte[DigestBytes];
        BinaryPrimitives.WriteUInt128LittleEndian(link, previous);
        hash.Append(link);
        hash.Append(entry);
        return hash.GetCurrentHashAsUInt128();
    }

    private static string ComputeFingerprint(MatchState state, bool includePhaseHistory)
    {
        ArgumentNullException.ThrowIfNull(state);
        var encoder = CanonicalEncoder.Rent();
        try
        {
            var writer = encoder.Writer;
            writer.Write(Magic);
            writer.Write(FormatVersion);
            WriteDefinitions(writer, state.Definitions);
            writer.Write((byte)state.Setup.Scenario);
            writer.Write((byte)state.Setup.Duration);
            writer.Write(state.Setup.InitialSeed);
            writer.Write((byte)state.Setup.AiMentality);
            writer.Write((byte)state.Setup.AiPolicy);
            writer.Write(state.Setup.Players.Count);
            foreach (var player in state.Setup.Players)
            {
                writer.Write(player.Id.Value);
                WriteString(writer, player.Name);
                writer.Write((byte)player.Controller);
                writer.Write(player.PortraitId);
            }

            writer.Write(state.Coordinator.Turn);
            writer.Write((byte)state.Coordinator.Phase);
            WriteNullableByte(writer, state.Coordinator.ExecutionPhase is { } execution ? (byte)execution : null);
            WriteNullableInt(writer, state.Coordinator.ActivePlayer?.Value);
            writer.Write(state.Random.State);
            writer.Write(state.Random.ConsumptionCount);
            WriteAiState(writer, state);
            writer.Write(state.NextEventSequence);
            writer.Write(state.Events.Count);
            WriteDigest(writer, state.EventHistoryDigest);
            if (includePhaseHistory)
            {
                writer.Write(state.PhaseHashes.Count);
                WriteDigest(writer, state.PhaseHashHistoryDigest);
            }
            WriteOutcome(writer, state.Outcome);

            var players = state.Players.OrderBy(item => item.Id.Value).ToArray();
            writer.Write(players.Length);
            foreach (var player in players) WritePlayer(writer, player);
            // The constructor requires sectors to be identified 0 through 63 in order, so they are
            // already in the order an OrderBy would produce.
            writer.Write(state.Sectors.Count);
            foreach (var sector in state.Sectors) WriteSector(writer, sector);

            var commands = state.Commands.ExecutionPlan().OrderBy(item => item.Sequence).ToArray();
            writer.Write(state.Commands.NextSequence);
            writer.Write(commands.Length);
            foreach (var command in commands)
            {
                writer.Write(command.Sequence);
                WriteCommand(writer, command.Command);
            }

            foreach (var player in players)
            {
                var notifications = state.NotificationsFor(player.Id);
                writer.Write(state.NextNotificationSequence(player.Id));
                writer.Write(notifications.Count);
                foreach (var notification in notifications) WriteNotification(writer, notification);
            }
            foreach (var player in players)
            {
                var inbox = state.ComlinkFor(player.Id);
                writer.Write(inbox.NextSequence);
                writer.Write(inbox.ReadSequences.Count);
                foreach (var sequence in inbox.ReadSequences) writer.Write(sequence);
                writer.Write(inbox.Count);
                foreach (var message in inbox.Messages)
                {
                    writer.Write(message.Sequence);
                    writer.Write(message.Turn);
                    writer.Write(message.Sender.Value);
                    WriteString(writer, message.Text);
                }
            }

            return encoder.FinishHex();
        }
        finally
        {
            encoder.Return();
        }
    }

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RCHX");

    private static void WriteAiState(BinaryWriter writer, MatchState state)
    {
        foreach (var reaction in state.AiStrategy.CaptureReactions()) writer.Write(reaction);
        foreach (var attitude in state.AiStrategy.CaptureAttitudes()) writer.Write(attitude);
        var planning = state.AiPlanning;
        foreach (var role in planning.CaptureCurrentHireRoles()) writer.Write(role);
        foreach (var role in planning.CapturePreviousHireRoles()) writer.Write(role);
        foreach (var family in planning.CaptureFamilies()) writer.Write(family);
        foreach (var anchor in planning.CaptureSectorAnchors()) writer.Write(anchor);
        foreach (var action in planning.CaptureOlderActions()) writer.Write((byte)action);
        foreach (var action in planning.CapturePreviousActions()) writer.Write((byte)action);
        foreach (var action in planning.CapturePlannedActions()) writer.Write((byte)action);
        foreach (var target in planning.CaptureOlderTargets()) WriteAiTarget(writer, target);
        foreach (var target in planning.CapturePreviousTargets()) WriteAiTarget(writer, target);
        foreach (var target in planning.CapturePlannedTargets()) WriteAiTarget(writer, target);
        foreach (var hasPlanned in planning.CaptureHasPlanned()) writer.Write(hasPlanned);
        foreach (var cooldown in planning.CaptureWeaponCooldowns()) writer.Write(cooldown);
        foreach (var cooldown in planning.CaptureArmorCooldowns()) writer.Write(cooldown);
        foreach (var sector in planning.CaptureFormationSectors()) writer.Write(sector);
        foreach (var sector in planning.CaptureCoverageSectors()) writer.Write(sector);
    }

    private static void WriteOutcome(BinaryWriter writer, MatchOutcome? outcome)
    {
        writer.Write(outcome is not null);
        if (outcome is null) return;
        WriteOutcomeBody(
            writer, outcome.Scenario, outcome.Reason, outcome.Turn,
            outcome.Winners, outcome.Standings, outcome.Awards);
    }

    /// <summary>
    /// The fields a match outcome contributes after its presence flag. The state hash and the
    /// MatchEnded event encode them in the same order and width.
    /// </summary>
    internal static void WriteOutcomeBody(
        BinaryWriter writer,
        ScenarioId scenario,
        MatchEndReason reason,
        int turn,
        IReadOnlyList<PlayerId> winners,
        IReadOnlyList<MatchStanding> standings,
        IReadOnlyList<EndgameAwardResult> awards)
    {
        writer.Write((byte)scenario);
        writer.Write((byte)reason);
        writer.Write(turn);
        writer.Write(winners.Count);
        foreach (var winner in winners) writer.Write(winner.Value);
        writer.Write(standings.Count);
        foreach (var standing in standings)
        {
            writer.Write(standing.Player.Value);
            writer.Write(standing.Place);
            writer.Write(standing.Score);
        }
        writer.Write(awards.Count);
        foreach (var award in awards)
        {
            writer.Write((byte)award.Award);
            writer.Write(award.Value);
            writer.Write(award.Recipients.Count);
            foreach (var recipient in award.Recipients) writer.Write(recipient.Value);
        }
    }

    private static void WriteDigest(BinaryWriter writer, UInt128 digest)
    {
        Span<byte> bytes = stackalloc byte[DigestBytes];
        BinaryPrimitives.WriteUInt128LittleEndian(bytes, digest);
        writer.Write(bytes);
    }

    /// <summary>
    /// A per-thread canonical byte encoder feeding one hash.
    /// </summary>
    /// <remarks>
    /// A fingerprint used to be built in a fresh <see cref="MemoryStream"/> regrown by doubling on
    /// every call, thousands of calls per match, so gen-2 collections dominated a headless replay.
    /// The buffer now lives with the thread and is reset between calls.
    /// </remarks>
    private sealed class CanonicalEncoder
    {
        [ThreadStatic] private static CanonicalEncoder? _current;

        private readonly MemoryStream _buffered = new(16 * 1024);
        private readonly XxHash128 _hash = new();
        private bool _inUse;

        private CanonicalEncoder()
        {
            Writer = new BinaryWriter(_buffered, Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>The writer every canonical field goes through.</summary>
        public BinaryWriter Writer { get; }

        public static CanonicalEncoder Rent()
        {
            var encoder = _current ??= new CanonicalEncoder();
            // A caller interrupted by an exception leaves the thread's encoder mid-document; a
            // nested fingerprint from within a fingerprint would too. Either gets a fresh one.
            if (encoder._inUse) encoder = new CanonicalEncoder();
            encoder._inUse = true;
            return encoder;
        }

        public string FinishHex()
        {
            Writer.Flush();
            _hash.Append(_buffered.GetBuffer().AsSpan(0, checked((int)_buffered.Length)));
            Span<byte> digest = stackalloc byte[DigestBytes];
            var written = _hash.GetHashAndReset(digest);
            return Convert.ToHexStringLower(digest[..written]);
        }

        public void Return()
        {
            Writer.Flush();
            _buffered.SetLength(0);
            // Whatever a failed call left in the hash must not leak into the next document.
            _hash.Reset();
            _inUse = false;
        }
    }

    private static void WriteAiTarget(BinaryWriter writer, AiActionTarget target)
    {
        writer.Write(target.First);
        writer.Write(target.Second);
    }

    /// <summary>
    /// The canonical definition digest, built once per <see cref="OriginalData"/> instance.
    /// </summary>
    /// <remarks>
    /// The definition block is the same bytes on every call for a given definition set, and a turn
    /// hashes 8 + 2P boundaries. Folding its digest into the state fingerprint avoids both
    /// re-serialising and re-hashing every site, gang and item (names and descriptions included).
    /// The entries are weak, so a definition set the process stops using is still collectable.
    /// </remarks>
    private sealed record DefinitionDigest(UInt128 Value);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OriginalData, DefinitionDigest>
        DefinitionDigests = [];

    private static void WriteDefinitions(BinaryWriter writer, OriginalData definitions)
    {
        var digest = DefinitionDigests.GetValue(definitions, static value =>
        {
            using var stream = new MemoryStream();
            using (var blockWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                WriteDefinitionBlock(blockWriter, value);
            var hash = new XxHash128();
            hash.Append(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
            return new DefinitionDigest(hash.GetCurrentHashAsUInt128());
        });
        WriteDigest(writer, digest.Value);
    }

    private static void WriteDefinitionBlock(BinaryWriter writer, OriginalData definitions)
    {
        writer.Write(definitions.Sites.Count);
        foreach (var site in definitions.Sites.OrderBy(item => item.Id))
        {
            writer.Write(site.Id); WriteString(writer, site.Name); writer.Write(site.Resistance); writer.Write(site.Support);
            writer.Write(site.Frequency); writer.Write(site.Tolerance); writer.Write(site.Cash); WriteStatistics(writer, site.Stats);
            writer.Write(site.Special);
        }
        writer.Write(definitions.Gangs.Count);
        foreach (var gang in definitions.Gangs.OrderBy(item => item.Id))
        {
            writer.Write(gang.Id); WriteString(writer, gang.Name); WriteString(writer, gang.Description); writer.Write(gang.Force);
            writer.Write(gang.Upkeep); writer.Write(gang.TechLevel); WriteStatistics(writer, gang.Stats);
        }
        writer.Write(definitions.Items.Count);
        foreach (var item in definitions.Items.OrderBy(value => value.Id))
        {
            writer.Write(item.Id); WriteString(writer, item.Name); WriteString(writer, item.Description); writer.Write(item.Type);
            writer.Write(item.ResearchDifficulty); writer.Write(item.Cost); writer.Write(item.TechLevel); WriteStatistics(writer, item.Stats);
            writer.Write(item.AttackAnimation); writer.Write(item.HitAnimation); writer.Write(item.Sound); writer.Write(item.CombatPortraitFrame);
        }
    }

    private static void WritePlayer(BinaryWriter writer, MatchPlayerState player)
    {
        writer.Write(player.Id.Value); writer.Write((byte)player.Status); writer.Write(player.Cash); writer.Write(player.Support);
        writer.Write(player.BigManPoints);
        writer.Write(player.UsesMaximumHireForce);
        writer.Write(player.Gangs.Count);
        // Roster slot order is play state: every phase resolver orders by slot, hire reuse takes the
        // first inactive slot, and the AI planning tables are indexed by slot. Hashing in gang id
        // order let two states that resolve differently produce the same fingerprint once a slot had
        // been reused, which a repair snapshot with a reordered roster could have exploited.
        foreach (var gang in player.Gangs)
        {
            writer.Write(gang.Id.Value); writer.Write(gang.Owner.Value); writer.Write(gang.DefinitionId); writer.Write(gang.SectorId);
            writer.Write(gang.Force); writer.Write(gang.Hidden); writer.Write(gang.HiredThisTurn);
            WriteNullableShort(writer, gang.WeaponItemId); WriteNullableShort(writer, gang.ArmorItemId); WriteNullableShort(writer, gang.MiscellaneousItemId);
        }
        foreach (var slot in player.HireOfferSlots)
        {
            WriteNullableShort(writer, slot.GangDefinitionId);
            WriteNullableShort(writer, slot.ExcludedDefinitionId);
            WriteNullableShort(writer, slot.LegacyReplacementDefinitionId);
        }
        WriteNullableShort(writer, player.SnubbedHireOffer);
        WriteNullableInt(writer, player.SnubbedHireOfferSlot);
        writer.Write(player.PendingHires.Count);
        foreach (var hire in player.PendingHires)
        {
            writer.Write(hire.GangDefinitionId);
            writer.Write(hire.TargetSectorId);
            writer.Write(hire.OfferSlot);
            writer.Write(hire.InitialCostPaid);
        }
        writer.Write(player.ResearchProgress.Count); foreach (var pair in player.ResearchProgress.OrderBy(item => item.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        writer.Write(player.ResearchedItems.Count); foreach (var id in player.ResearchedItems.Order()) writer.Write(id);
        writer.Write(player.Inventory.Count); foreach (var pair in player.Inventory.OrderBy(item => item.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        writer.Write(player.Statistics.CashEarned); writer.Write(player.Statistics.CashSpent); writer.Write(player.Statistics.DamageInflicted);
        writer.Write(player.Statistics.Casualties); writer.Write(player.Statistics.Overthrows);
        writer.Write(player.Statistics.TimesHidden);
    }

    private static void WriteSector(BinaryWriter writer, MatchSectorState sector)
    {
        writer.Write(sector.Id); WriteNullableInt(writer, sector.Owner?.Value); writer.Write(sector.Tolerance);
        writer.Write(sector.CrackdownActive); writer.Write(sector.IsImportant); writer.Write(sector.Sites.Count);
        // A sector orders its sites by slot when it is built, so the list is already in slot order.
        foreach (var site in sector.Sites)
        {
            writer.Write(site.Slot); writer.Write(site.DefinitionId); writer.Write(site.Resistance); WriteNullableInt(writer, site.InfluencedBy?.Value);
        }
        writer.Write(sector.Income);
        writer.Write(sector.CrackdownTurnsRemaining);
        writer.Write(sector.CrackdownHistory.Count);
        foreach (var turn in sector.CrackdownHistory) writer.Write(turn);
    }

    private static void WriteCommand(BinaryWriter writer, GameCommand command)
    {
        writer.Write(command.Player.Value); writer.Write(command.Gang.Value); writer.Write((byte)command.Action);
        WriteTarget(writer, command.Target); writer.Write(command.Repeat);
        WriteNullableTarget(writer, command.SecondaryTarget);
        WriteNullableTarget(writer, command.TertiaryTarget);
        WriteNullableTarget(writer, command.QuaternaryTarget);
    }

    internal static void WriteNullableTarget(BinaryWriter writer, CommandTarget? value)
    {
        writer.Write(value.HasValue);
        if (value is { } target) WriteTarget(writer, target);
    }

    internal static void WriteTarget(BinaryWriter writer, CommandTarget target) { writer.Write((byte)target.Kind); writer.Write(target.Id); }
    private static void WriteNotification(BinaryWriter writer, GameNotification notification)
    {
        writer.Write(notification.Sequence); writer.Write(notification.Turn); writer.Write((byte)notification.Phase);
        WriteNullableByte(writer, notification.ExecutionPhase is { } phase ? (byte)phase : null); writer.Write((byte)notification.Kind);
        WriteNullableInt(writer, notification.Gang?.Value); WriteNullableInt(writer, notification.SectorId); writer.Write(notification.RelatedEventSequence ?? -1);
    }

    private static void WriteStatistics(BinaryWriter writer, Statistics value)
    {
        writer.Write(value.Combat); writer.Write(value.Defense); writer.Write(value.Stealth); writer.Write(value.Detect);
        writer.Write(value.Chaos); writer.Write(value.Control); writer.Write(value.Heal); writer.Write(value.Influence);
        writer.Write(value.Research); writer.Write(value.Strength); writer.Write(value.Blade); writer.Write(value.Range);
        writer.Write(value.Fighting); writer.Write(value.MartialArts);
    }

    internal static void WriteString(BinaryWriter writer, string value)
    {
        // Length-prefixed UTF-8, encoded straight into a stack buffer when it fits: names and
        // hex digests are short, and a fingerprint writes hundreds of them.
        var byteCount = Encoding.UTF8.GetByteCount(value);
        writer.Write(byteCount);
        if (byteCount <= StackStringBytes)
        {
            Span<byte> bytes = stackalloc byte[StackStringBytes];
            var written = Encoding.UTF8.GetBytes(value, bytes);
            writer.Write(bytes[..written]);
        }
        else
        {
            writer.Write(Encoding.UTF8.GetBytes(value));
        }
    }
    private const int StackStringBytes = 256;
    internal static void WriteNullableInt(BinaryWriter writer, int? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    internal static void WriteNullableShort(BinaryWriter writer, short? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    internal static void WriteNullableByte(BinaryWriter writer, byte? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
}
