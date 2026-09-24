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
    bool Police = false,
    short? Sound = null,
    CombatClipForces? Forces = null);

public static class CombatAnimationRouting
{
    public const int FrameCount = 8;
    public const int FrameSize = 64;
    public const int FrameMilliseconds = 166;
    public const int FirstAnimationTick = 3;
    public const int LastAnimationTick = 10;
    public const int PreDamageTick = 12;
    public const int FirstDamageFlashTick = 13;
    public const int SecondDamageFlashTick = 15;
    public const int FinalResultTick = 16;
    public const int CompletionTick = 22;
    public const short EvadedAnimation = 27;
    public const short PoliceAttackAnimation = 28;
    public const short PoliceHitAnimation = 20;

    /// <param name="combatants">
    /// Resolves gangs retired since the event; without it their fights have no clips.
    /// </param>
    public static IReadOnlyList<CombatAnimationClip> ForEvent(
        MatchState state,
        GameEvent gameEvent,
        CombatantHistory? combatants = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved
            && gameEvent.Gang is { } policeTarget
            && gameEvent.PoliceAttack is { } police)
        {
            if (!police.Detected) return [];
            var policeForces = CombatForceTimeline.For(state, gameEvent)
                .Forces(gameEvent.Sequence, retaliation: false, null, policeTarget);
            return [new CombatAnimationClip(gameEvent.Sequence, null, policeTarget,
                PoliceAttackAnimation, HitAnimation(PoliceHitAnimation, police.Damage),
                Reversed: true, Police: true, Sound: AudioRouting.PoliceSound,
                Forces: policeForces)];
        }
        if (gameEvent.Action != GangAction.Attack
            || gameEvent.Gang is not { } attacker
            || gameEvent.Target.Kind != CommandTargetKind.Gang
            || gameEvent.Resolution is not { } resolution)
            return [];

        var defender = new GangId(gameEvent.Target.Id);
        // Presentation can encounter an event whose gang is absent from the state being drawn: a
        // hire in the same turn reuses the slot of a gang the fight wiped out, which the history
        // answers for, and a restored history or a client-side divergence can still pair an event
        // with a gang nothing remembers. Detailed combat is optional, so omit the clip instead of
        // turning an existing synchronization problem into a game crash.
        if (state.FindCombatant(attacker, combatants) is not { } attackingGang
            || state.FindCombatant(defender, combatants) is not { } defendingGang)
            return [];
        if (resolution.Code == CommandResolutionCode.TargetEvaded)
            return [new CombatAnimationClip(gameEvent.Sequence, attacker, defender,
                EvadedAnimation, 0, Reversed: false)];
        if (resolution.Code != CommandResolutionCode.Resolved) return [];

        var timeline = CombatForceTimeline.For(state, gameEvent);
        var attack = AnimationPair(state, attackingGang, resolution.ItemId, resolution.Damage);
        var clips = new List<CombatAnimationClip>(2)
        {
            new(gameEvent.Sequence, attacker, defender, attack.Attack, attack.Hit,
                Reversed: false,
                Sound: AudioRouting.GangAttackSound(state, attackingGang, resolution.ItemId),
                Forces: timeline.Forces(gameEvent.Sequence, retaliation: false, attacker, defender))
        };
        if (resolution.RetaliationRolls is { Count: > 0 })
        {
            var retaliation = AnimationPair(
                state, defendingGang, resolution.RetaliationItemId, resolution.RetaliationDamage);
            clips.Add(new CombatAnimationClip(
                gameEvent.Sequence, defender, attacker,
                retaliation.Attack, retaliation.Hit, Reversed: true,
                Sound: AudioRouting.GangAttackSound(
                    state, defendingGang, resolution.RetaliationItemId),
                Forces: timeline.Forces(gameEvent.Sequence, retaliation: true, defender, attacker)));
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
        MatchGangState gang,
        short? itemId,
        int damage)
    {
        if (itemId is { } equipped)
        {
            if (equipped < 0 || equipped >= state.Definitions.Items.Count)
                throw new ArgumentOutOfRangeException(nameof(itemId));
            var item = state.Definitions.Items[equipped];
            return (item.AttackAnimation, HitAnimation(item.HitAnimation, damage));
        }
        var definition = state.Definitions.Gang(gang.DefinitionId);
        var martialArts = definition.Stats.MartialArts > 0;
        return (
            martialArts ? (short)1 : (short)0,
            HitAnimation(martialArts ? (short)18 : (short)2, damage));
    }

    private static short HitAnimation(short animation, int damage) =>
        damage > 0 ? animation : (short)1;

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
    public int TimelineTick { get; private set; }
    public int Frame => Math.Clamp(
        TimelineTick - CombatAnimationRouting.FirstAnimationTick,
        0,
        CombatAnimationRouting.FrameCount - 1);
    public bool ShowsPreDamageForce => TimelineTick <= CombatAnimationRouting.PreDamageTick;
    public bool ShowsDamageFlash => TimelineTick is
        CombatAnimationRouting.FirstDamageFlashTick or
        CombatAnimationRouting.SecondDamageFlashTick;
    public bool IsPlaying => Active is not null;

    public void Enqueue(CombatAnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (Active is null)
        {
            Active = clip;
            TimelineTick = 0;
            _elapsedMilliseconds = 0;
        }
        else
        {
            _queue.Enqueue(clip);
        }
    }

    public IReadOnlyList<CombatAnimationClip> Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (Active is null) return [];
        List<CombatAnimationClip>? started = null;
        _elapsedMilliseconds += elapsed.TotalMilliseconds;
        while (Active is not null && _elapsedMilliseconds >= CombatAnimationRouting.FrameMilliseconds)
        {
            _elapsedMilliseconds -= CombatAnimationRouting.FrameMilliseconds;
            TimelineTick++;
            if (TimelineTick == CombatAnimationRouting.FirstAnimationTick)
                (started ??= []).Add(Active);
            if (TimelineTick < CombatAnimationRouting.CompletionTick) continue;
            Active = _queue.Count > 0 ? _queue.Dequeue() : null;
            TimelineTick = 0;
            if (Active is null) _elapsedMilliseconds = 0;
        }
        return started ?? [];
    }

    public void Clear()
    {
        _queue.Clear();
        Active = null;
        TimelineTick = 0;
        _elapsedMilliseconds = 0;
    }
}
