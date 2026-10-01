namespace Rechaos.Game;

/// <summary>RULE-AUDIO-001, RULE-AUDIO-003, FND-AUDIO-007: the CD fade subtracts
/// the initial high-byte volume divided by 32 before each 17 ms wait.</summary>
public sealed class SoundtrackFade
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(32 * 17);
    private readonly int _initialVolume;
    private readonly TimeSpan _startedAt;

    public SoundtrackFade(float volume, TimeSpan startedAt)
    {
        if (!float.IsFinite(volume) || volume is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(volume));
        _initialVolume = (int)MathF.Round(volume * ushort.MaxValue) >> 8;
        _startedAt = startedAt;
    }

    public float RestoredVolume => _initialVolume * 256 / (float)ushort.MaxValue;
    public bool IsComplete(TimeSpan now) => now - _startedAt >= Duration;

    public float VolumeAt(TimeSpan now)
    {
        if (IsComplete(now)) return 0;
        var elapsed = Math.Max(0, (now - _startedAt).Ticks);
        var steps = Math.Min(32, elapsed / TimeSpan.FromMilliseconds(17).Ticks + 1);
        return (_initialVolume - (int)steps * (_initialVolume / 32))
            * 256 / (float)ushort.MaxValue;
    }
}
