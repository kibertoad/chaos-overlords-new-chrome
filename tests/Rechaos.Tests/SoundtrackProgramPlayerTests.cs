using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[CollectionDefinition("Native soundtrack", DisableParallelization = true)]
public sealed class NativeSoundtrackCollection;

[Collection("Native soundtrack")]
public sealed class SoundtrackProgramPlayerTests(ITestOutputHelper output)
{
    [Fact]
    public void NativeCompletionCannotRaceMainThreadAdvancementOrProgramRestart()
    {
        // RULE-AUDIO-001, RULE-AUDIO-002: an intermediate track ending must continue the
        // program; only its final track ending makes the program eligible for the restart poll.
        var owner = Environment.CurrentManagedThreadId;
        var playingThreads = new ConcurrentBag<int>();
        // Invoke-Validation sets ALSOFT_DRIVERS=null before launching the test process.
        // A runtime Environment.SetEnvironmentVariable cannot configure native OpenAL on Unix.
        var audioPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.ogg");
        var songs = Enumerable.Range(1, 3)
            .Select(index => Song.FromUri($"silence{index}", new Uri(audioPath))).ToArray();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = 0;
        var blockedOnce = 0;
        var barrierTimedOut = 0;
        EventHandler<EventArgs> barrier = (_, _) =>
        {
            if (MediaPlayer.State == MediaState.Playing)
                playingThreads.Add(Environment.CurrentManagedThreadId);
            if (MediaPlayer.State != MediaState.Stopped || !ReferenceEquals(MediaPlayer.Queue.ActiveSong, songs[0])
                || Interlocked.Exchange(ref blockedOnce, 1) != 0) return;
            worker = Environment.CurrentManagedThreadId;
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) Interlocked.Exchange(ref barrierTimedOut, 1);
        };
        MediaPlayer.MediaStateChanged += barrier;
        using var player = new SoundtrackProgramPlayer(songs);
        try
        {
            MediaPlayer.IsRepeating = false;
            MediaPlayer.IsShuffled = false;
            MediaPlayer.Volume = 0;
            player.PlayProgram(songs);
            WaitFor(() => entered.IsSet);
            Assert.NotEqual(owner, worker);
            Assert.Equal(MediaState.Stopped, MediaPlayer.State);
            // Pause the actual worker before it can notify completion: a coincident poll
            // must not rewind the program, and no game-thread advance may start yet.
            Assert.False(player.ReadyToRestart);
            Assert.False(player.AdvanceTrack());
            release.Set();
            WaitFor(() => ReferenceEquals(MediaPlayer.Queue.ActiveSong, songs[1]), player);
            WaitFor(() => ReferenceEquals(MediaPlayer.Queue.ActiveSong, songs[2]), player);
            WaitFor(() => player.ReadyToRestart, player);
            Assert.Equal(0, Volatile.Read(ref barrierTimedOut));
            Assert.All(playingThreads, thread => Assert.Equal(owner, thread));
            Assert.Equal(3, playingThreads.Count);

            // RULE-AUDIO-001: a restart begins at the first track with its normal end bound.
            player.PlayProgram([songs[0]]);
            WaitFor(() => player.ReadyToRestart, player);
            Assert.Same(songs[0], MediaPlayer.Queue.ActiveSong);
            output.WriteLine($"Owner thread {owner}; completion worker {worker}; all starts stayed on owner.");
        }
        finally
        {
            release.Set();
            MediaPlayer.MediaStateChanged -= barrier;
            player.Dispose();
            foreach (var song in songs) song.Dispose();
        }
    }

    private static void WaitFor(Func<bool> condition, SoundtrackProgramPlayer? player = null)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition() && deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            player?.AdvanceTrack();
            FrameworkDispatcher.Update();
            Thread.Sleep(1);
        }
        Assert.True(condition(), "Native playback did not reach the expected boundary.");
    }
}
