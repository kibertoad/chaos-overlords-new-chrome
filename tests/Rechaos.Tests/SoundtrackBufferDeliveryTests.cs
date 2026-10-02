using System.Reflection;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;
using static Rechaos.Tests.SoundtrackStopTailTests;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class SoundtrackBufferDeliveryTests
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Assembly MonoGame = typeof(Song).Assembly;

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
        var reader = ReaderOf(stream);
        reader.GetType().GetProperty("TimePosition")!.SetValue(reader, TimeSpan.FromSeconds(seekSeconds));
        SetNativeLooping(stream, true);
        try
        {
            MediaPlayer.Volume = 0;
            player.PlayProgram([song]);
            WaitFor(() => DesktopGlSoundtrackStreaming.IsPending(song));
            Assert.Equal(song.Duration, DecodedPosition(stream));
            Assert.Equal(expectedQueued, BuffersQueued(stream));
            var bufferQuery = MonoGame.GetType("MonoGame.OpenAL.ALGetBufferi", true)!;
            var getBuffer = Al("GetBuffer", typeof(int), bufferQuery, typeof(int).MakeByRefType());
            var totalBytes = 0;
            foreach (var buffer in Field<int[]>(stream, "alBufferIds").Take(expectedQueued))
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
            Assert.Equal(expectedQueued, BuffersQueued(stream));
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
            Assert.Equal(nextSong.Duration, DecodedPosition(OggStreamOf(nextSong)));
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
        var reader = ReaderOf(stream);
        reader.GetType().GetProperty("TimePosition")!.SetValue(reader, TimeSpan.FromSeconds(1.75));
        SetNativeLooping(stream, true);
        try
        {
            MediaPlayer.Volume = 0;
            player.PlayProgram([song]);
            WaitFor(() => DesktopGlSoundtrackStreaming.IsPending(song));
            var source = Field<int>(stream, "alSourceId");
            lock (Field<object>(stream, "prepareMutex"))
            {
                SetNativeLooping(stream, false);
                Al("SourcePause", typeof(int)).Invoke(null, [source]);
                var integerParameter = MonoGame.GetType("MonoGame.OpenAL.ALSourcei", true)!;
                var sampleOffset = Enum.ToObject(integerParameter, Convert.ToInt32(Enum.Parse(SourceQuery, "SampleOffset")));
                Al("Source", typeof(int), integerParameter, typeof(int)).Invoke(null, [source, sampleOffset, 22050]);
                var unqueued = (int[])Al("SourceUnqueueBuffers", typeof(int), typeof(int)).Invoke(null, [source, 1])!;
                Assert.Equal(Field<int[]>(stream, "alBufferIds")[0], Assert.Single(unqueued));
                // The remaining native queue is buffers[1], buffers[2], not its array prefix.
                SetNativeLooping(stream, true);
                Al("SourcePlay", typeof(int)).Invoke(null, [source]);
                MediaPlayer.Pause();
                // The controlled queue rotation itself must leave OpenAL error-free.
                Assert.Equal(0, Convert.ToInt32(Al("GetError").Invoke(null, null)));
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

    private static Type SourceQuery => MonoGame.GetType("MonoGame.OpenAL.ALGetSourcei", true)!;

    private static MethodInfo Al(string name, params Type[] parameters) =>
        MonoGame.GetType("MonoGame.OpenAL.AL", true)!.GetMethod(name, Hidden, null, parameters, null)!;

    private static T Field<T>(object stream, string name) => (T)stream.GetType().GetField(name, Hidden)!.GetValue(stream)!;

    private static object ReaderOf(object stream) => stream.GetType().GetProperty("Reader", Hidden)!.GetValue(stream)!;

    private static TimeSpan DecodedPosition(object stream)
    {
        var reader = ReaderOf(stream);
        return (TimeSpan)reader.GetType().GetProperty("TimePosition")!.GetValue(reader)!;
    }

    private static int BuffersQueued(object stream)
    {
        object?[] args = [Field<int>(stream, "alSourceId"), Enum.Parse(SourceQuery, "BuffersQueued"), 0];
        Al("GetSource", typeof(int), SourceQuery, typeof(int).MakeByRefType()).Invoke(null, args);
        return (int)args[2]!;
    }

    private static bool Registered(Song song)
    {
        var type = MonoGame.GetType("Microsoft.Xna.Framework.Audio.OggStreamer", true)!;
        var instance = type.GetProperty("Instance", Hidden)!.GetValue(null)!;
        lock (type.GetField("iterationMutex", Hidden)!.GetValue(instance)!)
            return ((System.Collections.IEnumerable)type.GetField("streams", Hidden)!.GetValue(instance)!).Cast<object>().Contains(OggStreamOf(song));
    }
}
