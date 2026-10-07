using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Records what a player intended during Command as the order document the turn seals.
/// </summary>
/// <remarks>
/// <para>
/// The five operations are exactly the player intents <c>MatchReplayRecorder</c> accepts over a
/// turn. The phase transitions and the <c>Prepare*</c> steps are driven by the turn structure on
/// every client and are refused over the wire, so they are absent here by construction.
/// </para>
/// <para>
/// The document is a log, not a diff: the client submits it whole on every change and the server
/// replaces what it held. Submitting the accumulated intent rather than the resulting state is what
/// lets every client replay a turn through the same validator the replay reader uses, and see the
/// same acceptances and refusals.
/// </para>
/// <para>
/// A builder made by <see cref="ForTurn"/> leaves out the entries a later one makes moot, so a turn
/// holds at most one op per gang, one for the hire dock, and one per notification dismissed
/// (DEV-NET-002). A gang's later order replaces its earlier one whole (RULE-TURN-005), and the
/// core's validators read none of the state an order leaves behind (the queue, a gang's hiding
/// flag, the hire action), so dropping the earlier op changes neither what the later one is
/// judged against nor what the turn resolves. What does change is bookkeeping no rule reads: the
/// planning events in the match's log and the absolute command sequence numbers, of which
/// resolution uses only the order.
/// </para>
/// <para>
/// The server takes at most <see cref="MaxOps"/> ops. Compaction keeps every turn the original's
/// inputs can produce well below that, and <see cref="SpeculativeTurn"/> refuses an action
/// before it changes the planning copy when there is no room for it. Recording past the limit
/// throws, because a document the server refuses loses the seat every order of its turn.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = new OrderDocumentBuilder(new PlayerId(0));
/// builder.Submit(command);
/// await match.SubmitOrdersAsync(turn, new SubmitOrdersRequest(builder.Build(), ready: true), token);
/// </code>
/// </example>
public sealed class OrderDocumentBuilder
{
    private readonly List<OrderOp> _ops = [];

    /// <summary>What the turn started with, which compaction needs; null for a plain log.</summary>
    private readonly TurnStart? _start;

    /// <summary>A plain log: every op is kept in the order it was recorded.</summary>
    public OrderDocumentBuilder(PlayerId player)
        : this(player, null, MaxOps)
    {
    }

