using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

public enum ReplayOperationKind : byte
{
    SubmitCommand,
    CancelCommand,
    QueueHire,
    SnubHireOffer,
    FinishUpkeep,
    FinishCommand,
    FinishExecutionPhase,
    FinishHire,
    FinishPlayerElimination,
    DismissNotification,
    PrepareHireOffers,
    PrepareAiPlanning,
    PrepareAiHiring,
    SendComlinkMessage,
    MarkComlinkRead,
    /// <summary>One ordered pass drawing every seat's offers, as a simultaneous turn needs.</summary>
    PrepareSimultaneousHireOffers
}

public sealed record ReplayStep(
    ReplayOperationKind Kind,
    string ResultingStateSha256,
    GameCommand? Command = null,
    PlayerId? Player = null,
    GangId? Gang = null,
    short? GangDefinitionId = null,
    int? SectorId = null,
    bool? Accepted = null,
    int? ValidationCode = null,
    IReadOnlyList<PlayerId>? Recipients = null,
    string? Text = null,
    long? ComlinkSequence = null);

/// <summary>
/// Records every public match mutation together with its resulting canonical hash.
/// Callers must route mutations through this type while recording.
/// </summary>
public sealed class MatchReplayRecorder
{
    private readonly byte[] _initialSnapshot;
    private readonly string _initialStateSha256;
    private readonly List<ReplayStep> _steps = [];
    private string _currentStateSha256;

