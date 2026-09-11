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
            RetaliationItemId: 1));

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

    [Fact]
    public void EvasionAndDetectedPoliceUseRecoveredExtraSheets()
    {
        var state = CreateState();
        var evaded = Assert.Single(CombatAnimationRouting.ForEvent(state,
            AttackEvent(new CommandResolutionDetails(CommandResolutionCode.TargetEvaded, [], 0))));
        Assert.Equal(CombatAnimationRouting.EvadedAnimation, evaded.AttackAnimation);
        Assert.Null(evaded.HitAnimation);
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
    [InlineData((short)0, (short)0)]
    [InlineData((short)25, (short)1)]
    [InlineData((short)54, (short)2)]
    public void UnarmedStyleSelectsStrengthFightingOrMartialArtsSheet(
        short gangDefinition,
        short expectedAnimation)
    {
        var clip = Assert.Single(CombatAnimationRouting.ForEvent(
            CreateState(gangDefinition),
            AttackEvent(new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0))));

        Assert.Equal(expectedAnimation, clip.AttackAnimation);
        Assert.Equal(expectedAnimation, clip.HitAnimation);
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
