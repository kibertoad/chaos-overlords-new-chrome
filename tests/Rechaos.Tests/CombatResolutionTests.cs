using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatResolutionTests
{
    [Fact]
    public void AttackAndRetaliationUseInitialForceAndEffectiveStatistics()
    {
        var match = CreateMatch();
        QueueAndEnterCombat(match, playerOneAction: null);
        var attacker = match.FindGang(new GangId(10))!;
        var target = match.FindGang(new GangId(20))!;
        var attackerForce = attacker.Force;
        var targetForce = target.Force;
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attacker);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, target);
        var attackDice = ManualRules.AttackDiceCount(
            attackerForce, ManualRules.CombatRating(attackerStats, WeaponType(match, attacker)), targetStats.Defense);
        var retaliationDice = ManualRules.AttackDiceCount(
            targetForce, ManualRules.CombatRating(targetStats, WeaponType(match, target)), attackerStats.Defense);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(attackDice, resolution.Rolls.Count);
        Assert.Equal(retaliationDice, resolution.RetaliationRolls!.Count);
        Assert.Equal(ManualRules.CountSuccesses(resolution.Rolls), resolution.Damage);
        Assert.Equal(
            ManualRules.RetaliationDamage(ManualRules.CountSuccesses(resolution.RetaliationRolls)),
            resolution.RetaliationDamage);
        Assert.Equal(Math.Max(0, targetForce - resolution.Damage), target.Force);
        Assert.Equal(Math.Max(0, attackerForce - resolution.RetaliationDamage), attacker.Force);
        Assert.Equal(Math.Min(targetForce, resolution.Damage), match.Players[0].Statistics.DamageInflicted);
        Assert.Equal(0, match.Players[1].Statistics.DamageInflicted);
        Assert.Equal((attackDice + retaliationDice) * 3, match.Random.ConsumptionCount);
        Assert.Equal(GameNotificationKind.Combat,
            Assert.Single(match.NotificationsFor(new PlayerId(0)), value => value.Kind == GameNotificationKind.Combat).Kind);
    }

    [Fact]
    public void AllAttacksUsePhaseStartSnapshotsEvenWhenGangIsEliminated()
    {
        var match = CreateMatch(playerZeroForce: 2, playerOneForce: 2);
        QueueAndEnterCombat(match, GangAction.Attack);
        var initialZero = match.FindGang(new GangId(10))!.Force;
        var initialOne = match.FindGang(new GangId(20))!.Force;

        match.FinishExecutionPhase();

        Assert.Equal(2, match.LastPhaseResolutions.Count);
        Assert.All(match.LastPhaseResolutions, result => Assert.Equal(CommandResolutionCode.Resolved, result.Code));
        var zeroAttack = match.LastPhaseResolutions.Single(result => result.Command.Player == new PlayerId(0)).Event!.Resolution!;
        var oneAttack = match.LastPhaseResolutions.Single(result => result.Command.Player == new PlayerId(1)).Event!.Resolution!;
        Assert.Equal(Math.Max(0, initialZero - zeroAttack.RetaliationDamage - oneAttack.Damage),
            match.FindGang(new GangId(10))!.Force);
        Assert.Equal(Math.Max(0, initialOne - zeroAttack.Damage - oneAttack.RetaliationDamage),
            match.FindGang(new GangId(20))!.Force);
        Assert.NotEmpty(oneAttack.Rolls);
    }

    [Fact]
    public void BareHandedMartialArtistSuppressesNonMartialRetaliation()
    {
        var data = BundledOriginalData.Load();
        var martial = data.Gangs.OrderByDescending(gang => gang.Stats.MartialArts).First().Id;
        var nonMartial = data.Gangs.First(gang => gang.Stats.MartialArts <= 0).Id;
        var match = CreateMatch(playerZeroDefinition: martial, playerOneDefinition: nonMartial);
        QueueAndEnterCombat(match, playerOneAction: null);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.True(EffectiveStatisticsCalculator.ForGang(match, match.FindGang(new GangId(10))!).MartialArts > 0);
        Assert.Empty(resolution.RetaliationRolls!);
        Assert.Equal(0, resolution.RetaliationDamage);
    }

    [Fact]
    public void HiddenTargetCanEvadeAttackUsingIndividualDetect()
    {
        var data = BundledOriginalData.Load();
        var lowDetect = data.Gangs.OrderBy(gang => gang.Stats.Detect).First().Id;
        var highStealth = data.Gangs.OrderByDescending(gang => gang.Stats.Stealth).First().Id;
        var match = CreateMatch(playerZeroDefinition: lowDetect, playerOneDefinition: highStealth);
        QueueAndEnterCombat(match, GangAction.Hide);

        match.FinishExecutionPhase();

        var result = Assert.Single(match.LastPhaseResolutions);
        Assert.Equal(CommandResolutionCode.TargetEvaded, result.Code);
        Assert.Equal(GameEventKind.CommandFailed, result.Event!.Kind);
        Assert.Empty(result.Event.Resolution!.Rolls);
        Assert.Equal(0, result.Event.Resolution.DetectionChance);
        Assert.InRange(result.Event.Resolution.DetectionRoll!.Value, 1, 100);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void HittingHiddenTargetConsumesDetectionRollAndPreventsRetaliation()
    {
        var data = BundledOriginalData.Load();
        var highDetect = data.Gangs.OrderByDescending(gang => gang.Stats.Detect).First().Id;
        var lowStealth = data.Gangs.OrderBy(gang => gang.Stats.Stealth).First().Id;
        var match = CreateMatch(playerZeroDefinition: highDetect, playerOneDefinition: lowStealth);
        QueueAndEnterCombat(match, GangAction.Hide);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(CommandResolutionCode.Resolved, resolution.Code);
        Assert.Equal(100, resolution.DetectionChance);
        Assert.InRange(resolution.DetectionRoll!.Value, 1, 100);
        Assert.Empty(resolution.RetaliationRolls!);
        Assert.Equal((resolution.Rolls.Count + 1) * 3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void InfluencedSiteStatisticsApplyOnlyToOwnersGangsInThatSector()
    {
        const short researchLab = 4;
        var match = CreateMatch(influencedSiteDefinition: researchLab);
        var ownerGang = match.FindGang(new GangId(10))!;
        var enemyGang = match.FindGang(new GangId(20))!;
        var siteStats = match.Definitions.Sites.Single(site => site.Id == researchLab).Stats;
        var ownerBase = match.Definitions.Gangs.Single(gang => gang.Id == ownerGang.DefinitionId).Stats;
        var enemyBase = match.Definitions.Gangs.Single(gang => gang.Id == enemyGang.DefinitionId).Stats;

        Assert.Equal(EffectiveStatistics.From(ownerBase).Add(siteStats),
            EffectiveStatisticsCalculator.ForGang(match, ownerGang));
        Assert.Equal(EffectiveStatistics.From(enemyBase),
            EffectiveStatisticsCalculator.ForGang(match, enemyGang));
    }

    [Fact]
    public void SectorVisibilityUsesCooperativeDetectButNotHideState()
    {
        var data = BundledOriginalData.Load();
        var highDetect = data.Gangs.OrderByDescending(gang => gang.Stats.Detect).First().Id;
        var lowStealth = data.Gangs.OrderBy(gang => gang.Stats.Stealth).First().Id;
        var match = CreateMatch(playerZeroDefinition: highDetect, playerOneDefinition: lowStealth);

        Assert.True(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        Assert.True(match.CanPlayerDetectGang(new PlayerId(1), new GangId(20)));

        QueueAndEnterCombat(match, GangAction.Hide);

        Assert.True(match.FindGang(new GangId(20))!.Hidden);
        Assert.True(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
    }

    [Fact]
    public void CombatEliminationClearsEquipmentAndRecordsCasualty()
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs.OrderByDescending(gang =>
            gang.Stats.Combat + gang.Stats.Strength + gang.Stats.Blade).First().Id;
        var weapon = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type == 1)
            .OrderByDescending(value => value.item.Stats.Combat)
            .First().index;
        var match = CreateMatch(
            playerZeroDefinition: attackerDefinition,
            playerZeroWeapon: checked((short)weapon),
            playerOneForce: 1,
            playerOneWeapon: 0,
            playerOneArmor: 24,
            playerOneMiscellaneous: 38);
        QueueAndEnterCombat(match, playerOneAction: null);

        match.FinishExecutionPhase();

        var target = match.FindGang(new GangId(20))!;
        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal((short)weapon, resolution.ItemId);
        Assert.Equal((short)0, resolution.RetaliationItemId);
        Assert.Equal(0, target.Force);
        Assert.Null(target.WeaponItemId);
        Assert.Null(target.ArmorItemId);
        Assert.Null(target.MiscellaneousItemId);
        Assert.Equal(1, match.Players[1].Statistics.Casualties);
        Assert.Contains(match.NotificationsFor(new PlayerId(1)),
            notification => notification.Kind == GameNotificationKind.Elimination && notification.Gang == target.Id);
    }

    [Fact]
    public void EquivalentCombatRunsProduceIdenticalOutcomesAndHash()
    {
        var first = CreateMatch();
        var second = CreateMatch();
        QueueAndEnterCombat(first, playerOneAction: null);
        QueueAndEnterCombat(second, playerOneAction: null);

        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        Assert.Equal(first.FindGang(new GangId(10))!.Force, second.FindGang(new GangId(10))!.Force);
        Assert.Equal(first.FindGang(new GangId(20))!.Force, second.FindGang(new GangId(20))!.Force);
        Assert.Equal(first.LastPhaseResolutions[0].Event!.Resolution!.Rolls,
            second.LastPhaseResolutions[0].Event!.Resolution!.Rolls);
        Assert.Equal(first.LastPhaseResolutions[0].Event!.Resolution!.RetaliationRolls,
            second.LastPhaseResolutions[0].Event!.Resolution!.RetaliationRolls);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    private static short? WeaponType(MatchState match, MatchGangState gang) =>
        gang.WeaponItemId is { } weapon ? match.Definitions.Items[weapon].Type : null;

    private static void QueueAndEnterCombat(MatchState match, GangAction? playerOneAction)
    {
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Attack, CommandTarget.Gang(new GangId(20)))).Accepted);
        match.FinishCommand(new PlayerId(0));
        if (playerOneAction is { } action)
        {
            var command = action == GangAction.Attack
                ? new GameCommand(new PlayerId(1), new GangId(20), action, CommandTarget.Gang(new GangId(10)))
                : new GameCommand(new PlayerId(1), new GangId(20), action, CommandTarget.None);
            Assert.True(match.Submit(command).Accepted);
        }
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);
    }

    private static MatchState CreateMatch(
        short playerZeroDefinition = 1,
        short playerOneDefinition = 3,
        int playerZeroForce = 10,
        int playerOneForce = 10,
        short? playerZeroWeapon = null,
        short? playerOneWeapon = null,
        short? playerOneArmor = null,
        short? playerOneMiscellaneous = null,
        short? influencedSiteDefinition = null)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500,
                [new MatchGangState(new GangId(10), new PlayerId(0), playerZeroDefinition, 0,
                    playerZeroForce, weaponItemId: playerZeroWeapon)]),
            new(setups[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), playerOneDefinition, 0,
                    playerOneForce, playerOneWeapon, playerOneArmor, playerOneMiscellaneous)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id == 0 && influencedSiteDefinition is { } site ? site : (short)0, 7,
                    id == 0 && influencedSiteDefinition is not null ? new PlayerId(0) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
