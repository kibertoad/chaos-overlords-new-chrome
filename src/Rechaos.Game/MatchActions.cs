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
/// site that wants to mutate a player's match has the five methods below and nothing else.
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

    internal CommandSubmissionResult Submit(GameCommand command) =>
        _turn is null ? _replay.Submit(command) : _turn.Submit(command);

    internal CommandSubmissionResult Cancel(PlayerId player, GangId gang) =>
        _turn is null ? _replay.Cancel(player, gang) : _turn.Cancel(gang);

    internal HireSubmissionResult QueueHire(PlayerId player, short gangDefinitionId, int sectorId) =>
        _turn is null
            ? _replay.QueueHire(player, gangDefinitionId, sectorId)
            : _turn.QueueHire(gangDefinitionId, sectorId);

    internal HireOfferSnubResult SnubHireOffer(PlayerId player, short gangDefinitionId) =>
        _turn is null
            ? _replay.SnubHireOffer(player, gangDefinitionId)
            : _turn.SnubHireOffer(gangDefinitionId);

    internal bool DismissNotification(PlayerId player) =>
        _turn is null
            ? _replay.TryDismissNotification(player, out _)
            : _turn.DismissNotification();

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