    public MatchReplayRecorder(MatchState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        _initialStateSha256 = MatchStateHasher.ComputeSha256(state);
        _currentStateSha256 = _initialStateSha256;
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, state);
        _initialSnapshot = stream.ToArray();
    }

    /// <summary>Adopts a journal that was recorded earlier, so play continues appending to it.</summary>
    private MatchReplayRecorder(
        MatchState state,
        byte[] initialSnapshot,
        string initialStateSha256,
        IReadOnlyList<ReplayStep> steps)
    {
        State = state;
        _initialSnapshot = initialSnapshot;
        _initialStateSha256 = initialStateSha256;
        _steps.AddRange(steps);
        _currentStateSha256 = steps.Count == 0
            ? initialStateSha256
            : steps[^1].ResultingStateSha256;
        // The state has to be the one the journal ends at, or the first mutation would append a
        // step whose fingerprint nothing can reproduce. The caller replayed it to get here.
        EnsureSynchronized();
    }

    public MatchState State { get; }
    public IReadOnlyList<ReplayStep> Steps => _steps.ToArray();

    /// <summary>How many mutations this journal holds; the cost of carrying it, at a glance.</summary>
    public int StepCount => _steps.Count;

    /// <summary>
    /// Resumes recording into an existing journal.
    /// </summary>
    /// <remarks>
    /// This is what makes a saved game's history survive being loaded. Starting a fresh recorder
    /// from a loaded snapshot records only what happens next, so the one thing a bug report needs —
    /// the turns that led to the bug — is exactly what would be missing.
    /// </remarks>
    /// <param name="state">The state <paramref name="steps"/> ends at, already replayed.</param>
    internal static MatchReplayRecorder Resume(
        MatchState state,
        byte[] initialSnapshot,
        string initialStateSha256,
        IReadOnlyList<ReplayStep> steps) =>
        new(state, initialSnapshot, initialStateSha256, steps);

    public CommandSubmissionResult Submit(GameCommand command)
    {
        EnsureSynchronized();
        var result = State.Submit(command);
        Add(new ReplayStep(
            ReplayOperationKind.SubmitCommand, CurrentHash(), Command: command,
            Accepted: result.Accepted, ValidationCode: (int)result.Validation.Code));
        return result;
    }

    public CommandSubmissionResult Cancel(PlayerId player, GangId gang)
    {
        EnsureSynchronized();
        var result = State.Cancel(player, gang);
        Add(new ReplayStep(
            ReplayOperationKind.CancelCommand, CurrentHash(), Player: player, Gang: gang,
            Accepted: result.Accepted, ValidationCode: (int)result.Validation.Code));
        return result;
    }

    public HireSubmissionResult QueueHire(PlayerId player, short gangDefinitionId, int sectorId)
    {
        EnsureSynchronized();
        var result = State.QueueHire(player, gangDefinitionId, sectorId);
        Add(new ReplayStep(
            ReplayOperationKind.QueueHire, CurrentHash(), Player: player,
            GangDefinitionId: gangDefinitionId, SectorId: sectorId,
            Accepted: result.Accepted, ValidationCode: (int)result.Validation.Code));
        return result;
    }

    public IReadOnlyList<PlayerId> PrepareSimultaneousHireOffers()
    {
        EnsureSynchronized();
        var drawn = State.PrepareSimultaneousHireOffers();
        Add(new ReplayStep(ReplayOperationKind.PrepareSimultaneousHireOffers, CurrentHash()));
        return drawn;
    }

    public IReadOnlyList<short> PrepareHireOffers(PlayerId player)
    {
        EnsureSynchronized();
        var offers = State.PrepareHireOffers(player);
        Add(new ReplayStep(ReplayOperationKind.PrepareHireOffers, CurrentHash(), Player: player));
        return offers;
    }

    public void PrepareAiPlanning(PlayerId player)
    {
        EnsureSynchronized();
        State.PrepareAiPlanning(player);
        Add(new ReplayStep(ReplayOperationKind.PrepareAiPlanning, CurrentHash(), Player: player));
    }

    public AiTurnPlanner.HirePreparation PrepareAiHiring(PlayerId player)
    {
        EnsureSynchronized();
        var choice = State.PrepareAiHiring(player);
        Add(new ReplayStep(ReplayOperationKind.PrepareAiHiring, CurrentHash(), Player: player));
        return choice;
    }

    public HireOfferSnubResult SnubHireOffer(PlayerId player, short gangDefinitionId)
    {
        EnsureSynchronized();
        var result = State.SnubHireOffer(player, gangDefinitionId);
        Add(new ReplayStep(
            ReplayOperationKind.SnubHireOffer, CurrentHash(), Player: player,
            GangDefinitionId: gangDefinitionId,
            Accepted: result.Accepted, ValidationCode: (int)result.Validation.Code));
        return result;
    }

    public TurnTransition FinishUpkeep() => RecordTransition(
        ReplayOperationKind.FinishUpkeep, State.FinishUpkeep);

    public TurnTransition FinishCommand(PlayerId player) => RecordTransition(
        ReplayOperationKind.FinishCommand,
        () => State.FinishCommand(player),
        player);

    public TurnTransition FinishExecutionPhase() => RecordTransition(
        ReplayOperationKind.FinishExecutionPhase, State.FinishExecutionPhase);

    public TurnTransition FinishHire(PlayerId player) => RecordTransition(
        ReplayOperationKind.FinishHire,
        () => State.FinishHire(player),
        player);

    public TurnTransition FinishPlayerElimination() => RecordTransition(
        ReplayOperationKind.FinishPlayerElimination, State.FinishPlayerElimination);

    public bool TryDismissNotification(PlayerId player, out GameNotification? notification)
    {
        EnsureSynchronized();
        var removed = State.TryDismissNotification(player, out notification);
        Add(new ReplayStep(
            ReplayOperationKind.DismissNotification, CurrentHash(),
            Player: player, Accepted: removed));
        return removed;
    }

    public ComlinkSendResult SendComlinkMessage(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string message)
    {
        EnsureSynchronized();
        var result = State.SendComlinkMessage(sender, recipients, message);
        Add(new ReplayStep(
            ReplayOperationKind.SendComlinkMessage, CurrentHash(), Player: sender,
            Accepted: result.Accepted, ValidationCode: (int)result.Code,
            Recipients: recipients.ToArray(), Text: message));
        return result;
    }

    public bool MarkComlinkRead(PlayerId player, long sequence)
    {
        EnsureSynchronized();
        var changed = State.MarkComlinkRead(player, sequence);
        Add(new ReplayStep(
            ReplayOperationKind.MarkComlinkRead, CurrentHash(), Player: player,
            Accepted: changed, ComlinkSequence: sequence));
        return changed;
    }

    internal ReplayDocument Capture()
    {
        EnsureSynchronized();
        return new ReplayDocument(
            MatchReplaySerializer.CurrentFormatVersion,
            _initialStateSha256,
            _initialSnapshot,
            _steps.ToArray());
    }

    private TurnTransition RecordTransition(
        ReplayOperationKind kind,
        Func<TurnTransition> transition,
        PlayerId? player = null)
    {
        EnsureSynchronized();
        var result = transition();
        Add(new ReplayStep(kind, CurrentHash(), Player: player));
        return result;
    }

    private string CurrentHash() => MatchStateHasher.ComputeSha256(State);

    private void Add(ReplayStep step)
    {
        if (step.Recipients is not null)
            step = step with { Recipients = Array.AsReadOnly(step.Recipients.ToArray()) };
        _steps.Add(step);
        _currentStateSha256 = step.ResultingStateSha256;
    }

    private void EnsureSynchronized()
    {
        if (!StringComparer.Ordinal.Equals(_currentStateSha256, CurrentHash()))
            throw new InvalidOperationException("Match state changed outside the replay recorder.");
    }
}

