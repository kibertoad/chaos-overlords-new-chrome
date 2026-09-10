using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiEquipmentRulesTests
{
    [Fact]
    public void Family11ClassTiePrefersRangedWeapon()
    {
        var match = CreateMatch(gangDefinitionId: 5, cash: 500);
        var player = match.Players[0];
        var gang = player.Gangs[0];

        var selected = OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
            match, player, gang, player.Cash);

        Assert.Equal(12, selected); // COMBAT PISTOL: ranged wins the 2/2/2 class tie.
    }

    [Fact]
    public void Family11UpgradeUsesRawCashBoundaryAndRequiresStrictlyBetterCombat()
    {
        var below = CreateMatch(gangDefinitionId: 56, cash: 44, weaponItemId: 21);
        var exact = CreateMatch(gangDefinitionId: 56, cash: 45, weaponItemId: 21);

        Assert.Null(OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
            below, below.Players[0], below.Players[0].Gangs[0], below.Players[0].Cash));
        Assert.Equal(23, OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
            exact, exact.Players[0], exact.Players[0].Gangs[0], exact.Players[0].Cash));
    }

    [Fact]
    public void Family11BareHandedScoreCanRejectEveryWeaponClass()
    {
        var match = CreateMatch(gangDefinitionId: 58, cash: 500);

        Assert.Null(OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0], match.Players[0].Cash));
    }

    [Fact]
    public void ArmorUpgradeUsesStrictCashAndStrictDefenseBoundaries()
    {
        var exact = CreateMatch(gangDefinitionId: 56, cash: 40);
        var above = CreateMatch(gangDefinitionId: 56, cash: 41);

        Assert.Equal(35, OriginalAiEquipmentRules.SelectArmorUpgrade(
            exact, exact.Players[0], exact.Players[0].Gangs[0], exact.Players[0].Cash));
        Assert.Equal(37, OriginalAiEquipmentRules.SelectArmorUpgrade(
            above, above.Players[0], above.Players[0].Gangs[0], above.Players[0].Cash));
    }

    [Fact]
    public void MiscellaneousUpgradeMaximizesChaosAndRejectsCurrentMaximum()
    {
        var match = CreateMatch(gangDefinitionId: 5, cash: 500);
        var player = match.Players[0];
        var gang = player.Gangs[0];

        Assert.Equal(40, OriginalAiEquipmentRules.SelectMiscellaneousChaosUpgrade(
            match, player, gang));

        gang.MiscellaneousItemId = 40;
        Assert.Null(OriginalAiEquipmentRules.SelectMiscellaneousChaosUpgrade(
            match, player, gang));
    }

    [Fact]
    public void FamilyElevenMiscellaneousUpgradeMaximizesDetect()
    {
        var match = CreateMatch(gangDefinitionId: 0, cash: 500);

        Assert.Equal(50, OriginalAiEquipmentRules.SelectMiscellaneousDetectUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0]));
    }

    [Fact]
    public void FamilyOneEquipmentNeedChangesBetweenGreedAndOtherScenarios()
    {
        var greed = CreateEquipmentNeedMatch(ScenarioId.Greed, adjacentOwner: new PlayerId(1));
        var power = CreateEquipmentNeedMatch(ScenarioId.Power, adjacentOwner: new PlayerId(1));

        Assert.False(OriginalAiEquipmentRules.NeedsFamilyOneEquipment(
            greed, greed.Players[0], greed.Players[0].Gangs[0]));
        Assert.True(OriginalAiEquipmentRules.NeedsFamilyOneEquipment(
            power, power.Players[0], power.Players[0].Gangs[0]));
    }

    [Fact]
    public void FamilyOneEquipmentNeedPreservesBottomLeftSector64OwnerAlias()
    {
        var match = CreateEquipmentNeedMatch(ScenarioId.Power, adjacentOwner: null);

        Assert.False(OriginalAiEquipmentRules.NeedsFamilyOneEquipment(
            match, match.Players[0], match.Players[0].Gangs[0]));
        Assert.True(OriginalAiEquipmentRules.NeedsFamilyOneEquipment(
            match, match.Players[1], match.Players[1].Gangs[0]));
    }

    [Theory]
    [InlineData(0, GangAction.None, true)]
    [InlineData(-1, GangAction.Move, true)]
    [InlineData(1, GangAction.None, false)]
    [InlineData(0, GangAction.Attack, false)]
    public void Family11ReplacementGateUsesCooldownAndPreviousAttack(
        int cooldown,
        GangAction previousAction,
        bool expected) =>
        Assert.Equal(expected, OriginalAiEquipmentRules.CanReplaceWeapon(cooldown, previousAction));

    [Fact]
    public void Family11ReplacementCooldownIsThreeTimesRawCost() =>
        Assert.Equal(135, OriginalAiEquipmentRules.WeaponReplacementCooldown(45));

    private static MatchState CreateMatch(
        short gangDefinitionId,
        int cash,
        short? weaponItemId = null)
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "CPU", PlayerController.Computer);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer]);
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index))
            .ToHashSet();
        var gang = new MatchGangState(
            new GangId(10), setupPlayer.Id, gangDefinitionId, 0, 10, weaponItemId);
        var player = new MatchPlayerState(setupPlayer, cash, [gang], researchedItems: researched);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? setupPlayer.Id : null))
            .ToArray();
        return new MatchState(data, setup, [player], sectors);
    }

    private static MatchState CreateEquipmentNeedMatch(
        ScenarioId scenario,
        PlayerId? adjacentOwner)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU ZERO", PlayerController.Computer),
            new(new PlayerId(1), "CPU ONE", PlayerController.Computer)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 100,
                [new MatchGangState(new GangId(10), setups[0].Id, 58, 56, 10)]),
            new(setups[1], 100,
                [new MatchGangState(new GangId(20), setups[1].Id, 58, 56, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 57 ? adjacentOwner : null))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 1997, setups), players, sectors);
    }
}
