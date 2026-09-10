namespace Rechaos.Core.GameModel;

/// <summary>
/// Pure implementation of the original weighted sector selector at 0x00408642
/// for its fully recovered modes 1 through 5. Planner-specific queries remain
/// explicit inputs so this kernel does not guess at unrecovered outer policy.
/// </summary>
internal static class OriginalAiSectorSelectionRules
{
    private const int NeutralOwner = -1;
    private const int MinimumRawOwner = -3;
    private const int MaximumDestinationGangCount =
        MatchLimits.FriendlyGangsPerSector - 1;

    public static int Select(
        int mode,
        int sourceSectorId,
        PlayerId player,
        int family,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        Func<int, bool> canSoloControl,
        Func<int, bool> hasPriorChaos,
        Func<int, bool> isHostileOwner,
        Func<int, bool> isHumanOwner,
        IReadOnlyList<int> playerOrderValues,
        DeterministicRandom random)
    {
        ValidateInputs(
            mode, sourceSectorId, family, sectorOwners, sectorDisabled,
            sectorGangCounts, canSoloControl, hasPriorChaos,
            isHostileOwner, isHumanOwner, playerOrderValues, random);

        var scores = new int[MatchLimits.SectorCount];
        var sourceX = sourceSectorId % MatchLimits.BoardWidth;
        var sourceY = sourceSectorId / MatchLimits.BoardWidth;
        var found = false;
        for (var radius = 1; radius < MatchLimits.BoardWidth; radius++)
        {
            for (var deltaX = -radius; deltaX <= radius; deltaX++)
            {
                var x = sourceX + deltaX;
                if (x is < 0 or >= MatchLimits.BoardWidth) continue;
                for (var deltaY = -radius; deltaY <= radius; deltaY++)
                {
                    var y = sourceY + deltaY;
                    if (y is < 0 or >= MatchLimits.BoardWidth) continue;
                    var sectorId = y * MatchLimits.BoardWidth + x;
                    var owner = sectorOwners[sectorId];
                    var added = BaseScore(
                        mode, sectorId, player.Value, owner,
                        canSoloControl, hasPriorChaos, playerOrderValues);
                    if (added > 0)
                    {
                        scores[sectorId] = checked(scores[sectorId] + added);
                        found = true;
                    }
                    if (owner >= 0 && isHostileOwner(owner) && isHumanOwner(owner))
                        scores[sectorId] = checked(scores[sectorId] * 5);
                }
            }
            if (found) break;
        }

        scores[sourceSectorId] = 0;
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            if (sectorDisabled[sectorId]) scores[sectorId] = 0;
            if (family is 0 or 1
                && sectorOwners[sectorId] != player.Value
                && !canSoloControl(sectorId))
                scores[sectorId] = 0;
        }

        var sorted = Enumerable.Range(0, MatchLimits.SectorCount)
            .OrderByDescending(sectorId => scores[sectorId])
            .ToArray();
        var maximum = scores[sorted[0]];
        var routeOneStep = maximum < 1 || !IsNear(sourceSectorId, sorted[0]);
        var tieCount = sorted.TakeWhile(sectorId => scores[sectorId] == maximum).Count();
        var target = tieCount == 1
            ? sorted[0]
            : sorted[random.NextInclusive(tieCount) - 1];
        if (!routeOneStep) return target;

