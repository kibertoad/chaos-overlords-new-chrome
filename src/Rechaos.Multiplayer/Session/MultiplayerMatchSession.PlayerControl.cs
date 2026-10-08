using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    /// <summary>
    /// Who holds a seat, in the copy of the match this session drives.
    /// </summary>
    /// <remarks>
    /// The session hands the interface a fresh clone at each turn boundary and never shares its own
    /// — see <see cref="MatchStateClone"/> for why — so a seat changing hands, or pointedly not
    /// changing hands, between boundaries is invisible from outside. A control transfer announced
    /// after the final turn is exactly that: there is no later boundary for a clone to arrive at,
    /// which leaves this the only honest way to ask whether the transfer was ignored.
    /// </remarks>
    internal PlayerController? ControllerOfSlot(int slot) =>
        Replay.State.FindPlayer(new PlayerId(slot))?.Setup.Controller;

    /// <summary>A handover announced live, which takes effect before the turn the match is on.</summary>
    /// <remarks>
    /// In a match played from views the server's resolver hands the seat over, and the next view
    /// shows it; this client holds no state to change.
    /// </remarks>
    private void TransferPlayerToComputer(string playerId) =>
        _history?.HandOverSeat(playerId, PlayerController.Computer, Replay.State.Coordinator.Turn);

    /// <summary>A handover announced live; see <see cref="TransferPlayerToComputer"/>.</summary>
    private void TransferPlayerToHuman(string playerId) =>
        _history?.HandOverSeat(playerId, PlayerController.Human, Replay.State.Coordinator.Turn);

    /// <summary>A late join announced live, which takes effect before the turn the match is on.</summary>
    private void AddLatePlayer(string playerId, int slot)
    {
        var added = _history is { } history
            ? history.AddLatePlayer(playerId, slot, Replay.State.Coordinator.Turn)
            : _viewSeats.TryAdd(playerId, slot);
        if (added) _awaitedSlots.Add(slot);
    }
}
