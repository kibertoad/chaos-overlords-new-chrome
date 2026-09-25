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
    PrepareSimultaneousHireOffers,
    /// <summary>One authoritative, one-way handover of a departed human seat.</summary>
    TransferPlayerToComputer,
    /// <summary>Returns an AI-held online seat to its authenticated human owner.</summary>
    TransferPlayerToHuman
}

public sealed record ReplayStep(
    ReplayOperationKind Kind,
    string ResultingStateFingerprint,
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
    private readonly string _initialStateFingerprint;
    private readonly List<ReplayStep> _steps = [];
    private readonly bool _verifying;
    private string _currentStateFingerprint;

    public MatchReplayRecorder(MatchState state)
        : this(state, verifying: true)
    {
    }

    private MatchReplayRecorder(MatchState state, bool verifying)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        _verifying = verifying;
        _initialStateFingerprint = MatchStateHasher.ComputeFingerprint(state);
        _currentStateFingerprint = _initialStateFingerprint;
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, state);
        _initialSnapshot = stream.ToArray();
    }

    /// <summary>
    /// A recorder that records without CHECKING, for a copy nothing else can reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every mutation costs two full fingerprints of the whole match: one for
    /// <see cref="EnsureSynchronized"/>, which asks whether the state has moved behind the
    /// recorder's back, and one for the step's own fingerprint. The speculative copy an online
    /// player plans on routes every click, every cancel and every hire through that, on the render
    /// thread — and on a late-game six-player city a player feels it as a hitch each time they
    /// touch anything.
    /// </para>
    /// <para>
    /// The first of the two is what goes. It is an invariant about ALIASING — somebody holding the
    /// same <see cref="MatchState"/> and mutating it directly — and the speculative copy is made by
    /// <c>MatchStateClone.Of</c>, handed to one owner and thrown away when the turn seals. Nothing
    /// else has a reference to check for.
    /// </para>
    /// <para>
    /// The second stays, and the journal with it. A bug report filed from an online match attaches
    /// the turn being planned — that is the only history this client is the authority on — so a
    /// recorder that kept no journal would quietly strip a report of its most useful field.
    /// </para>
    /// </remarks>
    public static MatchReplayRecorder Unverified(MatchState state) => new(state, verifying: false);

    /// <summary>Whether this recorder re-hashes the state before each step; see `Unverified`.</summary>
    public bool IsVerifying => _verifying;

    /// <summary>Adopts a journal that was recorded earlier, so play continues appending to it.</summary>
    private MatchReplayRecorder(
        MatchState state,
        byte[] initialSnapshot,
        string initialStateFingerprint,
        IReadOnlyList<ReplayStep> steps)
    {
        State = state;
        _verifying = true;
        _initialSnapshot = initialSnapshot;
        _initialStateFingerprint = initialStateFingerprint;
        _steps.AddRange(steps);
        _currentStateFingerprint = MatchReplaySerializer.EndingFingerprint(
            initialStateFingerprint, steps);
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
        string initialStateFingerprint,
        IReadOnlyList<ReplayStep> steps) =>
        new(state, initialSnapshot, initialStateFingerprint, steps);

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

    public bool TransferPlayerToComputer(PlayerId player)
    {
        EnsureSynchronized();
        var changed = State.TransferPlayerToComputer(player);
        Add(new ReplayStep(
            ReplayOperationKind.TransferPlayerToComputer, CurrentHash(),
            Player: player, Accepted: changed));
        return changed;
    }

    public bool TransferPlayerToHuman(PlayerId player)
    {
        EnsureSynchronized();
        var changed = State.TransferPlayerToHuman(player);
        Add(new ReplayStep(
            ReplayOperationKind.TransferPlayerToHuman, CurrentHash(),
            Player: player, Accepted: changed));
        return changed;
    }

    internal ReplayDocument Capture()
    {
        EnsureSynchronized();
        return new ReplayDocument(
            MatchReplaySerializer.CurrentFormatVersion,
            _initialStateFingerprint,
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

    private string CurrentHash() => MatchStateHasher.ComputeFingerprint(State);

    private void Add(ReplayStep step)
    {
        if (step.Recipients is not null)
            step = step with { Recipients = Array.AsReadOnly(step.Recipients.ToArray()) };
        _steps.Add(step);
        _currentStateFingerprint = step.ResultingStateFingerprint;
    }

    private void EnsureSynchronized()
    {
        if (!_verifying) return;
        if (!StringComparer.Ordinal.Equals(_currentStateFingerprint, CurrentHash()))
            throw new InvalidOperationException("Match state changed outside the replay recorder.");
    }
}

public static class MatchReplaySerializer
{
    // 32 moves with the state-fingerprint encoding, whose events now record the gangs that fought
    // (MatchStateHasher.FormatVersion 3), and drops every older format: a journal is verified step
    // by step against the fingerprint of its day, so a journal from format 31 would diverge on its
    // first step and be reported as damage rather than as an older format.
    public const int CurrentFormatVersion = 41;
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
            state, document.InitialSnapshot, document.InitialStateFingerprint, document.Steps);
    }

    /// <summary>
    /// Adopts a journal onto a state that has already been restored, without replaying it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TryLoadResumable"/> replays a journal to arrive at the state it ends at, which is
    /// the only way to get one when the journal is all there is. Beside a save it is not: the save
    /// <em>is</em> that state, and re-deriving it costs a re-run of the whole match — every recorded
    /// operation plus a full-state fingerprint each — on the thread the player is waiting on. A
    /// thirty-turn match measured 147 ms, and it grows with the match, so the reward for a long
    /// session is a load that visibly stops.
    /// </para>
    /// <para>
    /// What a resumed journal actually needs is the history, and the history is the step list. The
    /// state comes from the save, and the one thing worth proving is that the two belong together:
    /// the last step's fingerprint against the restored state's. That is the same equality the
    /// replay was reduced to at the end, and it is exactly the invariant the recorder holds — the
    /// journal that replays somewhere else is a leftover from another game in the same slot, and is
    /// refused here as it was before.
    /// </para>
    /// <para>
    /// So the steps are carried rather than verified. The journal is this build's own companion
    /// file, and where it is read by something that cares whether every step still reproduces — a
    /// bug report, which replays it to anonymize it — that check happens there, off the game loop
    /// and on the copy about to be sent.
    /// </para>
    /// </remarks>
    /// <param name="resumed">The state the journal must end at: the one just loaded from the save.</param>
    /// <returns>A recorder continuing the journal, or null when it is not this save's.</returns>
    /// <exception cref="InvalidDataException">The journal is unreadable.</exception>
    public static MatchReplayRecorder? TryResumeOnto(Stream source, MatchState resumed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resumed);
        if (!source.CanRead)
            throw new ArgumentException("Source stream is not readable.", nameof(source));
        var document = Read(source);
        if (document.FormatVersion != CurrentFormatVersion) return null;
        if (document.Steps.Count > MaximumSteps)
            throw new InvalidDataException("Replay exceeds the operation limit.");
        var ending = EndingFingerprint(document.InitialStateFingerprint, document.Steps);
        if (!StringComparer.Ordinal.Equals(ending, MatchStateHasher.ComputeFingerprint(resumed)))
            return null;
        return MatchReplayRecorder.Resume(
            resumed, document.InitialSnapshot, document.InitialStateFingerprint, document.Steps);
    }

    /// <summary>
    /// The fingerprint a journal ends at: its last step's, or the initial one when it has no steps.
    /// </summary>
    internal static string EndingFingerprint(
        string initialStateFingerprint,
        IReadOnlyList<ReplayStep> steps) =>
        steps.Count == 0
            ? initialStateFingerprint
            : steps[^1].ResultingStateFingerprint;

    private static ReplayDocument Read(Stream source)
    {
        try
        {
            using var bounded = NativeSaveSerializer.ReadBounded(
                source, MaximumReplayBytes, "Replay exceeds the size limit.");
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
        if (document.FormatVersion != CurrentFormatVersion)
            throw IncompatibleSave.Create(
                document.FormatVersion > CurrentFormatVersion
                    ? IncompatibleSaveReason.NewerFormat
                    : IncompatibleSaveReason.OlderFormat,
                $"Unsupported replay format {document.FormatVersion}.");
        if (document.Steps.Count > MaximumSteps)
            throw new InvalidDataException("Replay exceeds the operation limit.");
        using var snapshot = new MemoryStream(document.InitialSnapshot, writable: false);
        var state = NativeSaveSerializer.Load(snapshot, definitions);
        VerifyFingerprint(document.InitialStateFingerprint, state, -1);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            var step = document.Steps[index];
            ApplyStep(state, step, index);
            VerifyFingerprint(step.ResultingStateFingerprint, state, index);
        }
        return state;
    }

    private static void ApplyStep(MatchState state, ReplayStep step, int index)
    {
        ValidateStepPayload(step, index);

        switch (step.Kind)
        {
            case ReplayOperationKind.SubmitCommand:
            {
                var command = step.Command
                    ?? throw new InvalidDataException($"Replay step {index} has no command.");
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
                var gangDefinitionId = RequiredGangDefinition(step, index);
                var sectorId = step.SectorId
                    ?? throw new InvalidDataException($"Replay step {index} has no sector.");
                var result = state.QueueHire(player, gangDefinitionId, sectorId);
                VerifyResult(step, result.Accepted, (int)result.Validation.Code, index);
                break;
            }
            case ReplayOperationKind.SnubHireOffer:
            {
                var player = Required(step.Player, index);
                var gangDefinitionId = RequiredGangDefinition(step, index);
                var result = state.SnubHireOffer(player, gangDefinitionId);
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
                var changed = state.MarkComlinkRead(
                    Required(step.Player, index),
                    step.ComlinkSequence
                        ?? throw new InvalidDataException(
                            $"Replay step {index} has no Comlink sequence."));
                if (step.Accepted != changed)
                    throw new InvalidDataException($"Replay step {index} produced a different Comlink read result.");
                break;
            }
            case ReplayOperationKind.TransferPlayerToComputer:
            case ReplayOperationKind.TransferPlayerToHuman:
            {
                var seat = Required(step.Player, index);
                var changed = step.Kind == ReplayOperationKind.TransferPlayerToComputer
                    ? state.TransferPlayerToComputer(seat)
                    : state.TransferPlayerToHuman(seat);
                if (step.Accepted != changed)
                    throw new InvalidDataException(
                        $"Replay step {index} produced a different control-transfer result.");
                break;
            }
            default: throw new InvalidDataException($"Replay step {index} has an unknown operation kind.");
        }
    }

    private static void ValidateStepPayload(ReplayStep step, int index)
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
                | ReplayStepFields.Accepted | ReplayStepFields.ComlinkSequence,
            ReplayOperationKind.TransferPlayerToComputer or ReplayOperationKind.TransferPlayerToHuman =>
                ReplayStepFields.Player | ReplayStepFields.Accepted,
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

    private static void VerifyResult(ReplayStep step, bool accepted, int validationCode, int index)
    {
        if (step.Accepted != accepted || step.ValidationCode != validationCode)
            throw new InvalidDataException($"Replay step {index} produced a different validation result.");
    }

    private static T Required<T>(T? value, int index) where T : struct =>
        value ?? throw new InvalidDataException($"Replay step {index} is missing a required value.");

    private static short RequiredGangDefinition(ReplayStep step, int index) =>
        step.GangDefinitionId
            ?? throw new InvalidDataException($"Replay step {index} has no gang definition.");

    private static void VerifyFingerprint(string expected, MatchState state, int index)
    {
        if (!MatchStateHasher.IsFingerprint(expected))
            throw new InvalidDataException($"Replay step {index} has an invalid state fingerprint.");
        if (!string.Equals(expected, MatchStateHasher.ComputeFingerprint(state), StringComparison.Ordinal))
            throw new InvalidDataException($"Replay diverged after step {index}.");
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
    string InitialStateFingerprint,
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
        // Replays are long-lived verification artifacts. Read back the new
        // file before promotion and preserve the last valid generation. A replay that cannot
        // reproduce itself reaches the player as a failed save, so report it as one rather than
        // as an InvalidDataException no caller filters for.
        AtomicGenerationRecovery.SaveAtomic(
            fullPath, directory, BackupSuffix,
            "The replay was written but could not be replayed back, so it was not promoted.",
            stream => MatchReplaySerializer.Save(stream, recorder),
            candidate => _ = LoadAndReplay(candidate, recorder.State.Definitions));
    }

    public static MatchState LoadAndReplay(string path, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return MatchReplaySerializer.LoadAndReplay(stream, definitions);
    }

    /// <summary>
    /// Loads a replay, falling back to its backup generation when the primary is damaged.
    /// </summary>
    /// <param name="path">The primary file.</param>
    /// <param name="definitions">The bundled definitions this build plays with.</param>
    /// <param name="repairPrimary">Whether a successful backup load may also rewrite the primary.</param>
    /// <remarks>
    /// A file <see cref="IncompatibleSave"/> recognises is deliberately not recovered from, for the
    /// reason given on <see cref="NativeSaveStore.LoadRecoveringBackup"/>.
    /// </remarks>
    public static MatchReplayLoadResult LoadAndReplayRecoveringBackup(
        string path,
        OriginalData definitions,
        bool repairPrimary = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(definitions);
        var (state, recovered, repaired) = AtomicGenerationRecovery.LoadRecoveringBackup(
            path, BackupSuffix, repairPrimary, candidate => LoadAndReplay(candidate, definitions));
        return new MatchReplayLoadResult(state, recovered, repaired);
    }
}
