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

    public SmackerPlaybackTimeline(int frameCount, TimeSpan frameDuration) =>
        playback = new(frameCount, frameDuration);

    public bool IsComplete => playback.IsComplete;
    public int FrameIndex => playback.FrameIndex;

    public SmackerTimelineAdvance Advance(TimeSpan elapsed)
    {
        var frames = 0;
        playback.Advance(elapsed, _ => frames++);
        return new SmackerTimelineAdvance(frames, playback.IsComplete);
    }

    public void Skip() => playback.Skip();
}
