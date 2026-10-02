using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackRestartPollTests
{
    [Fact]
    public void OriginalTimerSlotZeroControlsPollingInsteadOfFrameRate()
    {
        // FND-AUDIO-007, RULE-AUDIO-002: the pump polls when timer slot 0 is set.
        var poll = new SoundtrackRestartPoll();
        Assert.False(poll.Advance(true, true, TimeSpan.Zero));
        Assert.False(poll.Advance(true, true, TimeSpan.FromMilliseconds(165)));
        Assert.True(poll.Advance(true, true, TimeSpan.FromMilliseconds(166)));
        Assert.False(poll.Advance(true, true, TimeSpan.FromMilliseconds(167)));
        Assert.False(poll.Advance(true, true, TimeSpan.FromMilliseconds(331)));
        Assert.True(poll.Advance(true, true, TimeSpan.FromMilliseconds(332)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void DisabledMusicAndInactiveWindowSuppressOriginalRestartPoll(bool enabled, bool active)
    {
        // FND-AUDIO-007, RULE-AUDIO-002: both enabled and not inactive are required.
        var poll = new SoundtrackRestartPoll();
        Assert.False(poll.Advance(enabled, active, PresentationClock.Period));
        Assert.False(poll.Advance(true, true, PresentationClock.Period + TimeSpan.FromMilliseconds(1)));
        Assert.True(poll.Advance(true, true, PresentationClock.Period * 2));
    }
}
