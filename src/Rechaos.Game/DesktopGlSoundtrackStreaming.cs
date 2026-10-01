using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework.Media;

namespace Rechaos.Game;

/// <summary>RULE-AUDIO-001, RULE-AUDIO-002: submit every decoded frame before completion.</summary>
internal static class DesktopGlSoundtrackStreaming
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly object InitializationMutex = new();
    private static readonly object CompletionMutex = new();
    private static Backend? _backend;

    internal static void EnsureInitialized()
    {
        lock (InitializationMutex)
        {
            if (_backend is null) _backend = new Backend();
            else if (_backend.ShutdownRequested)
            {
                _backend.Join();
                _backend = new Backend();
            }
            _backend.ThrowIfFailed();
        }
    }

    internal static IDisposable SerializeTransport()
    {
        Monitor.Enter(CompletionMutex);
        return new TransportLock();
    }

    private sealed class TransportLock : IDisposable
    {
        public void Dispose() => Monitor.Exit(CompletionMutex);
    }

    internal static void CancelCompletion() => _backend?.CancelCompletion();
    internal static long CompletedPumpCycles => _backend?.CompletedPumpCycles ?? 0;
    internal static bool IsPending(Song song) => _backend?.IsPending(song) == true;

    internal static void ResetForNewSong(Song song)
    {
        EnsureInitialized();
        _backend!.Reset(song);
    }

    internal static void Shutdown()
    {
        lock (InitializationMutex)
        {
            _backend?.StopAndJoin();
            _backend = null;
        }
    }

    private sealed class Backend
    {
        private readonly object _streamer;
        private readonly Type _streamType;
        private readonly object _iterationMutex;
        private readonly FieldInfo _streams;
        private readonly FieldInfo _cancelled;
        private readonly MethodInfo _fillBuffer;
        private readonly Type _al;
        private readonly Type _sourceQuery;
        private readonly ConcurrentDictionary<object, bool> _atEnd = new();
        private Exception? _failure;
        private readonly Thread _thread;
        private volatile bool _stop;
        private readonly int _updateInterval;
        private long _generation;
        private long _completedPumpCycles;

        internal Backend()
        {
            var assembly = typeof(Song).Assembly;
            var type = assembly.GetType("Microsoft.Xna.Framework.Audio.OggStreamer", true)!;
            _streamType = assembly.GetType("Microsoft.Xna.Framework.Audio.OggStream", true)!;
            _al = assembly.GetType("MonoGame.OpenAL.AL", true)!;
            _sourceQuery = assembly.GetType("MonoGame.OpenAL.ALGetSourcei", true)!;
            _streamer = type.GetProperty("Instance", Members)!.GetValue(null)!;
            var updateRate = (float)type.GetProperty("UpdateRate", Members)!.GetValue(_streamer)!;
            _updateInterval = (int)(1000 / (updateRate > 0 ? updateRate : 1));
            _iterationMutex = type.GetField("iterationMutex", Members)!.GetValue(_streamer)!;
            _streams = type.GetField("streams", Members)!;
            _cancelled = type.GetField("cancelled", Members)!;
            _fillBuffer = type.GetMethod("FillBuffer", Members)!;
            // This runs before a soundtrack starts. Quiesce the stock worker, which
            // discards a whole fill batch when its last read reaches EOF.
            type.GetMethod("Shutdown", Members)!.Invoke(_streamer, null);
            ((Thread)type.GetField("underlyingThread", Members)!.GetValue(_streamer)!).Join();
            _cancelled.SetValue(_streamer, false);
            _thread = new Thread(Run) { IsBackground = true, Name = "Soundtrack streaming" };
            _thread.Start();
        }

        internal long CompletedPumpCycles => Interlocked.Read(ref _completedPumpCycles);
        internal bool ShutdownRequested => (bool)_cancelled.GetValue(_streamer)!;
        internal void Join() => _thread.Join();
        internal void StopAndJoin()
        {
            _stop = true;
            _thread.Join();
        }

        private object Stream(Song song) => typeof(Song).GetField("stream", Members)!.GetValue(song)!;

        internal bool IsPending(Song song) => _atEnd.GetValueOrDefault(Stream(song));

        internal void CancelCompletion() => Interlocked.Increment(ref _generation);

        internal void Reset(Song song)
        {
            var stream = Stream(song);
            lock (Field(stream, "prepareMutex"))
            {
                CancelCompletion();
                _atEnd.TryRemove(stream, out _);
            }
        }

        internal void ThrowIfFailed()
        {
            if (Volatile.Read(ref _failure) is { } failure)
                throw new InvalidOperationException("Soundtrack streaming failed.", failure);
        }

        private object Field(object stream, string name) => _streamType.GetField(name, Members)!.GetValue(stream)!;
        private object Property(object stream, string name) => _streamType.GetProperty(name, Members)!.GetValue(stream)!;

        private object? Call(string name, Type[] signature, params object?[] args)
        {
            var result = _al.GetMethod(name, Members, null, signature, null)!.Invoke(null, args);
            var error = _al.GetMethod("GetError", Members, null, Type.EmptyTypes, null)!.Invoke(null, null);
            if (Convert.ToInt32(error) != 0)
                throw new InvalidOperationException($"OpenAL {name} failed: {error}.");
            return result;
        }

        private int Query(int source, string name)
        {
            object?[] args = [source, Enum.Parse(_sourceQuery, name), 0];
            Call("GetSource", [typeof(int), _sourceQuery, typeof(int).MakeByRefType()], args);
            return (int)args[2]!;
        }

        private void Queue(int source, int[] buffers) =>
            Call("SourceQueueBuffers", [typeof(int), typeof(int), typeof(int[])], source, buffers.Length, buffers);

        private void Run()
        {
            try
            {
                while (!_stop && !(bool)_cancelled.GetValue(_streamer)!)
                {
                    Thread.Sleep(_updateInterval);
                    object[] snapshot;
                    lock (_iterationMutex)
                        snapshot = ((System.Collections.IEnumerable)_streams.GetValue(_streamer)!).Cast<object>().ToArray();
                    foreach (var stream in snapshot) Pump(stream);
                    foreach (var stale in _atEnd.Keys.Except(snapshot).ToArray())
                    {
                        // Pause removes a live stream from the native registry. Its EOF
                        // still describes the retained queue and must survive resume.
                        // New starts explicitly reset it; disposal closes the reader.
                        lock (Field(stale, "prepareMutex"))
                            if (!Registered(stale) && Property(stale, "Reader") is null)
                                _atEnd.TryRemove(stale, out _);
                    }
                    Interlocked.Increment(ref _completedPumpCycles);
                }
            }
            catch (Exception failure)
            {
                Volatile.Write(ref _failure, failure);
            }
        }

        private bool Registered(object stream)
        {
            lock (_iterationMutex)
                return ((System.Collections.IEnumerable)_streams.GetValue(_streamer)!).Cast<object>().Contains(stream);
        }

        private void Pump(object stream)
        {
            Action? completed = null;
            long completionGeneration = 0;
            lock (Field(stream, "prepareMutex"))
            {
                if (!Registered(stream)) return;
                var source = (int)Field(stream, "alSourceId");
                var queued = Query(source, "BuffersQueued");
                var processed = Query(source, "BuffersProcessed");
                int[] available;
                if (processed > 0)
                    available = (int[])Call("SourceUnqueueBuffers", [typeof(int), typeof(int)], source, processed)!;
                else
                    available = ((int[])Field(stream, "alBufferIds")).Skip(queued).ToArray();
                queued -= processed;
                var tail = _atEnd.GetValueOrDefault(stream);
                var filled = new List<int>();
                foreach (var buffer in available)
                {
                    if (tail) break;
                    var reader = Property(stream, "Reader");
                    var position = reader.GetType().GetProperty("TimePosition")!;
                    var before = (TimeSpan)position.GetValue(reader)!;
                    tail = (bool)_fillBuffer.Invoke(_streamer, [stream, buffer])!;
                    // A short final buffer is valid; an empty EOF buffer is not.
                    if ((TimeSpan)position.GetValue(reader)! > before) filled.Add(buffer);
                }
                if (filled.Count > 0)
                {
                    Queue(source, filled.ToArray());
                    queued += filled.Count;
                }
                _atEnd[stream] = tail;
                if (tail && queued == 0)
                {
                    lock (_iterationMutex)
                        _streams.GetValue(_streamer)!.GetType().GetMethod("Remove")!.Invoke(_streams.GetValue(_streamer), [stream]);
                    _atEnd.TryRemove(stream, out _);
                    completionGeneration = Volatile.Read(ref _generation);
                    completed = (Action)Property(stream, "FinishedAction");
                }
            }
            // Completion calls MediaPlayer.Stop, which takes stopMutex then prepareMutex.
            if (completed is not null)
            {
                lock (CompletionMutex)
                    if (completionGeneration == Volatile.Read(ref _generation)) completed();
                return;
            }
            lock (Field(stream, "stopMutex"))
            {
                if (!Registered(stream) || (bool)Property(stream, "Preparing")) return;
                var source = (int)Field(stream, "alSourceId");
                var state = Call("GetSourceState", [typeof(int)], source)!.ToString();
                if (state == "Stopped") Call("SourcePlay", [typeof(int)], source);
            }
        }
    }
}