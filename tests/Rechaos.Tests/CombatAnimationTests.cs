using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatAnimationTests
{
    [Fact]
    public void EquippedAttackAndRetaliationUseRecordedWeaponAnimationPairs()
    {
        var state = CreateState();
        var gameEvent = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            ItemId: 0,
            RetaliationRolls: [4],
            RetaliationItemId: 1,
            Damage: 1,
            RetaliationDamage: 1));

        var clips = CombatAnimationRouting.ForEvent(state, gameEvent);

        Assert.Equal(2, clips.Count);
        Assert.Equal((short)3, clips[0].AttackAnimation);
        Assert.Equal((short)2, clips[0].HitAnimation);
        Assert.Equal(state.Definitions.Items[0].Sound, clips[0].Sound);
        Assert.False(clips[0].Reversed);
        Assert.Equal((short)4, clips[1].AttackAnimation);
        Assert.Equal((short)3, clips[1].HitAnimation);
        Assert.Equal(state.Definitions.Items[1].Sound, clips[1].Sound);
        Assert.True(clips[1].Reversed);
    }

    /// <summary>
    /// Whichever side the viewer is on, the gang whose order it was strikes first and its target
    /// answers second: a viewer attacked sees the enemy's blow before their own retaliation, and a
    /// viewer attacking sees their own blow before the enemy's.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AttackPlaysBeforeRetaliationFromEitherSide(int attackingPlayer)
    {
        var state = CreateState();
        var attacker = new GangId(attackingPlayer == 0 ? 10 : 20);
        var defender = new GangId(attackingPlayer == 0 ? 20 : 10);
        var gameEvent = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            RetaliationRolls: [4], Damage: 1, RetaliationDamage: 1),
            new PlayerId(attackingPlayer), attacker, defender);

        var player = new CombatAnimationPlayer();
        foreach (var clip in CombatAnimationRouting.ForEvent(state, gameEvent)) player.Enqueue(clip);
        var started = player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.CompletionTick + CombatAnimationRouting.FirstAnimationTick)
            * CombatAnimationRouting.FrameMilliseconds));

        Assert.Collection(started,
            clip =>
            {
                Assert.Equal(attacker, clip.Attacker);
                Assert.Equal(defender, clip.Defender);
                Assert.False(clip.Reversed);
            },
            clip =>
            {
                Assert.Equal(defender, clip.Attacker);
                Assert.Equal(attacker, clip.Defender);
                Assert.True(clip.Reversed);
            });
    }

    [Fact]
    public void EvasionAndDetectedPoliceUseRecoveredPairedSheets()
    {
        var state = CreateState();
        var evaded = Assert.Single(CombatAnimationRouting.ForEvent(state,
            AttackEvent(new CommandResolutionDetails(CommandResolutionCode.TargetEvaded, [], 0))));
        Assert.Equal(CombatAnimationRouting.EvadedAnimation, evaded.AttackAnimation);
        Assert.Equal((short)0, evaded.HitAnimation);
        Assert.Null(evaded.Sound);

        var policeEvent = new GameEvent(
            2, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.PoliceAttackResolved, new PlayerId(1), new GangId(20),
            GangAction.None, CommandTarget.Sector(0),
            PoliceAttack: new PoliceAttackResolutionDetails(0, 100, 1, true, 20, 0, [4], 1, 1, 10, 9));
        var police = Assert.Single(CombatAnimationRouting.ForEvent(state, policeEvent));
        Assert.Equal((short)28, police.AttackAnimation);
        Assert.Equal((short)20, police.HitAnimation);
        Assert.True(police.Reversed);
        Assert.True(police.Police);
        Assert.Equal(AudioRouting.PoliceSound, police.Sound);
    }

    [Theory]
    [InlineData((short)0, (short)0, (short)2)]
    [InlineData((short)54, (short)1, (short)18)]
    public void UnarmedStyleSelectsOrdinaryOrMartialArtsPair(
        short gangDefinition,
        short expectedAttack,
        short expectedHit)
    {
        var clip = Assert.Single(CombatAnimationRouting.ForEvent(
            CreateState(gangDefinition),
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, Damage: 1))));

        Assert.Equal(expectedAttack, clip.AttackAnimation);
        Assert.Equal(expectedHit, clip.HitAnimation);
    }

    [Fact]
    public void ZeroDamageUsesNoDamageHitSheetForWeaponsUnarmedAndPolice()
    {
        var state = CreateState();
        var weapon = Assert.Single(CombatAnimationRouting.ForEvent(state,
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, ItemId: 0, Damage: 0))));
        var unarmed = Assert.Single(CombatAnimationRouting.ForEvent(state,
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, Damage: 0))));
        var police = Assert.Single(CombatAnimationRouting.ForEvent(state, new GameEvent(
            2, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.PoliceAttackResolved, new PlayerId(1), new GangId(20),
            GangAction.None, CommandTarget.Sector(0),
            PoliceAttack: new PoliceAttackResolutionDetails(0, 100, 1, true, 20, 0, [], 0, 0, 10, 10))));

        Assert.Equal((short)1, weapon.HitAnimation);
        Assert.Equal((short)1, unarmed.HitAnimation);
        Assert.Equal((short)1, police.HitAnimation);
    }

    [Fact]
    public void UndetectedPoliceHasNoDetailedCombatPresentation()
    {
        var state = CreateState();
        var policeEvent = new GameEvent(
            2, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.PoliceAttackResolved, new PlayerId(1), new GangId(20),
            GangAction.None, CommandTarget.Sector(0),
            PoliceAttack: new PoliceAttackResolutionDetails(0, 0, 20, false, 20, 0, [], 0, 0, 10, 10));

        Assert.Empty(CombatAnimationRouting.ForEvent(state, policeEvent));
    }

    [Fact]
    public void EventGangMissingFromCurrentStateDoesNotCrashCombatPresentation()
    {
        var state = CreateState();
        var gameEvent = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, Damage: 10)) with
        {
            Gang = new GangId(999),
        };

        Assert.Empty(CombatAnimationRouting.ForEvent(state, gameEvent));
        Assert.Null(AudioRouting.CombatSound(state, gameEvent));
    }

    [Fact]
    public void SecondAttackOnTheSameGangStartsWhereTheFirstLeftIt()
    {
        var state = CreateState();
        var target = state.FindGang(new GangId(20))!;
        target.Force = 0;
        var first = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, PreviousValue: 10, ResultValue: 0, Damage: 10));
        var second = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, PreviousValue: 10, ResultValue: 0, Damage: 8)) with
        {
            Sequence = 2,
            Gang = new GangId(11),
        };
        GameEvent[] events = [first, second];

        var firstForces = CombatForceTimeline.For(state, events, first)
            .Forces(first.Sequence, retaliation: false, new GangId(10), target.Id);
        var secondForces = CombatForceTimeline.For(state, events, second)
            .Forces(second.Sequence, retaliation: false, new GangId(11), target.Id);

        Assert.Equal(new CombatClipForces(10, 0, 10), firstForces);
        Assert.Equal((0, 0), (secondForces.DefenderBefore, secondForces.DefenderAfter));
        Assert.Equal(0, secondForces.DefenderDamage);
    }

    [Fact]
    public void ForcesFollowAttackThenRetaliationThenPoliceThroughThePhase()
    {
        var state = CreateState();
        state.FindGang(new GangId(10))!.Force = 4;
        state.FindGang(new GangId(20))!.Force = 3;
        var attack = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, PreviousValue: 10, ResultValue: 3,
            RetaliationRolls: [6], Damage: 5, RetaliationDamage: 6));
        var police = new GameEvent(
            2, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.PoliceAttackResolved, new PlayerId(1), new GangId(20),
            GangAction.None, CommandTarget.Sector(0),
            PoliceAttack: new PoliceAttackResolutionDetails(0, 100, 1, true, 20, 0, [4, 4], 2, 2, 10, 3));
        GameEvent[] events = [attack, police];
        var timeline = CombatForceTimeline.For(state, events, attack);

        Assert.Equal(new CombatClipForces(10, 5, 10),
            timeline.Forces(1, retaliation: false, new GangId(10), new GangId(20)));
        // The attacker entered with its current force plus every hit it took, since no event targets it.
        Assert.Equal(new CombatClipForces(10, 4, 5),
            timeline.Forces(1, retaliation: true, new GangId(20), new GangId(10)));
        Assert.Equal(new CombatClipForces(5, 3, null),
            timeline.Forces(2, retaliation: false, null, new GangId(20)));
    }

    [Fact]
    public void RoutedClipsCarryTheirPlaceInThePhase()
    {
        var state = CreateState();
        state.FindGang(new GangId(10))!.Force = 8;
        state.FindGang(new GangId(20))!.Force = 7;
        var clips = CombatAnimationRouting.ForEvent(state, AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, PreviousValue: 10, ResultValue: 7,
            RetaliationRolls: [6], Damage: 3, RetaliationDamage: 2)));

        Assert.Equal(new CombatClipForces(10, 7, 10), clips[0].Forces);
        Assert.Equal(new CombatClipForces(10, 8, 7), clips[1].Forces);
    }

    [Fact]
    public void FileGroupsAndFrameCoordinatesMatchRecoveredStrips()
    {
        Assert.Equal(166, CombatAnimationRouting.FrameMilliseconds);
        Assert.Equal("PX07003.bmp", CombatAnimationRouting.AttackFile(3, false));
        Assert.Equal("PX07102.bmp", CombatAnimationRouting.HitFile(2, false));
        Assert.Equal("PX07228.bmp", CombatAnimationRouting.AttackFile(28, true));
        Assert.Equal("PX07320.bmp", CombatAnimationRouting.HitFile(20, true));
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(448, 0, 64, 64),
            CombatAnimationRouting.FrameSource(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatAnimationRouting.FrameSource(8));
    }

    [Fact]
    public void PlayerRunsRecoveredAnimationBlinkAndHoldTimelineInQueuedOrder()
    {
        var player = new CombatAnimationPlayer();
        var first = new CombatAnimationClip(
            1, new GangId(10), new GangId(20), 3, 2, false, Sound: 5);
        var second = new CombatAnimationClip(
            1, new GangId(20), new GangId(10), 4, 3, true, Sound: 8);
        player.Enqueue(first);
        player.Enqueue(second);

        Assert.Empty(player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.FirstAnimationTick - 1)
            * CombatAnimationRouting.FrameMilliseconds)));
        var firstStarted = player.Advance(TimeSpan.FromMilliseconds(
            CombatAnimationRouting.FrameMilliseconds));
        Assert.Equal([first], firstStarted);
        player.Advance(TimeSpan.FromMilliseconds(
            7 * CombatAnimationRouting.FrameMilliseconds));
        Assert.Equal(first, player.Active);
        Assert.Equal(7, player.Frame);
        Assert.True(player.ShowsPreDamageForce);
        player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.FirstDamageFlashTick - player.TimelineTick)
            * CombatAnimationRouting.FrameMilliseconds));
        Assert.True(player.ShowsDamageFlash);
        player.Advance(TimeSpan.FromMilliseconds(CombatAnimationRouting.FrameMilliseconds));
        Assert.False(player.ShowsDamageFlash);
        player.Advance(TimeSpan.FromMilliseconds(CombatAnimationRouting.FrameMilliseconds));
        Assert.True(player.ShowsDamageFlash);
        player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.CompletionTick - player.TimelineTick)
            * CombatAnimationRouting.FrameMilliseconds));
        Assert.Equal(second, player.Active);
        Assert.Equal(0, player.Frame);
        Assert.Equal(0, player.TimelineTick);
        Assert.Empty(player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.FirstAnimationTick - 1)
            * CombatAnimationRouting.FrameMilliseconds)));
        var secondStarted = player.Advance(TimeSpan.FromMilliseconds(
            CombatAnimationRouting.FrameMilliseconds));
        Assert.Equal([second], secondStarted);
        player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.CompletionTick - CombatAnimationRouting.FirstAnimationTick)
            * CombatAnimationRouting.FrameMilliseconds));
        Assert.False(player.IsPlaying);
        Assert.Null(player.Active);
    }

    [Fact]
    public void PlayerCanClearAQueuedDetailedPresentationWithoutAdvancingSimulation()
    {
        var player = new CombatAnimationPlayer();
        player.Enqueue(new CombatAnimationClip(1, new GangId(10), new GangId(20), 3, 2, false));
        player.Enqueue(new CombatAnimationClip(1, new GangId(20), new GangId(10), 4, 3, true));

        player.Advance(TimeSpan.FromMilliseconds(500));
        player.Clear();

        Assert.False(player.IsPlaying);
        Assert.Null(player.Active);
        Assert.Equal(0, player.TimelineTick);
        player.Advance(TimeSpan.FromDays(1));
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void LargeElapsedIntervalCompletesDetailedQueueWithoutHanging()
    {
        var player = new CombatAnimationPlayer();
        for (var index = 0; index < 1_000; index++)
            player.Enqueue(new CombatAnimationClip(index, new GangId(10), new GangId(20), 3, 2, false));

        player.Advance(TimeSpan.FromDays(1));

        Assert.False(player.IsPlaying);
        Assert.Null(player.Active);
    }

    private static GameEvent AttackEvent(CommandResolutionDetails resolution) =>
        AttackEvent(resolution, new PlayerId(0), new GangId(10), new GangId(20));

    private static GameEvent AttackEvent(
        CommandResolutionDetails resolution,
        PlayerId player,
        GangId attacker,
        GangId defender) => new(
        1, 1, TurnPhase.Execution, ExecutionPhase.Combat,
        resolution.Code == CommandResolutionCode.Resolved
            ? GameEventKind.CommandResolved
            : GameEventKind.CommandFailed,
        player, attacker, GangAction.Attack,
        CommandTarget.Gang(defender), Resolution: resolution);

    private static MatchState CreateState(short attackerDefinition = 0)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human)
        ];
        var players = new[]
        {
            new MatchPlayerState(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, attackerDefinition, 0, 10)]),
            new MatchPlayerState(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 1, 0, 10)])
        };
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ]))
            .ToArray();
        return new MatchState(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1, setups),
            players, sectors);
    }
}
