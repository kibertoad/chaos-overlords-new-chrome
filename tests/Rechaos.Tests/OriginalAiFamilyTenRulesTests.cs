using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyTenRulesTests
{
    // RULE-AI-005, FND-AI-055: selector 0x72 keeps the first researched armor within the gang's
    // Tech Level with the most Stealth, starting from item 1. For a Tech 7 gang that is item 28,
    // Stealth 3; item 36 has more Stealth but needs Tech 9.
    [Fact]
    public void ArmorSelectorUsesStrictStealthMaximumWithinRawTech()
    {
        var data = BundledOriginalData.Load();
        short[] armor = data.Items
            .Select((item, index) => (item, index))
            .Where(entry => entry.item.Type == 3)
            .Select(entry => checked((short)entry.index))
            .ToArray();
        var match = CreateMatch(data, researchedItems: armor);
        var player = match.Players[0];
        var gang = player.Gangs[0];

        Assert.Equal(28, OriginalAiFamilyTenRules.SelectArmorUpgrade(match, player, gang));

        // Without item 28 researched no armor within Tech 7 beats item 1's Stealth 0.
        var unresearched = CreateMatch(data, researchedItems: armor.Where(id => id != 28));
        Assert.Null(OriginalAiFamilyTenRules.SelectArmorUpgrade(
            unresearched, unresearched.Players[0], unresearched.Players[0].Gangs[0]));
    }

    [Fact]
    public void ArmorSelectorReturnsNoAlreadyEquippedMaximum()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data,
            researchedItems: Enumerable.Range(24, 10).Select(value => (short)value),
            armorItemId: 28);

        Assert.Null(OriginalAiFamilyTenRules.SelectArmorUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0]));
    }

    [Theory]
    [InlineData(0, 10, 10, true)]
    [InlineData(1, 10, 10, false)]
    [InlineData(0, 11, 10, false)]
    public void ArmorOpportunityUsesCooldownAndInclusiveCashBoundary(
        int cooldown,
        int itemCost,
        int cash,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTenRules.CanEquipArmor(cooldown, itemCost, cash));

    [Fact]
    public void SmokeBombOpportunityRequiresResearchAndEmptyMiscellaneousSlot()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researchedItems: [44]);
        var player = match.Players[0];
        var gang = player.Gangs[0];

        Assert.True(OriginalAiFamilyTenRules.ShouldEquipSmokeBombs(player, gang));

        gang.MiscellaneousItemId = 38;
        Assert.False(OriginalAiFamilyTenRules.ShouldEquipSmokeBombs(player, gang));
    }

    [Theory]
    [InlineData(9, -3, false, true)]
    [InlineData(10, -3, false, false)]
    [InlineData(9, -4, false, false)]
    [InlineData(9, -3, true, false)]
    public void HealGateUsesForceHealAndVisibilityBoundaries(
        int force,
        int effectiveHeal,
        bool hasVisibleOpponent,
        bool expected) =>
        Assert.Equal(expected, OriginalAiFamilyTenRules.ShouldHeal(
            force, effectiveHeal, hasVisibleOpponent));

    [Theory]
    [InlineData(0, GangAction.Chaos)]
    [InlineData(1, GangAction.Hide)]
    [InlineData(2, GangAction.Hide)]
    public void StationaryActionAvoidsAnotherPriorChaos(
        int priorChaosCount,
        GangAction expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTenRules.SelectStationaryAction(priorChaosCount));

    [Fact]
    public void CompletedStealthScoreIgnoresUnfinishedSites()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researchedItems: [], completedStealthSites: true);

        Assert.Equal(2,
            OriginalAiFamilyTenRules.CompletedStealthScore(match, 0));
    }

    // RULE-AI-028, FND-AI-071: selector 8 replaces its value with the Stealth of each finished
    // site in slot order, so it gives the last finished site's Stealth, not a sum, and 0 when no
    // site is finished.
    [Fact]
    public void LastFinishedSiteStealthTakesTheLastFinishedSlot()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researchedItems: [], completedStealthSites: true);
        var sites = match.Sectors[0].Sites;

        Assert.Equal((int)data.Site(sites[1].DefinitionId).Stats.Stealth,
            OriginalAiFamilyTenRules.LastFinishedSiteStealth(match, 0));

        sites[2].Resistance = 0;
        Assert.Equal((int)data.Site(sites[2].DefinitionId).Stats.Stealth,
            OriginalAiFamilyTenRules.LastFinishedSiteStealth(match, 0));

        foreach (var site in sites) site.Resistance = 1;
        Assert.Equal(0, OriginalAiFamilyTenRules.LastFinishedSiteStealth(match, 0));
    }

    private static MatchState CreateMatch(
        OriginalData data,
        IEnumerable<short> researchedItems,
        short? armorItemId = null,
        bool completedStealthSites = false)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 100,
                [new MatchGangState(new GangId(10), setups[0].Id, 0, 0, 10,
                    armorItemId: armorItemId)],
                researchedItems: researchedItems.ToHashSet()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
                completedStealthSites && id == 0
                    ?
                    [
                        new MatchSiteState(0, 9, 0),
                        new MatchSiteState(1, 11, 0),
                        new MatchSiteState(2, 14, 1)
                    ]
                    :
                    [
                        new MatchSiteState(0, 0, 7),
                        new MatchSiteState(1, 3, 13),
                        new MatchSiteState(2, 5, 15)
                    ],
                owner: id == 0 ? setups[0].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Eliminate, GameDuration.SixMonths, 41, setups), players, sectors);
    }
}
