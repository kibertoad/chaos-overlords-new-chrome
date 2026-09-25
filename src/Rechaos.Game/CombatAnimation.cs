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
    CombatClipForces Forces,
    bool Police = false,
    short? Sound = null,
    bool HandsOff = false,
    int? AttackerStart = null,
    int? DefenderStart = null)
{
    // AttackerStart and DefenderStart are the gangs' force_start, which their upper tracks keep
    // through the presentation (SCR-COMBAT-002, FND-COMBAT-009). Null draws the track from the
    // clip's own starting Force.

    /// <summary>
    /// The tick the clip ends on: the final-result tick when it hands off to the reply of a gang it
    /// attacked, which the original plays without holding the result (SCR-COMBAT-002).
    /// </summary>
    public int CompletionTick => HandsOff
        ? CombatAnimationRouting.FinalResultTick
        : CombatAnimationRouting.CompletionTick;
}

public static class CombatAnimationRouting
{
    public const int FrameCount = 8;
    public const int FrameSize = 64;
    public const int FrameMilliseconds = PresentationClock.PeriodMilliseconds;
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

    /// <summary>The detailed-combat clips of one event, as <paramref name="viewer"/> sees them.</summary>
    /// <remarks>
    /// The original presents each of the viewer's gangs on the left. Its own attack plays the
    /// <c>PX070xx</c>/<c>PX071xx</c> pair; an attack on it, by a gang or the police, plays the
    /// mirrored <c>PX072xx</c>/<c>PX073xx</c> pair with the attacker on the right. Retaliation has
    /// no clip of its own: the attack's clip takes it off the attacker's force
    /// (SCR-COMBAT-002). <paramref name="timeline"/> is the event's combat phase, shared by
    /// every event of that phase so a turn's clips replay it once.
    /// </remarks>
    public static IReadOnlyList<CombatAnimationClip> ForEvent(
        MatchState state,
        GameEvent gameEvent,
        PlayerId viewer,
        CombatForceTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(timeline);
        if (!timeline.Covers(gameEvent))
            throw new ArgumentException("The timeline is not the event's combat phase.", nameof(timeline));
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved
            && gameEvent.Gang is { } policeTarget
            && gameEvent.PoliceAttack is { } police)
        {
            if (!police.Detected) return [];
            return [new CombatAnimationClip(gameEvent.Sequence, null, policeTarget,
                PoliceAttackAnimation, HitAnimation(PoliceHitAnimation, police.Damage),
                Reversed: true, timeline.Forces(gameEvent.Sequence, null, policeTarget),
                Police: true, Sound: AudioRouting.PoliceSound,
                DefenderStart: timeline.PhaseStartForce(policeTarget))];
        }
        if (gameEvent.Action != GangAction.Attack
            || gameEvent.Gang is not { } attacker
            || gameEvent.Target.Kind != CommandTargetKind.Gang
            || gameEvent.Resolution is not { } resolution)
            return [];

        var defender = new GangId(gameEvent.Target.Id);
        // Presentation can encounter an event whose gang is absent from the state being drawn: a
        // hire in the same turn reuses the slot of a gang the fight wiped out, which the event's own
        // record answers for, and a client-side divergence can still pair an event with a gang
        // neither the state nor the event knows. Detailed combat is optional, so omit the clip
        // instead of turning an existing synchronization problem into a game crash.
        if (state.FindCombatant(gameEvent, attacker) is not { } attackingGang
            || state.FindCombatant(gameEvent, defender) is not { } defendingGang)
            return [];
        if (resolution.Code is not (CommandResolutionCode.TargetEvaded or CommandResolutionCode.Resolved))
            return [];
        var incoming = attackingGang.Owner != viewer && defendingGang.Owner == viewer;
        var forces = timeline.Forces(gameEvent.Sequence, attacker, defender);
        var attackerStart = timeline.PhaseStartForce(attacker);
        var defenderStart = timeline.PhaseStartForce(defender);
        if (resolution.Code == CommandResolutionCode.TargetEvaded)
            return [new CombatAnimationClip(gameEvent.Sequence, attacker, defender,
                EvadedAnimation, 0, Reversed: incoming, forces,
                AttackerStart: attackerStart, DefenderStart: defenderStart)];

        var attack = AnimationPair(state, attackingGang, resolution.ItemId, resolution.Damage);
        return [new CombatAnimationClip(gameEvent.Sequence, attacker, defender,
            attack.Attack, attack.Hit, Reversed: incoming, forces,
            Sound: AudioRouting.GangAttackSound(state, attackingGang, resolution.ItemId),
            AttackerStart: attackerStart, DefenderStart: defenderStart)];
    }

    /// <summary>
    /// The clips of <paramref name="events"/> as <paramref name="viewer"/> sees them, each combat
    /// phase in the original's presentation order (<see cref="CombatPresentationOrder"/>).
    /// </summary>
    /// <remarks>
    /// Routing omits a fight whose gangs neither the state nor the event can name, but it still
    /// throws for an item id outside this state's definitions, which a client-side divergence can
    /// pair with an event. Detailed combat is optional presentation, so such an event is left out
    /// instead of crashing the game over it.
    /// </remarks>
    public static IReadOnlyList<CombatAnimationClip> ForPresentation(
        MatchState state,
        IEnumerable<GameEvent> events,
        PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(events);
        var clips = new List<CombatAnimationClip>();
        // A turn has one combat phase.
        foreach (var phase in events
                     .OrderBy(gameEvent => gameEvent.Sequence)
                     .GroupBy(gameEvent => gameEvent.Turn)
                     .Select(turn => turn.ToArray()))
        {
            var presented = CombatPresentationOrder.Order(state, phase, viewer);
            var timeline = CombatForceTimeline.ForPresentation(
                state, presented.Select(entry => entry.Event).ToArray());
            var phaseClips = new List<(CombatAnimationClip Clip, bool HandsOff)>();
            foreach (var (gameEvent, handsOff) in presented)
            {
                try
                {
                    foreach (var clip in ForEvent(state, gameEvent, viewer, timeline))
                        phaseClips.Add((clip, handsOff));
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
            // A clip hands off only to the reply that actually plays straight after it.
            for (var index = 0; index < phaseClips.Count; index++)
            {
                var (clip, handsOff) = phaseClips[index];
                var reply = index + 1 < phaseClips.Count ? phaseClips[index + 1].Clip : null;
                clips.Add(handsOff && reply is not null
                        && reply.Attacker == clip.Defender && reply.Defender == clip.Attacker
                    ? clip with { HandsOff = true }
                    : clip);
            }
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
            if (TimelineTick < Active.CompletionTick) continue;
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
