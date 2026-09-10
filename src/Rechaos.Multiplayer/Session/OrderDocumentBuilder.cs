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
/// </remarks>
/// <example>
/// <code>
/// var builder = new OrderDocumentBuilder(new PlayerId(0));
/// builder.Submit(command);
/// await match.SubmitOrdersAsync(turn, new SubmitOrdersRequest(builder.Build(), ready: true), token);
/// </code>
/// </example>
public sealed class OrderDocumentBuilder(PlayerId player)
{
    private readonly List<OrderOp> _ops = [];

    /// <summary>The slot every op is recorded under; the server refuses any that names another.</summary>
    public PlayerId Player { get; } = player;

    /// <summary>How many ops the turn's document currently holds.</summary>
    public int Count => _ops.Count;

    /// <summary>Forgets everything recorded, for the start of a new turn.</summary>
    public void Clear() => _ops.Clear();

    /// <summary>Records a queued command. Mirrors <c>MatchState.Submit</c>.</summary>
    public void Submit(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireOwnSlot(command.Player);
        _ops.Add(new SubmitCommandOp(
            command.Player.Value,
            command.Gang.Value,
            (int)command.Action,
            ToWire(command.Target),
            command.Repeat,
            command.SecondaryTarget is { } secondary ? ToWire(secondary) : null));
    }

    /// <summary>Records a cancelled command. Mirrors <c>MatchState.Cancel</c>.</summary>
    public void Cancel(PlayerId player, GangId gang)
    {
        RequireOwnSlot(player);
        _ops.Add(new CancelCommandOp(player.Value, gang.Value));
    }

    /// <summary>Records a queued hire. Mirrors <c>MatchState.QueueHire</c>.</summary>
    public void QueueHire(PlayerId player, short gangDefinitionId, int sectorId)
    {
        RequireOwnSlot(player);
        _ops.Add(new QueueHireOp(player.Value, gangDefinitionId, sectorId));
    }

    /// <summary>Records a snubbed offer. Mirrors <c>MatchState.SnubHireOffer</c>.</summary>
    public void SnubHireOffer(PlayerId player, short gangDefinitionId)
    {
        RequireOwnSlot(player);
        _ops.Add(new SnubHireOfferOp(player.Value, gangDefinitionId));
    }

    /// <summary>Records a dismissed notification. Mirrors <c>MatchState.TryDismissNotification</c>.</summary>
    public void DismissNotification(PlayerId player)
    {
        RequireOwnSlot(player);
        _ops.Add(new DismissNotificationOp(player.Value));
    }

    /// <summary>The document as it stands, ready to submit.</summary>
    public OrderDocument Build() => new(OrderDocumentSchemaVersion, _ops.ToArray());

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
}
