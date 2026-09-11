using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GeneralSoundSlot
{
    public const int PanelOpen = 0;
    public const int PanelClose = 1;
    public const int ButtonPress = 2;
    public const int AcceptedSelection = 3;
    public const int RejectedInput = 4;
    public const int ReportAlert = 6;
    public const int CountdownWarning = 7;
    public const int FinalSecondWarning = 8;
    public const int LoadedWithoutCallSite = 9;
}

public static class AudioRouting
{
    public const int MinimumEffectVolumeLevel = 0;
    public const int MaximumEffectVolumeLevel = 10;
    public const int DefaultEffectVolumeLevel = 6;
    public const short UnarmedSound = 0;
    public const short MartialArtsSound = 1;
    public const short PoliceSound = 18;
    private const int OriginalVolumeStep = 25 * 256;
    private static readonly IReadOnlyDictionary<int, int> GeneralSoundResources =
        new Dictionary<int, int>
        {
            [GeneralSoundSlot.PanelOpen] = 200,
            [GeneralSoundSlot.PanelClose] = 201,
            [GeneralSoundSlot.ButtonPress] = 202,
            [GeneralSoundSlot.AcceptedSelection] = 203,
            [GeneralSoundSlot.RejectedInput] = 204,
            [GeneralSoundSlot.ReportAlert] = 205,
            [GeneralSoundSlot.CountdownWarning] = 206,
            [GeneralSoundSlot.FinalSecondWarning] = 207,
            [GeneralSoundSlot.LoadedWithoutCallSite] = 208
        };

    public static IReadOnlyList<int> GeneralSoundSlots { get; } =
        GeneralSoundResources.Keys.Order().ToArray();

    public static int? PlayerCountResultSound(bool changed, bool pointerButton) =>
        pointerButton && changed
            ? null
            : changed
                ? GeneralSoundSlot.AcceptedSelection
                : GeneralSoundSlot.RejectedInput;

    public static short? CombatSound(MatchState state, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved)
            return gameEvent.PoliceAttack?.Detected == true ? PoliceSound : null;
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution?.Code != CommandResolutionCode.Resolved
            || gameEvent.Gang is not { } gangId)
            return null;
        var itemId = gameEvent.Resolution.ItemId ?? state.FindGang(gangId)?.WeaponItemId;
        return GangAttackSound(state, gangId, itemId);
    }

    public static short GangAttackSound(MatchState state, GangId gangId, short? itemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (itemId is { } weapon)
        {
            if (weapon < 0 || weapon >= state.Definitions.Items.Count)
                throw new ArgumentOutOfRangeException(nameof(itemId));
            return state.Definitions.Items[weapon].Sound;
        }
        var gang = state.FindGang(gangId)
            ?? throw new ArgumentOutOfRangeException(nameof(gangId));
        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        return definition.Stats.MartialArts > 0 ? MartialArtsSound : UnarmedSound;
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
