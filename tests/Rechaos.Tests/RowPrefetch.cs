namespace Rechaos.Tests;

/// <summary>
/// Computes the slow part of a theory's rows on background workers, ahead of the rows. xUnit runs
/// the rows of one class one after another, so a class whose rows each replay a match or start the
/// game is a chain no other core can help with. Once a second row asks for its value, every key
/// is queued for <see cref="Workers"/> threads; a row whose key no worker has taken yet computes
/// it on its own thread, and a row whose key is taken waits for it. A run that asks for one row
/// queues nothing, so a filtered run does no work it does not need. A key asked for again after
/// its row had it is computed again.
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
    // Half the cores, since the other test classes run beside these workers.
    public static int Workers { get; } = Math.Max(2, Environment.ProcessorCount / 2);

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
        var pending = new Queue<(TKey, Slot)>();
        foreach (var key in keys().Distinct().Where(key => !_asked.Contains(key)))
        {
            if (!_slots.TryGetValue(key, out var slot)) _slots[key] = slot = new Slot();
            pending.Enqueue((key, slot));
        }
        using var _ = ExecutionContext.SuppressFlow();
        for (var worker = 0; worker < Workers; worker++)
            new Thread(() =>
            {
                while (true)
                {
                    (TKey Key, Slot Slot) next;
                    lock (pending)
                        if (!pending.TryDequeue(out next)) return;
                    if (next.Slot.Claim()) Fill(next.Key, next.Slot);
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
