using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    private void TransferPlayerToComputer(string playerId)
    {
        if (!_slotsByPlayerId.TryGetValue(playerId, out var slot)) return;
        var player = _replay.State.FindPlayer(new PlayerId(slot));
        if (player is null || player.Setup.Controller == PlayerController.Computer) return;
        _replay.TransferPlayerToComputer(player.Id);
    }

    private void TransferPlayerToHuman(string playerId)
    {
        if (!_slotsByPlayerId.TryGetValue(playerId, out var slot)) return;
        var player = _replay.State.FindPlayer(new PlayerId(slot));
        if (player is null || player.Setup.Controller == PlayerController.Human) return;
        _replay.TransferPlayerToHuman(player.Id);
    }

    private void AddLatePlayer(string playerId, int slot)
    {
        if (_slotsByPlayerId.TryGetValue(playerId, out var knownSlot))
        {
            if (knownSlot != slot)
                throw new MultiplayerProtocolException("a late player changed seats");
        }
        else if (_slotsByPlayerId.Values.Contains(slot))
            throw new MultiplayerProtocolException("a late player claimed a human-owned seat");
        else
        {
            _slotsByPlayerId[playerId] = slot;
            _awaitedSeats++;
        }
        var player = _replay.State.FindPlayer(new PlayerId(slot));
        if (player?.Setup.Controller == PlayerController.Computer)
            _replay.TransferPlayerToHuman(player.Id);
    }
}
