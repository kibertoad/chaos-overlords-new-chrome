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
    // closes it at counter n, about 100 ms a step, the frame time of FND-VIDEO-001. The rebuild plays
    // the same files in the same order, and its timeline for n frames of 100 ms decodes n frames and
    // ends the movie at the step after the last, n frame times after the first.
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
            // Under the debugger the steps keep to the frame time within a few percent.
            var step = (movie.Milliseconds[^1] - movie.Milliseconds[0]) / (double)movie.Frames;
            Assert.InRange(step, 95, 105);

            var timeline = new SmackerPlaybackTimeline(movie.Frames, frameTime);
            var steps = 1;
            var decoded = timeline.Advance(TimeSpan.Zero).FramesToDecode;
            while (!timeline.IsComplete)
            {
                decoded += timeline.Advance(frameTime).FramesToDecode;
                steps++;
            }
            Assert.Equal(movie.Shown.Count, steps);
            Assert.Equal(movie.Frames, decoded);
        }
    }
}
