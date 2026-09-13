using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class StartupMoviePolicy
{
    public static IReadOnlyList<string> FileNames { get; } =
        ["MVLOGOS.smk", "MVINTRO.smk"];

    public static Rectangle Destination(int width, int height) =>
        new((VirtualInput.Width - width) / 2, (VirtualInput.Height - height) / 2, width, height);
}

public readonly record struct SmackerTimelineAdvance(
    int FramesToDecode,
    bool Completed);

/// <summary>Presentation-only fixed-cadence clock for a decoded movie.</summary>
public sealed class SmackerPlaybackTimeline
{
    private readonly int _frameCount;
    private readonly TimeSpan _frameDuration;
    private TimeSpan _elapsedInFrame;
    private int _frameIndex = -1;

    public SmackerPlaybackTimeline(int frameCount, TimeSpan frameDuration)
    {
        if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (frameDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(frameDuration));
        _frameCount = frameCount;
        _frameDuration = frameDuration;
    }

    public bool IsComplete { get; private set; }
    public int FrameIndex => _frameIndex;

    public SmackerTimelineAdvance Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (IsComplete) return new SmackerTimelineAdvance(0, true);
        if (_frameIndex < 0)
        {
            _frameIndex = 0;
            return new SmackerTimelineAdvance(1, false);
        }

        _elapsedInFrame += elapsed;
        var frames = 0;
        while (_elapsedInFrame >= _frameDuration)
        {
            _elapsedInFrame -= _frameDuration;
            if (_frameIndex + 1 >= _frameCount)
            {
                IsComplete = true;
                break;
            }
            _frameIndex++;
            frames++;
        }
        return new SmackerTimelineAdvance(frames, IsComplete);
    }

    public void Skip() => IsComplete = true;
}
