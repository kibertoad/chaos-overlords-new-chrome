using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
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
        // RULE-COMBAT-001: the stored Combat already holds the weapon skills.
        var attackDice = ManualRules.AttackDiceCount(
            attackerForce, attackerStats.Combat, targetStats.Defense);
        var retaliationDice = ManualRules.AttackDiceCount(
            targetForce, targetStats.Combat, attackerStats.Defense);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(attackDice, resolution.Rolls.Count);
        Assert.Equal(retaliationDice, resolution.RetaliationRolls!.Count);
        Assert.Equal(
            OriginalResolutionRules.MainAttackDamage(
                attackDice, OriginalResolutionRules.CountSuccesses(resolution.Rolls, 5)),
            resolution.Damage);
        Assert.Equal(
            ManualRules.RetaliationDamage(
                OriginalResolutionRules.CountSuccesses(resolution.RetaliationRolls, 5)),
            resolution.RetaliationDamage);
        Assert.Equal(Math.Max(0, targetForce - resolution.Damage), target.Force);
        Assert.Equal(Math.Max(0, attackerForce - resolution.RetaliationDamage), attacker.Force);
        Assert.Equal(resolution.Damage, match.Players[0].Statistics.DamageInflicted);
        Assert.Equal(0, match.Players[1].Statistics.DamageInflicted);
        Assert.Equal(
            Math.Max(AiStrategicState.MinimumAttitude,
                1 - Math.Max(match.AiStrategy.Reaction(new PlayerId(1)), resolution.Damage)),
            match.AiStrategy.Attitude(new PlayerId(1), new PlayerId(0)));
        Assert.Equal(1, match.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));
        Assert.Equal((attackDice + retaliationDice) * 3, match.Random.ConsumptionCount);
        Assert.Equal(GameNotificationKind.Combat,
            Assert.Single(match.NotificationsFor(new PlayerId(0)), value => value.Kind == GameNotificationKind.Combat).Kind);
    }

    [Fact]
    public void GangsAttackingEachOtherEachRollAnAttackAndARetaliation()
    {
        var match = CreateMatch(playerZeroForce: 2, playerOneForce: 2);
        QueueAndEnterCombat(match, GangAction.Attack);
        var initialZero = match.FindGang(new GangId(10))!.Force;
        var initialOne = match.FindGang(new GangId(20))!.Force;

        match.FinishExecutionPhase();

        Assert.Equal([new GangId(10), new GangId(20)],
            match.LastPhaseResolutions.Select(result => result.Command.Gang).ToArray());
        var zero = match.LastPhaseResolutions[0].Event!.Resolution!;
        var one = match.LastPhaseResolutions[1].Event!.Resolution!;
        Assert.All(match.LastPhaseResolutions,
            result => Assert.Equal(CommandResolutionCode.Resolved, result.Code));
        Assert.All([zero, one], resolution =>
        {
            Assert.NotEmpty(resolution.Rolls);
            Assert.NotEmpty(resolution.RetaliationRolls!);
        });
        // Both attacks and both retaliations roll from phase-start Force, then land together.
        Assert.Equal(Math.Max(0, initialZero - zero.RetaliationDamage - one.Damage),
            match.FindGang(new GangId(10))!.Force);
        Assert.Equal(Math.Max(0, initialOne - zero.Damage - one.RetaliationDamage),
            match.FindGang(new GangId(20))!.Force);
    }

    [Theory]
    [InlineData(0, 10, 20)]
    [InlineData(1, 20, 10)]
    public void MutualAttackPlaysTheViewersAttackThenHandsOffToTheReply(
        int viewer,
        int viewersGang,
        int opponent)
    {
        var match = CreateMatch(playerZeroForce: 8, playerOneForce: 8);
        QueueAndEnterCombat(match, GangAction.Attack);
        match.FinishExecutionPhase();
        var events = match.LastPhaseResolutions.Select(result => result.Event!).ToArray();

        var clips = CombatAnimationRouting.ForPresentation(match, events, new PlayerId(viewer));

        Assert.Equal(2, clips.Count);
        Assert.Equal((new GangId(viewersGang), false, true),
            (clips[0].Attacker!.Value, clips[0].Reversed, clips[0].HandsOff));
        Assert.Equal((new GangId(opponent), true, false),
            (clips[1].Attacker!.Value, clips[1].Reversed, clips[1].HandsOff));
        // The reply starts from the forces the first clip left, and ends on the resolved forces.
        Assert.Equal(clips[0].Forces.AttackerAfter, clips[1].Forces.DefenderBefore);
        Assert.Equal(clips[0].Forces.DefenderAfter, clips[1].Forces.AttackerBefore);
        Assert.Equal(match.FindGang(new GangId(viewersGang))!.Force, clips[1].Forces.DefenderAfter);
        Assert.Equal(match.FindGang(new GangId(opponent))!.Force, clips[1].Forces.AttackerAfter);
    }

    [Fact]
    public void AttacksRollAndEmitEventsInRosterOrderRatherThanSubmissionOrder()
    {
        var match = CreateMatch(secondPlayerZeroGang: true);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);

        match.FinishExecutionPhase();

        Assert.Equal([new GangId(10), new GangId(11)],
            match.LastPhaseResolutions.Select(result => result.Command.Gang).ToArray());
    }

    [Fact]
    public void MultipleAttacksCreditFullRolledDamageBeyondTargetForce()
    {
        var match = CreateMatch(playerOneForce: 1, secondPlayerZeroGang: true);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();

        match.FinishExecutionPhase();

        var damage = match.LastPhaseResolutions.Sum(
            result => result.Event!.Resolution!.Damage);
        Assert.True(damage > 1);
        Assert.Equal(0, match.FindGang(new GangId(20))!.Force);
        Assert.Equal(damage, match.Players[0].Statistics.DamageInflicted);
        Assert.Equal(0, match.Players[1].Statistics.DamageInflicted);
    }

    [Fact]
    public void BareHandedMartialArtistSuppressesNonMartialRetaliation()
    {
        var data = BundledOriginalData.Load();
        var martial = data.Gangs
            .Where(gang => gang.Stats.MartialArts > 0)
            .OrderBy(gang => gang.Stats.Defense)
            .First().Id;
        var nonMartial = data.Gangs
            .Where(gang => gang.Stats.MartialArts <= 0)
            .OrderByDescending(gang => gang.Stats.Combat + gang.Stats.Strength + gang.Stats.Fighting)
            .First().Id;
        var match = CreateMatch(playerZeroDefinition: martial, playerOneDefinition: nonMartial);
        QueueAndEnterCombat(match, playerOneAction: null);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.True(EffectiveStatisticsCalculator.ForGang(match, match.FindGang(new GangId(10))!).MartialArts > 0);
        Assert.Empty(resolution.RetaliationRolls!);
        Assert.Equal(0, resolution.RetaliationDamage);
    }

    [Fact]
    public void BareHandedMartialArtistCanRetaliateAgainstBareHandedMartialArtist()
    {
        var data = BundledOriginalData.Load();
        var martial = data.Gangs.OrderByDescending(gang => gang.Stats.MartialArts).First().Id;
        var match = CreateMatch(playerZeroDefinition: martial, playerOneDefinition: martial);
        QueueAndEnterCombat(match, playerOneAction: null);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.True(EffectiveStatisticsCalculator.ForGang(match, match.FindGang(new GangId(10))!).MartialArts > 0);
        Assert.True(EffectiveStatisticsCalculator.ForGang(match, match.FindGang(new GangId(20))!).MartialArts > 0);
        Assert.NotEmpty(resolution.RetaliationRolls!);
    }

    [Fact]
    public void ArmedMartialArtistDoesNotSuppressRetaliation()
    {
        var data = BundledOriginalData.Load();
        var martial = data.Gangs
            .Where(gang => gang.Stats.MartialArts > 0)
            .OrderBy(gang => gang.Stats.Defense)
            .First().Id;
        var nonMartial = data.Gangs
            .Where(gang => gang.Stats.MartialArts <= 0)
            .OrderByDescending(gang => gang.Stats.Combat + gang.Stats.Strength + gang.Stats.Fighting)
            .First().Id;
        var weapon = checked((short)data.Items
            .Select((item, index) => (item, index))
            .First(value => value.item.Type is >= 0 and <= 2).index);
        var match = CreateMatch(
            playerZeroDefinition: martial,
            playerOneDefinition: nonMartial,
            playerZeroWeapon: weapon);
        QueueAndEnterCombat(match, playerOneAction: null);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.NotEmpty(resolution.RetaliationRolls!);
    }

    [Fact]
    public void HiddenTargetCanEvadeAttackUsingIndividualDetect()
    {
        var data = BundledOriginalData.Load();
        var lowDetect = data.Gangs.OrderBy(gang => gang.Stats.Detect).First().Id;
        var highStealth = data.Gangs.OrderByDescending(gang => gang.Stats.Stealth).First().Id;
        var match = CreateMatch(
            playerZeroDefinition: lowDetect,
            playerOneDefinition: highStealth,
            playerZeroName: "SMGHUBBLE");
        Assert.True(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        QueueAndEnterCombat(match, GangAction.Hide);

        match.FinishExecutionPhase();

        var result = Assert.Single(match.LastPhaseResolutions);
        Assert.Equal(CommandResolutionCode.TargetEvaded, result.Code);
        Assert.Equal(GameEventKind.CommandFailed, result.Event!.Kind);
        Assert.Empty(result.Event.Resolution!.Rolls);
        Assert.Equal(0, result.Event.Resolution.DetectionChance);
        Assert.InRange(result.Event.Resolution.DetectionRoll!.Value, 1, 100);
        Assert.Equal(3, match.Random.ConsumptionCount);
        // RULE-AI-016: an evaded attack still lowers the target player's attitude by its reaction.
        Assert.Equal(
            Math.Max(AiStrategicState.MinimumAttitude, 1 - match.AiStrategy.Reaction(new PlayerId(1))),
            match.AiStrategy.Attitude(new PlayerId(1), new PlayerId(0)));
    }

    [Fact]
    public void HittingHiddenTargetConsumesDetectionRollAndPreventsRetaliation()
    {
        var data = BundledOriginalData.Load();
        var highDetect = data.Gangs.OrderByDescending(gang => gang.Stats.Detect).First().Id;
        var lowStealth = data.Gangs.OrderBy(gang => gang.Stats.Stealth).First().Id;
        var match = CreateMatch(
            playerZeroDefinition: highDetect,
            playerOneDefinition: lowStealth,
            playerZeroName: "ONE",
            playerOneName: "TWO");
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
    public void CombatEliminationRetainsEquipmentAndRecordsCasualty()
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs
            .OrderBy(gang => gang.Stats.Defense)
            .ThenByDescending(gang => gang.Stats.Combat + gang.Stats.Strength + gang.Stats.Blade)
            .First().Id;
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
        var attacker = match.FindGang(new GangId(10))!;
        var target = match.FindGang(new GangId(20))!;
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attacker);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, target);
        var retaliationDice = ManualRules.AttackDiceCount(
            target.Force,
            targetStats.Combat,
            attackerStats.Defense);
        Assert.True(retaliationDice > 0);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal((short)weapon, resolution.ItemId);
        Assert.Equal((short)0, resolution.RetaliationItemId);
        Assert.Equal(0, target.Force);
        Assert.Equal((short)0, target.WeaponItemId);
        Assert.Equal((short)24, target.ArmorItemId);
        Assert.Equal((short)38, target.MiscellaneousItemId);
        Assert.Equal(retaliationDice, resolution.RetaliationRolls!.Count);
        Assert.Equal(1, match.Players[1].Statistics.Casualties);
        Assert.Contains(match.NotificationsFor(new PlayerId(1)),
            notification => notification.Kind == GameNotificationKind.Elimination && notification.Gang == target.Id);
    }

    [Fact]
    public void CombatEliminationTakesTheDeadGangsRecurringCommandWithIt()
    {
        var match = KillingMatch(secondPlayerOneGang: true);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Heal, CommandTarget.None,
            Repeat: true)).Accepted);
        match.FinishCommand(new PlayerId(1));
        // Instant resolves the Heal, then Combat kills the gang that ordered it.
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);

        match.FinishExecutionPhase();

        var target = match.FindGang(new GangId(20))!;
        Assert.Equal(0, target.Force);
        Assert.Null(target.QueuedCommand);
        Assert.False(match.Commands.TryGet(new GangId(20), out _));

        foreach (var _ in TurnStructure.ExecutionOrder.Skip(2)) match.FinishExecutionPhase();
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        match.FinishExecutionPhase();

        // Without the cancellation the repeating Heal resolved against a destroyed gang and
        // brought it back.
        Assert.Equal(0, match.FindGang(new GangId(20))!.Force);
    }

    // RULE-TURN-004, RULE-HEAL-001: the original tests a recurring Heal only at turn start. A gang
    // healed to Force 10 in the instant phase and then hurt in Combat keeps its order, starts the
    // next turn below 10, and heals again.
    [Fact]
    public void RecurringHealThatReachesTenAndIsHurtInCombatHealsAgainNextTurn()
    {
        var match = CreateMatch(playerOneForce: 9);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Heal, CommandTarget.None,
            Repeat: true)).Accepted);
        match.FinishCommand(new PlayerId(1));
        var healer = match.FindGang(new GangId(20))!;

        match.FinishExecutionPhase();
        Assert.Equal(10, healer.Force);
        Assert.True(match.Commands.TryGet(healer.Id, out _));

        match.FinishExecutionPhase();
        Assert.InRange(healer.Force, 1, 9);

        foreach (var _ in TurnStructure.ExecutionOrder.Skip(2)) match.FinishExecutionPhase();
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();
        match.FinishUpkeep();

        Assert.True(match.Commands.TryGet(healer.Id, out var queued));
        Assert.Equal(GangAction.Heal, queued!.Command.Action);
        Assert.True(queued.Command.Repeat);
        var hurt = healer.Force;
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        match.FinishExecutionPhase();
        var heal = Assert.Single(match.LastPhaseResolutions,
            result => result.Command.Gang == healer.Id);
        Assert.Equal(GangAction.Heal, heal.Command.Action);
        Assert.Equal(Math.Min(hurt + heal.Event!.Resolution!.Successes, 10), healer.Force);
    }

    // RULE-TURN-004: a recurring Heal still at Force 10 when the next turn starts is dropped there.
    [Fact]
    public void RecurringHealAtTenWhenTheTurnStartsIsDropped()
    {
        var match = CreateMatch(playerOneForce: 9);
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Heal, CommandTarget.None,
            Repeat: true)).Accepted);
        match.FinishCommand(new PlayerId(1));
        var healer = match.FindGang(new GangId(20))!;

        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        Assert.Equal(10, healer.Force);
        Assert.True(match.Commands.TryGet(healer.Id, out _));
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();
        match.FinishUpkeep();

        Assert.False(match.Commands.TryGet(healer.Id, out _));
        Assert.Null(healer.QueuedCommand);
    }

    [Fact]
    public void SimpleAndDetailedPresentationProduceIdenticalOutcomesAndHash()
    {
        var simple = CreateMatch();
        var detailed = CreateMatch();
        QueueAndEnterCombat(simple, playerOneAction: null);
        QueueAndEnterCombat(detailed, playerOneAction: null);

        simple.FinishExecutionPhase();
        detailed.FinishExecutionPhase();
        var player = new CombatAnimationPlayer();
        var detailedEvent = Assert.Single(detailed.LastPhaseResolutions).Event!;
        foreach (var clip in CombatAnimationRouting.ForEvent(
            detailed, detailedEvent, detailedEvent.Player, CombatForceTimeline.For(detailed, detailedEvent)))
            player.Enqueue(clip);
        player.Advance(TimeSpan.FromDays(1));

        Assert.False(player.IsPlaying);
        Assert.Equal(simple.FindGang(new GangId(10))!.Force, detailed.FindGang(new GangId(10))!.Force);
        Assert.Equal(simple.FindGang(new GangId(20))!.Force, detailed.FindGang(new GangId(20))!.Force);
        Assert.Equal(simple.LastPhaseResolutions[0].Event!.Resolution!.Rolls,
            detailed.LastPhaseResolutions[0].Event!.Resolution!.Rolls);
        Assert.Equal(simple.LastPhaseResolutions[0].Event!.Resolution!.RetaliationRolls,
            detailed.LastPhaseResolutions[0].Event!.Resolution!.RetaliationRolls);
        Assert.Equal(simple.PhaseHashes[^1].Fingerprint, detailed.PhaseHashes[^1].Fingerprint);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(simple), MatchStateHasher.ComputeFingerprint(detailed));
    }

    /// <summary>A match whose attacker destroys the defender in one pass.</summary>
    private static MatchState KillingMatch(bool secondPlayerOneGang = false)
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs
            .OrderBy(gang => gang.Stats.Defense)
            .ThenByDescending(gang => gang.Stats.Combat + gang.Stats.Strength + gang.Stats.Blade)
            .First().Id;
        var weapon = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type == 1)
            .OrderByDescending(value => value.item.Stats.Combat)
            .First().index;
        return CreateMatch(
            playerZeroDefinition: attackerDefinition,
            playerZeroWeapon: checked((short)weapon),
            playerOneForce: 1,
            secondPlayerOneGang: secondPlayerOneGang);
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
        short? influencedSiteDefinition = null,
        bool secondPlayerZeroGang = false,
        bool secondPlayerOneGang = false,
        string playerZeroName = "SMGHUBBLE",
        string playerOneName = "SMGHUBBLE")
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), playerZeroName, PlayerController.Human),
            new(new PlayerId(1), playerOneName, PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        var playerZeroGangs = new List<MatchGangState>
        {
            new(new GangId(10), new PlayerId(0), playerZeroDefinition, 0,
                playerZeroForce, weaponItemId: playerZeroWeapon)
        };
        if (secondPlayerZeroGang)
            playerZeroGangs.Add(new MatchGangState(
                new GangId(11), new PlayerId(0), playerZeroDefinition, 0, playerZeroForce));
        var playerOneGangs = new List<MatchGangState>
        {
            new(new GangId(20), new PlayerId(1), playerOneDefinition, 0,
                playerOneForce, playerOneWeapon, playerOneArmor, playerOneMiscellaneous)
        };
        if (secondPlayerOneGang)
            playerOneGangs.Add(new MatchGangState(
                new GangId(21), new PlayerId(1), playerOneDefinition, 1, playerOneForce));
        MatchPlayerState[] players =
        [
            new(setups[0], 500, playerZeroGangs),
            new(setups[1], 500, playerOneGangs)
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id == 0 && influencedSiteDefinition is { } site ? site : (short)0,
                    id == 0 && influencedSiteDefinition is not null ? 0 : 7,
                    id == 0 && influencedSiteDefinition is not null ? new PlayerId(0) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 && influencedSiteDefinition is not null
                ? new PlayerId(0)
                : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
