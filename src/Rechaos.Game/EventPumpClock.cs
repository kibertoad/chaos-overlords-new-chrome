namespace Rechaos.Game;

/// <summary>
/// The presentation ticks the original's event pump has taken (RULE-UI-008, RULE-TIMER-003).
/// </summary>
/// <remarks>
/// <para>
/// The pump drives the planning bar and its warning sounds, <c>comlink_blink_step</c> (the
/// blinking console lights, the selection frame and the Comlink alert repeat) and the music poll,
/// one step per presentation tick it takes. Outside a hold it takes every tick the clock gives
/// (DEV-TIMER-001), so <see cref="Time"/> runs with the game clock.
/// </para>
/// <para>
/// While an offer, a console tile, a gang card's portrait or a control of the held-button helpers
/// is held under the left button, the original runs a loop that dispatches window messages without
/// calling the pump (FND-UI-044, FND-UI-046), so <see cref="Time"/> stops. Those loops leave timer
/// slot 0 alone, so its flag keeps one of the ticks that fell during the hold and the pump takes
/// it on its first call after the release; the other ticks of the hold are lost, and every step
/// the pump drives stays that many ticks behind the game clock from then on.
/// </para>
/// <para>
/// A soundtrack fade stops <see cref="Time"/> in the same way: it runs inside the pump's music
/// step and leaves timer slot 0 alone (FND-AUDIO-017).
/// </para>
/// <para>
/// A panel that steps on slot 0 in its own loop takes the flag after the event its pass handled,
/// so a hold of one of its faces stops its step and the release pass takes the kept tick, as the
/// pump does (FND-UI-047). The item rotations, the researched item of Last Turn Events, the
/// Comlink Send caret and the idle-gang warning's line read this clock for that reason.
/// </para>
/// </remarks>
public sealed class EventPumpClock
{
    private long _lostTicks;
    private long _lastTakenTick;
    private bool _holding;

    /// <summary>
    /// The game time less the ticks the pump lost, frozen while a hold lasts. Its
    /// <see cref="PresentationClock.Ticks"/> are the ticks the pump has taken.
    /// </summary>
    public TimeSpan Time { get; private set; }

    /// <summary>The presentation ticks the pump has taken.</summary>
    public long Ticks => PresentationClock.Ticks(Time);

    /// <summary>
    /// <see cref="Ticks"/> with the tick a hold in progress keeps for its release, when one fell
    /// by <paramref name="now"/>. A counter that a panel starts after another panel's release
    /// pass took that tick (FND-UI-047) starts from here, so it does not take the tick again.
    /// </summary>
    public long TicksAfterHold(TimeSpan now) =>
        _holding && PresentationClock.Ticks(now) > _lastTakenTick ? Ticks + 1 : Ticks;

    /// <summary>
    /// One pass of the game loop. <paramref name="holding"/> says whether a hold or a fade that
    /// keeps the pump from running is in progress.
    /// </summary>
    public void Update(TimeSpan now, bool holding)
    {
        var tick = PresentationClock.Ticks(now);
        if (holding)
        {
            _holding = true;
            return;
        }
        if (_holding)
        {
            _holding = false;
            // The flag of timer slot 0 holds one tick, however many fell during the hold.
            var fell = tick - _lastTakenTick;
            if (fell > 1) _lostTicks += fell - 1;
        }
        _lastTakenTick = tick;
        Time = now - TimeSpan.FromTicks(PresentationClock.Period.Ticks * _lostTicks);
    }
}
