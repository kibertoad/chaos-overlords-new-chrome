using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatAnimationTests
{
    [Fact]
    public void RetaliationPlaysNoClipOfItsOwn()
    {
        var state = CreateState();
        var gameEvent = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            ItemId: 0,
            RetaliationRolls: [4],
            RetaliationItemId: 1,
            Damage: 1,
            RetaliationDamage: 1));

        var clip = Assert.Single(Clips(state, gameEvent, new PlayerId(0)));

        Assert.Equal(new GangId(10), clip.Attacker);
        Assert.Equal((short)3, clip.AttackAnimation);
        Assert.Equal((short)2, clip.HitAnimation);
        Assert.Equal(state.Definitions.Items[0].Sound, clip.Sound);
        Assert.False(clip.Reversed);
        Assert.Equal(1, clip.Forces.AttackerDamage);
    }

    [Fact]
    public void AttackOnTheViewersGangPlaysTheMirroredPairWithTheAttackersCue()
    {
        var state = CreateState();
        var gameEvent = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            ItemId: 0,
            RetaliationRolls: [4],
            RetaliationItemId: 1,
            Damage: 1,
            RetaliationDamage: 1));

        var clip = Assert.Single(Clips(state, gameEvent, new PlayerId(1)));

        Assert.True(clip.Reversed);
        Assert.Equal(new GangId(10), clip.Attacker);
        Assert.Equal(new GangId(20), clip.Defender);
        Assert.Equal((short)3, clip.AttackAnimation);
        Assert.Equal((short)2, clip.HitAnimation);
        Assert.Equal(state.Definitions.Items[0].Sound, clip.Sound);
        Assert.Equal("PX07203.bmp", CombatAnimationRouting.AttackFile(clip.AttackAnimation, clip.Reversed));
        Assert.Equal("PX07302.bmp", CombatAnimationRouting.HitFile(clip.HitAnimation!.Value, clip.Reversed));

        var evaded = Assert.Single(Clips(state,
            AttackEvent(new CommandResolutionDetails(CommandResolutionCode.TargetEvaded, [], 0)),
            new PlayerId(1)));
        Assert.True(evaded.Reversed);
    }

    [Fact]
    public void EvasionAndDetectedPoliceUseRecoveredPairedSheets()
    {
        var state = CreateState();
        var evaded = Assert.Single(Clips(state,
            AttackEvent(new CommandResolutionDetails(CommandResolutionCode.TargetEvaded, [], 0)), Attacker));
        Assert.Equal(CombatAnimationRouting.EvadedAnimation, evaded.AttackAnimation);
        Assert.Equal((short)0, evaded.HitAnimation);
        Assert.Null(evaded.Sound);

        var policeEvent = new GameEvent(
            2, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.PoliceAttackResolved, new PlayerId(1), new GangId(20),
            GangAction.None, CommandTarget.Sector(0),
            PoliceAttack: new PoliceAttackResolutionDetails(0, 100, 1, true, 20, 0, [4], 1, 1, 10, 9));
        var police = Assert.Single(Clips(state, policeEvent, Defender));
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
        var clip = Assert.Single(Clips(
            CreateState(gangDefinition),
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, Damage: 1)),
            Attacker));

        Assert.Equal(expectedAttack, clip.AttackAnimation);
        Assert.Equal(expectedHit, clip.HitAnimation);
    }

    [Fact]
    public void ZeroDamageUsesNoDamageHitSheetForWeaponsUnarmedAndPolice()
    {
        var state = CreateState();
        var weapon = Assert.Single(Clips(state,
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, ItemId: 0, Damage: 0)), Attacker));
        var unarmed = Assert.Single(Clips(state,
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, Damage: 0)), Attacker));
        var police = Assert.Single(Clips(state, new GameEvent(
            2, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.PoliceAttackResolved, new PlayerId(1), new GangId(20),
            GangAction.None, CommandTarget.Sector(0),
            PoliceAttack: new PoliceAttackResolutionDetails(0, 100, 1, true, 20, 0, [], 0, 0, 10, 10)), Defender));

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

        Assert.Empty(Clips(state, policeEvent, Defender));
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

        Assert.Empty(Clips(state, gameEvent, Attacker));
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
            .Forces(first.Sequence, new GangId(10), target.Id);
        var secondForces = CombatForceTimeline.For(state, events, second)
            .Forces(second.Sequence, new GangId(11), target.Id);

        Assert.Equal(new CombatClipForces(10, 0, 10, 10), firstForces);
        Assert.Equal((0, 0), (secondForces.DefenderBefore, secondForces.DefenderAfter));
        Assert.Equal(0, secondForces.DefenderDamage);
    }

    [Fact]
    public void ForcesFollowAttackWithItsRetaliationThenPoliceThroughThePhase()
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

        // The attacker entered with its current force plus every hit it took, since no event
        // targets it, and loses its retaliation in the attack's own clip.
        Assert.Equal(new CombatClipForces(10, 5, 10, 4),
            timeline.Forces(1, new GangId(10), new GangId(20)));
        Assert.Equal(new CombatClipForces(5, 3, null, null),
            timeline.Forces(2, null, new GangId(20)));
    }

    [Fact]
    public void RoutedClipsCarryTheirPlaceInThePhase()
    {
        var state = CreateState();
        state.FindGang(new GangId(10))!.Force = 8;
        state.FindGang(new GangId(20))!.Force = 7;
        var clip = Assert.Single(Clips(state, AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, PreviousValue: 10, ResultValue: 7,
            RetaliationRolls: [6], Damage: 3, RetaliationDamage: 2)), Attacker));

        Assert.Equal(new CombatClipForces(10, 7, 10, 8), clip.Forces);
    }

    [Fact]
    public void OnePhaseTimelineRoutesEveryEventOfThatPhase()
    {
        var state = CreateState();
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

        Assert.True(timeline.Covers(police));
        var policeClip = Assert.Single(
            CombatAnimationRouting.ForEvent(state, police, Defender, timeline));
        Assert.Equal(new CombatClipForces(5, 3, null, null), policeClip.Forces);
    }

    [Fact]
    public void RoutingRefusesATimelineOfAnotherPhase()
    {
        var state = CreateState();
        var attack = AttackEvent(new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0, Damage: 1));
        var later = attack with { Sequence = 2, Turn = 2 };
        GameEvent[] events = [attack, later];

        var timeline = CombatForceTimeline.For(state, events, attack);

        Assert.False(timeline.Covers(later));
        Assert.Throws<ArgumentException>(() =>
            CombatAnimationRouting.ForEvent(state, later, Attacker, timeline));
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
            1, new GangId(10), new GangId(20), 3, 2, false, default, Sound: 5);
        var second = new CombatAnimationClip(
            2, new GangId(20), new GangId(10), 4, 3, true, default, Sound: 8);
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
        player.Enqueue(new CombatAnimationClip(1, new GangId(10), new GangId(20), 3, 2, false, default));
        player.Enqueue(new CombatAnimationClip(2, new GangId(20), new GangId(10), 4, 3, true, default));

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
            player.Enqueue(new CombatAnimationClip(index, new GangId(10), new GangId(20), 3, 2, false, default));

        player.Advance(TimeSpan.FromDays(1));

        Assert.False(player.IsPlaying);
        Assert.Null(player.Active);
    }

    [Fact]
    public void PresentationWalksTheViewersGangsBySectorThenOwnAttackReplyIncomingAndPolice()
    {
        var state = CreateState();
        GameEvent Attack(long sequence, int gang, int owner, int target, int targetOwner, int sector) =>
            AttackEvent(new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, Damage: 1,
                Attacker: new CombatantDetails(new PlayerId(owner), 0, sector, null, null, null),
                Defender: new CombatantDetails(new PlayerId(targetOwner), 0, sector, null, null, null))) with
            {
                Sequence = sequence,
                Player = new PlayerId(owner),
                Gang = new GangId(gang),
                Target = CommandTarget.Gang(new GangId(target)),
            };
        GameEvent[] phase =
        [
            Attack(1, 10, 0, 20, 1, sector: 5),
            Attack(2, 20, 1, 10, 0, sector: 5),
            Attack(3, 21, 1, 10, 0, sector: 5),
            Attack(4, 22, 1, 11, 0, sector: 2),
            new GameEvent(
                5, 1, TurnPhase.Execution, ExecutionPhase.Combat,
                GameEventKind.PoliceAttackResolved, new PlayerId(0), new GangId(10),
                GangAction.None, CommandTarget.Sector(5),
                PoliceAttack: new PoliceAttackResolutionDetails(5, 100, 1, true, 20, 0, [4], 1, 1, 10, 9)),
        ];

        var presented = CombatPresentationOrder.Order(state, phase, Attacker);

        // Gang 11 fights in sector 2, so it plays before gang 10 in sector 5. Gang 10's own attack
        // hands off to gang 20's reply, then the other attack on it and the police follow.
        Assert.Equal([4L, 1L, 2L, 3L, 5L], presented.Select(entry => entry.Event.Sequence).ToArray());
        Assert.Equal([false, true, false, false, false],
            presented.Select(entry => entry.HandsOff).ToArray());
    }

    [Fact]
    public void AClipThatHandsOffEndsOnTheFinalResultTick()
    {
        var player = new CombatAnimationPlayer();
        var forces = new CombatClipForces(10, 9, 10, 8);
        var attack = new CombatAnimationClip(
            1, new GangId(10), new GangId(20), 3, 2, false, forces, HandsOff: true);
        var reply = new CombatAnimationClip(2, new GangId(20), new GangId(10), 3, 2, true, forces);
        player.Enqueue(attack);
        player.Enqueue(reply);

        player.Advance(TimeSpan.FromMilliseconds(
            (CombatAnimationRouting.FinalResultTick - 1) * CombatAnimationRouting.FrameMilliseconds));
        Assert.Equal(attack, player.Active);
        player.Advance(TimeSpan.FromMilliseconds(CombatAnimationRouting.FrameMilliseconds));

        Assert.Equal(reply, player.Active);
        Assert.Equal(CombatAnimationRouting.CompletionTick, reply.CompletionTick);
    }

    private static readonly PlayerId Attacker = new(0);
    private static readonly PlayerId Defender = new(1);

    private static IReadOnlyList<CombatAnimationClip> Clips(
        MatchState state,
        GameEvent gameEvent,
        PlayerId viewer) =>
        CombatAnimationRouting.ForEvent(state, gameEvent, viewer, CombatForceTimeline.For(state, gameEvent));

    private static GameEvent AttackEvent(CommandResolutionDetails resolution) => new(
        1, 1, TurnPhase.Execution, ExecutionPhase.Combat,
        resolution.Code == CommandResolutionCode.Resolved
            ? GameEventKind.CommandResolved
            : GameEventKind.CommandFailed,
        new PlayerId(0), new GangId(10), GangAction.Attack,
        CommandTarget.Gang(new GangId(20)), Resolution: resolution);

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
