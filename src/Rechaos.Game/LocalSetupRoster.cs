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

    public int? RemoveLastHuman()
    {
        if (_humanSlots.Count == LocalSetupPolicy.DefaultHumanPlayerCount) return null;
        var slot = _humanSlots[^1];
        _humanSlots.RemoveAt(_humanSlots.Count - 1);
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
