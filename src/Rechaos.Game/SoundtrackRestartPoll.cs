namespace Rechaos.Game;

/// <summary>RULE-AUDIO-002: the event pump polls once per presentation tick while active.</summary>
public sealed class SoundtrackRestartPoll
{
    private long _lastTick;

    public bool Advance(bool enabled, bool windowActive, TimeSpan now)
    {
        var tick = PresentationClock.Ticks(now);
        if (tick == _lastTick) return false;
        _lastTick = tick;
        return enabled && windowActive;
    }
}
