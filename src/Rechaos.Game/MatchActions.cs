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
/// the player played and nobody else saw.
/// </para>
/// </remarks>
internal sealed class MatchActions
{
    private readonly SpeculativeTurn? _turn;

    /// <summary>A hot-seat match: mutations are the match.</summary>
    internal MatchActions(MatchReplayRecorder replay)
    {
        Replay = replay;
    }

    /// <summary>An online turn: mutations are a copy, and a document.</summary>
    internal MatchActions(SpeculativeTurn turn)
    {
        _turn = turn;
        Replay = turn.Replay;
    }

    /// <summary>The recorder the interface reads state through.</summary>
    internal MatchReplayRecorder Replay { get; }

    internal MatchState State => Replay.State;

    /// <summary>The turn being planned online, or null in a hot-seat match.</summary>
    internal SpeculativeTurn? OnlineTurn => _turn;

    internal CommandSubmissionResult Submit(GameCommand command) =>
        _turn is null ? Replay.Submit(command) : _turn.Submit(command);

    internal CommandSubmissionResult Cancel(PlayerId player, GangId gang) =>
        _turn is null ? Replay.Cancel(player, gang) : _turn.Cancel(gang);

    internal HireSubmissionResult QueueHire(PlayerId player, short gangDefinitionId, int sectorId) =>
        _turn is null
            ? Replay.QueueHire(player, gangDefinitionId, sectorId)
            : _turn.QueueHire(gangDefinitionId, sectorId);

    internal HireOfferSnubResult SnubHireOffer(PlayerId player, short gangDefinitionId) =>
        _turn is null
            ? Replay.SnubHireOffer(player, gangDefinitionId)
            : _turn.SnubHireOffer(gangDefinitionId);

    internal bool DismissNotification(PlayerId player) =>
        _turn is null
            ? Replay.TryDismissNotification(player, out _)
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
        if (_turn is null) Replay.PrepareHireOffers(player);
    }
}