        var result = sourceSectorId;
        var targetX = target % MatchLimits.BoardWidth;
        var targetY = target / MatchLimits.BoardWidth;
        if (sourceX < targetX
            && sectorGangCounts[result + 1] <= MaximumDestinationGangCount)
            result++;
        if (sourceX > targetX
            && sectorGangCounts[result - 1] <= MaximumDestinationGangCount)
            result--;
        if (sourceY < targetY
            && sectorGangCounts[result + MatchLimits.BoardWidth]
                <= MaximumDestinationGangCount)
            result += MatchLimits.BoardWidth;
        if (sourceY > targetY
            && sectorGangCounts[result - MatchLimits.BoardWidth]
                <= MaximumDestinationGangCount)
            result -= MatchLimits.BoardWidth;
        return result;
    }

    internal static bool LiteralPlayerOrderAccepts(
        int player,
        int owner,
        IReadOnlyList<int> playerOrderValues)
    {
        ArgumentNullException.ThrowIfNull(playerOrderValues);
        if (player is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        if (playerOrderValues.Count != MatchLimits.PlayerCount)
            throw new ArgumentException(
                "Player-order values must contain all six original player slots.",
                nameof(playerOrderValues));
        if (owner == NeutralOwner || owner == player) return false;

        var playerIndex = FirstIndexOrEnd(playerOrderValues, player);
        if (playerIndex == 0) return true;
        var ownerIndex = FirstIndexOrEnd(playerOrderValues, owner);
        return ownerIndex < playerIndex;
    }

    private static int BaseScore(
        int mode,
        int sectorId,
        int player,
        int owner,
        Func<int, bool> canSoloControl,
        Func<int, bool> hasPriorChaos,
        IReadOnlyList<int> playerOrderValues) => mode switch
        {
            1 => owner == NeutralOwner && canSoloControl(sectorId) ? 1 : 0,
            2 => owner == player ? 1 : 0,
            3 => owner != player && owner > NeutralOwner ? 1 : 0,
            4 => LiteralPlayerOrderAccepts(player, owner, playerOrderValues) ? 1 : 0,
            5 when owner == NeutralOwner && canSoloControl(sectorId) => 5,
            5 when owner == player && !hasPriorChaos(sectorId) => 2,
            5 when owner != player && owner > NeutralOwner => 1,
            _ => 0
        };

    private static bool IsNear(int sourceSectorId, int targetSectorId) =>
        Math.Abs(sourceSectorId % MatchLimits.BoardWidth
                 - targetSectorId % MatchLimits.BoardWidth) <= 1
        && Math.Abs(sourceSectorId / MatchLimits.BoardWidth
                    - targetSectorId / MatchLimits.BoardWidth) <= 1;

    private static int FirstIndexOrEnd(IReadOnlyList<int> values, int sought)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == sought) return index;
        return values.Count;
    }

    private static void ValidateInputs(
        int mode,
        int sourceSectorId,
        int family,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        Func<int, bool> canSoloControl,
        Func<int, bool> hasPriorChaos,
        Func<int, bool> isHostileOwner,
        Func<int, bool> isHumanOwner,
        IReadOnlyList<int> playerOrderValues,
        DeterministicRandom random)
    {
        if (mode is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(mode));
        if (sourceSectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sourceSectorId));
        if (family != AiPlanningState.UnusedFamily
            && (family is < 0 or > 14 || family == 8))
            throw new ArgumentOutOfRangeException(nameof(family));
        ArgumentNullException.ThrowIfNull(sectorOwners);
        ArgumentNullException.ThrowIfNull(sectorDisabled);
        ArgumentNullException.ThrowIfNull(sectorGangCounts);
        ArgumentNullException.ThrowIfNull(canSoloControl);
        ArgumentNullException.ThrowIfNull(hasPriorChaos);
        ArgumentNullException.ThrowIfNull(isHostileOwner);
        ArgumentNullException.ThrowIfNull(isHumanOwner);
        ArgumentNullException.ThrowIfNull(playerOrderValues);
        ArgumentNullException.ThrowIfNull(random);
        if (sectorOwners.Count != MatchLimits.SectorCount)
            throw new ArgumentException("Sector owners must contain all 64 sectors.", nameof(sectorOwners));
        if (sectorDisabled.Count != MatchLimits.SectorCount)
            throw new ArgumentException("Disabled flags must contain all 64 sectors.", nameof(sectorDisabled));
        if (sectorGangCounts.Count != MatchLimits.SectorCount)
            throw new ArgumentException(
                "Gang counts must contain all 64 sectors.",
                nameof(sectorGangCounts));
        if (sectorGangCounts.Any(count => count is < 0 or > AiPlanningState.GangSlotsPerPlayer))
            throw new ArgumentOutOfRangeException(nameof(sectorGangCounts));
        if (sectorOwners.Any(owner => owner is < MinimumRawOwner or >= MatchLimits.PlayerCount))
            throw new ArgumentOutOfRangeException(nameof(sectorOwners));
        if (playerOrderValues.Count != MatchLimits.PlayerCount)
            throw new ArgumentException(
                "Player-order values must contain all six original player slots.",
                nameof(playerOrderValues));
    }
}