public static class MatchReplaySerializer
{
    // 25 embeds native save 23 and canonical hash 26, which authenticates the AI policy.
    public const int CurrentFormatVersion = 25;
    public const int MaximumReplayBytes = 32 * 1024 * 1024;
    public const int MaximumSteps = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = NativeSaveSerializer.CreateCompatibleJsonOptions();

    public static void Save(Stream destination, MatchReplayRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(recorder);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream is not writable.", nameof(destination));
        JsonSerializer.Serialize(destination, recorder.Capture(), JsonOptions);
    }

    public static MatchState LoadAndReplay(Stream source, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!source.CanRead) throw new ArgumentException("Source stream is not readable.", nameof(source));
        return ApplyGuarded(Read(source), definitions);
    }

    /// <summary>
    /// Loads a journal and answers a recorder that continues it, or null when it cannot be continued.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a journal written by this build is resumable. Every step carries the fingerprint of the
    /// state it produced, under the hashing rules of the format that wrote it; appending a step
    /// hashed by today's rules to a document declaring an older format would produce a journal whose
    /// two halves disagree and which therefore replays from neither. An older journal is still
    /// perfectly replayable — that is what <see cref="LoadAndReplay"/> is for — it just cannot be
    /// grown, so the caller starts a fresh one and says the history before this point is not in it.
    /// </para>
    /// <para>
    /// Answering null rather than throwing is the point: a save from last week loading into a new
    /// build is ordinary, not an error, and the player must not be shown one.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidDataException">The journal is unreadable or diverged.</exception>
    public static MatchReplayRecorder? TryLoadResumable(Stream source, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!source.CanRead)
            throw new ArgumentException("Source stream is not readable.", nameof(source));
        var document = Read(source);
        if (document.FormatVersion != CurrentFormatVersion) return null;
        var state = ApplyGuarded(document, definitions);
        return MatchReplayRecorder.Resume(
            state, document.InitialSnapshot, document.InitialStateSha256, document.Steps);
    }

    private static ReplayDocument Read(Stream source)
    {
        try
        {
            using var bounded = ReadBounded(source);
            return JsonSerializer.Deserialize<ReplayDocument>(bounded, JsonOptions)
                ?? throw new InvalidDataException("Replay is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Replay JSON is invalid.", exception);
        }
    }

    /// <summary>
    /// <see cref="Apply"/>, with every way a malformed journal can surface reported as bad data.
    /// </summary>
    private static MatchState ApplyGuarded(ReplayDocument document, OriginalData definitions)
    {
        try
        {
            return Apply(document, definitions);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            throw new InvalidDataException("Replay operation is invalid.", exception);
        }
    }

    private static MatchState Apply(ReplayDocument document, OriginalData definitions)
    {
        if (document.FormatVersion is < 2 or > CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported replay format {document.FormatVersion}.");
        if (document.Steps.Count > MaximumSteps)
            throw new InvalidDataException("Replay exceeds the operation limit.");
        using var snapshot = new MemoryStream(document.InitialSnapshot, writable: false);
        var state = NativeSaveSerializer.Load(snapshot, definitions);
        VerifyHash(document.InitialStateSha256, state, -1, document.FormatVersion);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            var step = document.Steps[index];
            ApplyStep(state, step, index, document.FormatVersion);
            VerifyHash(step.ResultingStateSha256, state, index, document.FormatVersion);
        }
        return state;
    }

    private static void ApplyStep(MatchState state, ReplayStep step, int index, int replayVersion)
    {
        var minimumVersion = MinimumVersionFor(step.Kind);
        if (replayVersion < minimumVersion)
            throw new InvalidDataException(
                $"Replay step {index} uses an operation introduced in replay format {minimumVersion}.");
        ValidateStepPayload(step, index, replayVersion);

        switch (step.Kind)
        {
            case ReplayOperationKind.SubmitCommand:
            {
                var command = step.Command
                    ?? throw new InvalidDataException($"Replay step {index} has no command.");
                ValidateCommandVersion(command, replayVersion, index);
                var result = state.Submit(command);
                VerifyResult(step, result.Accepted, (int)result.Validation.Code, index);
                break;
            }
            case ReplayOperationKind.CancelCommand:
            {
                var result = state.Cancel(Required(step.Player, index), Required(step.Gang, index));
                VerifyResult(step, result.Accepted, (int)result.Validation.Code, index);
                break;
            }
            case ReplayOperationKind.QueueHire:
            {
                var player = Required(step.Player, index);
                var gangDefinitionId = step.GangDefinitionId
                    ?? throw new InvalidDataException($"Replay step {index} has no gang definition.");
                var sectorId = step.SectorId
                    ?? throw new InvalidDataException($"Replay step {index} has no sector.");
                var result = replayVersion <= 9
                    ? state.QueueHireLegacyImmediatePayment(player, gangDefinitionId, sectorId)
                    : state.QueueHire(player, gangDefinitionId, sectorId);
                VerifyResult(step, result.Accepted, (int)result.Validation.Code, index);
                break;
            }
            case ReplayOperationKind.SnubHireOffer:
            {
                var player = Required(step.Player, index);
                var gangDefinitionId = step.GangDefinitionId
                    ?? throw new InvalidDataException($"Replay step {index} has no gang definition.");
                var result = replayVersion <= 9
                    ? state.SnubHireOfferLegacySingleAction(player, gangDefinitionId)
                    : state.SnubHireOffer(player, gangDefinitionId);
                VerifyResult(step, result.Accepted, (int)result.Validation.Code, index);
                break;
            }
            case ReplayOperationKind.FinishUpkeep: state.FinishUpkeep(); break;
            case ReplayOperationKind.FinishCommand: state.FinishCommand(Required(step.Player, index)); break;
            case ReplayOperationKind.FinishExecutionPhase: state.FinishExecutionPhase(); break;
            case ReplayOperationKind.FinishHire: state.FinishHire(Required(step.Player, index)); break;
            case ReplayOperationKind.FinishPlayerElimination: state.FinishPlayerElimination(); break;
            case ReplayOperationKind.DismissNotification:
            {
                var removed = state.TryDismissNotification(Required(step.Player, index), out _);
                if (step.Accepted != removed)
                    throw new InvalidDataException($"Replay step {index} produced a different notification result.");
                break;
            }
            case ReplayOperationKind.PrepareHireOffers:
                state.PrepareHireOffers(Required(step.Player, index));
                break;
            case ReplayOperationKind.PrepareSimultaneousHireOffers:
                state.PrepareSimultaneousHireOffers();
                break;
            case ReplayOperationKind.PrepareAiPlanning:
                state.PrepareAiPlanning(Required(step.Player, index));
                break;
            case ReplayOperationKind.PrepareAiHiring:
                state.PrepareAiHiring(Required(step.Player, index));
                break;
            case ReplayOperationKind.SendComlinkMessage:
            {
                var result = state.SendComlinkMessage(
                    Required(step.Player, index),
                    step.Recipients
                        ?? throw new InvalidDataException($"Replay step {index} has no Comlink recipients."),
                    step.Text
                        ?? throw new InvalidDataException($"Replay step {index} has no Comlink text."));
                VerifyResult(step, result.Accepted, (int)result.Code, index);
                break;
            }
            case ReplayOperationKind.MarkComlinkRead:
            {
                var changed = replayVersion >= 24
                    ? state.MarkComlinkRead(
                        Required(step.Player, index),
                        step.ComlinkSequence
                            ?? throw new InvalidDataException(
                                $"Replay step {index} has no Comlink sequence."))
                    : state.MarkAllComlinkReadLegacy(Required(step.Player, index));
                if (step.Accepted != changed)
                    throw new InvalidDataException($"Replay step {index} produced a different Comlink read result.");
                break;
            }
            default: throw new InvalidDataException($"Replay step {index} has an unknown operation kind.");
        }
    }

    private static void ValidateStepPayload(ReplayStep step, int index, int replayVersion)
    {
        var actual = ReplayStepFields.None;
        if (step.Command is not null) actual |= ReplayStepFields.Command;
        if (step.Player is not null) actual |= ReplayStepFields.Player;
        if (step.Gang is not null) actual |= ReplayStepFields.Gang;
        if (step.GangDefinitionId is not null) actual |= ReplayStepFields.GangDefinition;
        if (step.SectorId is not null) actual |= ReplayStepFields.Sector;
        if (step.Accepted is not null) actual |= ReplayStepFields.Accepted;
        if (step.ValidationCode is not null) actual |= ReplayStepFields.Validation;
        if (step.Recipients is not null) actual |= ReplayStepFields.Recipients;
        if (step.Text is not null) actual |= ReplayStepFields.Text;
        if (step.ComlinkSequence is not null) actual |= ReplayStepFields.ComlinkSequence;

        var result = ReplayStepFields.Accepted | ReplayStepFields.Validation;
        var expected = step.Kind switch
        {
            ReplayOperationKind.SubmitCommand => ReplayStepFields.Command | result,
            ReplayOperationKind.CancelCommand =>
                ReplayStepFields.Player | ReplayStepFields.Gang | result,
            ReplayOperationKind.QueueHire => ReplayStepFields.Player
                | ReplayStepFields.GangDefinition | ReplayStepFields.Sector | result,
            ReplayOperationKind.SnubHireOffer =>
                ReplayStepFields.Player | ReplayStepFields.GangDefinition | result,
            ReplayOperationKind.FinishCommand or ReplayOperationKind.FinishHire
                or ReplayOperationKind.PrepareHireOffers
                or ReplayOperationKind.PrepareAiPlanning
                or ReplayOperationKind.PrepareAiHiring => ReplayStepFields.Player,
            ReplayOperationKind.DismissNotification =>
                ReplayStepFields.Player | ReplayStepFields.Accepted,
            ReplayOperationKind.MarkComlinkRead => ReplayStepFields.Player
                | ReplayStepFields.Accepted
                | (replayVersion >= 24 ? ReplayStepFields.ComlinkSequence : ReplayStepFields.None),
            ReplayOperationKind.SendComlinkMessage => ReplayStepFields.Player | result
                | ReplayStepFields.Recipients | ReplayStepFields.Text,
            ReplayOperationKind.FinishUpkeep
                or ReplayOperationKind.FinishExecutionPhase
                or ReplayOperationKind.FinishPlayerElimination
                or ReplayOperationKind.PrepareSimultaneousHireOffers => ReplayStepFields.None,
            _ => (ReplayStepFields)(-1)
        };
        if (actual != expected)
            throw new InvalidDataException(
                $"Replay step {index} has an invalid payload for {step.Kind}.");
    }

    private static int MinimumVersionFor(ReplayOperationKind kind) => kind switch
    {
        ReplayOperationKind.PrepareHireOffers => 3,
        ReplayOperationKind.PrepareAiPlanning => 6,
        ReplayOperationKind.PrepareAiHiring => 8,
        ReplayOperationKind.SendComlinkMessage or ReplayOperationKind.MarkComlinkRead => 18,
        ReplayOperationKind.PrepareSimultaneousHireOffers => 21,
        _ => 2
    };

    private static void ValidateCommandVersion(GameCommand command, int replayVersion, int index)
    {
        if (replayVersion < 19 && command.TertiaryTarget is not null)
            throw new InvalidDataException(
                $"Replay step {index} uses a command target introduced in replay format 19.");
        if (replayVersion < 20 && command.QuaternaryTarget is not null)
            throw new InvalidDataException(
                $"Replay step {index} uses a command target introduced in replay format 20.");
    }

    private static void VerifyResult(ReplayStep step, bool accepted, int validationCode, int index)
    {
        if (step.Accepted != accepted || step.ValidationCode != validationCode)
            throw new InvalidDataException($"Replay step {index} produced a different validation result.");
    }

    private static T Required<T>(T? value, int index) where T : struct =>
        value ?? throw new InvalidDataException($"Replay step {index} is missing a required value.");

    private static void VerifyHash(string expected, MatchState state, int index, int replayVersion)
    {
        byte[] expectedBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expected);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"Replay step {index} has an invalid state fingerprint.", exception);
        }
        string[] candidateHashes = replayVersion switch
        {
            >= 25 => [MatchStateHasher.ComputeSha256(state)],
            24 => [MatchStateHasher.ComputeVersionTwentyFiveSha256(state)],
            23 => [MatchStateHasher.ComputeVersionTwentyFourSha256(state)],
            22 => [MatchStateHasher.ComputeVersionTwentyThreeSha256(state)],
            20 or 21 => [MatchStateHasher.ComputeVersionTwentyTwoSha256(state)],
            19 => [MatchStateHasher.ComputeVersionTwentyOneSha256(state)],
            18 => [MatchStateHasher.ComputeVersionTwentySha256(state)],
            17 => [MatchStateHasher.ComputeVersionNineteenSha256(state)],
            16 => [MatchStateHasher.ComputeVersionEighteenSha256(state)],
            15 => [MatchStateHasher.ComputeVersionSeventeenSha256(state)],
            14 => [MatchStateHasher.ComputeVersionSixteenSha256(state)],
            13 => [MatchStateHasher.ComputeVersionFifteenSha256(state)],
            12 => [MatchStateHasher.ComputeVersionFourteenSha256(state)],
            11 => [MatchStateHasher.ComputeVersionThirteenSha256(state)],
            10 => [MatchStateHasher.ComputeVersionTwelveSha256(state)],
            9 => [MatchStateHasher.ComputeVersionElevenSha256(state)],
            7 or 8 => [MatchStateHasher.ComputeVersionTenSha256(state)],
            5 or 6 => [MatchStateHasher.ComputeVersionSixSha256(state)],
            4 => [MatchStateHasher.ComputeVersionFiveSha256(state)],
            _ =>
            [
                MatchStateHasher.ComputeVersionFourSha256(state),
                MatchStateHasher.ComputeVersionThreeSha256(state),
                MatchStateHasher.ComputeVersionTwoSha256(state),
                MatchStateHasher.ComputeLegacySha256(state)
            ]
        };
        if (!candidateHashes.Select(Convert.FromHexString).Any(actualBytes =>
                expectedBytes.Length == actualBytes.Length
                && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes)))
            throw new InvalidDataException($"Replay diverged after step {index}.");
    }

    private static MemoryStream ReadBounded(Stream source)
    {
        if (source.CanSeek && source.Length - source.Position > MaximumReplayBytes)
            throw new InvalidDataException("Replay exceeds the size limit.");
        var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (memory.Length + read > MaximumReplayBytes)
                throw new InvalidDataException("Replay exceeds the size limit.");
            memory.Write(buffer, 0, read);
        }
        memory.Position = 0;
        return memory;
    }

    [Flags]
    private enum ReplayStepFields
    {
        None = 0,
        Command = 1 << 0,
        Player = 1 << 1,
        Gang = 1 << 2,
        GangDefinition = 1 << 3,
        Sector = 1 << 4,
        Accepted = 1 << 5,
        Validation = 1 << 6,
        Recipients = 1 << 7,
        Text = 1 << 8,
        ComlinkSequence = 1 << 9
    }
}

