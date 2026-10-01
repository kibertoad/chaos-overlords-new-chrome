using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class SoundtrackStopTailTests
{
    [Fact]
    public void ReplacingAProgramAfterItsTailWasDecodedDecodesTheWholeNextTrack()
    {
        // RULE-AUDIO-001, RULE-AUDIO-002, FND-AUDIO-007: a new program runs
        // from its first track to the last track's end, regardless of the old stop position.
        using var shortSong = Song.FromUri("short", new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.ogg")));
        using var longSong = Song.FromUri("long", new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "long-silence.ogg")));
        using var player = new SoundtrackProgramPlayer([shortSong, longSong]);
        var streamType = typeof(Song).Assembly.GetType("Microsoft.Xna.Framework.Audio.OggStreamer", throwOnError: true)!;
        var streamer = streamType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var pending = streamType.GetField("pendingFinish", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var oldStream = Stream(shortSong);
        var prepareMutex = oldStream.GetType().GetField("prepareMutex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(oldStream)!;
        MediaPlayer.IsRepeating = false;
        MediaPlayer.IsShuffled = false;
        MediaPlayer.Volume = 0;
        // Pin the old native buffer so EOF remains pending even on a loaded runner.
        SetNativeLooping(oldStream, true);
        try
        {
            player.PlayProgram([shortSong]);
            WaitFor(() => (bool)pending.GetValue(streamer)!);
            lock (prepareMutex)
            {
                Assert.Equal(MediaState.Playing, MediaPlayer.State);
                Assert.True((bool)pending.GetValue(streamer)!);
                SetNativeLooping(oldStream, false);
                player.PlayProgram([longSong]);
            }
            WaitFor(() => player.ReadyToRestart);
            var reader = Stream(longSong).GetType().GetProperty("Reader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Stream(longSong))!;
            var decoded = (TimeSpan)reader.GetType().GetProperty("TimePosition")!.GetValue(reader)!;
            // Decoder coverage does not compare audible output or final partial-buffer delivery.
            Assert.Equal(longSong.Duration, decoded);
        }
        finally
        {
            SetNativeLooping(oldStream, false);
        }
    }

    private static void SetNativeLooping(object stream, bool value)
    {
        var sourceId = (int)stream.GetType().GetField("alSourceId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
        var assembly = typeof(SoundEffect).Assembly;
        var sourceBoolean = assembly.GetType("MonoGame.OpenAL.ALSourceb", throwOnError: true)!;
        var setter = assembly.GetType("MonoGame.OpenAL.AL", throwOnError: true)!.GetMethod("Source",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            [typeof(int), sourceBoolean, typeof(bool)], null)!;
        setter.Invoke(null, [sourceId, Enum.Parse(sourceBoolean, "Looping"), value]);
    }

    private static object Stream(Song song) => typeof(Song).GetField("stream", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(song)!;

    private static void WaitFor(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            FrameworkDispatcher.Update();
            Thread.Sleep(1);
        }
        Assert.True(condition(), "Native stream boundary did not arrive.");
    }
}
