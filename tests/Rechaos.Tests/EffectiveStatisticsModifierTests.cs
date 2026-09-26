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
        ], modifiers
            .Where(modifier => modifier.Source != GangModifierSource.WeaponSkills)
            .Select(modifier => (modifier.Source, modifier.Name)));
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

    // RULE-GANG-001: statistics are stored at the rebuild before planning, so an item bought during
    // the turn changes nothing until the next rebuild.
    [Fact]
    public void StoredStatisticsChangeOnlyAtTheRebuildBeforePlanning()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: null, influencedSiteSlots: []);
        var gang = match.FindGang(new GangId(10))!;
        var before = EffectiveStatisticsCalculator.ForGang(match, gang);
        var armor = data.Items.Select((item, index) => (item, index))
            .First(entry => entry.item.Type == 3 && entry.item.Stats.Defense > 0).index;

        gang.ArmorItemId = checked((short)armor);

        Assert.Equal(before, EffectiveStatisticsCalculator.ForGang(match, gang));
        match.FinishUpkeep();
        Assert.Equal(before.Defense + data.Items[armor].Stats.Defense,
            EffectiveStatisticsCalculator.ForGang(match, gang).Defense);
    }

    // RULE-COMBAT-001: an unarmed gang's stored Combat holds its Strength, Fighting and Martial Arts.
    [Fact]
    public void AnUnarmedGangsStoredCombatHoldsItsSkills()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: null, influencedSiteSlots: []);
        var gang = match.FindGang(new GangId(10))!;
        var stats = data.Gangs.Single(value => value.Id == gang.DefinitionId).Stats;

        Assert.Equal(stats.Combat + stats.Strength + stats.Fighting + stats.MartialArts,
            EffectiveStatisticsCalculator.ForGang(match, gang).Combat);
    }

    // RULE-GANG-001: the completed sites of a sector another player owns add nothing.
    [Fact]
    public void CompletedSitesOfASectorAnotherPlayerOwnsDoNotModifyTheGang()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: null,
            influencedSiteSlots: [0], influencedBy: new PlayerId(1));
        var gang = match.FindGang(new GangId(10))!;

        Assert.DoesNotContain(EffectiveStatisticsCalculator.ModifiersForGang(match, gang),
            modifier => modifier.Source == GangModifierSource.Site);
    }

    // RULE-GANG-001: in a sector its player owns, a gang takes every completed site, whoever
    // completed it.
    [Fact]
    public void CompletedSitesOfAnOwnedSectorModifyTheGangWhoeverCompletedThem()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: null,
            influencedSiteSlots: [0], influencedBy: new PlayerId(1));
        // Player 0 takes the sector with the site player 1 completed.
        match.Sectors[0].Owner = new PlayerId(0);
        var gang = match.FindGang(new GangId(10))!;

        Assert.Equal([(GangModifierSource.Site, SiteName(data, match, 0))],
            EffectiveStatisticsCalculator.ModifiersForGang(match, gang)
                .Where(modifier => modifier.Source == GangModifierSource.Site)
                .Select(modifier => (modifier.Source, modifier.Name)));
    }

    // RULE-GANG-001: a generated match, which is built through a synthetic restore, stores every
    // gang's values before the first planning phase, as a bootstrapped one does.
    [Fact]
    public void AGeneratedMatchStoresEveryGangsStatistics()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var match = OriginalMatchFactory.Create(
            data, new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups));

        Assert.All(match.Players.SelectMany(player => player.Gangs), gang =>
            Assert.Equal(EffectiveStatisticsCalculator.Rebuilt(match, gang), gang.StoredStatistics));
    }

    // RULE-GANG-001: every gang joins a match with stored values, so a gang without them is refused
    // rather than read live.
    [Fact]
    public void AGangWithoutStoredStatisticsIsRefused()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, weaponItemId: null, influencedSiteSlots: []);
        var detached = new MatchGangState(new GangId(99), new PlayerId(0), data.Gangs[0].Id, 0, 5);

        Assert.Throws<InvalidOperationException>(
            () => EffectiveStatisticsCalculator.ForGang(match, detached));
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
