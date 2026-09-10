namespace Rechaos.Core.GameModel;

/// <summary>
/// Pure implementation of the original AI hire destination selector at
/// <c>0x00408214</c>. The destination write is reported explicitly because
/// modes 2 through 63 return 99 without updating the caller's destination.
/// </summary>
internal static class OriginalAiHirePlacementRules
{
    internal const int OriginalGangSlotCount = 81;
    internal const int InactiveGangSector = 100;

    public static OriginalAiHirePlacementResult Select(
        PlayerId player,
        int mode,
        int offerSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<int> gangSectorIds,
        DeterministicRandom random)
    {
        if (mode < 0) throw new ArgumentOutOfRangeException(nameof(mode));

        if (mode >= MatchLimits.SectorCount)
            return new OriginalAiHirePlacementResult(
                mode - MatchLimits.SectorCount,
                WritesDestination: true);

        if (mode >= 2)
            return new OriginalAiHirePlacementResult(99, WritesDestination: false);

        ArgumentNullException.ThrowIfNull(random);
        ValidateSelectionInputs(player, sectorOwners, gangSectorIds);

        if (mode == 0 && offerSlot >= 0)
            ConsumeModeZeroExtremeSelection(gangSectorIds, random);

        // Mode zero deliberately falls through the original mode-one path and
        // overwrites its tentative extreme-sector choice.
        var candidates = new List<int>(
            MatchLimits.SectorCount + OriginalGangSlotCount);
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
            if (sectorOwners[sectorId] == player.Value)
                candidates.Add(sectorId);
        for (var slot = 0; slot < OriginalGangSlotCount; slot++)
            if (gangSectorIds[slot] != InactiveGangSector)
                candidates.Add(gangSectorIds[slot]);

        // The executable's bounded wrapper clamps a zero bound to one. It
        // therefore still consumes three raw rand() calls before the empty
        // ordinal scan leaves the destination at -1.
        var ordinal = random.NextInclusive(Math.Max(1, candidates.Count));
        var target = candidates.Count == 0 ? -1 : candidates[ordinal - 1];
        return new OriginalAiHirePlacementResult(target, WritesDestination: true);
    }

    private static void ConsumeModeZeroExtremeSelection(
        IReadOnlyList<int> gangSectorIds,
        DeterministicRandom random)
    {
        var occupied = gangSectorIds
            .Where(sectorId => sectorId != InactiveGangSector)
            .ToArray();
        var minimumMultiplicity = 0;
        var maximumMultiplicity = 0;
        if (occupied.Length > 0)
        {
            var minimum = occupied.Min();
            var maximum = occupied.Max();
            minimumMultiplicity = occupied.Count(sectorId => sectorId == minimum);
            maximumMultiplicity = occupied.Count(sectorId => sectorId == maximum);
        }

        if (minimumMultiplicity < MatchLimits.FriendlyGangsPerSector
            || maximumMultiplicity < MatchLimits.FriendlyGangsPerSector)
        {
            _ = random.NextInclusive(2);
        }
    }

    private static void ValidateSelectionInputs(
        PlayerId player,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<int> gangSectorIds)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        ArgumentNullException.ThrowIfNull(sectorOwners);
        ArgumentNullException.ThrowIfNull(gangSectorIds);
        if (sectorOwners.Count != MatchLimits.SectorCount)
            throw new ArgumentException(
                "Sector owners must contain all 64 original sectors.",
                nameof(sectorOwners));
        if (gangSectorIds.Count != OriginalGangSlotCount)
            throw new ArgumentException(
                "Gang sectors must contain all 81 original gang slots.",
                nameof(gangSectorIds));
        if (gangSectorIds.Any(sectorId =>
                sectorId != InactiveGangSector
                && sectorId is < 0 or >= MatchLimits.SectorCount))
            throw new ArgumentOutOfRangeException(nameof(gangSectorIds));
    }
}

internal readonly record struct OriginalAiHirePlacementResult(
    int TargetSectorId,
    bool WritesDestination);