    private OrderDocumentBuilder(PlayerId player, TurnStart? start, int limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), limit, "A document holds at least one op.");
        Player = player;
        _start = start;
        Limit = limit;
    }

    /// <summary>
    /// A compacting builder for the turn <paramref name="planningCopy"/> is about to be planned on.
    /// </summary>
    /// <remarks>
    /// It reads the two facts compaction depends on before any op is applied: which of the seat's
    /// gangs already hold an order (a recurring one carried into the turn), because cancelling one
    /// of those has to stay in the document while cancelling an order given this turn leaves
    /// nothing to send; and whether the hire dock starts the turn with no hire or snub, because
    /// only then is the dock's final state one op from the start.
    /// </remarks>
    public static OrderDocumentBuilder ForTurn(MatchState planningCopy, PlayerId player) =>
        ForTurn(planningCopy, player, MaxOps);

    /// <summary><see cref="ForTurn(MatchState, PlayerId)"/> with a smaller limit, for tests.</summary>
    internal static OrderDocumentBuilder ForTurn(MatchState planningCopy, PlayerId player, int limit)
    {
        ArgumentNullException.ThrowIfNull(planningCopy);
        var seat = planningCopy.FindPlayer(player)
            ?? throw new ArgumentOutOfRangeException(nameof(player), player, "That seat is not at this table.");
        var commanded = seat.Gangs
            .Where(gang => planningCopy.Commands.TryGet(gang.Id, out _))
            .Select(gang => gang.Id.Value)
            .ToHashSet();
        var hireClear = seat.PendingHires.Count == 0 && !seat.HasSnubbedHireOfferThisTurn;
        return new OrderDocumentBuilder(player, new TurnStart(commanded, hireClear), Math.Min(limit, MaxOps));
    }

    /// <summary>
    /// The most ops one document may hold: <c>LIMITS.ordersMaxOps</c>, which the server enforces.
    /// </summary>
    public const int MaxOps = WireLimits.OrdersMaxOps;

    /// <summary>The slot every op is recorded under; the server refuses any that names another.</summary>
    public PlayerId Player { get; }

    /// <summary>How many ops this document may hold; <see cref="MaxOps"/> outside tests.</summary>
    internal int Limit { get; }

    /// <summary>How many ops the turn's document currently holds.</summary>
    public int Count => _ops.Count;

    /// <summary>
    /// Goes up by one on every change, so a caller can tell whether there is anything new to
    /// build without building it.
    /// </summary>
    /// <remarks>
    /// The game loop asks every frame whether the draft on the server is stale. Building and
    /// hashing the document to answer "no" sixty times a second is work for nothing; comparing
    /// two integers is not.
    /// </remarks>
    public int Version { get; private set; }

    /// <summary>Whether one more op of any kind fits.</summary>
    public bool HasRoom => _ops.Count < Limit;

    /// <summary>
    /// Whether an order or a cancellation for <paramref name="gang"/> fits: always when the
    /// document already holds an op for that gang, which the new one replaces.
    /// </summary>
    public bool HasRoomFor(GangId gang) => HasRoom || (_start is not null && IndexOfGang(gang.Value) >= 0);

    /// <summary>
    /// Whether a hire or a snub fits: always when the document already holds the dock's op, which
    /// the new one replaces.
    /// </summary>
    public bool HasRoomForHireAction => HasRoom || (_start is { HireClear: true } && IndexOfHireAction() >= 0);

    /// <summary>Forgets everything recorded, for the start of a new turn.</summary>
    public void Clear()
    {
        _ops.Clear();
        Version++;
    }

    /// <summary>Records a queued command. Mirrors <c>MatchState.Submit</c>.</summary>
    /// <remarks>Compacting, it replaces the gang's earlier op, whichever it was.</remarks>
    public void Submit(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireOwnSlot(command.Player);
        if (_start is not null) Remove(IndexOfGang(command.Gang.Value));
        Record(new SubmitCommandOp(
            command.Player.Value,
            command.Gang.Value,
            (int)command.Action,
            ToWire(command.Target),
            command.Repeat,
            command.SecondaryTarget is { } secondary ? ToWire(secondary) : null,
            command.TertiaryTarget is { } tertiary ? ToWire(tertiary) : null,
            command.QuaternaryTarget is { } quaternary ? ToWire(quaternary) : null));
    }

    /// <summary>Records a cancelled command. Mirrors <c>MatchState.Cancel</c>.</summary>
    /// <remarks>
    /// Compacting, it replaces the gang's earlier op, and is itself left out when the gang held no
    /// order when the turn started: the turn then ends with the gang holding none either way.
    /// </remarks>
    public void Cancel(PlayerId player, GangId gang)
    {
        RequireOwnSlot(player);
        if (_start is { } start)
        {
            Remove(IndexOfGang(gang.Value));
            if (!start.CommandedGangs.Contains(gang.Value)) return;
        }
        Record(new CancelCommandOp(player.Value, gang.Value));
    }

    /// <summary>Records a queued hire. Mirrors <c>MatchState.QueueHire</c>.</summary>
    /// <remarks>
    /// Compacting, it replaces the dock's earlier op: a hire clears whatever hire or snub the turn
    /// held before it, so from a clear dock the last one is the whole story.
    /// </remarks>
    public void QueueHire(PlayerId player, short gangDefinitionId, int sectorId)
    {
        RequireOwnSlot(player);
        if (_start is { HireClear: true }) Remove(IndexOfHireAction());
        Record(new QueueHireOp(player.Value, gangDefinitionId, sectorId));
    }

    /// <summary>Records a snubbed offer. Mirrors <c>MatchState.SnubHireOffer</c>.</summary>
    /// <param name="player">The seat that snubbed.</param>
    /// <param name="gangDefinitionId">The offer snubbed.</param>
    /// <param name="withdrewHireAction">
    /// True when the core took the snub as withdrawing the turn's hire or snub of that same offer
    /// rather than snubbing it, which leaves the dock with no action. Only a compacting builder
    /// reads it: it then drops the dock's op and records nothing.
    /// </param>
    public void SnubHireOffer(PlayerId player, short gangDefinitionId, bool withdrewHireAction = false)
    {
        RequireOwnSlot(player);
        if (_start is { HireClear: true })
        {
            Remove(IndexOfHireAction());
            if (withdrewHireAction) return;
        }
        Record(new SnubHireOfferOp(player.Value, gangDefinitionId));
    }

    /// <summary>Records a dismissed notification. Mirrors <c>MatchState.TryDismissNotification</c>.</summary>
    public void DismissNotification(PlayerId player)
    {
        RequireOwnSlot(player);
        Record(new DismissNotificationOp(player.Value));
    }

    /// <summary>The document as it stands, ready to submit.</summary>
    public OrderDocument Build() => new(OrderDocumentSchemaVersion, _ops.ToArray());

    private void Record(OrderOp op)
    {
        if (_ops.Count >= Limit)
        {
            // The caller was meant to ask HasRoom first and refuse the action before applying it
            // to the planning copy. Recording anyway would build a document the server refuses
            // whole, and the seat would lose every order of the turn.
            throw new InvalidOperationException(
                $"The order document already holds {_ops.Count} ops, the most the server accepts.");
        }
        _ops.Add(op);
        Version++;
    }

    private void Remove(int index)
    {
        if (index < 0) return;
        _ops.RemoveAt(index);
        Version++;
    }

    private int IndexOfGang(int gang) => _ops.FindIndex(op => op switch
    {
        SubmitCommandOp submit => submit.Gang == gang,
        CancelCommandOp cancel => cancel.Gang == gang,
        _ => false,
    });

    private int IndexOfHireAction() => _ops.FindIndex(op => op is QueueHireOp or SnubHireOfferOp);

    /// <summary>The only document version the server accepts.</summary>
    public const int OrderDocumentSchemaVersion = 1;

    /// <summary>
    /// A target in the wire's vocabulary.
    /// </summary>
    /// <remarks>
    /// <see cref="CommandTargetKind.None"/> carries the core's sentinel id of -1, which the wire
    /// has no field for and no need of: the kind alone says there is no target.
    /// </remarks>
    private static Generated.CommandTarget ToWire(Core.GameModel.CommandTarget target) => target.Kind switch
    {
        CommandTargetKind.None => new NoneTarget(),
        CommandTargetKind.Gang => new GangTarget(target.Id),
        CommandTargetKind.Sector => new SectorTarget(target.Id),
        CommandTargetKind.Site => new SiteTarget(target.Id),
        CommandTargetKind.Item => new ItemTarget(target.Id),
        _ => throw new ArgumentOutOfRangeException(
            nameof(target), target.Kind, "The command target kind has no wire form."),
    };

    /// <summary>
    /// An op that named another slot would be refused by the server anyway; failing here says which
    /// call site built it, rather than which turn it was rejected in.
    /// </summary>
    private void RequireOwnSlot(PlayerId acting)
    {
        if (acting != Player)
        {
            throw new InvalidOperationException(
                $"Player {acting.Value} cannot be recorded in slot {Player.Value}'s order document.");
        }
    }

    /// <param name="CommandedGangs">The seat's gangs that held an order when the turn started.</param>
    /// <param name="HireClear">Whether the seat's hire dock started the turn with no hire or snub.</param>
    private sealed record TurnStart(IReadOnlySet<int> CommandedGangs, bool HireClear);
}
