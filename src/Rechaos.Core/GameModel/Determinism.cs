using System.Security.Cryptography;
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
    string Sha256)
{
    /// <summary>Appends this boundary in the encoding the state fingerprint hashes it under.</summary>
    internal static void AppendCanonical(Stream stream, PhaseBoundaryHash boundary)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(boundary.Turn);
        writer.Write((byte)boundary.Phase);
        MatchStateHasher.WriteNullableByte(writer, boundary.ExecutionPhase is { } phase ? (byte)phase : null);
        MatchStateHasher.WriteString(writer, boundary.Sha256);
    }
}

/// <summary>Canonical little-endian encoding of all authoritative headless match state.</summary>
public static class MatchStateHasher
{
    private const int FormatVersion = 28;

    internal static string ComputeLegacySha256(MatchState state) =>
        ComputeSha256(state, 4, includeSectorIncome: false, includeCrackdownDuration: false,
            includeCrackdownHistory: false, includeDifficulty: false, includeAiStrategy: false,
            includeAiPlanning: false, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionTwoSha256(MatchState state) =>
        ComputeSha256(state, 5, includeSectorIncome: true, includeCrackdownDuration: false,
            includeCrackdownHistory: false, includeDifficulty: false, includeAiStrategy: false,
            includeAiPlanning: false, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionThreeSha256(MatchState state) =>
        ComputeSha256(state, 6, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: false, includeDifficulty: false, includeAiStrategy: false,
            includeAiPlanning: false, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionFourSha256(MatchState state) =>
        ComputeSha256(state, 7, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: false, includeAiStrategy: false,
            includeAiPlanning: false, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionFiveSha256(MatchState state) =>
        ComputeSha256(state, 8, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: false,
            includeAiPlanning: false, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionSixSha256(MatchState state) =>
        ComputeSha256(state, 9, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: false, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionTenSha256(MatchState state) =>
        ComputeSha256(state, 10, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: false, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionElevenSha256(MatchState state) =>
        ComputeSha256(state, 11, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: false,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionTwelveSha256(MatchState state) =>
        ComputeSha256(state, 12, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: false, includeSectorAnchors: false);

    internal static string ComputeVersionThirteenSha256(MatchState state) =>
        ComputeSha256(state, 13, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: false);

    internal static string ComputeVersionFourteenSha256(MatchState state) =>
        ComputeSha256(state, 14, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true);

    internal static string ComputeVersionFifteenSha256(MatchState state) =>
        ComputeSha256(state, 15, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true);

    internal static string ComputeVersionSixteenSha256(MatchState state) =>
        ComputeSha256(state, 16, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true);

    internal static string ComputeVersionSeventeenSha256(MatchState state) =>
        ComputeSha256(state, 17, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true);

    internal static string ComputeVersionEighteenSha256(MatchState state) =>
        ComputeSha256(state, 18, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true);

    internal static string ComputeVersionNineteenSha256(MatchState state) =>
        ComputeSha256(state, 19, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true);

    internal static string ComputeVersionTwentySha256(MatchState state) =>
        ComputeSha256(state, 20, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true);

    internal static string ComputeVersionTwentyOneSha256(MatchState state) =>
        ComputeSha256(state, 21, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true);

    internal static string ComputeVersionTwentyTwoSha256(MatchState state) =>
        ComputeSha256(state, 22, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true);

    internal static string ComputeVersionTwentyThreeSha256(MatchState state) =>
        ComputeSha256(state, 23, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true, includeEventHistory: true);

    internal static string ComputeVersionTwentyFourSha256(MatchState state) =>
        ComputeSha256(state, 24, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true, includeEventHistory: true,
            includePhaseHistory: true);

    internal static string ComputeVersionTwentyFiveSha256(MatchState state) =>
        ComputeSha256(state, 25, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true, includeEventHistory: true,
            includePhaseHistory: true, includeComlinkReadSequences: true);

    internal static string ComputeVersionTwentySixSha256(MatchState state) =>
        ComputeSha256(state, 26, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true, includeEventHistory: true,
            includePhaseHistory: true, includeComlinkReadSequences: true,
            includeAiPolicy: true);

    internal static string ComputeVersionTwentySevenSha256(MatchState state) =>
        ComputeSha256(state, 27, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true, includeEventHistory: true,
            includePhaseHistory: true, includeComlinkReadSequences: true,
            includeAiPolicy: true, includeSectorChaos: false);

    public static string ComputeSha256(MatchState state)
        => ComputeSha256(state, FormatVersion, includeSectorIncome: true, includeCrackdownDuration: true,
            includeCrackdownHistory: true, includeDifficulty: true, includeAiStrategy: true,
            includeAiPlanning: true, includeHireSlots: true, includeHirePayment: true,
            includeMaximumHireForce: true, includeSectorAnchors: true, includeAiActions: true,
            includeFirstPlanningFlags: true, includeAiTargets: true, includeAiCooldowns: true,
            includeAiFormationSectors: true, includeAiCoverageSectors: true,
            includeComlink: true, includeTertiaryTargets: true,
            includeQuaternaryTargets: true, includeEventHistory: true,
            includePhaseHistory: true, includeComlinkReadSequences: true,
            includeAiPolicy: true, includeSectorChaos: false,
            includeRosterSlotOrder: true);

    private static string ComputeSha256(
        MatchState state,
        int formatVersion,
        bool includeSectorIncome,
        bool includeCrackdownDuration,
        bool includeCrackdownHistory,
        bool includeDifficulty,
        bool includeAiStrategy,
        bool includeAiPlanning,
        bool includeHireSlots,
        bool includeHirePayment,
        bool includeMaximumHireForce,
        bool includeSectorAnchors,
        bool includeAiActions = false,
        bool includeFirstPlanningFlags = false,
        bool includeAiTargets = false,
        bool includeAiCooldowns = false,
        bool includeAiFormationSectors = false,
        bool includeAiCoverageSectors = false,
        bool includeComlink = false,
        bool includeTertiaryTargets = false,
        bool includeQuaternaryTargets = false,
        bool includeEventHistory = false,
        bool includePhaseHistory = false,
        bool includeComlinkReadSequences = false,
        bool includeAiPolicy = false,
        bool includeSectorChaos = true,
        bool includeRosterSlotOrder = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        var encoder = CanonicalEncoder.Rent();
        try
        {
            var writer = encoder.Writer;
            writer.Write(Magic);
            writer.Write(formatVersion);
            WriteDefinitions(writer, state.Definitions);
            writer.Write((byte)state.Setup.Scenario);
            writer.Write((byte)state.Setup.Duration);
            writer.Write(state.Setup.InitialSeed);
            if (includeDifficulty) writer.Write((byte)state.Setup.AiMentality);
            if (includeAiPolicy) writer.Write((byte)state.Setup.AiPolicy);
            writer.Write(state.Setup.Players.Count);
            foreach (var player in state.Setup.Players)
            {
                writer.Write(player.Id.Value);
                WriteString(writer, player.Name);
                writer.Write((byte)player.Controller);
                if (includeDifficulty) writer.Write(player.PortraitId);
            }

            writer.Write(state.Coordinator.Turn);
            writer.Write((byte)state.Coordinator.Phase);
            WriteNullableByte(writer, state.Coordinator.ExecutionPhase is { } execution ? (byte)execution : null);
            WriteNullableInt(writer, state.Coordinator.ActivePlayer?.Value);
            writer.Write(state.Random.State);
            writer.Write(state.Random.ConsumptionCount);
            if (includeAiStrategy)
            {
                foreach (var reaction in state.AiStrategy.CaptureReactions()) writer.Write(reaction);
                foreach (var attitude in state.AiStrategy.CaptureAttitudes()) writer.Write(attitude);
            }
            if (includeAiPlanning)
            {
                foreach (var role in state.AiPlanning.CaptureCurrentHireRoles()) writer.Write(role);
                foreach (var role in state.AiPlanning.CapturePreviousHireRoles()) writer.Write(role);
                foreach (var family in state.AiPlanning.CaptureFamilies()) writer.Write(family);
                if (includeSectorAnchors)
                    foreach (var anchor in state.AiPlanning.CaptureSectorAnchors()) writer.Write(anchor);
                if (includeAiActions)
                {
                    foreach (var action in state.AiPlanning.CaptureOlderActions()) writer.Write((byte)action);
                    foreach (var action in state.AiPlanning.CapturePreviousActions()) writer.Write((byte)action);
                    foreach (var action in state.AiPlanning.CapturePlannedActions()) writer.Write((byte)action);
                }
                if (includeAiTargets)
                {
                    foreach (var target in state.AiPlanning.CaptureOlderTargets()) WriteAiTarget(writer, target);
                    foreach (var target in state.AiPlanning.CapturePreviousTargets()) WriteAiTarget(writer, target);
                    foreach (var target in state.AiPlanning.CapturePlannedTargets()) WriteAiTarget(writer, target);
                }
                if (includeFirstPlanningFlags)
                    foreach (var hasPlanned in state.AiPlanning.CaptureHasPlanned()) writer.Write(hasPlanned);
                if (includeAiCooldowns)
                {
                    foreach (var cooldown in state.AiPlanning.CaptureWeaponCooldowns()) writer.Write(cooldown);
                    foreach (var cooldown in state.AiPlanning.CaptureArmorCooldowns()) writer.Write(cooldown);
                }
                if (includeAiFormationSectors)
                    foreach (var sector in state.AiPlanning.CaptureFormationSectors()) writer.Write(sector);
                if (includeAiCoverageSectors)
                    foreach (var sector in state.AiPlanning.CaptureCoverageSectors()) writer.Write(sector);
            }
            writer.Write(state.NextEventSequence);
            if (includeEventHistory)
            {
                writer.Write(state.Events.Count);
                encoder.Append(state.CanonicalEventHistory);
            }
            if (includePhaseHistory)
            {
                writer.Write(state.PhaseHashes.Count);
                encoder.Append(state.CanonicalPhaseHashHistory);
            }
            writer.Write(state.Outcome is not null);
            if (state.Outcome is { } outcome)
            {
                writer.Write((byte)outcome.Scenario);
                writer.Write((byte)outcome.Reason);
                writer.Write(outcome.Turn);
                writer.Write(outcome.Winners.Count);
                foreach (var winner in outcome.Winners) writer.Write(winner.Value);
                writer.Write(outcome.Standings.Count);
                foreach (var standing in outcome.Standings)
                {
                    writer.Write(standing.Player.Value);
                    writer.Write(standing.Place);
                    writer.Write(standing.Score);
                }
                writer.Write(outcome.Awards.Count);
                foreach (var award in outcome.Awards)
                {
                    writer.Write((byte)award.Award);
                    writer.Write(award.Value);
                    writer.Write(award.Recipients.Count);
                    foreach (var recipient in award.Recipients) writer.Write(recipient.Value);
                }
            }

            var players = state.Players.OrderBy(item => item.Id.Value).ToArray();
            writer.Write(players.Length);
            foreach (var player in players)
                WritePlayer(writer, player, includeHireSlots, includeHirePayment,
                    includeMaximumHireForce, includeRosterSlotOrder);
            // The constructor requires sectors to be identified 0 through 63 in order, so they are
            // already in the order an OrderBy would produce.
            writer.Write(state.Sectors.Count);
            foreach (var sector in state.Sectors)
                WriteSector(writer, sector, includeSectorIncome, includeCrackdownDuration,
                    includeCrackdownHistory, includeSectorChaos);

            var commands = state.Commands.ExecutionPlan().OrderBy(item => item.Sequence).ToArray();
            writer.Write(state.Commands.NextSequence);
            writer.Write(commands.Length);
            foreach (var command in commands)
            {
                writer.Write(command.Sequence);
                WriteCommand(writer, command.Command, includeTertiaryTargets, includeQuaternaryTargets);
            }

            foreach (var player in players)
            {
                var notifications = state.NotificationsFor(player.Id);
                writer.Write(state.NextNotificationSequence(player.Id));
                writer.Write(notifications.Count);
                foreach (var notification in notifications) WriteNotification(writer, notification);
            }
            if (includeComlink)
            {
                foreach (var player in players)
                {
                    var inbox = state.ComlinkFor(player.Id);
                    writer.Write(inbox.NextSequence);
                    if (includeComlinkReadSequences)
                    {
                        writer.Write(inbox.ReadSequences.Count);
                        foreach (var sequence in inbox.ReadSequences) writer.Write(sequence);
                    }
                    else
                    {
                        writer.Write(inbox.LegacyReadThroughSequence);
                    }
                    writer.Write(inbox.Count);
                    foreach (var message in inbox.Messages)
                    {
                        writer.Write(message.Sequence);
                        writer.Write(message.Turn);
                        writer.Write(message.Sender.Value);
                        WriteString(writer, message.Text);
                    }
                }
            }

            return encoder.FinishHex();
        }
        finally
        {
            encoder.Return();
        }
    }

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RCHS");

    /// <summary>
    /// A per-thread canonical byte encoder feeding one incremental SHA-256.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fingerprint used to be built in a fresh <see cref="MemoryStream"/>: a few hundred
    /// kilobytes late in a match, regrown by doubling on every call, thousands of calls per match,
    /// so the large-object heap churned and gen-2 collections dominated a headless replay.
    /// </para>
    /// <para>
    /// The buffer now lives with the thread and is reset between calls, and the two append-only
    /// histories the state already keeps in canonical form are fed to the hash straight from the
    /// state's own buffers, so the whole document is never assembled in one place. The digest is
    /// the same: SHA-256 of a sequence of appends is SHA-256 of their concatenation.
    /// </para>
    /// </remarks>
    private sealed class CanonicalEncoder
    {
        [ThreadStatic] private static CanonicalEncoder? _current;

        private readonly MemoryStream _buffered = new(16 * 1024);
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private bool _inUse;

        private CanonicalEncoder()
        {
            Writer = new BinaryWriter(_buffered, Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>The writer every canonical field goes through; buffered until an append or the finish.</summary>
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

        /// <summary>Feeds bytes that are already canonical, after everything written so far.</summary>
        public void Append(ReadOnlySpan<byte> canonical)
        {
            Flush();
            _hash.AppendData(canonical);
        }

        public string FinishHex()
        {
            Flush();
            Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
            var written = _hash.GetHashAndReset(digest);
            return Convert.ToHexStringLower(digest[..written]);
        }

        public void Return()
        {
            Writer.Flush();
            _buffered.SetLength(0);
            // Whatever a failed call left in the hash must not leak into the next document.
            Span<byte> discarded = stackalloc byte[SHA256.HashSizeInBytes];
            _hash.TryGetHashAndReset(discarded, out _);
            _inUse = false;
        }

        private void Flush()
        {
            Writer.Flush();
            _hash.AppendData(_buffered.GetBuffer(), 0, checked((int)_buffered.Length));
            _buffered.SetLength(0);
        }
    }

    private static void WriteAiTarget(BinaryWriter writer, AiActionTarget target)
    {
        writer.Write(target.First);
        writer.Write(target.Second);
    }

    /// <summary>
    /// The canonical definition block, built once per <see cref="OriginalData"/> instance.
    /// </summary>
    /// <remarks>
    /// The block is the same bytes on every call for a given definition set, and a turn hashes
    /// 8 + 2P boundaries, so re-serialising every site, gang and item (names and descriptions
    /// included) each time was pure repetition. The entries are weak, so a definition set the
    /// process stops using is still collectable.
    /// </remarks>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OriginalData, byte[]>
        DefinitionBlocks = [];

    private static void WriteDefinitions(BinaryWriter writer, OriginalData definitions)
    {
        var block = DefinitionBlocks.GetValue(definitions, static value =>
        {
            using var stream = new MemoryStream();
            using (var blockWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                WriteDefinitionBlock(blockWriter, value);
            return stream.ToArray();
        });
        writer.Write(block);
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

    private static void WritePlayer(
        BinaryWriter writer,
        MatchPlayerState player,
        bool includeHireSlots,
        bool includeHirePayment,
        bool includeMaximumHireForce,
        bool includeRosterSlotOrder)
    {
        writer.Write(player.Id.Value); writer.Write((byte)player.Status); writer.Write(player.Cash); writer.Write(player.Support);
        writer.Write(player.BigManPoints);
        if (includeMaximumHireForce) writer.Write(player.UsesMaximumHireForce);
        writer.Write(player.Gangs.Count);
        // Roster slot order is play state: every phase resolver orders by slot, hire reuse takes the
        // first inactive slot, and the AI planning tables are indexed by slot. Hashing in gang id
        // order let two states that resolve differently produce the same fingerprint once a slot had
        // been reused, which a repair snapshot with a reordered roster could have exploited.
        var roster = includeRosterSlotOrder
            ? (IEnumerable<MatchGangState>)player.Gangs
            : player.Gangs.OrderBy(item => item.Id.Value);
        foreach (var gang in roster)
        {
            writer.Write(gang.Id.Value); writer.Write(gang.Owner.Value); writer.Write(gang.DefinitionId); writer.Write(gang.SectorId);
            writer.Write(gang.Force); writer.Write(gang.Hidden); writer.Write(gang.HiredThisTurn);
            WriteNullableShort(writer, gang.WeaponItemId); WriteNullableShort(writer, gang.ArmorItemId); WriteNullableShort(writer, gang.MiscellaneousItemId);
        }
        if (includeHireSlots)
        {
            foreach (var slot in player.HireOfferSlots)
            {
                WriteNullableShort(writer, slot.GangDefinitionId);
                WriteNullableShort(writer, slot.ExcludedDefinitionId);
                WriteNullableShort(writer, slot.LegacyReplacementDefinitionId);
            }
        }
        else
        {
            var legacyHirePool = player.CaptureLegacyHirePool();
            writer.Write(legacyHirePool.Count);
            foreach (var id in legacyHirePool) writer.Write(id);
        }
        WriteNullableShort(writer, player.SnubbedHireOffer);
        if (includeHireSlots) WriteNullableInt(writer, player.SnubbedHireOfferSlot);
        writer.Write(player.PendingHires.Count);
        foreach (var hire in player.PendingHires)
        {
            writer.Write(hire.GangDefinitionId);
            writer.Write(hire.TargetSectorId);
            if (includeHireSlots) writer.Write(hire.OfferSlot);
            if (includeHirePayment) writer.Write(hire.InitialCostPaid);
        }
        writer.Write(player.ResearchProgress.Count); foreach (var pair in player.ResearchProgress.OrderBy(item => item.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        writer.Write(player.ResearchedItems.Count); foreach (var id in player.ResearchedItems.Order()) writer.Write(id);
        writer.Write(player.Inventory.Count); foreach (var pair in player.Inventory.OrderBy(item => item.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        writer.Write(player.Statistics.CashEarned); writer.Write(player.Statistics.CashSpent); writer.Write(player.Statistics.DamageInflicted);
        writer.Write(player.Statistics.Casualties); writer.Write(player.Statistics.Overthrows);
        writer.Write(player.Statistics.TimesHidden);
    }

    private static void WriteSector(
        BinaryWriter writer,
        MatchSectorState sector,
        bool includeIncome,
        bool includeCrackdownDuration,
        bool includeCrackdownHistory,
        bool includeChaos)
    {
        writer.Write(sector.Id); WriteNullableInt(writer, sector.Owner?.Value); writer.Write(sector.Tolerance);
        if (includeChaos) writer.Write(sector.LegacyChaos);
        writer.Write(sector.CrackdownActive); writer.Write(sector.IsImportant); writer.Write(sector.Sites.Count);
        // A sector orders its sites by slot when it is built, so the list is already in slot order.
        foreach (var site in sector.Sites)
        {
            writer.Write(site.Slot); writer.Write(site.DefinitionId); writer.Write(site.Resistance); WriteNullableInt(writer, site.InfluencedBy?.Value);
        }
        if (includeIncome) writer.Write(sector.Income);
        if (includeCrackdownDuration) writer.Write(sector.CrackdownTurnsRemaining);
        if (includeCrackdownHistory)
        {
            writer.Write(sector.CrackdownHistory.Count);
            foreach (var turn in sector.CrackdownHistory) writer.Write(turn);
        }
    }

    private static void WriteCommand(
        BinaryWriter writer,
        GameCommand command,
        bool includeTertiaryTarget,
        bool includeQuaternaryTarget)
    {
        writer.Write(command.Player.Value); writer.Write(command.Gang.Value); writer.Write((byte)command.Action);
        WriteTarget(writer, command.Target); writer.Write(command.Repeat);
        writer.Write(command.SecondaryTarget.HasValue); if (command.SecondaryTarget is { } target) WriteTarget(writer, target);
        if (includeTertiaryTarget)
        {
            writer.Write(command.TertiaryTarget.HasValue);
            if (command.TertiaryTarget is { } tertiary) WriteTarget(writer, tertiary);
        }
        if (includeQuaternaryTarget)
        {
            writer.Write(command.QuaternaryTarget.HasValue);
            if (command.QuaternaryTarget is { } quaternary) WriteTarget(writer, quaternary);
        }
    }

    private static void WriteTarget(BinaryWriter writer, CommandTarget target) { writer.Write((byte)target.Kind); writer.Write(target.Id); }
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
    private static void WriteNullableInt(BinaryWriter writer, int? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    private static void WriteNullableShort(BinaryWriter writer, short? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    internal static void WriteNullableByte(BinaryWriter writer, byte? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
}
