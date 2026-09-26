using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class IntroMoviePlaybackTests
{
    [Fact]
    public void StartupOrderAndNativeCenteredDestinationAreStable()
    {
        Assert.Equal(new[] { "MVLOGOS.smk", "MVINTRO.smk" }, IntroMoviePolicy.FileNames);
        Assert.Equal(new Rectangle(80, 102, 480, 256), IntroMoviePolicy.Destination(480, 256));
    }

    // DEV-VIDEO-003: Intro only once, on by default, skips the intro after a recorded showing.
    // RULE-VIDEO-001: switched off, the intro plays at every start, as in the original.
    [Fact]
    public void IntroStreamsOnceUnlessIntroOnlyOnceIsSwitchedOff()
    {
        Assert.True(OriginalOptionsPolicy.IntroOnlyOnceByDefault);
        Assert.True(IntroMoviePolicy.PlaysAtStartup(introOnlyOnce: false, introMoviesSeen: false));
        Assert.True(IntroMoviePolicy.PlaysAtStartup(introOnlyOnce: false, introMoviesSeen: true));
        Assert.True(IntroMoviePolicy.PlaysAtStartup(introOnlyOnce: true, introMoviesSeen: false));
        Assert.False(IntroMoviePolicy.PlaysAtStartup(introOnlyOnce: true, introMoviesSeen: true));
    }

    [Fact]
    public void TimelinePresentsFirstFrameImmediatelyAndUsesExactCadence()
    {
        var timeline = new SmackerPlaybackTimeline(3, TimeSpan.FromMilliseconds(100));

        Assert.Equal(new SmackerTimelineAdvance(1, false), timeline.Advance(TimeSpan.Zero));
        Assert.Equal(new SmackerTimelineAdvance(0, false),
            timeline.Advance(TimeSpan.FromMilliseconds(99)));
        Assert.Equal(new SmackerTimelineAdvance(1, false),
            timeline.Advance(TimeSpan.FromMilliseconds(1)));
        Assert.Equal(1, timeline.FrameIndex);
        Assert.Equal(new SmackerTimelineAdvance(1, true),
            timeline.Advance(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(2, timeline.FrameIndex);
    }

    [Fact]
    public void TimelineCanBeSkippedWithoutAdvancingMoreFrames()
    {
        var timeline = new SmackerPlaybackTimeline(200, TimeSpan.FromMilliseconds(100));
        timeline.Advance(TimeSpan.Zero);

        timeline.Skip();

        Assert.Equal(new SmackerTimelineAdvance(0, true),
            timeline.Advance(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void UnsignedMoviePcmConvertsToSignedSixteenBitLittleEndian()
    {
        var converted = SmackerPcmConversion.ToSigned16LittleEndian([0, 128, 255]);

        Assert.Equal(new byte[] { 0, 128, 0, 0, 0, 127 }, converted);
    }
}
