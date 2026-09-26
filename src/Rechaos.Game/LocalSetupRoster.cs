using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public enum LocalSetupMoveResult : byte
{
    Invalid,
    Unchanged,
    MovedToEmptyColor,
    ExchangedHumanColors
}

public sealed class LocalSetupRoster
{
    private readonly List<int> _humanSlots = [0];

    public int Count => _humanSlots.Count;
    public IReadOnlyList<int> HumanSlots => _humanSlots.ToArray();
    public bool IsHuman(int slot) => _humanSlots.Contains(slot);

    /// <summary>Replaces the humans with <paramref name="slots"/>, as a reopened setup does.</summary>
    public void Restore(IEnumerable<int> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var restored = slots.Distinct().ToArray();
        if (restored.Length is < LocalSetupPolicy.DefaultHumanPlayerCount or > MatchLimits.PlayerCount
            || restored.Any(slot => slot is < 0 or >= MatchLimits.PlayerCount))
            throw new ArgumentOutOfRangeException(nameof(slots));
        _humanSlots.Clear();
        _humanSlots.AddRange(restored);
    }

    public int? AddHuman()
    {
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            if (IsHuman(slot)) continue;
            _humanSlots.Add(slot);
            return slot;
        }
        return null;
    }

    public int? RemoveHuman(int slot)
    {
        if (_humanSlots.Count == LocalSetupPolicy.DefaultHumanPlayerCount || !IsHuman(slot))
            return null;
        _humanSlots.Remove(slot);
        return slot;
    }

    public LocalSetupMoveResult MoveHuman(int sourceSlot, int targetSlot)
    {
        if (sourceSlot is < 0 or >= MatchLimits.PlayerCount
            || targetSlot is < 0 or >= MatchLimits.PlayerCount
            || !IsHuman(sourceSlot))
            return LocalSetupMoveResult.Invalid;
        if (sourceSlot == targetSlot) return LocalSetupMoveResult.Unchanged;
        if (IsHuman(targetSlot)) return LocalSetupMoveResult.ExchangedHumanColors;
        _humanSlots[_humanSlots.IndexOf(sourceSlot)] = targetSlot;
        return LocalSetupMoveResult.MovedToEmptyColor;
    }
}
