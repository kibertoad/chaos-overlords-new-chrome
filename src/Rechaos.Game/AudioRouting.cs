using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class AudioRouting
{
    public const int MinimumEffectVolumeLevel = 0;
    public const int MaximumEffectVolumeLevel = 10;
    public const int DefaultEffectVolumeLevel = 6;
    private const int OriginalVolumeStep = 25 * 256;
    private static readonly IReadOnlyDictionary<int, int> GeneralSoundResources =
        new Dictionary<int, int>
        {
            [0] = 200,
            [1] = 201,
            [2] = 202,
            [3] = 203,
            [4] = 204,
            [6] = 205,
            [7] = 206,
            [8] = 207,
            [9] = 208
        };

    public static IReadOnlyList<int> GeneralSoundSlots { get; } =
        GeneralSoundResources.Keys.Order().ToArray();

    public static short? WeaponSound(MatchState state, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution?.Code != CommandResolutionCode.Resolved
            || gameEvent.Gang is not { } gangId)
            return null;
        var itemId = gameEvent.Resolution.ItemId ?? state.FindGang(gangId)?.WeaponItemId;
        return itemId is { } weapon ? state.Definitions.Items[weapon].Sound : null;
    }

    public static string SoundFile(short index)
    {
        if (index is < 0 or > 18) throw new ArgumentOutOfRangeException(nameof(index));
        return $"SND005{index:00}.wav";
    }

    public static float EffectVolumeForLevel(int level)
    {
        if (level is < MinimumEffectVolumeLevel or > MaximumEffectVolumeLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return level * OriginalVolumeStep / (float)ushort.MaxValue;
    }

    public static string GeneralSoundFile(int slot)
    {
        if (!GeneralSoundResources.TryGetValue(slot, out var resource))
            throw new ArgumentOutOfRangeException(nameof(slot));
        return $"SND00{resource:000}.wav";
    }
}
