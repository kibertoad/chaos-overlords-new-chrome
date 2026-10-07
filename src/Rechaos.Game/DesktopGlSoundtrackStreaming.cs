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

    /// <summary>Starts or restarts the worker. Call it before taking the transport lock:
    /// a restart joins the previous worker, which may be waiting on that lock.</summary>
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

    internal static void ResetForNewSong(Song song) =>
        (_backend ?? throw new InvalidOperationException("Soundtrack streaming is not initialized.")).Reset(song);

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
        private readonly object _iterationMutex;
        private readonly object _registry;
        private readonly FieldInfo _cancelled;
        private readonly FieldInfo _songStream;
        private readonly FieldInfo _prepareMutex;
        private readonly FieldInfo _stopMutex;
        private readonly FieldInfo _sourceId;
        private readonly FieldInfo _bufferIds;
        private readonly PropertyInfo _reader;
        private readonly PropertyInfo _preparing;
        private readonly PropertyInfo _finishedAction;
        private readonly MethodInfo _fillBuffer;
        private readonly MethodInfo _removeStream;
        private readonly MethodInfo _registryContains;
        private readonly MethodInfo _getSource;
        private readonly MethodInfo _getSourceState;
        private readonly MethodInfo _sourcePlay;
        private readonly MethodInfo _queueBuffers;
        private readonly MethodInfo _unqueueBuffers;
        private readonly MethodInfo _getError;
        private readonly object _buffersQueued;
        private readonly object _buffersProcessed;
        private PropertyInfo? _timePosition;
        // Streams whose decoder has reached EOF; a stream is absent until it does.
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
            var streamType = assembly.GetType("Microsoft.Xna.Framework.Audio.OggStream", true)!;
            var al = assembly.GetType("MonoGame.OpenAL.AL", true)!;
            var sourceQuery = assembly.GetType("MonoGame.OpenAL.ALGetSourcei", true)!;
            _streamer = type.GetProperty("Instance", Members)!.GetValue(null)!;
            var updateRate = (float)type.GetProperty("UpdateRate", Members)!.GetValue(_streamer)!;
            _updateInterval = (int)(1000 / (updateRate > 0 ? updateRate : 1));
            _iterationMutex = type.GetField("iterationMutex", Members)!.GetValue(_streamer)!;
            _registry = type.GetField("streams", Members)!.GetValue(_streamer)!;
            _registryContains = _registry.GetType().GetMethod("Contains")!;
            _cancelled = type.GetField("cancelled", Members)!;
            _fillBuffer = type.GetMethod("FillBuffer", Members)!;
            _removeStream = type.GetMethod("RemoveStream", Members)!;
            _songStream = typeof(Song).GetField("stream", Members)!;
            _prepareMutex = streamType.GetField("prepareMutex", Members)!;
            _stopMutex = streamType.GetField("stopMutex", Members)!;
            _sourceId = streamType.GetField("alSourceId", Members)!;
            _bufferIds = streamType.GetField("alBufferIds", Members)!;
            _reader = streamType.GetProperty("Reader", Members)!;
            _preparing = streamType.GetProperty("Preparing", Members)!;
            _finishedAction = streamType.GetProperty("FinishedAction", Members)!;
            _getSource = al.GetMethod("GetSource", Members, null, [typeof(int), sourceQuery, typeof(int).MakeByRefType()], null)!;
            _getSourceState = al.GetMethod("GetSourceState", Members, null, [typeof(int)], null)!;
            _sourcePlay = al.GetMethod("SourcePlay", Members, null, [typeof(int)], null)!;
            _queueBuffers = al.GetMethod("SourceQueueBuffers", Members, null, [typeof(int), typeof(int), typeof(int[])], null)!;
            _unqueueBuffers = al.GetMethod("SourceUnqueueBuffers", Members, null, [typeof(int), typeof(int)], null)!;
            _getError = al.GetMethod("GetError", Members, null, Type.EmptyTypes, null)!;
            _buffersQueued = Enum.Parse(sourceQuery, "BuffersQueued");
            _buffersProcessed = Enum.Parse(sourceQuery, "BuffersProcessed");
            var stockWorker = (Thread)type.GetField("underlyingThread", Members)!.GetValue(_streamer)!;
            if (stockWorker.IsAlive)
            {
                // This runs before a soundtrack starts. Quiesce the stock worker, which
                // discards a whole fill batch when its last read reaches EOF. Its shutdown
                // clears the stream registry, so a restarted replacement must not repeat it.
                type.GetMethod("Shutdown", Members)!.Invoke(_streamer, null);
                stockWorker.Join();
            }
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

        private object Stream(Song song) => _songStream.GetValue(song)!;
        private static object MutexOf(FieldInfo mutex, object stream) => mutex.GetValue(stream)!;

        internal bool IsPending(Song song) => _atEnd.GetValueOrDefault(Stream(song));

        internal void CancelCompletion() => Interlocked.Increment(ref _generation);

        internal void Reset(Song song)
        {
            var stream = Stream(song);
            lock (MutexOf(_prepareMutex, stream))
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

        private object? Call(MethodInfo method, params object?[] args)
        {
            var result = method.Invoke(null, args);
            var error = _getError.Invoke(null, null);
            if (Convert.ToInt32(error) != 0)
                throw new InvalidOperationException($"OpenAL {method.Name} failed: {error}.");
            return result;
        }

        private int Query(int source, object parameter)
        {
            object?[] args = [source, parameter, 0];
            Call(_getSource, args);
            return (int)args[2]!;
        }

        private bool StopRequested => _stop || (bool)_cancelled.GetValue(_streamer)!;

        private void Run()
        {
            try
            {
                while (!StopRequested)
                {
                    Thread.Sleep(_updateInterval);
                    // Shutdown may arrive during the sleep; do not touch native sources after it.
                    if (StopRequested) break;
                    object[] snapshot;
                    lock (_iterationMutex)
                        snapshot = ((System.Collections.IEnumerable)_registry).Cast<object>().ToArray();
                    foreach (var stream in snapshot) Pump(stream);
                    foreach (var stale in _atEnd.Keys.Except(snapshot).ToArray())
                    {
                        // Pause removes a live stream from the native registry. Its EOF
                        // still describes the retained queue and must survive resume.
                        // New starts explicitly reset it; disposal closes the reader.
                        lock (MutexOf(_prepareMutex, stale))
                            if (!Registered(stale) && _reader.GetValue(stale) is null)
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
                return (bool)_registryContains.Invoke(_registry, [stream])!;
        }

        private void Pump(object stream)
        {
            Action? completed = null;
            long completionGeneration = 0;
            bool submitted;
            lock (MutexOf(_prepareMutex, stream))
            {
                if (!Registered(stream)) return;
                var source = (int)_sourceId.GetValue(stream)!;
                var queued = Query(source, _buffersQueued);
                var processed = Query(source, _buffersProcessed);
                int[] available;
                if (processed > 0)
                    available = (int[])Call(_unqueueBuffers, source, processed)!;
                else
                    available = ((int[])_bufferIds.GetValue(stream)!).Skip(queued).ToArray();
                queued -= processed;
                var tail = _atEnd.GetValueOrDefault(stream);
                var filled = new List<int>();
                foreach (var buffer in available)
                {
                    if (tail) break;
                    var reader = _reader.GetValue(stream)!;
                    _timePosition ??= reader.GetType().GetProperty("TimePosition")!;
                    var before = (TimeSpan)_timePosition.GetValue(reader)!;
                    tail = (bool)_fillBuffer.Invoke(_streamer, [stream, buffer])!;
                    // A short final buffer is valid; an empty EOF buffer is not.
                    if ((TimeSpan)_timePosition.GetValue(reader)! > before) filled.Add(buffer);
                }
                submitted = filled.Count > 0;
                if (submitted)
                {
                    Call(_queueBuffers, source, filled.Count, filled.ToArray());
                    queued += filled.Count;
                }
                // Only pending EOF is kept, so the stale scan below visits tails alone.
                if (tail) _atEnd[stream] = true;
                if (tail && queued == 0)
                {
                    _removeStream.Invoke(_streamer, [stream]);
                    _atEnd.TryRemove(stream, out _);
                    completionGeneration = Volatile.Read(ref _generation);
                    completed = (Action)_finishedAction.GetValue(stream)!;
                }
            }
            // Completion calls MediaPlayer.Stop, which takes stopMutex then prepareMutex.
            if (completed is not null)
            {
                lock (CompletionMutex)
                    if (completionGeneration == Volatile.Read(ref _generation)) completed();
                return;
            }
            // Restart only to play newly submitted data. Without it, a source that drains
            // after the queue query would replay its unqueued final buffers from the start.
            if (!submitted) return;
            lock (MutexOf(_stopMutex, stream))
            {
                if (!Registered(stream) || (bool)_preparing.GetValue(stream)!) return;
                var source = (int)_sourceId.GetValue(stream)!;
                var state = Call(_getSourceState, source)!.ToString();
                if (state == "Stopped") Call(_sourcePlay, source);
            }
        }
    }
}
