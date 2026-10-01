using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class SoundtrackBufferDeliveryTests
{
    [Theory]
    [InlineData(2.25, 2)]
    [InlineData(1.75, 3)]
    [InlineData(2.5, 1)]
    public void DecodedFinalBuffersAreQueuedBeforeCompletion(double seekSeconds, int expectedQueued)
    {
        // RULE-AUDIO-001, RULE-AUDIO-002, FND-AUDIO-007: the requested endpoint includes the last frame.
        using var song = Song.FromUri("tail", new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "long-silence.ogg")));
        using var player = new SoundtrackProgramPlayer([song]);
        var stream = Stream(song);
        var reader = stream.GetType().GetProperty("Reader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
        reader.GetType().GetProperty("TimePosition")!.SetValue(reader, TimeSpan.FromSeconds(seekSeconds));
        SetNativeLooping(stream, true);
        try
        {
            MediaPlayer.Volume = 0;
            player.PlayProgram([song]);
            WaitFor(() => DesktopGlSoundtrackStreaming.IsPending(song));
            var assembly = typeof(Song).Assembly;
            var queryType = assembly.GetType("MonoGame.OpenAL.ALGetSourcei", true)!;
            var query = assembly.GetType("MonoGame.OpenAL.AL", true)!.GetMethod("GetSource",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                [typeof(int), queryType, typeof(int).MakeByRefType()], null)!;
            var source = stream.GetType().GetField("alSourceId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
            object?[] args = [source, Enum.Parse(queryType, "BuffersQueued"), 0];
            query.Invoke(null, args);
            Assert.Equal(song.Duration, (TimeSpan)reader.GetType().GetProperty("TimePosition")!.GetValue(reader)!);
            Assert.Equal(expectedQueued, (int)args[2]!);
            var bufferQuery = assembly.GetType("MonoGame.OpenAL.ALGetBufferi", true)!;
            var getBuffer = assembly.GetType("MonoGame.OpenAL.AL", true)!.GetMethod("GetBuffer",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                [typeof(int), bufferQuery, typeof(int).MakeByRefType()], null)!;
            var buffers = (int[])stream.GetType().GetField("alBufferIds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
            var totalBytes = 0;
            foreach (var buffer in buffers.Take(expectedQueued))
            {
                object?[] bufferArgs = [buffer, Enum.Parse(bufferQuery, "Size"), 0];
                getBuffer.Invoke(null, bufferArgs);
                totalBytes += (int)bufferArgs[2]!;
            }
            // The first prepared half-second plus every remaining decoded frame:
            // 44,100 Hz, stereo, signed 16-bit PCM in the native queue.
            Assert.Equal((int)((0.5 + 2.5 - seekSeconds) * 44100 * 2 * 2), totalBytes);
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            Assert.False(player.ReadyToRestart);
            MediaPlayer.Pause();
            Assert.Equal(MediaState.Paused, MediaPlayer.State);
            Assert.False(player.ResumeThroughDiscEnd());
            WaitFor(() => DesktopGlSoundtrackStreaming.IsPending(song));
            query.Invoke(null, args);
            Assert.Equal(expectedQueued, (int)args[2]!);
        }
        finally
        {
            SetNativeLooping(stream, false);
            player.Release();
            // Join before native source disposal, and require the next case to restart the worker.
            DesktopGlSoundtrackStreaming.Shutdown();
        }
    }

    [Fact]
    public void CompletionWaitingToNotifyCannotStopAReplacementProgram()
    {
        // RULE-AUDIO-001, RULE-AUDIO-002: an old program's endpoint cannot stop a new one.
        using var oldSong = Song.FromUri("old", new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.ogg")));
        using var nextSong = Song.FromUri("next", new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "long-silence.ogg")));
        using var player = new SoundtrackProgramPlayer([oldSong, nextSong]);
        try
        {
            MediaPlayer.Volume = 0;
            using (DesktopGlSoundtrackStreaming.SerializeTransport())
            {
                player.PlayProgram([oldSong]);
                // Hold notification after the worker drains and unregisters the old stream.
                // The owner re-enters the transport lock; the actual worker must wait.
                WaitFor(() => !Registered(oldSong));
                Assert.Equal(MediaState.Playing, MediaPlayer.State);
                player.PlayProgram([nextSong]);
            }
            WaitFor(() => player.ReadyToRestart);
            var reader = Stream(nextSong).GetType().GetProperty("Reader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Stream(nextSong))!;
            Assert.Equal(nextSong.Duration, (TimeSpan)reader.GetType().GetProperty("TimePosition")!.GetValue(reader)!);
            Assert.Same(nextSong, MediaPlayer.Queue.ActiveSong);
        }
        finally
        {
            player.Release();
            DesktopGlSoundtrackStreaming.Shutdown();
        }
    }

    private static bool Registered(Song song)
    {
        var type = typeof(Song).Assembly.GetType("Microsoft.Xna.Framework.Audio.OggStreamer", true)!;
        var instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
        var mutex = type.GetField("iterationMutex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
        lock (mutex)
            return ((System.Collections.IEnumerable)type.GetField("streams", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!).Cast<object>().Contains(Stream(song));
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
