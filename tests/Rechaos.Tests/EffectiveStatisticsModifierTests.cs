using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class EffectiveStatisticsModifierTests
{
    [Fact]
    public void ModifiersNameTheEquipmentAndTheLocallyInfluencedSites()
    {
        var data = BundledOriginalData.Load();
        var weapon = (short)1;
        var match = CreateMatch(data, weapon, influencedSiteSlots: [0, 2]);
        var gang = match.FindGang(new GangId(10))!;

        var modifiers = EffectiveStatisticsCalculator.ModifiersForGang(match, gang);

        Assert.Equal(
        [
            (GangModifierSource.Weapon, data.Items[weapon].Name),
            (GangModifierSource.Site, SiteName(data, match, 0)),
            (GangModifierSource.Site, SiteName(data, match, 2))
        ], modifiers.Select(modifier => (modifier.Source, modifier.Name)));
    }

    [Fact]
    public void ModifiersSumToTheEffectiveStatisticsUsedByResolution()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: 1, influencedSiteSlots: [0, 1, 2]);
        var gang = match.FindGang(new GangId(10))!;
        var definition = data.Gangs.Single(value => value.Id == gang.DefinitionId);

        var summed = EffectiveStatisticsCalculator.ModifiersForGang(match, gang)
            .Aggregate(EffectiveStatistics.From(definition.Stats),
                (result, modifier) => result.Add(modifier.Stats));

        Assert.Equal(EffectiveStatisticsCalculator.ForGang(match, gang), summed);
    }

    [Fact]
    public void SitesInfluencedByOtherPlayersDoNotModifyTheGang()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: null,
            influencedSiteSlots: [0], influencedBy: new PlayerId(1));
        var gang = match.FindGang(new GangId(10))!;

        Assert.Empty(EffectiveStatisticsCalculator.ModifiersForGang(match, gang));
    }

    private static string SiteName(OriginalData data, MatchState match, int slot) =>
        data.Sites.Single(site => site.Id == match.Sectors[0].Sites[slot].DefinitionId).Name;

    private static MatchState CreateMatch(
        OriginalData data,
        short? weaponItemId,
        IReadOnlyCollection<int> influencedSiteSlots,
        PlayerId? influencedBy = null)
    {
        var owner = new PlayerId(0);
        MatchPlayerSetup[] setups =
        [
            new(owner, "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500,
                [new MatchGangState(new GangId(10), owner, data.Gangs[0].Id, 0, 10, weaponItemId)]),
            new(setups[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), data.Gangs[0].Id, 3, 9)])
        ];
        var influencer = influencedBy ?? owner;
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id, Enumerable.Range(0, MatchLimits.SitesPerSector)
                .Select(slot => id == 0 && influencedSiteSlots.Contains(slot)
                    // An influenced site has been worn down to no remaining resistance.
                    ? new MatchSiteState(slot, (short)slot, 0, influencer)
                    : new MatchSiteState(slot, (short)slot, 7))
                .ToArray(),
                // An influenced site only exists in a sector its influencer controls.
                id == 0 && influencedSiteSlots.Count > 0 ? influencer : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