internal sealed record ReplayDocument(
    int FormatVersion,
    string InitialStateSha256,
    byte[] InitialSnapshot,
    IReadOnlyList<ReplayStep> Steps);

public sealed record MatchReplayLoadResult(
    MatchState State,
    bool RecoveredFromBackup,
    bool PrimaryRepaired = false);

public static class MatchReplayStore
{
    public const string BackupSuffix = ".bak";

    public static void SaveAtomic(string path, MatchReplayRecorder recorder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(recorder);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Replay path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, FileOptions.WriteThrough))
            {
                MatchReplaySerializer.Save(stream, recorder);
                stream.Flush(flushToDisk: true);
            }

            // Replays are long-lived verification artifacts. Read back the new
            // file before promotion and preserve the last valid generation.
            _ = LoadAndReplay(temporaryPath, recorder.State.Definitions);
            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
            }
            else if (IsValid(fullPath, recorder.State.Definitions))
            {
                File.Replace(temporaryPath, fullPath, fullPath + BackupSuffix);
            }
            else
            {
                File.Move(temporaryPath, fullPath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static MatchState LoadAndReplay(string path, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return MatchReplaySerializer.LoadAndReplay(stream, definitions);
    }

    public static MatchReplayLoadResult LoadAndReplayRecoveringBackup(
        string path,
        OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(definitions);
        try
        {
            return new MatchReplayLoadResult(LoadAndReplay(path, definitions), false);
        }
        catch (Exception primaryFailure) when (primaryFailure is IOException or InvalidDataException)
        {
            var backupPath = Path.GetFullPath(path) + BackupSuffix;
            if (!File.Exists(backupPath)) throw;
            var state = LoadAndReplay(backupPath, definitions);
            var fullPath = Path.GetFullPath(path);
            var repaired = AtomicGenerationRecovery.TryRestore(
                fullPath, backupPath, candidate => _ = LoadAndReplay(candidate, definitions));
            return new MatchReplayLoadResult(state, true, repaired);
        }
    }

    private static bool IsValid(string path, OriginalData definitions)
    {
        try
        {
            _ = LoadAndReplay(path, definitions);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return false;
        }
    }
}
