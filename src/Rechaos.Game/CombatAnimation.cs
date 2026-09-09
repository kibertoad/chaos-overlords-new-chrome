using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed record CombatAnimationClip(
    long EventSequence,
    GangId? Attacker,
    GangId Defender,
    short AttackAnimation,
    short? HitAnimation,
    bool Reversed,
    bool Police = false);

public static class CombatAnimationRouting
{
    public const int FrameCount = 8;
    public const int FrameSize = 64;
    public const int FrameMilliseconds = 90;
    public const short EvadedAnimation = 27;
    public const short PoliceAttackAnimation = 28;
    public const short PoliceHitAnimation = 20;

    public static IReadOnlyList<CombatAnimationClip> ForEvent(MatchState state, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved
            && gameEvent.Gang is { } policeTarget
            && gameEvent.PoliceAttack is { } police)
        {
            return police.Detected
                ? [new CombatAnimationClip(gameEvent.Sequence, null, policeTarget,
                    PoliceAttackAnimation, PoliceHitAnimation, Reversed: true, Police: true)]
                : [new CombatAnimationClip(gameEvent.Sequence, null, policeTarget,
                    EvadedAnimation, null, Reversed: true, Police: true)];
        }
        if (gameEvent.Action != GangAction.Attack
            || gameEvent.Gang is not { } attacker
            || gameEvent.Target.Kind != CommandTargetKind.Gang
            || gameEvent.Resolution is not { } resolution)
            return [];

        var defender = new GangId(gameEvent.Target.Id);
        if (resolution.Code == CommandResolutionCode.TargetEvaded)
            return [new CombatAnimationClip(gameEvent.Sequence, attacker, defender,
                EvadedAnimation, null, Reversed: false)];
        if (resolution.Code != CommandResolutionCode.Resolved) return [];

        var attack = AnimationPair(state, attacker, resolution.ItemId);
        var clips = new List<CombatAnimationClip>(2)
        {
            new(gameEvent.Sequence, attacker, defender, attack.Attack, attack.Hit, Reversed: false)
        };
        if (resolution.RetaliationRolls is { Count: > 0 })
        {
            var retaliation = AnimationPair(state, defender, resolution.RetaliationItemId);
            clips.Add(new CombatAnimationClip(
                gameEvent.Sequence, defender, attacker,
                retaliation.Attack, retaliation.Hit, Reversed: true));
        }
        return clips;
    }

    public static string AttackFile(short animation, bool reversed)
    {
        ValidateAttack(animation, reversed);
        return $"PX07{(reversed ? 2 : 0)}{animation:00}.bmp";
    }

    public static string HitFile(short animation, bool reversed)
    {
        ValidateHit(animation, reversed);
        return $"PX07{(reversed ? 3 : 1)}{animation:00}.bmp";
    }

    public static Rectangle FrameSource(int frame)
    {
        if (frame is < 0 or >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return new Rectangle(frame * FrameSize, 0, FrameSize, FrameSize);
    }

    private static (short Attack, short Hit) AnimationPair(
        MatchState state,
        GangId gangId,
        short? itemId)
    {
        if (itemId is { } equipped)
        {
            if (equipped < 0 || equipped >= state.Definitions.Items.Count)
                throw new ArgumentOutOfRangeException(nameof(itemId));
            var item = state.Definitions.Items[equipped];
            return (item.AttackAnimation, item.HitAnimation);
        }
        var gang = state.FindGang(gangId)
            ?? throw new ArgumentOutOfRangeException(nameof(gangId));
        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        var animation = UnarmedAnimation(definition);
        return (animation, animation);
    }

    private static short UnarmedAnimation(GangDefinition gang)
    {
        if (gang.Stats.MartialArts > gang.Stats.Fighting
            && gang.Stats.MartialArts > gang.Stats.Strength)
            return 2;
        return gang.Stats.Fighting > gang.Stats.Strength ? (short)1 : (short)0;
    }

    private static void ValidateAttack(short animation, bool reversed)
    {
        var maximum = reversed ? 28 : 27;
        if (animation is < 0 || animation > maximum)
            throw new ArgumentOutOfRangeException(nameof(animation));
    }

    private static void ValidateHit(short animation, bool reversed)
    {
        var maximum = reversed ? 20 : 19;
        if (animation is < 0 || animation > maximum)
            throw new ArgumentOutOfRangeException(nameof(animation));
    }
}

public sealed class CombatAnimationPlayer
{
    private readonly Queue<CombatAnimationClip> _queue = [];
    private double _elapsedMilliseconds;

    public CombatAnimationClip? Active { get; private set; }
    public int Frame { get; private set; }
    public bool IsPlaying => Active is not null;

    public void Enqueue(CombatAnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (Active is null)
        {
            Active = clip;
            Frame = 0;
            _elapsedMilliseconds = 0;
        }
        else
        {
            _queue.Enqueue(clip);
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (Active is null) return;
        _elapsedMilliseconds += elapsed.TotalMilliseconds;
        while (Active is not null && _elapsedMilliseconds >= CombatAnimationRouting.FrameMilliseconds)
        {
            _elapsedMilliseconds -= CombatAnimationRouting.FrameMilliseconds;
            Frame++;
            if (Frame < CombatAnimationRouting.FrameCount) continue;
            Active = _queue.Count > 0 ? _queue.Dequeue() : null;
            Frame = 0;
            if (Active is null) _elapsedMilliseconds = 0;
        }
    }

    public void Clear()
    {
        _queue.Clear();
        Active = null;
        Frame = 0;
        _elapsedMilliseconds = 0;
    }
}
