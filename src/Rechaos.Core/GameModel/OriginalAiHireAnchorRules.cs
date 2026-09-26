namespace Rechaos.Core.GameModel;

/// <summary>
/// Pure implementation of the original AI hire-placement selectors 0x24,
/// 0x25, and their 0x26 neighbor-count helper. The 65-entry literal arrays
/// deliberately expose the original's readable index-64 sentinel.
/// </summary>
internal static class OriginalAiHireAnchorRules
{
    private const int NeutralOwner = -1;
    private const int NoSector = -1;
    private const int InactiveGangSector = 100;
    private const int OriginalNeighborLimit = 65;
    // FND-AI-051: the owner read for a centre of -1 lands on the byte at 0x004A08C4, which holds 0.
    private const int FailedAnchorOwner = 0;
    private static readonly int[] BigManRadiusOne = [18, 26, 19, 27];
    private static readonly int[] BigManRadiusTwo =
    [
        9, 17, 25, 33,
        10, 18, 26, 34,
        11, 19, 27, 35,
        12, 20, 28, 36
    ];

    public static int Select(
        PlayerId player,
        ScenarioId scenario,
        int anchorSectorId,
        IReadOnlyList<int> literalSectorOwners,
        IReadOnlyList<byte> literalAvailability,
        Func<int, int> activeGangCount,
        Func<int, int> priorChaosCount)
    {
        ValidateInputs(
            player, anchorSectorId, literalSectorOwners, literalAvailability,
            activeGangCount, priorChaosCount);

        if (scenario == ScenarioId.BigMan)
            return SelectBigMan(
                player.Value, anchorSectorId, literalSectorOwners, activeGangCount);

        var selected = -1;
        var maximumVacancies = 0;
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            if (!IsEligibleOwnedSector(
                    player.Value, sectorId, literalSectorOwners, activeGangCount))
                continue;

            if (CountAvailableNeutralNeighbors(
                    player, sectorId, literalSectorOwners, literalAvailability)
                <= maximumVacancies)
                continue;

            selected = sectorId;
            // Selector 0x25 invokes selector 0x24 again when storing a new max.
            maximumVacancies = CountAvailableNeutralNeighbors(
                player, sectorId, literalSectorOwners, literalAvailability);
        }
        if (selected >= 0) return selected;

        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            if (IsEligibleOwnedSector(
                    player.Value, sectorId, literalSectorOwners, activeGangCount)
                && priorChaosCount(sectorId) == 0)
                return sectorId;
        }

        var minimumNonOwnedNeighbors = 9;
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            if (!IsEligibleOwnedSector(
                    player.Value, sectorId, literalSectorOwners, activeGangCount))
                continue;

            var count = CountNonOwnedNeighbors(player, sectorId, literalSectorOwners);
            if (count == 0 || count >= minimumNonOwnedNeighbors) continue;
            selected = sectorId;
            minimumNonOwnedNeighbors = count;
        }
        return selected;
    }

    /// <summary>
    /// RULE-AI-013, FND-AI-051: the anchor is kept when it has free land next to it, room in it
    /// and the scenario is not Big Man, tested in that order and stopping at the first failure. A
    /// failed anchor decodes to sector -1 and goes through the same neighbourhood count: its owner
    /// read gives 0, so only player 0 gets past it, and the signed remainder -1 excludes no column,
    /// leaving sectors 0, 6, 7 and 8. Its occupancy read gives 0.
    /// </summary>
    public static bool KeepsAnchor(
        PlayerId player,
        ScenarioId scenario,
        int anchorSectorId,
        IReadOnlyList<int> literalSectorOwners,
        IReadOnlyList<byte> literalAvailability,
        Func<int, int> activeGangCount)
    {
        ValidateLiteralArrays(literalSectorOwners, literalAvailability);
        ArgumentNullException.ThrowIfNull(activeGangCount);
        // DEV-AI-004: anchor 164 (sector 100) is replaced; the original reads past the sectors.
        if (anchorSectorId != NoSector && anchorSectorId is < 0 or >= MatchLimits.SectorCount)
            return false;
        return CountFreeNeighbours(
                   player, anchorSectorId, literalSectorOwners, literalAvailability) > 0
            && AnchorOccupancy(anchorSectorId, activeGangCount) < MatchLimits.FriendlyGangsPerSector
            && scenario != ScenarioId.BigMan;
    }

    public static int CountAvailableNeutralNeighbors(
        PlayerId player,
        int centerSectorId,
        IReadOnlyList<int> literalSectorOwners,
        IReadOnlyList<byte> literalAvailability)
    {
        ValidateLiteralArrays(literalSectorOwners, literalAvailability);
        ValidateCenter(centerSectorId);
        return CountFreeNeighbours(
            player, centerSectorId, literalSectorOwners, literalAvailability);
    }

    public static int CountNonOwnedNeighbors(
        PlayerId player,
        int centerSectorId,
        IReadOnlyList<int> literalSectorOwners)
    {
        ArgumentNullException.ThrowIfNull(literalSectorOwners);
        ValidateCenter(centerSectorId);
        if (literalSectorOwners.Count != OriginalNeighborLimit)
            throw new ArgumentException(
                "Literal sector owners must include the original index-64 sentinel.",
                nameof(literalSectorOwners));

        var count = 0;
        VisitLiteralNeighborhood(centerSectorId, candidate =>
        {
            if (literalSectorOwners[candidate] != player.Value) count++;
        });
        return count;
    }

    /// <summary>
    /// RULE-AI-013 free_neighbours (selector 0x24) for a centre from -1 to 63; the public entry
    /// point accepts only real sectors, the keep test also the failed anchor.
    /// </summary>
    private static int CountFreeNeighbours(
        PlayerId player,
        int centerSectorId,
        IReadOnlyList<int> literalSectorOwners,
        IReadOnlyList<byte> literalAvailability)
    {
        var centerOwner = centerSectorId == NoSector
            ? FailedAnchorOwner
            : literalSectorOwners[centerSectorId];
        if (centerOwner != player.Value) return 0;

        var count = 0;
        VisitLiteralNeighborhood(centerSectorId, candidate =>
        {
            if (literalSectorOwners[candidate] == NeutralOwner
                && literalAvailability[candidate] == 0)
                count++;
        });
        return count;
    }

    // FND-AI-051: for player 0 the occupancy read of the failed anchor gives 0. For players 1 to 5
    // it would read the previous player's row, but the owner test has already failed for them.
    private static int AnchorOccupancy(int anchorSectorId, Func<int, int> activeGangCount) =>
        anchorSectorId == NoSector ? 0 : activeGangCount(anchorSectorId);

    private static int SelectBigMan(
        int player,
        int anchorSectorId,
        IReadOnlyList<int> owners,
        Func<int, int> activeGangCount)
    {
        foreach (var sectorId in BigManRadiusOne)
            if (IsEligibleOwnedSector(player, sectorId, owners, activeGangCount))
                return sectorId;

        // The repeated radius-one entries are intentional original scan behavior.
        foreach (var sectorId in BigManRadiusTwo)
            if (IsEligibleOwnedSector(player, sectorId, owners, activeGangCount))
                return sectorId;

        return anchorSectorId;
    }

    private static bool IsEligibleOwnedSector(
        int player,
        int sectorId,
        IReadOnlyList<int> owners,
        Func<int, int> activeGangCount) =>
        owners[sectorId] == player
        && activeGangCount(sectorId) < MatchLimits.FriendlyGangsPerSector;

    private static void VisitLiteralNeighborhood(int centerSectorId, Action<int> visit)
    {
        // The signed remainder, as selector 0x24 takes it (FND-AI-051): -1 for the failed anchor,
        // which then excludes no column. For a centre from 0 to 63 this row-wrap guard excludes
        // the same cells as selector 0x26's test of centerX + deltaX against the board.
        var centerX = centerSectorId % MatchLimits.BoardWidth;
        for (var deltaY = -1; deltaY <= 1; deltaY++)
        for (var deltaX = -1; deltaX <= 1; deltaX++)
        {
            if ((centerX == 0 && deltaX == -1)
                || (centerX == MatchLimits.BoardWidth - 1 && deltaX == 1))
                continue;
            // The linear bound is intentionally <65, so bottom-edge index 64 remains.
            var candidate = centerSectorId + deltaY * MatchLimits.BoardWidth + deltaX;
            if (candidate is < 0 or >= OriginalNeighborLimit) continue;
            visit(candidate);
        }
    }

    private static void ValidateInputs(
        PlayerId player,
        int anchorSectorId,
        IReadOnlyList<int> owners,
        IReadOnlyList<byte> availability,
        Func<int, int> activeGangCount,
        Func<int, int> priorChaosCount)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        if (anchorSectorId != NoSector
            && anchorSectorId != InactiveGangSector
            && anchorSectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(anchorSectorId));
        ValidateLiteralArrays(owners, availability);
        ArgumentNullException.ThrowIfNull(activeGangCount);
        ArgumentNullException.ThrowIfNull(priorChaosCount);
    }

    private static void ValidateLiteralArrays(
        IReadOnlyList<int> owners,
        IReadOnlyList<byte> availability)
    {
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(availability);
        if (owners.Count != OriginalNeighborLimit)
            throw new ArgumentException(
                "Literal sector owners must include the original index-64 sentinel.",
                nameof(owners));
        if (availability.Count != OriginalNeighborLimit)
            throw new ArgumentException(
                "Literal availability must include the original index-64 sentinel.",
                nameof(availability));
    }

    private static void ValidateCenter(int centerSectorId)
    {
        if (centerSectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(centerSectorId));
    }
}
