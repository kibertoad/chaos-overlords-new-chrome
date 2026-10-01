using System.Reflection;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;
using static Rechaos.Tests.SoundtrackStopTailTests;

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
        var stream = OggStreamOf(song);
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
            var reader = OggStreamOf(nextSong).GetType().GetProperty("Reader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(OggStreamOf(nextSong))!;
            Assert.Equal(nextSong.Duration, (TimeSpan)reader.GetType().GetProperty("TimePosition")!.GetValue(reader)!);
            Assert.Same(nextSong, MediaPlayer.Queue.ActiveSong);
        }
        finally
        {
            player.Release();
            DesktopGlSoundtrackStreaming.Shutdown();
        }
    }

    [Fact]
    public void PausingAfterEarlierBuffersDrainRetainsThePendingTail()
    {
        // RULE-AUDIO-002, FND-AUDIO-007: positionless resume retains the queued tail.
        using var song = Song.FromUri("rotated", new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "long-silence.ogg")));
        using var player = new SoundtrackProgramPlayer([song]);
        var stream = OggStreamOf(song);
        var reader = stream.GetType().GetProperty("Reader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
        reader.GetType().GetProperty("TimePosition")!.SetValue(reader, TimeSpan.FromSeconds(1.75));
        SetNativeLooping(stream, true);
        try
        {
            MediaPlayer.Volume = 0;
            player.PlayProgram([song]);
            WaitFor(() => DesktopGlSoundtrackStreaming.IsPending(song));
            var mutex = stream.GetType().GetField("prepareMutex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
            var assembly = typeof(Song).Assembly;
            var al = assembly.GetType("MonoGame.OpenAL.AL", true)!;
            var source = (int)stream.GetType().GetField("alSourceId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
            lock (mutex)
            {
                SetNativeLooping(stream, false);
                al.GetMethod("SourcePause", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(int)], null)!.Invoke(null, [source]);
                var integerParameter = assembly.GetType("MonoGame.OpenAL.ALSourcei", true)!;
                al.GetMethod("Source", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                    [typeof(int), integerParameter, typeof(int)], null)!.Invoke(null, [source, Enum.ToObject(integerParameter, Convert.ToInt32(Enum.Parse(assembly.GetType("MonoGame.OpenAL.ALGetSourcei", true)!, "SampleOffset"))), 22050]);
                var unqueued = (int[])al.GetMethod("SourceUnqueueBuffers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, [typeof(int), typeof(int)], null)!.Invoke(null, [source, 1])!;
                var buffers = (int[])stream.GetType().GetField("alBufferIds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stream)!;
                Assert.Equal(buffers[0], Assert.Single(unqueued));
                // The remaining native queue is buffers[1], buffers[2], not its array prefix.
                SetNativeLooping(stream, true);
                al.GetMethod("SourcePlay", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(int)], null)!.Invoke(null, [source]);
                MediaPlayer.Pause();
                // The controlled queue rotation itself must leave OpenAL error-free.
                Assert.Equal(0, Convert.ToInt32(al.GetMethod("GetError", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null)!.Invoke(null, null)));
            }
            var pausedCycle = DesktopGlSoundtrackStreaming.CompletedPumpCycles;
            WaitFor(() => DesktopGlSoundtrackStreaming.CompletedPumpCycles >= pausedCycle + 2);
            Assert.Equal(MediaState.Paused, MediaPlayer.State);
            Assert.False(player.ResumeThroughDiscEnd());
            var resumedCycle = DesktopGlSoundtrackStreaming.CompletedPumpCycles;
            WaitFor(() =>
            {
                Assert.False(player.AdvanceTrack()); // Also reports a native worker error.
                return DesktopGlSoundtrackStreaming.CompletedPumpCycles >= resumedCycle + 2;
            });
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            Assert.True(DesktopGlSoundtrackStreaming.IsPending(song));
        }
        finally
        {
            SetNativeLooping(stream, false);
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
            return ((System.Collections.IEnumerable)type.GetField("streams", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!).Cast<object>().Contains(OggStreamOf(song));
    }
}
