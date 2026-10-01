namespace Rechaos.Core.GameModel;

/// <summary>
/// The fields of one of the original's gang records (FMT-STATE-001) that its strength test reads,
/// as the record stands for any roster slot, including a gone gang's or one never used. BUG-AI-007:
/// five handlers pass their sector number where the test expects a roster slot, so the test can
/// read such a record.
/// </summary>
internal readonly record struct OriginalGangRecord(int Sector, int Force, int Combat, int Defense)
{
    private const int RecordsPerPlayer = 81; // FMT-STATE-001
    /// <summary>The sector byte of a record that holds no living gang (FMT-STATE-001).</summary>
    public const int InactiveSector = 100;

    // The original keeps 81 records per player; one never used is all zero but for its sector.
    private static readonly OriginalGangRecord Unused = new(InactiveSector, 0, 0, 0);

    // BUG-AI-007: a lookup that finds no gang returns -1, and the test then reads the 32 bytes
    // before the first gang record, which every recorded run held at zero (EXP-TURN-022).
    private static readonly OriginalGangRecord BeforeFirst = new(0, 0, 0, 0);

    /// <summary>
    /// Selector <c>0x5A</c> and the attacker side of selector <c>0x2B</c>: roster slot
    /// <paramref name="slot"/> of <paramref name="player"/>.
    /// </summary>
    public static OriginalGangRecord At(MatchState state, PlayerId player, int slot)
    {
        ArgumentNullException.ThrowIfNull(state);
        var gangs = state.FindPlayer(player)?.Gangs
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        if ((uint)slot >= RecordsPerPlayer)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return slot < gangs.Count ? Of(state, gangs[slot]) : Unused;
    }

    /// <summary>
    /// Selector <c>0x91</c>: the <paramref name="ordinal"/>-th record, by player and then roster
    /// slot, of a player other than <paramref name="observer"/> whose sector byte is
    /// <paramref name="sector"/> and whose <c>visible_to</c> byte for the observer is set.
    /// </summary>
    public static OriginalGangRecord VisibleAt(MatchState state, PlayerId observer, int sector, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(state);
        var count = 0;
        foreach (var player in state.Players.OrderBy(player => player.Id.Value))
        {
            if (player.Id == observer) continue;
            foreach (var gang in player.Gangs)
            {
                var record = Of(state, gang);
                if (record.Sector != sector || !VisibleTo(state, observer, gang)) continue;
                if (++count == ordinal) return record;
            }
        }
        return BeforeFirst;
    }

    private static OriginalGangRecord Of(MatchState state, MatchGangState gang)
    {
        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        return gang.IsActive
            ? new OriginalGangRecord(gang.SectorId, gang.Force, statistics.Combat, statistics.Defense)
            : new OriginalGangRecord(InactiveSector, gang.RetiredForce ?? 0, statistics.Combat, statistics.Defense);
    }

    private static bool VisibleTo(MatchState state, PlayerId observer, MatchGangState gang) =>
        gang.IsActive
            ? state.CanPlayerDetectGang(observer, gang.Id)
            : (gang.VisibilityMask & (1 << observer.Value)) != 0;
}
