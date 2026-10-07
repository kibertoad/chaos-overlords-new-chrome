namespace Rechaos.Tests;

/// <summary>
/// Computes the slow part of a theory's rows on background workers, ahead of the rows. xUnit runs
/// the rows of one class one after another, so a class whose rows each replay a match or start the
/// game is a chain no other core can help with. Once a second row asks for its value, every key
/// is queued for <see cref="RowPrefetchWorkers.Count"/> threads; a row whose key no worker has taken
/// yet computes it on its own thread, and a row whose key is taken waits for it. A run that asks
/// for one row queues nothing. A filtered run that asks for two or more rows still queues every
/// key, and the workers compute values no row reads until the test host exits
/// (<see cref="RowPrefetchWorkers"/> stops them). A key asked for again after its row had it is
/// computed again.
/// </summary>
/// <remarks>
/// The value, or the exception computing it threw, reaches the row that asks for its key, so a
/// failure or an <c>Assert.Skip</c> is reported by that row alone. The workers start without the
/// asking test's execution context, so <paramref name="compute"/> must not write to
/// <c>TestContext.Current</c>.
/// </remarks>
internal sealed class RowPrefetch<TKey, TValue>(Func<IEnumerable<TKey>> keys, Func<TKey, TValue> compute)
    where TKey : notnull
{
    private sealed class Slot
    {
        private int _claimed;
        public TaskCompletionSource<TValue> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Claim() => Interlocked.Exchange(ref _claimed, 1) == 0;
    }

    private readonly object _gate = new();
    private readonly Dictionary<TKey, Slot> _slots = [];
    private readonly HashSet<TKey> _asked = [];
    private bool _queued;
    private int _requests;

    public TValue Get(TKey key)
    {
        Slot slot;
        lock (_gate)
        {
            _asked.Add(key);
            if (++_requests == 2) Queue();
            if (!_slots.TryGetValue(key, out slot!)) _slots[key] = slot = new Slot();
        }
        if (slot.Claim()) Fill(key, slot);
        try
        {
            return slot.Result.Task.GetAwaiter().GetResult();
        }
        finally
        {
            // Each row asks once, so the value is not kept after it has it.
            lock (_gate) _slots.Remove(key);
        }
    }

    // Called under the lock.
    private void Queue()
    {
        if (_queued) return;
        _queued = true;
        List<TKey> listed;
        try
        {
            listed = keys().Distinct().Where(key => !_asked.Contains(key)).ToList();
        }
        catch (Exception)
        {
            // The row asking has nothing to do with the failure, so nothing is queued and every
            // row computes its own value. The rows' data comes from the same source and reports it.
            return;
        }
        var pending = new Queue<(TKey, Slot)>();
        foreach (var key in listed)
        {
            if (!_slots.TryGetValue(key, out var slot)) _slots[key] = slot = new Slot();
            pending.Enqueue((key, slot));
        }
        using var _ = ExecutionContext.SuppressFlow();
        for (var worker = 0; worker < RowPrefetchWorkers.Count; worker++)
            new Thread(() =>
            {
                while (true)
                {
                    (TKey Key, Slot Slot) next;
                    lock (pending)
                        if (!pending.TryDequeue(out next)) return;
                    // The slot is claimed only once a share of the budget is held, so a row that
                    // reaches it first computes it rather than wait behind another prefetch.
                    try
                    {
                        RowPrefetchWorkers.Budget.Wait(RowPrefetchWorkers.Stopping);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    try
                    {
                        if (!RowPrefetchWorkers.Stopping.IsCancellationRequested && next.Slot.Claim())
                            Fill(next.Key, next.Slot);
                    }
                    finally
                    {
                        RowPrefetchWorkers.Budget.Release();
                    }
                }
            })
            { IsBackground = true, Name = $"RowPrefetch {worker}" }.Start();
    }

    private void Fill(TKey key, Slot slot)
    {
        try
        {
            slot.Result.SetResult(compute(key));
        }
        catch (Exception exception)
        {
            slot.Result.SetException(exception);
        }
    }
}

/// <summary>
/// The workers every <see cref="RowPrefetch{TKey, TValue}"/> shares. Each prefetch starts this many
/// threads, and the budget lets only this many compute at once across all of them.
/// </summary>
/// <remarks>
/// A filtered run can end while workers compute values no row asked for. When the test host
/// exits, <see cref="Stopping"/> is cancelled: no worker takes another key, a game a worker started
/// is killed (see <c>RebuildFrame.Render</c>), and the exit waits up to
/// <see cref="DrainTimeout"/> for the running computations, so their temporary folders are deleted.
/// </remarks>
internal static class RowPrefetchWorkers
{
    private static readonly CancellationTokenSource Exit = new();

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(30);

    static RowPrefetchWorkers() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Drain();

    // Half the cores, since the other test classes run beside these workers.
    public static int Count { get; } = Math.Max(2, Environment.ProcessorCount / 2);

    public static SemaphoreSlim Budget { get; } = new(Count);

    /// <summary>Cancelled when the test host exits.</summary>
    public static CancellationToken Stopping => Exit.Token;

    private static void Drain()
    {
        Exit.Cancel();
        // A worker holds a share while it computes, so holding every share means none is running.
        var deadline = DateTime.UtcNow + DrainTimeout;
        for (var share = 0; share < Count; share++)
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero || !Budget.Wait(left)) return;
        }
    }
}
