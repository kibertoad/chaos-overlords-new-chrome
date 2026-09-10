using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Plays one sealed turn through the deterministic core: every player's ops in slot order, the
/// computer players planned locally, then Execution, Hire, Elimination and Upkeep.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the lockstep contract. The server holds no rules and simulates nothing; it
/// answers what was ordered and in which order, and every client reaches the same state from it or
/// says so. Because the same core resolves the turn everywhere, an op that the rules refuse is
/// refused identically everywhere too, and a refusal is a fact of the turn rather than a divergence.
/// </para>
/// <para>
/// Ops are applied under the slot the seal attributed them to, never under the <c>player</c> field
/// inside them. The server already refuses a document whose ops name another slot, so the two
/// always agree — but trusting the attribution rather than the payload is what makes that a
/// belt-and-braces check instead of the only one.
/// </para>
/// </remarks>
public static class SealedTurnApplier
{
    /// <summary>
    /// Applies a sealed set and runs the turn to the next Command phase.
    /// </summary>
    /// <param name="replay">The recorder every mutation is routed through.</param>
    /// <param name="sealedOrders">The set the server froze, in slot order.</param>
    /// <param name="humanSlots">
    /// The slots a human is seated in. Every other slot is a computer player and is planned by the
    /// deterministic AI on every client identically, so its orders never cross the wire.
    /// </param>
    /// <returns>The canonical state hash to report.</returns>
    public static string Apply(
        MatchReplayRecorder replay,
        SealedOrdersView sealedOrders,
        IReadOnlySet<int> humanSlots)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(sealedOrders);
        ArgumentNullException.ThrowIfNull(humanSlots);
        var state = replay.State;
        if (state.Coordinator.Phase != TurnPhase.Command)
        {
            throw new InvalidOperationException(
                $"A sealed turn is applied during Command, not {state.Coordinator.Phase}.");
        }

        var bySlot = sealedOrders.Players.ToDictionary(entry => entry.Slot, entry => entry.Orders);
        // Slot order, every slot, so the sequence of mutations is the same on every client even
        // though only some of them have documents.
        for (var slot = 0; slot < state.Setup.Players.Count; slot++)
        {
            var player = new PlayerId(slot);
            if (bySlot.TryGetValue(slot, out var document)) ApplyDocument(replay, player, document);
            else if (!humanSlots.Contains(slot)) PlanComputerTurn(replay, player);
            replay.FinishCommand(player);
        }

        // Reaching the next Command phase is part of the sealed turn, not of the interface: see
        // CommandPhase for why the hire draw cannot wait for a player to open the dock.
        CommandPhase.Enter(replay);
        return MatchStateHasher.ComputeSha256(state);
    }

    /// <summary>
    /// One player's ops, attributed to <paramref name="player"/> whatever the payload says.
    /// </summary>
    /// <remarks>
    /// A result is not checked. The core's own validator decides what each op does, and every
    /// client runs the same one over the same state, so a refusal here is a refusal everywhere; the
    /// state hash is what says whether that held.
    /// </remarks>
    private static void ApplyDocument(
        MatchReplayRecorder replay,
        PlayerId player,
        OrderDocument document)
    {
        foreach (var op in document.Ops)
        {
            switch (op)
            {
                case SubmitCommandOp submit:
                    replay.Submit(new GameCommand(
                        player,
                        new GangId(submit.Gang),
                        (GangAction)submit.Action,
                        FromWire(submit.Target),
                        submit.Repeat,
                        submit.SecondaryTarget is { } secondary ? FromWire(secondary) : null));
                    break;
                case CancelCommandOp cancel:
                    replay.Cancel(player, new GangId(cancel.Gang));
                    break;
                case QueueHireOp hire:
                    replay.QueueHire(player, checked((short)hire.GangDefinitionId), hire.SectorId);
                    break;
                case SnubHireOfferOp snub:
                    replay.SnubHireOffer(player, checked((short)snub.GangDefinitionId));
                    break;
                case DismissNotificationOp:
                    replay.TryDismissNotification(player, out _);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"The sealed set carries an op this client cannot apply: {op.Op}.");
            }
        }
    }

    /// <summary>
    /// A computer player's turn, planned identically on every client from the shared state.
    /// </summary>
    /// <remarks>
    /// Empty slots and departed players are computer players too, which is why their orders never
    /// need to cross the wire: the same planner over the same state produces the same commands
    /// everywhere, and a client whose planner disagreed would surface as a desync like any other.
    /// </remarks>
    private static void PlanComputerTurn(MatchReplayRecorder replay, PlayerId player)
    {
        replay.PrepareAiPlanning(player);
        foreach (var command in AiTurnPlanner.Plan(replay.State, player)) replay.Submit(command);
        // The offers are already drawn: every client refilled every seat on entering Command.
        var hiring = replay.PrepareAiHiring(player);
        if (hiring.Choice is { } choice) replay.QueueHire(player, choice.GangDefinitionId, choice.SectorId);
        else if (hiring.RejectedGangDefinitionId is { } rejected) replay.SnubHireOffer(player, rejected);
    }

    private static Core.GameModel.CommandTarget FromWire(Generated.CommandTarget target) => target switch
    {
        NoneTarget => Core.GameModel.CommandTarget.None,
        GangTarget gang => Core.GameModel.CommandTarget.Gang(new GangId(gang.Id)),
        SectorTarget sector => Core.GameModel.CommandTarget.Sector(sector.Id),
        SiteTarget site => Core.GameModel.CommandTarget.Site(site.Id),
        ItemTarget item => Core.GameModel.CommandTarget.Item(item.Id),
        _ => throw new InvalidOperationException(
            $"The sealed set carries a target kind this client cannot apply: {target.Kind}."),
    };
}
