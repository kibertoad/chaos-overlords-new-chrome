namespace Rechaos.Core.GameModel;

/// <summary>
/// Pure implementation of the original weighted sector selector at 0x00408642
/// for its fully recovered modes 1 through 10, 12 through 16,
/// and encoded fixed-sector modes 0x40 through 0x7f, and of its mode 0 random
/// neighbour (RULE-AI-007). Planner-specific queries remain explicit inputs so
/// this kernel does not guess at unrecovered outer policy.
/// </summary>
internal static class OriginalAiSectorSelectionRules
{
    /// <summary>FND-AI-068: selector 0x9A's value past the end of the weight-10 list.</summary>
    public const int GuardTargetEndMarker = 100;
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
        DeterministicRandom random,
        bool? hasHumanPlayers = null,
        int? formationSectorId = null,
        IReadOnlyList<int>? scenarioStandings = null,
        Func<int, int>? unfinishedSiteScore = null,
        Func<int, bool>? hasPriorInfluence = null,
        Func<int, int>? completedSiteScore = null,
        Func<int, int>? ownerQuery = null,
        AiPlanningState? planning = null)
    {
        ValidateInputs(
            mode, sourceSectorId, family, sectorOwners, sectorDisabled,
            sectorGangCounts, canSoloControl, hasPriorChaos,
            isHostileOwner, isHumanOwner, random,
            hasHumanPlayers, formationSectorId, scenarioStandings, unfinishedSiteScore,
            hasPriorInfluence, completedSiteScore, ownerQuery);
        if (mode == 0) return RandomNeighbour(sourceSectorId, random);

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
                    // RULE-AI-006, FND-AI-056: mode 4 scores the owner query (RULE-AI-004), which
                    // gives -2 under police presence, rather than the owner byte.
                    var scoredOwner = mode == 4 ? ownerQuery!(sectorId) : owner;
                    var added = BaseScore(
                        mode, sectorId, player.Value, scoredOwner,
                        sectorGangCounts, canSoloControl, hasPriorChaos,
                        isHostileOwner, isHumanOwner,
                        hasHumanPlayers, formationSectorId, scenarioStandings, unfinishedSiteScore,
                        hasPriorInfluence, completedSiteScore);
                    if (added > 0)
                    {
                        scores[sectorId] = checked(scores[sectorId] + added);
                        found = true;
                    }
                    // FND-AI-067: an encoded mode adds 1 to its own sector's score for every cell
                    // the ring visits, so the search stops at radius 1 with that score at the
                    // number of cells on the board around the gang.
                    if (mode >= 0x40)
                    {
                        var encoded = EncodedSector(mode - 0x40);
                        scores[encoded] = checked(scores[encoded] + 1);
                        found = true;
                    }
                    if (owner >= 0 && isHostileOwner(owner) && isHumanOwner(owner))
                        scores[sectorId] = checked(scores[sectorId] * 5);
                }
            }
            if (found) break;
        }

        scores[sourceSectorId] = 0;
        return Choose(
            scores, sourceSectorId, player, family, sectorOwners, sectorDisabled,
            sectorGangCounts, canSoloControl, random, planning ?? AiPlanningState.Initialize());
    }

    /// <summary>
    /// RULE-AI-006, FND-AI-066: the selector's second half. The score of each sector is copied into
    /// the persistent pair list, except where the late filter of families 0 and 1 zeroes it, which
    /// leaves that pair with the score an earlier call sorted into it. The pairs are then numbered
    /// 0 to 63 and exchange-sorted by score, highest first. The ties with the first pair are counted
    /// with no bound, so when every pair ties the count runs on through the score table and the
    /// planning records that follow the list in memory, and the pick reads its sector from there.
    /// </summary>
    private static int Choose(
        int[] scores,
        int sourceSectorId,
        PlayerId player,
        int family,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        Func<int, bool> canSoloControl,
        DeterministicRandom random,
        AiPlanningState planning)
    {
        var pairScores = planning.SectorChoiceScores;
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            if (sectorDisabled[sectorId]) scores[sectorId] = 0;
            // The native late filter reads the acting planning record's family byte
            // at +0 and applies selector 0x2c only to literal families 0 and 1.
            // Family 11's mode-10 anchors and mode-16 followers deliberately bypass it.
            if (family is 0 or 1
                && sectorOwners[sectorId] != player.Value
                && !canSoloControl(sectorId))
                scores[sectorId] = 0;
            else
                pairScores[sectorId] = scores[sectorId];
        }

        var pairSectors = new int[MatchLimits.SectorCount];
        for (var index = 0; index < pairSectors.Length; index++) pairSectors[index] = index;
        for (var first = 0; first < MatchLimits.SectorCount; first++)
        for (var second = first; second < MatchLimits.SectorCount; second++)
        {
            if (pairScores[first] >= pairScores[second]) continue;
            (pairScores[first], pairScores[second]) = (pairScores[second], pairScores[first]);
            (pairSectors[first], pairSectors[second]) = (pairSectors[second], pairSectors[first]);
        }

        var memory = new PairMemory(pairScores, pairSectors, scores, planning.PlanningRecordImage());
        var maximum = pairScores[0];
        var sourceX = sourceSectorId % MatchLimits.BoardWidth;
        var sourceY = sourceSectorId / MatchLimits.BoardWidth;
        var best = pairSectors[0];
        if (maximum > 0
            && Math.Abs(best % MatchLimits.BoardWidth - sourceX) <= 1
            && Math.Abs(best / MatchLimits.BoardWidth - sourceY) <= 1)
            return PickTied(memory, maximum, best, random);

        var target = PickTied(memory, maximum, best, random);
        var targetX = target % MatchLimits.BoardWidth;
        var targetY = target / MatchLimits.BoardWidth;
        var result = sourceSectorId;
        if (sourceX < targetX && GangCount(sectorGangCounts, ++result) > MaximumDestinationGangCount) result--;
        if (sourceX > targetX && GangCount(sectorGangCounts, --result) > MaximumDestinationGangCount) result++;
        if (sourceY < targetY
            && GangCount(sectorGangCounts, result += MatchLimits.BoardWidth) > MaximumDestinationGangCount)
            result -= MatchLimits.BoardWidth;
        if (sourceY > targetY
            && GangCount(sectorGangCounts, result -= MatchLimits.BoardWidth) > MaximumDestinationGangCount)
            result += MatchLimits.BoardWidth;
        return result;
    }

    private static int PickTied(PairMemory memory, int maximum, int best, DeterministicRandom random)
    {
        var tieCount = 1;
        while (memory.Score(tieCount) == maximum) tieCount++;
        if (tieCount == 1) return best;
        // FND-AI-066: the sector field of pair roll(count) - 1.
        return memory.Sector(random.NextInclusive(tieCount) - 1)
            ?? throw new InvalidOperationException("The sector selector picked a pair past the modelled memory.");
    }

    // DEV-AI-005: a step off the board reads past the player's row of the count list and, when
    // that memory holds 5 or less, leaves the city in the original. The rebuild takes such a step
    // as blocked, so the result stays on the board. A step on the board reads the player's row.
    private static int GangCount(IReadOnlyList<int> sectorGangCounts, int sectorId) =>
        sectorId is >= 0 and < MatchLimits.SectorCount
            ? sectorGangCounts[sectorId]
            : int.MaxValue;

    /// <summary>
    /// FND-AI-066, FND-STATE-007: the memory the pair list at 0x00489F50 runs into. Pair index 64
    /// onward reads the 8 by 8 score table at 0x0048A150, whose dword x * 8 + y holds the score of
    /// sector y * 8 + x, and index 96 onward reads the planning records at 0x0048A250. The records
    /// end at 0x0048C0B0; the memory past them is not modelled, and a count that reaches it stops.
    /// </summary>
    private sealed class PairMemory(int[] pairScores, int[] pairSectors, int[] table, byte[] records)
    {
        private const int TablePairs = MatchLimits.SectorCount / 2;

        public int? Score(int pair) => Dword(pair, 0);

        public int? Sector(int pair) => Dword(pair, 1);

        private int? Dword(int pair, int field)
        {
            if (pair < MatchLimits.SectorCount)
                return field == 0 ? pairScores[pair] : pairSectors[pair];
            pair -= MatchLimits.SectorCount;
            if (pair < TablePairs)
            {
                var dword = pair * 2 + field;
                return table[dword % MatchLimits.BoardWidth * MatchLimits.BoardWidth + dword / MatchLimits.BoardWidth];
            }
            var offset = (pair - TablePairs) * 8 + field * 4;
            return offset + 4 <= records.Length ? BitConverter.ToInt32(records, offset) : null;
        }
    }

    private static readonly int[] NeighbourOffsets = [-9, -8, -7, -1, 1, 7, 8, 9];

    /// <summary>
    /// RULE-AI-007, FND-MOVE-003: mode 0 builds no score map. It draws one of the eight offsets with
    /// roll(8) and draws again while the neighbour would leave the city or wrap a row. The owner
    /// test of the original rejects only an owner byte below -1, which no sector holds, and no
    /// capacity test is made.
    /// </summary>
    internal static int RandomNeighbour(int sourceSectorId, DeterministicRandom random)
    {
        if (sourceSectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sourceSectorId));
        ArgumentNullException.ThrowIfNull(random);
        var column = sourceSectorId % MatchLimits.BoardWidth;
        while (true)
        {
            var candidate = sourceSectorId + NeighbourOffsets[random.NextInclusive(NeighbourOffsets.Length) - 1];
            if (candidate is >= 0 and < MatchLimits.SectorCount
                && Math.Abs(candidate % MatchLimits.BoardWidth - column) <= 1)
                return candidate;
        }
    }

    /// <summary>
    /// RULE-AI-006, FND-AI-056: mode 4's test (selector 0x2D). It searches the six standings bytes
    /// (FND-STATE-004) for the player's and the owner's slot numbers as values, not as indices, and
    /// accepts the owner when the player's value is found first; a value that is absent is found
    /// at the end.
    /// </summary>
    internal static bool StandingsAccept(
        int player,
        int owner,
        IReadOnlyList<int> scenarioStandings)
    {
        ArgumentNullException.ThrowIfNull(scenarioStandings);
        if (player is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        if (scenarioStandings.Count != MatchLimits.PlayerCount)
            throw new ArgumentException(
                "Scenario standings must contain all six original player slots.",
                nameof(scenarioStandings));
        if (owner == NeutralOwner || owner == player) return false;

        var playerIndex = FirstIndexOrEnd(scenarioStandings, player);
        if (playerIndex == 0) return true;
        var ownerIndex = FirstIndexOrEnd(scenarioStandings, owner);
        return ownerIndex < playerIndex;
    }

    private static int BaseScore(
        int mode,
        int sectorId,
        int player,
        int owner,
        IReadOnlyList<int> sectorGangCounts,
        Func<int, bool> canSoloControl,
        Func<int, bool> hasPriorChaos,
        Func<int, bool> isHostileOwner,
        Func<int, bool> isHumanOwner,
        bool? hasHumanPlayers,
        int? formationSectorId,
        IReadOnlyList<int>? scenarioStandings,
        Func<int, int>? unfinishedSiteScore,
        Func<int, bool>? hasPriorInfluence,
        Func<int, int>? completedSiteScore) => mode switch
        {
            1 => owner == NeutralOwner && canSoloControl(sectorId) ? 1 : 0,
            2 => owner == player ? 1 : 0,
            3 => owner != player && owner > NeutralOwner ? 1 : 0,
            4 => StandingsAccept(player, owner, scenarioStandings!) ? 1 : 0,
            5 when owner == NeutralOwner && canSoloControl(sectorId) => 5,
            5 when owner == player && !hasPriorChaos(sectorId) => 2,
            5 when owner != player && owner > NeutralOwner => 1,
            6 => ModeSixBaseScore(
                sectorId, player, owner, sectorGangCounts,
                isHostileOwner, isHumanOwner,
                hasHumanPlayers!.Value, scenarioStandings!),
            7 when owner == player && !hasPriorInfluence!(sectorId) =>
                Math.Max(0, unfinishedSiteScore!(sectorId)),
            8 when owner == player => Math.Max(0, unfinishedSiteScore!(sectorId)),
            9 when owner == player => Math.Max(0, completedSiteScore!(sectorId)),
            10 when hasHumanPlayers == true && owner >= 0 && isHumanOwner(owner) => 1,
            10 when hasHumanPlayers == false && owner >= 0 && owner != player => 1,
            12 when IsBigManObjective(sectorId)
                && owner != player
                && sectorGangCounts[sectorId] < MatchLimits.FriendlyGangsPerSector =>
                ObjectiveModeBaseScore(owner, isHostileOwner, isHumanOwner),
            13 when IsEliminateObjective(sectorId)
                && owner != player
                && sectorGangCounts[sectorId] < MatchLimits.FriendlyGangsPerSector =>
                ObjectiveModeBaseScore(owner, isHostileOwner, isHumanOwner),
            14 when IsBigManObjective(sectorId)
                && sectorGangCounts[sectorId] < MatchLimits.FriendlyGangsPerSector =>
                ObjectiveModeBaseScore(owner, isHostileOwner, isHumanOwner),
            15 when IsEliminateObjective(sectorId)
                && sectorGangCounts[sectorId] < MatchLimits.FriendlyGangsPerSector =>
                ObjectiveModeBaseScore(owner, isHostileOwner, isHumanOwner),
            16 when sectorId == formationSectorId => 1,
            _ => 0
        };

    /// <summary>
    /// FND-AI-067: the sector whose score an encoded mode raises. The selector adds to the score
    /// table at (t % 8) * 8 + t / 8, which is sector t for 0 to 63; the guard end marker t = 100
    /// lands on the table entry of sector 37.
    /// </summary>
    internal static int EncodedSector(int encoded)
    {
        var tableIndex = encoded % MatchLimits.BoardWidth * MatchLimits.BoardWidth + encoded / MatchLimits.BoardWidth;
        return tableIndex % MatchLimits.BoardWidth * MatchLimits.BoardWidth + tableIndex / MatchLimits.BoardWidth;
    }

    internal static int ObjectiveModeBaseScore(
        int owner,
        Func<int, bool> isHostileOwner,
        Func<int, bool> isHumanOwner) => owner >= 0
            && isHostileOwner(owner)
            && isHumanOwner(owner)
                // The common post-switch multiplier applies again, preserving
                // the original objective mode's effective 25-point weight.
                ? 5
                : 1;

    internal static int ModeSixBaseScore(
        int sectorId,
        int player,
        int owner,
        IReadOnlyList<int> sectorGangCounts,
        Func<int, bool> isHostileOwner,
        Func<int, bool> isHumanOwner,
        bool hasHumanPlayers,
        IReadOnlyList<int> scenarioStandings)
    {
        if (owner < 0) return 0;

        var score = hasHumanPlayers
            && owner != player
            && isHumanOwner(owner)
            && isHostileOwner(owner)
                ? 2
                : 0;
        var leaders = Enumerable.Range(0, MatchLimits.PlayerCount)
            .Where(candidate => scenarioStandings[candidate] == 0)
            .ToArray();
        if (leaders.Length != 1)
            return score + (scenarioStandings[owner] == 0 ? 1 : 0);

        var leader = leaders[0];
        if (leader != player) return score + (owner == leader ? 1 : 0);
        return score + (owner != player
            && sectorGangCounts[sectorId] < 4 ? 1 : 0);
    }

    private static bool IsBigManObjective(int sectorId) =>
        sectorId is 27 or 28 or 35 or 36;

    private static bool IsEliminateObjective(int sectorId) =>
        OriginalCityGenerator.HeadquartersCandidates.Contains(sectorId);

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
        DeterministicRandom random,
        bool? hasHumanPlayers,
        int? formationSectorId,
        IReadOnlyList<int>? scenarioStandings,
        Func<int, int>? unfinishedSiteScore,
        Func<int, bool>? hasPriorInfluence,
        Func<int, int>? completedSiteScore,
        Func<int, int>? ownerQuery)
    {
        if (mode is not (>= 0 and <= 10 or >= 12 and <= 16
                or >= 0x40 and < 0x80 or 0x40 + GuardTargetEndMarker))
            throw new ArgumentOutOfRangeException(nameof(mode));
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
        if (mode is 6 or 10 && hasHumanPlayers is null)
            throw new ArgumentNullException(nameof(hasHumanPlayers));
        if (mode is 4 or 6 && scenarioStandings is null)
            throw new ArgumentNullException(nameof(scenarioStandings));
        if (mode == 4 && ownerQuery is null)
            throw new ArgumentNullException(nameof(ownerQuery));
        if (scenarioStandings is not null
            && (scenarioStandings.Count != MatchLimits.PlayerCount
                || scenarioStandings.Any(standing =>
                    standing is < 0 or > MatchLimits.PlayerCount - 1
                    && standing != byte.MaxValue)))
            throw new ArgumentOutOfRangeException(nameof(scenarioStandings));
        if (mode == 16
            && formationSectorId is not (>= AiPlanningState.InactiveFormationSector
                and < MatchLimits.SectorCount))
            throw new ArgumentOutOfRangeException(nameof(formationSectorId));
        if (mode is 7 or 8 && unfinishedSiteScore is null)
            throw new ArgumentNullException(nameof(unfinishedSiteScore));
        if (mode == 7 && hasPriorInfluence is null)
            throw new ArgumentNullException(nameof(hasPriorInfluence));
        if (mode == 9 && completedSiteScore is null)
            throw new ArgumentNullException(nameof(completedSiteScore));
    }
}
