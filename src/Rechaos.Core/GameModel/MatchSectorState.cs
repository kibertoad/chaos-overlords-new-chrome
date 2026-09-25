namespace Rechaos.Core.GameModel;

public sealed class MatchSectorState
{
    private readonly List<int> _crackdownHistory;

    public MatchSectorState(
        int id,
        IReadOnlyList<MatchSiteState> sites,
        PlayerId? owner = null,
        int tolerance = ManualRules.MinimumTolerance,
        int chaos = 0,
        bool crackdownActive = false,
        bool isImportant = false,
        int income = ManualRules.MinimumSectorIncome,
        int crackdownTurnsRemaining = 0,
        IReadOnlyList<int>? crackdownHistory = null,
        int? baseTolerance = null,
        int support = 0,
        int? cashYield = null)
    {
        if (id is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentNullException.ThrowIfNull(sites);
        if (sites.Count != MatchLimits.SitesPerSector)
            throw new ArgumentException($"A sector must contain exactly {MatchLimits.SitesPerSector} sites.", nameof(sites));
        if (sites.Select(site => site.Slot).Order().SequenceEqual(Enumerable.Range(0, MatchLimits.SitesPerSector)) is false)
            throw new ArgumentException("Site slots must be exactly 0, 1, and 2.", nameof(sites));
        if (chaos < 0) throw new ArgumentOutOfRangeException(nameof(chaos));
        if (income < 0) throw new ArgumentOutOfRangeException(nameof(income));
        // FMT-STATE-002 `crackdown_turns` is a signed byte that repeated neutralizing Crackdowns
        // can wrap below 0 (RULE-POLICE-002), so an inactive sector may carry a negative value.
        if (crackdownTurnsRemaining < sbyte.MinValue) throw new ArgumentOutOfRangeException(nameof(crackdownTurnsRemaining));
        if (!crackdownActive && crackdownTurnsRemaining > 0)
            throw new ArgumentException("Inactive police cannot have turns remaining.", nameof(crackdownTurnsRemaining));
        if (crackdownHistory is { Count: > 2 }
            || crackdownHistory?.Any(turn => turn < 1) == true
            || crackdownHistory?.Zip(crackdownHistory.Skip(1), (left, right) => left > right).Any(invalid => invalid) == true)
            throw new ArgumentException("Crackdown history must contain at most two nondecreasing positive turns.", nameof(crackdownHistory));
        Id = id;
        Sites = sites.OrderBy(site => site.Slot).ToArray();
        Owner = owner;
        Tolerance = tolerance;
        BaseTolerance = baseTolerance ?? tolerance;
        Support = support;
        LegacyChaos = chaos;
        // Only a newly activated sector needs the minimum; restores preserve a decayed duration.
        CrackdownTurnsRemaining = crackdownActive
            ? crackdownTurnsRemaining > 0 ? crackdownTurnsRemaining : ManualRules.MinimumCrackdownTurns
            : crackdownTurnsRemaining;
        IsImportant = isImportant;
        Income = income;
        _crackdownHistory = crackdownHistory?.ToList() ?? [];
        _cashYield = cashYield;
    }

    public int Id { get; }
    public IReadOnlyList<MatchSiteState> Sites { get; }
    public PlayerId? Owner { get; internal set; }

    /// <summary>
    /// The Tolerance the Chaos test compares with: the base plus the completed sites' Tolerance,
    /// rebuilt before planning and left alone during resolution (RULE-SITE-001).
    /// </summary>
    public int Tolerance { get; internal set; }

    /// <summary>
    /// The Tolerance without the sites' part, which Bribe, Snitch, the return toward normal and
    /// the clamp change during resolution (RULE-TOLERANCE-001, RULE-TOLERANCE-002). FMT-STATE-002
    /// keeps it as a signed byte.
    /// </summary>
    public int BaseTolerance { get; internal set; }

    /// <summary>
    /// The Support of the sector's completed sites, rebuilt before planning with the Tolerance and
    /// read by the Control pass (FMT-STATE-002, RULE-CONTROL-001). A site lost during resolution
    /// still counts until the next rebuild.
    /// </summary>
    public int Support { get; internal set; }

    /// <summary>
    /// The Cash the sector pays its owner at Upkeep: 1 plus the completed sites' Cash, rebuilt
    /// before planning and left alone during resolution (FMT-STATE-002 `cash_yield`,
    /// RULE-SITE-001, RULE-UPKEEP-001). A site reset by a takeover still counts until the next
    /// rebuild, so the new owner collects its Cash once. A match fills in the value of a sector
    /// built without one from its sites when the match is constructed.
    /// </summary>
    public int CashYield
    {
        get => _cashYield
            ?? throw new InvalidOperationException("The sector's Cash yield has not been rebuilt.");
        internal set => _cashYield = value;
    }

    /// <summary>The Cash yield given at construction, or null when the match derives it.</summary>
    internal int? StoredCashYield => _cashYield;
    private int? _cashYield;
    // Retained only so pre-v24 saves/replays can verify their historical fingerprints.
    // The original executable has no persistent sector-Chaos accumulator.
    internal int LegacyChaos { get; }
    /// <summary>
    /// Police are in the sector: `crackdown_turns` is above 0, the test the police phase, the
    /// turn-start end of a recurring Control and the order menus make (RULE-POLICE-001,
    /// RULE-TURN-004, FND-UI-021). A value the signed byte wrapped below 0 is not police.
    /// </summary>
    public bool CrackdownActive
    {
        get => CrackdownTurnsRemaining > 0;
        internal set => CrackdownTurnsRemaining = value
            ? Math.Max(ManualRules.MinimumCrackdownTurns, CrackdownTurnsRemaining)
            : 0;
    }
    /// <summary>
    /// `crackdown_turns` is not 0, the test the Control pass and the computer players make
    /// (RULE-CONTROL-001, RULE-AI-004, RULE-AI-006, RULE-AI-011, RULE-AI-013). It differs from
    /// <see cref="CrackdownActive"/> only after the signed byte wrapped below 0.
    /// </summary>
    public bool HasCrackdownTurns => CrackdownTurnsRemaining != 0;

    /// <summary>
    /// FMT-STATE-002 `crackdown_turns`: police Combat phases left, held in the signed byte's
    /// range from -128 to 127 once a Crackdown has added to it (RULE-POLICE-002).
    /// </summary>
    public int CrackdownTurnsRemaining { get; internal set; }
    public IReadOnlyList<int> CrackdownHistory => _crackdownHistory;
    public bool IsImportant { get; }
    public int Income { get; }

    internal bool RecordCrackdown(int turn)
    {
        if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
        if (_crackdownHistory.Count > 0 && turn <= _crackdownHistory[^1])
            throw new InvalidOperationException("A sector can record at most one Crackdown per turn.");
        _crackdownHistory.RemoveAll(previous => previous < turn - 5);
        var losesControl = _crackdownHistory.Count >= 2;
        if (losesControl)
        {
            _crackdownHistory.Clear();
            _crackdownHistory.Add(turn);
        }
        _crackdownHistory.Add(turn);
        return losesControl;
    }
}

public sealed class MatchSiteState
{
    public MatchSiteState(int slot, short definitionId, int resistance, PlayerId? influencedBy = null)
    {
        if (slot is < 0 or >= MatchLimits.SitesPerSector) throw new ArgumentOutOfRangeException(nameof(slot));
        if (resistance < 0) throw new ArgumentOutOfRangeException(nameof(resistance));
        Slot = slot;
        DefinitionId = definitionId;
        Resistance = resistance;
        InfluencedBy = influencedBy;
    }

    public int Slot { get; }
    public short DefinitionId { get; }
    public int Resistance { get; internal set; }
    public PlayerId? InfluencedBy { get; internal set; }
}
