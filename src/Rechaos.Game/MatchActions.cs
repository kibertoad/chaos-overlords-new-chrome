using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

/// <summary>
/// The one place a player's mutations go, in either kind of match.
/// </summary>
/// <remarks>
/// <para>
/// Hot-seat play applies them straight to the match. Online play applies them to a copy and records
/// them as the turn's order document instead, because the authoritative state advances only by
/// applying a sealed turn — see <see cref="SpeculativeTurn"/>.
/// </para>
/// <para>
/// Routing every call site through here is what keeps the two apart: an interface that reached for
/// the recorder directly would work in hot-seat and quietly fail to record online, which is a turn
/// the player played and nobody else saw. That is not a rule anybody can be asked to remember, so
/// there is no recorder to reach for: the turn structure still needs one, and
/// <see cref="HotSeatRecorder"/> is it, which refuses to hand one over in an online match. A call
/// site that wants to mutate a player's match has the methods below and nothing else.
/// </para>
/// </remarks>
internal sealed class MatchActions
{
    private readonly MatchReplayRecorder _replay;
    private readonly SpeculativeTurn? _turn;

    /// <summary>A hot-seat match: mutations are the match.</summary>
    internal MatchActions(MatchReplayRecorder replay)
    {
        _replay = replay;
    }

    /// <summary>An online turn: mutations are a copy, and a document.</summary>
    internal MatchActions(SpeculativeTurn turn)
    {
        _turn = turn;
        _replay = turn.Replay;
    }

    internal MatchState State => _replay.State;

    /// <summary>The turn being planned online, or null in a hot-seat match.</summary>
    internal SpeculativeTurn? OnlineTurn => _turn;

    /// <summary>Whether this is an online match, where the turn structure is the server's to drive.</summary>
    internal bool IsOnline => _turn is not null;

    /// <summary>
    /// The recorder, for the steps only a hot-seat match takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Finishing a phase, planning the computer players, saving a replay: all of them drive or record
    /// the turn structure, and online the turn structure belongs to the server's barrier. Every
    /// caller is already behind a hot-seat check; this refuses rather than trusting that, because the
    /// cost of one that is not is a player's orders silently going nowhere.
    /// </para>
    /// <para>
    /// Reads do not need this. <see cref="State"/> is the state the interface draws, whichever kind of
    /// match it came from.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The match is online.</exception>
    internal MatchReplayRecorder HotSeatRecorder => _turn is null
        ? _replay
        : throw new InvalidOperationException(
            "An online match advances by applying sealed turns, so its recorder is not the "
            + "interface's to drive. Queue the player's intent through MatchActions instead.");

    /// <summary>
    /// The journal this client owns, for reading rather than driving.
    /// </summary>
    /// <remarks>
    /// Always a real journal, and always replayable from its own first step — but what that first
    /// step is differs by match kind, which is why this is separate from
    /// <see cref="HotSeatRecorder"/>. Hot-seat, it is the whole match from setup. Online, it is the
    /// turn being planned, over a copy taken from the last authoritative state, because that is all
    /// this client is the authority on. A bug report attaches whichever there is; only the hot-seat
    /// one is a session's history, so only that one travels with a save.
    /// </remarks>
    internal MatchReplayRecorder Journal => _replay;

    /// <summary>The journal only a hot-seat match has: this whole session, from its first turn.</summary>
    internal MatchReplayRecorder? HotSeatJournal => _turn is null ? _replay : null;

    /// <summary>The turn the match was last saved or loaded in, or null since an order changed it.</summary>
    private int? _savedTurn;

    /// <summary>
    /// RULE-UI-015, FND-UI-058: whether the match is as it was last saved or loaded. A new match
    /// starts unsaved; a save or a load marks it saved; an accepted order, the clearing of one and
    /// a hire mark it unsaved, and so does each resolved turn, which moves the turn number on. An
    /// online match counts as saved, as a network game does in the original.
    /// </summary>
    internal bool IsSaved => _turn is not null || _savedTurn == State.Coordinator.Turn;

    /// <summary>RULE-UI-015: records a save or a load of the match as it stands.</summary>
    internal void MarkSaved() => _savedTurn = State.Coordinator.Turn;

    internal CommandSubmissionResult Submit(GameCommand command) =>
        Changed(_turn is null ? _replay.Submit(command) : _turn.Submit(command));

    internal CommandSubmissionResult Cancel(PlayerId player, GangId gang) =>
        Changed(_turn is null ? _replay.Cancel(player, gang) : _turn.Cancel(gang));

    internal HireSubmissionResult QueueHire(PlayerId player, short gangDefinitionId, int sectorId)
    {
        var result = _turn is null
            ? _replay.QueueHire(player, gangDefinitionId, sectorId)
            : _turn.QueueHire(gangDefinitionId, sectorId);
        if (result.Accepted) _savedTurn = null;
        return result;
    }

    private CommandSubmissionResult Changed(CommandSubmissionResult result)
    {
        if (result.Accepted) _savedTurn = null;
        return result;
    }

    internal HireOfferSnubResult SnubHireOffer(PlayerId player, short gangDefinitionId) =>
        _turn is null
            ? _replay.SnubHireOffer(player, gangDefinitionId)
            : _turn.SnubHireOffer(gangDefinitionId);

    internal bool DismissNotification(PlayerId player) =>
        _turn is null
            ? _replay.TryDismissNotification(player, out _)
            : _turn.DismissNotification();

    /// <summary>
    /// Sends a Comlink message.
    /// </summary>
    /// <remarks>
    /// Online the message goes into the turn's order document and reaches the real inboxes when the
    /// turn seals, because an inbox is hashed state every client must change at the same point.
    /// </remarks>
    internal ComlinkSendResult SendComlinkMessage(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string message) =>
        _turn is null
            ? _replay.SendComlinkMessage(sender, recipients, message)
            : _turn.SendComlinkMessage(recipients, message);

    /// <summary>
    /// Marks one displayed Comlink message read.
    /// </summary>
    /// <remarks>
    /// Online the copy the player plans on is marked at once, so the panel and the alert behave as
    /// in hot-seat play, and the mark goes into the order document for the seal.
    /// </remarks>
    internal void MarkComlinkRead(PlayerId player, long sequence)
    {
        if (_turn is null) _replay.MarkComlinkRead(player, sequence);
        else _turn.MarkComlinkRead(sequence);
    }

    /// <summary>
    /// Refills the dock, in a hot-seat match.
    /// </summary>
    /// <remarks>
    /// Online it does nothing: every seat's offers were drawn when the turn reached Command, so
    /// that every client spends the shared PRNG identically. Drawing again here would be a seventh
    /// draw on one client and a sixth on the others.
    /// </remarks>
    internal void PrepareHireOffers(PlayerId player)
    {
        if (_turn is null) _replay.PrepareHireOffers(player);
    }
}
