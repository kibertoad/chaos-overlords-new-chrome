using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> IntroRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].IntroMovies is not null)
                    data.Add(experiment, run);
        return data;
    }

    // RULE-VIDEO-001, FND-VIDEO-002: the probe let both intro movies play out and recorded each
    // frame the frame helper showed (EXP-VIDEO-001). The original plays the logos movie and then the
    // intro movie, steps each of n frames n + 1 times with the slot's counter running 0 to n, and
    // closes it at counter n. The first n steps come about 100 ms apart, the frame time of
    // FND-VIDEO-001, and the last within 10 ms of the one before it. The rebuild plays the same files
    // in the same order, and its timeline for n frames of 100 ms, stepped at the original's steps,
    // decodes one frame at each of the first n and ends the movie at the last.
    [Theory]
    [MemberData(nameof(IntroRuns))]
    public void TheIntroPlaysEachMovieToTheStepAfterItsLastFrame(string experiment, int run)
    {
        var movies = Run(experiment, run).IntroMovies!;
        Assert.Equal(
            IntroMoviePolicy.FileNames.Select(name => Path.GetFileNameWithoutExtension(name).ToUpperInvariant()),
            movies.Select(movie => movie.Name[(movie.Name.LastIndexOf('\\') + 1)..].ToUpperInvariant()));
        var frameTime = TimeSpan.FromMilliseconds(100);
        foreach (var movie in movies)
        {
            Assert.Equal(Enumerable.Range(0, movie.Frames + 1), movie.Shown);
            Assert.Equal(movie.Frames, movie.ClosedAt);
            // Under the debugger each step up to counter n - 1 keeps to the frame time within 15 ms.
            var intervals = movie.Milliseconds.Zip(movie.Milliseconds.Skip(1), (earlier, later) => later - earlier).ToArray();
            Assert.All(intervals[..^1], interval => Assert.InRange(interval, 85L, 115L));
            Assert.InRange(intervals[^1], 0L, 15L);

            var timeline = new SmackerPlaybackTimeline(movie.Frames, frameTime);
            Assert.Equal(new SmackerTimelineAdvance(1, false), timeline.Advance(TimeSpan.Zero));
            for (var step = 1; step < movie.Frames; step++)
                Assert.Equal(new SmackerTimelineAdvance(1, false), timeline.Advance(frameTime));
            Assert.Equal(new SmackerTimelineAdvance(0, true),
                timeline.Advance(TimeSpan.FromMilliseconds(intervals[^1])));
        }
    }
}
