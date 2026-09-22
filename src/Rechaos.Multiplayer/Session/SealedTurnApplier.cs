using System.Diagnostics;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

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
    /// <returns>The canonical state hash to report.</returns>
    /// <exception cref="MultiplayerProtocolException">
    /// The set is for another turn than the one the match is on, or carries something this build
    /// cannot apply.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Which seats the computer plays is read out of the match being applied, not passed in. It is
    /// part of the state every client hashes, so it is the one answer every client is already
    /// guaranteed to agree on; a roster read from the server alongside it could be newer on one
    /// client than another, and a slot planned by the AI on one and left idle on another is a desync
    /// on the turn after the one that caused it.
    /// </para>
    /// <para>
    /// That makes a human seat with no document in the set do nothing for the turn, which is what a
    /// player who ran out of clock ordered: nothing. An absent or departed player's gangs likewise
    /// hold position while the other players vote to wait. <see cref="MatchReplayRecorder.TransferPlayerToComputer"/>
    /// is invoked only by the authoritative approved-takeover event, at the exact same boundary on
    /// every live or reconnecting client.
    /// </para>
    /// </remarks>
    public static string Apply(MatchReplayRecorder replay, SealedOrdersView sealedOrders)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(sealedOrders);
        var state = replay.State;
        if (state.Coordinator.Phase != TurnPhase.Command)
        {
            throw new InvalidOperationException(
                $"A sealed turn is applied during Command, not {state.Coordinator.Phase}.");
        }
        if (sealedOrders.Turn != state.Coordinator.Turn)
        {
            // Applying turn N's orders to turn M's state would be a desync nobody reported: every
            // hash after it would be wrong, and the server would never be told why.
            throw new MultiplayerProtocolException(
                $"the sealed set is for turn {sealedOrders.Turn}, but the match is on turn "
                + state.Coordinator.Turn);
        }

        // Every document is read before any is applied, so a set this build cannot read is refused
        // before the replay records a single step of it.
        var bySlot = OrdersBySlot(sealedOrders, state.Setup.Players.Count);
        // Slot order, every slot, so the sequence of mutations is the same on every client even
        // though only some of them have documents.
        for (var slot = 0; slot < state.Setup.Players.Count; slot++)
        {
            var player = new PlayerId(slot);
            if (bySlot.TryGetValue(slot, out var orders)) ApplyOrders(replay, player, orders);
            // An eliminated seat is skipped, as HeadlessMatchRunner.Advance skips it offline. The
            // planner produces no commands for a dead seat, but the hire placement draw still spends
            // the shared RNG, so planning it made online and offline runs of one seed diverge.
            else if (state.Setup.Players[slot].Controller == PlayerController.Computer
                     && state.FindPlayer(player)?.Status == Core.GameModel.PlayerStatus.Active)
                PlanComputerTurn(replay, player);
            replay.FinishCommand(player);
        }

        // Reaching the next Command phase is part of the sealed turn, not of the interface: see
        // CommandPhase for why the hire draw cannot wait for a player to open the dock.
        CommandPhase.Enter(replay);
        return MatchStateHasher.ComputeFingerprint(state);
    }

    /// <summary>
    /// The set decoded and indexed by slot, refusing a shape no client could apply consistently.
    /// </summary>
    /// <remarks>
    /// Two documents for one slot has no defined meaning — which of them the turn contains would
    /// come down to enumeration order — and a slot past the board cannot be applied at all. Both are
    /// protocol failures rather than exceptions out of a dictionary, so the session reports them as
    /// the server having sent something it cannot act on. So is an op naming something the core
    /// cannot construct, which is why every op is decoded here rather than as it is applied.
    /// </remarks>
    private static Dictionary<int, DecodedOrderOp[]> OrdersBySlot(
        SealedOrdersView sealedOrders,
        int slotCount)
    {
        var bySlot = new Dictionary<int, DecodedOrderOp[]>(sealedOrders.Players.Count);
        foreach (var entry in sealedOrders.Players)
        {
            if (entry.Slot < 0 || entry.Slot >= slotCount)
            {
                throw new MultiplayerProtocolException(
                    $"the sealed set names slot {entry.Slot}, which this match does not have");
            }
            if (bySlot.ContainsKey(entry.Slot))
            {
                throw new MultiplayerProtocolException(
                    $"the sealed set carries two documents for slot {entry.Slot}");
            }
            bySlot.Add(entry.Slot, OrderOpDecoder.Decode(entry.Orders.Ops, new PlayerId(entry.Slot), Document));
        }
        return bySlot;
    }

    /// <summary>
    /// One player's ops, attributed to <paramref name="player"/> whatever the payload says.
    /// </summary>
    /// <remarks>
    /// A result is not checked. The core's own validator decides what each op does, and every
    /// client runs the same one over the same state, so a refusal here is a refusal everywhere; the
    /// state hash is what says whether that held.
    /// </remarks>
    private static void ApplyOrders(
        MatchReplayRecorder replay,
        PlayerId player,
        DecodedOrderOp[] orders)
    {
        foreach (var op in orders)
        {
            switch (op)
            {
                case DecodedOrderOp.Submit submit:
                    replay.Submit(submit.Command);
                    break;
                case DecodedOrderOp.Cancel cancel:
                    replay.Cancel(player, cancel.Gang);
                    break;
                case DecodedOrderOp.QueueHire hire:
                    replay.QueueHire(player, hire.GangDefinitionId, hire.SectorId);
                    break;
                case DecodedOrderOp.SnubHireOffer snub:
                    replay.SnubHireOffer(player, snub.GangDefinitionId);
                    break;
                case DecodedOrderOp.DismissNotification:
                    replay.TryDismissNotification(player, out _);
                    break;
                default:
                    throw new UnreachableException($"a decoded op this applier does not handle: {op}");
            }
        }
    }

    /// <summary>What a refusal names, so a player is told which payload could not be read.</summary>
    private const string Document = "the sealed set";

    /// <summary>
    /// A computer player's turn, planned identically on every client from the shared state.
    /// </summary>
    /// <remarks>
    /// Every seat no human took at match start is a computer player, which is why those orders never
    /// need to cross the wire: the same planner over the same state produces the same commands
    /// everywhere, and a client whose planner disagreed would surface as a desync like any other.
    /// </remarks>
    private static void PlanComputerTurn(MatchReplayRecorder replay, PlayerId player)
    {
        replay.PrepareAiPlanning(player);
        foreach (var command in AiPolicyPlanner.Plan(replay.State, player)) replay.Submit(command);
        // The offers are already drawn: every client refilled every seat on entering Command.
        var hiring = replay.PrepareAiHiring(player);
        if (hiring.Choice is { } choice) replay.QueueHire(player, choice.GangDefinitionId, choice.SectorId);
        else if (hiring.RejectedGangDefinitionId is { } rejected) replay.SnubHireOffer(player, rejected);
    }
}
