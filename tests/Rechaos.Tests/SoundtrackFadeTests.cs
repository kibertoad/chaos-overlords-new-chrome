using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackFadeTests
{
    [Fact]
    public void DefaultLevelUsesTruncatedDecrementAndWaitsBeforeStopping()
    {
        // FND-AUDIO-007, RULE-AUDIO-001: v=125 gives decrement 3, not 125/32.0.
        var start = TimeSpan.FromSeconds(4);
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(5), start);
        Assert.Equal(122 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start));
        Assert.Equal(122 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start + TimeSpan.FromMilliseconds(16)));
        Assert.Equal(119 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start + TimeSpan.FromMilliseconds(17)));
        Assert.Equal(29 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start + TimeSpan.FromMilliseconds(543)));
        Assert.False(fade.IsComplete(start + TimeSpan.FromMilliseconds(543)));
        Assert.True(fade.IsComplete(start + TimeSpan.FromMilliseconds(544)));
        Assert.Equal(0, fade.VolumeAt(start + TimeSpan.FromMilliseconds(544)));
        Assert.Equal(OriginalSoundtrackPolicy.VolumeForLevel(5), fade.RestoredVolume);
    }

    [Theory]
    [InlineData(1, 25)]
    [InlineData(6, 22)]
    [InlineData(10, 26)]
    public void LowLevelMayHaveNoIntermediateAttenuation(int level, int residual)
    {
        // FND-AUDIO-007, RULE-AUDIO-003: the initial high byte is divided once.
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(level), TimeSpan.Zero);
        Assert.Equal(residual * 256 / (float)ushort.MaxValue, fade.VolumeAt(TimeSpan.FromMilliseconds(543)));
        Assert.Equal(0, fade.VolumeAt(SoundtrackFade.Duration));
    }

    [Fact]
    public void MissedFramesUseElapsedTimeWithoutExtendingTheFade()
    {
        // FND-AUDIO-007: waits are measured from the start, not from each preceding step.
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(5), TimeSpan.Zero);
        Assert.Equal(65 * 256 / (float)ushort.MaxValue, fade.VolumeAt(TimeSpan.FromMilliseconds(330)));
        Assert.True(fade.IsComplete(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, fade.VolumeAt(TimeSpan.FromSeconds(1)));
    }
}
