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

    private static readonly int[] FailedAnchorNeighbours = [0, 6, 7, 8];
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
    /// and the scenario is not Big Man, tested in that order. A failed anchor decodes to sector
    /// -1: its owner read lands on a byte holding 0, so only player 0 passes; the remainder -1
    /// excludes no column, leaving sectors 0, 6, 7 and 8; and its occupancy read gives 0.
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
        int freeNeighbours;
        int occupancy;
        if (anchorSectorId == NoSector)
        {
            if (player.Value != 0) return false;
            freeNeighbours = FailedAnchorNeighbours.Count(sectorId =>
                literalSectorOwners[sectorId] == NeutralOwner
                && literalAvailability[sectorId] == 0);
            occupancy = 0;
        }
        else if (anchorSectorId is >= 0 and < MatchLimits.SectorCount)
        {
            freeNeighbours = CountAvailableNeutralNeighbors(
                player, anchorSectorId, literalSectorOwners, literalAvailability);
            if (freeNeighbours == 0) return false;
            occupancy = activeGangCount(anchorSectorId);
        }
        else
        {
            return false;
        }
        return freeNeighbours > 0
            && occupancy < MatchLimits.FriendlyGangsPerSector
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
        if (literalSectorOwners[centerSectorId] != player.Value) return 0;

        var count = 0;
        VisitLiteralNeighborhood(centerSectorId, candidate =>
        {
            if (literalSectorOwners[candidate] == NeutralOwner
                && literalAvailability[candidate] == 0)
                count++;
        });
        return count;
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
        var centerX = centerSectorId % MatchLimits.BoardWidth;
        for (var deltaY = -1; deltaY <= 1; deltaY++)
        for (var deltaX = -1; deltaX <= 1; deltaX++)
        {
            // This column check is the original row-wrap guard. The separate
            // linear bound is intentionally <65, so bottom-edge index 64 remains.
            var candidateX = centerX + deltaX;
            if (candidateX is < 0 or >= MatchLimits.BoardWidth) continue;
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
