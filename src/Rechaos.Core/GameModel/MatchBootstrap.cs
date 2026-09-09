using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public sealed record MatchPlayerStart(
    PlayerId Player,
    int HeadquartersSectorId,
    int RightHandsForce,
    int StandardStartingCash,
    IReadOnlyList<short> HirePool);

/// <summary>
/// Builds authoritative match state from an explicit city and placement layout.
/// City generation and headquarters selection remain separate parity tasks.
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
        if (sectors.Count != MatchLimits.SectorCount
            || !sectors.Select(sector => sector.Id).SequenceEqual(Enumerable.Range(0, MatchLimits.SectorCount)))
            throw new ArgumentException("The city must contain sectors ordered from 0 through 63.", nameof(sectors));

        // The bootstrap is transactional with respect to its inputs: ownership is
        // applied to a private state graph, not to the caller's reusable layout.
        var sectorArray = sectors.Select(CloneSector).ToArray();
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

        var allResearch = setup.Scenario == ScenarioId.Armageddon
            ? definitions.Items
                .Select((item, index) => (item, index))
                .Where(entry => entry.item.Type != 99)
                .Select(entry => checked((short)entry.index))
                .ToHashSet()
            : null;
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
                setup.Scenario == ScenarioId.Armageddon
                    ? ArmageddonStartingCash
                    : start.StandardStartingCash,
                [rightHands], start.HirePool,
                researchedItems: allResearch);
        }

        return new MatchState(definitions, setup, players, sectorArray);
    }

    private static MatchSectorState CloneSector(MatchSectorState sector) => new(
        sector.Id,
        sector.Sites.Select(site => new MatchSiteState(
            site.Slot, site.DefinitionId, site.Resistance, site.InfluencedBy)).ToArray(),
        sector.Owner, sector.Tolerance, sector.Chaos, sector.CrackdownActive, sector.IsImportant, sector.Income);
}
