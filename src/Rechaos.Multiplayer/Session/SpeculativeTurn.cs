using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The copy of the match a player plans an online turn on, and the document that records what they
/// did on it.
/// </summary>
/// <remarks>
/// <para>
/// Two things force this. Queueing a command has to show in the interface at once, but applying it
/// to the authoritative state would put this client ahead of its peers and the sealed set would
/// then apply it a second time. And the game core walks the seats one at a time, so a player in
/// slot 3 is not the active one at the start of a turn and the core would refuse everything they
/// tried.
/// </para>
/// <para>
/// So the copy is advanced to the local seat and thrown away when the turn seals. Nothing it does
/// reaches anybody: the record of the turn is the order document, and the state everyone ends up on
/// is the one the sealed set produces.
/// </para>
/// <para>
/// The hire dock is correct on it, which is the point of drawing every seat's offers before anyone
/// plans: the copy inherits offers already drawn, so what the player is shown is what the sealed
/// turn grants.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var turn = SpeculativeTurn.For(session.InitialState, definitions, session.Slot);
/// turn.Submit(command);                       // shows at once, and is recorded
/// await session.SubmitOrdersAsync(1, turn.Orders.Build(), ready: true, token);
/// </code>
/// </example>
public sealed class SpeculativeTurn
{
    private readonly MatchReplayRecorder _replay;

    private SpeculativeTurn(MatchReplayRecorder replay, PlayerId player)
    {
        _replay = replay;
        Player = player;
        Orders = new OrderDocumentBuilder(player);
    }

    /// <summary>The seat this client plays.</summary>
    public PlayerId Player { get; }

    /// <summary>What the player has done this turn, ready to submit.</summary>
    public OrderDocumentBuilder Orders { get; }

    /// <summary>The copy the interface reads and draws.</summary>
    public MatchState State => _replay.State;

    /// <summary>The recorder every mutation goes through, for the interface's own reads.</summary>
    public MatchReplayRecorder Replay => _replay;

    /// <summary>
    /// A fresh copy of <paramref name="authoritative"/>, with the local seat made the active one.
    /// </summary>
    /// <remarks>
    /// Advancing the coordinator seat by seat is what makes the local player active; the seats it
    /// steps past do nothing, because nothing this copy does is ever resolved.
    /// </remarks>
    public static SpeculativeTurn For(MatchState authoritative, OriginalData definitions, int slot)
    {
        ArgumentNullException.ThrowIfNull(authoritative);
        ArgumentNullException.ThrowIfNull(definitions);
        if (authoritative.Coordinator.Phase != TurnPhase.Command)
        {
            throw new InvalidOperationException(
                $"A turn is planned during Command, not {authoritative.Coordinator.Phase}.");
        }
        if (slot < 0 || slot >= authoritative.Setup.Players.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot), slot, "That seat is not at this table.");
        }
        var player = new PlayerId(slot);
        var replay = new MatchReplayRecorder(MatchStateClone.Of(authoritative, definitions));
        while (replay.State.Coordinator.ActivePlayer is { } active && active != player)
        {
            replay.FinishCommand(active);
        }
        // The coordinator walks every seat, so the local one always comes up. Saying so out loud
        // matters because the failure if it ever stopped being true is silent: the copy would be
        // left past Command, the core would refuse every command the player gave it, and the turn
        // would submit an empty document from an interface that looked like it was working.
        if (replay.State.Coordinator.ActivePlayer != player)
        {
            throw new InvalidOperationException(
                $"Seat {slot} never becomes the active player, so there is no turn to plan on it.");
        }
        return new SpeculativeTurn(replay, player);
    }

    /// <summary>Queues a command, and records it when the core accepted it.</summary>
    public CommandSubmissionResult Submit(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = _replay.Submit(command);
        if (result.Accepted) Orders.Submit(command);
        return result;
    }

    /// <summary>Cancels a queued command, and records it when the core accepted it.</summary>
    public CommandSubmissionResult Cancel(GangId gang)
    {
        var result = _replay.Cancel(Player, gang);
        if (result.Accepted) Orders.Cancel(Player, gang);
        return result;
    }

    /// <summary>Queues a hire, and records it when the core accepted it.</summary>
    public HireSubmissionResult QueueHire(short gangDefinitionId, int sectorId)
    {
        var result = _replay.QueueHire(Player, gangDefinitionId, sectorId);
        if (result.Accepted) Orders.QueueHire(Player, gangDefinitionId, sectorId);
        return result;
    }

    /// <summary>Snubs an offer, and records it when the core accepted it.</summary>
    public HireOfferSnubResult SnubHireOffer(short gangDefinitionId)
    {
        var result = _replay.SnubHireOffer(Player, gangDefinitionId);
        if (result.Accepted) Orders.SnubHireOffer(Player, gangDefinitionId);
        return result;
    }

    /// <summary>Dismisses a notification, and records it when there was one to dismiss.</summary>
    public bool DismissNotification()
    {
        var removed = _replay.TryDismissNotification(Player, out _);
        if (removed) Orders.DismissNotification(Player);
        return removed;
    }

    /// <summary>The document to submit for this turn.</summary>
    public OrderDocument Build() => Orders.Build();
}
