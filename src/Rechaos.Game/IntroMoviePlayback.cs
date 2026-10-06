using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class IntroMoviePolicy
{
    public static IReadOnlyList<string> FileNames { get; } =
        ["MVLOGOS.smk", "MVINTRO.smk"];

    /// <summary>The intro streams unattended until a showing has been recorded while Intro only
    /// once is on, its default (DEV-VIDEO-003); switched off, it plays at every start, as in the
    /// original.</summary>
    public static bool PlaysAtStartup(bool introOnlyOnce, bool introMoviesSeen) =>
        !introOnlyOnce || !introMoviesSeen;

    public static Rectangle Destination(int width, int height) =>
        new((VirtualInput.Width - width) / 2, (VirtualInput.Height - height) / 2, width, height);
}

public readonly record struct SmackerTimelineAdvance(
    int FramesToDecode,
    bool Completed);

/// <summary>Presentation-only fixed-cadence clock for a decoded movie.</summary>
public sealed class SmackerPlaybackTimeline
{
    private readonly RefurbishedDinosaurs.Media.Playback.MoviePlayback playback;
    private readonly int frameCount;
    private int decoded;

    public SmackerPlaybackTimeline(int frameCount, TimeSpan frameDuration)
    {
        playback = new(frameCount, frameDuration);
        this.frameCount = frameCount;
    }

    public bool IsComplete => playback.IsComplete;
    public int FrameIndex => playback.FrameIndex;

    public SmackerTimelineAdvance Advance(TimeSpan elapsed)
    {
        if (playback.IsComplete) return new SmackerTimelineAdvance(0, true);
        // RULE-VIDEO-001, EXP-VIDEO-001: the original closes a movie that played out at the step
        // after its last frame, which comes within 10 ms of that frame instead of a frame time
        // later. The movie therefore ends at the first update after the one that decoded its last
        // frame, so the last frame is drawn once and the movie does not hold it for a frame time.
        if (frameCount > 0 && decoded == frameCount)
        {
            playback.Skip();
            return new SmackerTimelineAdvance(0, true);
        }
        var frames = 0;
        playback.Advance(elapsed, _ => frames++);
        decoded += frames;
        return new SmackerTimelineAdvance(frames, playback.IsComplete);
    }

    public void Skip() => playback.Skip();
}
