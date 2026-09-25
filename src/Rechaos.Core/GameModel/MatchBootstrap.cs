using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public sealed record MatchPlayerStart(
    PlayerId Player,
    int HeadquartersSectorId,
    int RightHandsForce,
    int StandardStartingCash,
    IReadOnlyList<short> HirePool);

/// <summary>
/// The starting rosters and city a match is constructed over, before any
/// <see cref="MatchState"/> exists. Callers that still have layout to finish — extra starting
/// gangs, scenario landmarks — work on this, so exactly one match is ever built over these player
/// instances and each of them belongs to that one match.
/// </summary>
internal sealed record MatchFoundation(
    IReadOnlyList<MatchPlayerState> Players,
    IReadOnlyList<MatchSectorState> Sectors);

/// <summary>
/// Builds authoritative match state from an explicit city and placement layout.
/// The recovered original generator and headquarters selector feed this boundary,
/// while explicit layouts remain available for tests and imported scenarios.
/// </summary>
public static class MatchBootstrap
{
    public const short RightHandsDefinitionId = 0;
    public const short HeadquartersDefinitionId = 21;
    public const int ArmageddonStartingCash = 500;

    public static MatchState Create(
        OriginalData definitions,
        MatchSetup setup,
        IReadOnlyList<MatchSectorState> sectors,
        IReadOnlyList<MatchPlayerStart> starts)
    {
        var foundation = Compose(definitions, setup, sectors, starts);
        return new MatchState(definitions, setup, foundation.Players, foundation.Sectors);
    }

    /// <summary>
    /// The starting rosters and city on their own, for the original generation path, which finishes
    /// the layout before the match is constructed rather than reaching into a built one.
    /// </summary>
    internal static MatchFoundation Compose(
        OriginalData definitions,
        MatchSetup setup,
        IReadOnlyList<MatchSectorState> sectors,
        IReadOnlyList<MatchPlayerStart> starts)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(sectors);
        ArgumentNullException.ThrowIfNull(starts);
        if (starts.Count != setup.Players.Count)
            throw new ArgumentException("Every configured player must have one start.", nameof(starts));
        if (!starts.Select(start => start.Player).SequenceEqual(setup.Players.Select(player => player.Id)))
            throw new ArgumentException("Player starts must follow setup player order.", nameof(starts));
        if (starts.Select(start => start.HeadquartersSectorId).Distinct().Count() != starts.Count)
            throw new ArgumentException("Players must have distinct headquarters sectors.", nameof(starts));
        RequireOrderedCity(sectors, nameof(sectors));

        // The bootstrap is transactional with respect to its inputs: ownership is
        // applied to a private state graph, not to the caller's reusable layout.
        var sectorArray = sectors.Select(sector => CloneSector(sector)).ToArray();
        foreach (var start in starts)
        {
            if (start.HeadquartersSectorId is < 0 or >= MatchLimits.SectorCount)
                throw new ArgumentOutOfRangeException(nameof(starts), "A headquarters sector is outside the city.");
            if (start.RightHandsForce is < 1 or > ManualRules.MaximumForce)
                throw new ArgumentOutOfRangeException(nameof(starts), "Right Hands Force must be between 1 and 10.");
            if (start.StandardStartingCash < 0)
                throw new ArgumentOutOfRangeException(nameof(starts), "Starting cash cannot be negative.");
            var sector = sectorArray.SingleOrDefault(candidate => candidate.Id == start.HeadquartersSectorId)
                ?? throw new ArgumentException("A headquarters sector is missing from the city.", nameof(sectors));
            if (sector.Owner is not null)
                throw new ArgumentException("A headquarters sector must be neutral before initialization.", nameof(sectors));
            if (sector.Sites.All(site => site.DefinitionId != HeadquartersDefinitionId))
                throw new ArgumentException("A player's starting sector must contain a Headquarters site.", nameof(sectors));
        }

        // Original initializer 0x0046dc10 copies every ITEMS research-difficulty byte into the
        // 64-by-6 progress table, or zeroes the entire table for Armageddon. Type-99 records are
        // padding rather than technologies, so authoritative recreation state deliberately omits
        // them while preserving the observable result for every real item.
        var startingResearch = definitions.Items
            .Select((item, index) => (item, index))
            .Where(entry => entry.item.Type != 99
                && (setup.Scenario == ScenarioId.Armageddon || entry.item.ResearchDifficulty == 0))
            .Select(entry => checked((short)entry.index))
            .ToHashSet();
        var players = new MatchPlayerState[starts.Count];
        for (var index = 0; index < starts.Count; index++)
        {
            var start = starts[index];
            sectorArray[start.HeadquartersSectorId].Owner = start.Player;
            var rightHands = new MatchGangState(
                new GangId(index), start.Player, RightHandsDefinitionId,
                start.HeadquartersSectorId, start.RightHandsForce);
            players[index] = new MatchPlayerState(
                setup.Players[index],
                OriginalSetupNameRules.ApplyStartingCash(
                    setup.Players[index].Name,
                    setup.Scenario == ScenarioId.Armageddon
                        ? ArmageddonStartingCash
                        : start.StandardStartingCash),
                [rightHands], start.HirePool,
                researchedItems: startingResearch,
                usesMaximumHireForce: OriginalHireCheatRules.DetectMaximumHireForce(
                    setup.Players[index].Name));
        }

        return new MatchFoundation(players, sectorArray);
    }

    /// <summary>Throws unless the city holds all 64 sectors, each at the index of its own id.</summary>
    internal static void RequireOrderedCity(IReadOnlyList<MatchSectorState> sectors, string parameterName)
    {
        if (sectors.Count != MatchLimits.SectorCount
            || !sectors.Select(sector => sector.Id).SequenceEqual(Enumerable.Range(0, MatchLimits.SectorCount)))
            throw new ArgumentException("The city must contain sectors ordered from 0 through 63.", parameterName);
    }

    /// <summary>
    /// A copy of the sector with its own site objects. The owner is kept, and
    /// <paramref name="isImportant"/> replaces the flag when given.
    /// </summary>
    internal static MatchSectorState CloneSector(MatchSectorState sector, bool? isImportant = null) => new(
        sector.Id,
        sector.Sites.Select(site => new MatchSiteState(
            site.Slot, site.DefinitionId, site.Resistance, site.InfluencedBy)).ToArray(),
        sector.Owner, sector.Tolerance, sector.LegacyChaos, sector.CrackdownActive,
        isImportant ?? sector.IsImportant, sector.Income,
        sector.CrackdownTurnsRemaining, sector.CrackdownHistory, sector.BaseTolerance, sector.Support,
        sector.StoredCashYield);
}
