namespace Rechaos.Core.GameModel;

public readonly record struct HireOfferSlotState(
    short? GangDefinitionId,
    short? ExcludedDefinitionId,
    short? LegacyReplacementDefinitionId = null)
{
    public static HireOfferSlotState Uninitialized => new(null, null);
    public static HireOfferSlotState Available(short gangDefinitionId) => new(gangDefinitionId, null);
    public static HireOfferSlotState Vacant(short excludedDefinitionId) => new(null, excludedDefinitionId);
}

public sealed partial class MatchPlayerState
{
    public IReadOnlyList<HireOfferSlotState> HireOfferSlots => _hireOfferSlots;
    public IReadOnlyList<short> HirePool => _hireOfferSlots
        .Where(slot => slot.GangDefinitionId.HasValue)
        .Select(slot => slot.GangDefinitionId!.Value)
        .ToArray();

    internal int FindHireOfferSlot(short gangDefinitionId) =>
        Array.FindIndex(_hireOfferSlots, slot => slot.GangDefinitionId == gangDefinitionId);

    internal void SetHireOfferSlot(int slot, HireOfferSlotState value) =>
        _hireOfferSlots[slot] = value;

    internal void MarkHireOfferSnubbed(short gangDefinitionId, int slot)
    {
        SnubbedHireOffer = gangDefinitionId;
        SnubbedHireOfferSlot = slot;
    }

    internal void ClearSnubbedHireOffer()
    {
        SnubbedHireOffer = null;
        SnubbedHireOfferSlot = null;
    }
}
