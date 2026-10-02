using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackFadeTests
{
    [Fact]
    public void DefaultLevelUsesTruncatedDecrementAndWaitsBeforeStopping()
    {
        // FND-AUDIO-016, RULE-AUDIO-001: v=125 gives decrement 3; deadline zero
        // separates the initial write from the second write at the same time.
        var start = TimeSpan.FromSeconds(4);
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(5), start);
        Assert.Equal(122 * 256 / (float)ushort.MaxValue, fade.FirstStepVolume);
        Assert.Equal(119 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start));
        Assert.Equal(119 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start + TimeSpan.FromMilliseconds(16)));
        Assert.Equal(116 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start + TimeSpan.FromMilliseconds(17)));
        Assert.Equal(29 * 256 / (float)ushort.MaxValue, fade.VolumeAt(start + TimeSpan.FromMilliseconds(526)));
        Assert.False(fade.IsComplete(start + TimeSpan.FromMilliseconds(526)));
        Assert.True(fade.IsComplete(start + TimeSpan.FromMilliseconds(527)));
        Assert.Equal(0, fade.VolumeAt(start + TimeSpan.FromMilliseconds(527)));
        Assert.Equal(OriginalSoundtrackPolicy.VolumeForLevel(5), fade.RestoredVolume);
    }

    [Fact]
    public void EveryZeroBasedDeadlineMatchesTheRecordedIntegerLoop()
    {
        // FND-AUDIO-016: after dispatch n, the next write has occurred, except
        // after dispatch 31 when zero/stop/restore replaces the residual.
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(5), TimeSpan.Zero);
        for (var counter = 0; counter < 31; counter++)
        {
            var deadline = TimeSpan.FromMilliseconds(counter * 17);
            var expected = (125 - (counter + 2) * 3) * 256 / (float)ushort.MaxValue;
            Assert.Equal(expected, fade.VolumeAt(deadline));
            Assert.Equal(expected, fade.VolumeAt(deadline + TimeSpan.FromMilliseconds(16)));
            Assert.False(fade.IsComplete(deadline));
        }
        Assert.True(fade.IsComplete(TimeSpan.FromMilliseconds(31 * 17)));
        Assert.Equal(0, fade.VolumeAt(TimeSpan.FromMilliseconds(31 * 17)));
    }

    [Theory]
    [InlineData(1, 25)]
    [InlineData(6, 22)]
    [InlineData(10, 26)]
    public void LowLevelMayHaveNoIntermediateAttenuation(int level, int residual)
    {
        // FND-AUDIO-007, RULE-AUDIO-003: the initial high byte is divided once.
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(level), TimeSpan.Zero);
        Assert.Equal(residual * 256 / (float)ushort.MaxValue, fade.VolumeAt(TimeSpan.FromMilliseconds(526)));
        Assert.Equal(0, fade.VolumeAt(SoundtrackFade.Duration));
    }

    [Fact]
    public void MissedFramesUseElapsedTimeWithoutExtendingTheFade()
    {
        // FND-AUDIO-016: waits are measured from the start, with a zero-based counter.
        var fade = new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(5), TimeSpan.Zero);
        Assert.Equal(62 * 256 / (float)ushort.MaxValue, fade.VolumeAt(TimeSpan.FromMilliseconds(330)));
        Assert.True(fade.IsComplete(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, fade.VolumeAt(TimeSpan.FromSeconds(1)));
    }
}
