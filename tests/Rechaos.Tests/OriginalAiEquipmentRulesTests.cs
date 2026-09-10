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
}
