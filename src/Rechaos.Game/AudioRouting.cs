using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class AudioRouting
{
    public static short? WeaponSound(MatchState state, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution?.Code != CommandResolutionCode.Resolved
            || gameEvent.Gang is not { } gangId)
            return null;
        var gang = state.FindGang(gangId);
        return gang?.WeaponItemId is { } itemId ? state.Definitions.Items[itemId].Sound : null;
    }

    public static string SoundFile(short index)
    {
        if (index is < 0 or > 18) throw new ArgumentOutOfRangeException(nameof(index));
        return $"SND005{index:00}.wav";
    }
}
