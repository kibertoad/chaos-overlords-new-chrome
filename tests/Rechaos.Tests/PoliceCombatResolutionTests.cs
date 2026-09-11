using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class PoliceCombatResolutionTests
{
    [Fact]
    public void CrackdownAttacksEveryActiveGangInSectorInStableOrder()
    {
        var match = CreateMatch(
            new MatchGangState(new GangId(12), new PlayerId(0), 1, 0, 10),
            new MatchGangState(new GangId(10), new PlayerId(0), 3, 0, 10),
            new MatchGangState(new GangId(11), new PlayerId(0), 1, 1, 10),
            new MatchGangState(new GangId(13), new PlayerId(0), 1, 0, 0));

        EnterAndResolveCombat(match);

        Assert.Equal([new GangId(10), new GangId(12)],
            match.LastPoliceAttackResolutions.Select(result => result.Gang));
        Assert.All(match.LastPoliceAttackResolutions, result =>
        {
            var gang = match.FindGang(result.Gang)!;
            Assert.Equal(ManualRules.PoliceDetectionPercent(
                EffectiveStatisticsCalculator.ForGang(match, gang).Stealth), result.Details.DetectionChance);
            Assert.Equal(Math.Max(0, ManualRules.PoliceCombat
                - EffectiveStatisticsCalculator.ForGang(match, gang).Defense), result.Details.AttackValue);
            Assert.Equal(ManualRules.CountSuccesses(result.Details.Rolls), result.Details.Successes);
            Assert.Equal(Math.Max(0, result.Details.PreviousForce - result.Details.Damage), result.Details.ResultForce);
            Assert.Equal(GameEventKind.PoliceAttackResolved, result.Event.Kind);
        });
        Assert.Equal(match.LastPoliceAttackResolutions.Sum(result => (result.Details.AttackValue + 1) * 3),
            match.Random.ConsumptionCount);
    }

    [Fact]
    public void StealthTwentyFiveCannotBeDetectedOrDamaged()
    {
        const short ebonOrder = 84;
        const short invisoCloak = 36;
        var match = CreateMatch(
            [new MatchGangState(new GangId(10), new PlayerId(0), ebonOrder, 0, 7,
                miscellaneousItemId: invisoCloak)],
            stealthBoostingSite: true);

        EnterAndResolveCombat(match);

        var result = Assert.Single(match.LastPoliceAttackResolutions);
        Assert.Equal(0, result.Details.DetectionChance);
        Assert.False(result.Details.Detected);
        Assert.Empty(result.Details.Rolls);
        Assert.Equal(0, result.Details.Damage);
        Assert.Equal(7, match.FindGang(new GangId(10))!.Force);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void HiddenGangUsesPoliceDetectTwelveProbability()
    {
        var match = CreateMatch(
            new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 10));
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Hide, CommandTarget.None)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishExecutionPhase();
        Assert.True(match.FindGang(new GangId(10))!.Hidden);

        match.FinishExecutionPhase();

        var result = Assert.Single(match.LastPoliceAttackResolutions);
        var stealth = EffectiveStatisticsCalculator.ForGang(match, match.FindGang(new GangId(10))!).Stealth;
        Assert.Equal(ManualRules.HiddenAttackHitPercent(ManualRules.PoliceDetect, stealth),
            result.Details.DetectionChance);
        Assert.NotEqual(ManualRules.PoliceDetectionPercent(stealth), result.Details.DetectionChance);
    }

    [Fact]
    public void PoliceDamageEliminatesGangAndClearsEquipment()
    {
        var data = BundledOriginalData.Load();
        var vulnerableDefinition = data.Gangs.OrderBy(gang => gang.Stats.Defense).First().Id;
        var match = CreateMatch(new MatchGangState(
            new GangId(10), new PlayerId(0), vulnerableDefinition, 0, 1,
            weaponItemId: 0, armorItemId: 24, miscellaneousItemId: 38));

        EnterAndResolveCombat(match);

        var result = Assert.Single(match.LastPoliceAttackResolutions);
        Assert.True(result.Details.Detected);
        Assert.True(result.Details.Successes > 0);
        var gang = match.FindGang(new GangId(10))!;
        Assert.Equal(0, gang.Force);
        Assert.Null(gang.WeaponItemId);
        Assert.Null(gang.ArmorItemId);
        Assert.Null(gang.MiscellaneousItemId);
        Assert.Equal(1, match.Players[0].Statistics.Casualties);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)), notification =>
            notification.Kind == GameNotificationKind.Police && notification.Gang == gang.Id);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)), notification =>
            notification.Kind == GameNotificationKind.Elimination && notification.Gang == gang.Id);
    }

    [Fact]
    public void EquivalentCrackdownsProduceIdenticalEventsAndHash()
    {
        var first = CreateMatch(new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 10));
        var second = CreateMatch(new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 10));

        EnterAndResolveCombat(first);
        EnterAndResolveCombat(second);

        var firstDetails = first.LastPoliceAttackResolutions[0].Details;
        var secondDetails = second.LastPoliceAttackResolutions[0].Details;
        Assert.Equal(firstDetails with { Rolls = [] }, secondDetails with { Rolls = [] });
        Assert.Equal(firstDetails.Rolls, secondDetails.Rolls);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    private static void EnterAndResolveCombat(MatchState match)
    {
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);
        match.FinishExecutionPhase();
    }

    private static MatchState CreateMatch(params MatchGangState[] gangs) => CreateMatch(gangs, false);

    private static MatchState CreateMatch(MatchGangState[] gangs, bool stealthBoostingSite)
    {
        var data = BundledOriginalData.Load();
        var playerSetup = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [playerSetup]);
        var player = new MatchPlayerState(playerSetup, 500, gangs);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id == 0 && stealthBoostingSite ? (short)17 : (short)0, 7,
                    id == 0 && stealthBoostingSite ? new PlayerId(0) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 && stealthBoostingSite ? new PlayerId(0) : null,
                crackdownActive: id == 0))
            .ToArray();
        return new MatchState(data, setup, [player], sectors);
    }
}
