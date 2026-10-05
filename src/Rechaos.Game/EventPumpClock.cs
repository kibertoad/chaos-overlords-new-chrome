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
