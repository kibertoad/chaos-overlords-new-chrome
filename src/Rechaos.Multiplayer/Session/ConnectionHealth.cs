namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Whether the server is answering, as several independent retry loops see it at once.
/// </summary>
/// <remarks>
/// <para>
/// Each loop that retries on its own — the event stream, the calls a received fact leads to, the
/// order outbox, the state-hash reporter — reports into a <see cref="Lane"/> of its own. The
/// combined answer is connected only when every lane is, so a submission that lands while the
/// stream is still down does not announce a recovery the player will watch fail again a second
/// later; and the detail shown is whichever lane is failing, because that is the one with
/// something to say.
/// </para>
/// <para>
/// Transitions are what the callback hears: a failure every time, because each attempt is a line
/// in the reconnect log, and a recovery once, when the last failing lane comes back. The callback
/// runs under the lock so that reports from two threads reach it in the order they were made, so
/// it has to be cheap and must not report back into this object.
/// </para>
/// </remarks>
public sealed class ConnectionHealth
{
    private readonly Lock _gate = new();
    private readonly List<Lane> _lanes = [];
    private readonly Action<bool, string?, int> _onChanged;
    private bool _connected = true;

    /// <param name="onChanged">
    /// Told whether the server is answering, what went wrong when it is not, and which attempt.
    /// </param>
    public ConnectionHealth(Action<bool, string?, int> onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        _onChanged = onChanged;
    }

    /// <summary>Whether every lane last reached the server.</summary>
    public bool IsConnected
    {
        get
        {
            lock (_gate) return _connected;
        }
    }

    /// <summary>A retry loop's own view, healthy until it says otherwise.</summary>
    public Lane Open(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var lane = new Lane(this, name);
        lock (_gate) _lanes.Add(lane);
        return lane;
    }

    private void Failed(Lane lane, string detail, int attempt)
    {
        lock (_gate)
        {
            lane.Healthy = false;
            _connected = false;
            _onChanged(false, detail, attempt);
        }
    }

    private void Recovered(Lane lane)
    {
        lock (_gate)
        {
            lane.Healthy = true;
            if (_connected) return;
            foreach (var other in _lanes)
            {
                if (!other.Healthy) return;
            }
            _connected = true;
            _onChanged(true, null, 0);
        }
    }

    /// <summary>One retry loop's health, reported by that loop alone.</summary>
    public sealed class Lane
    {
        private readonly ConnectionHealth _owner;

        internal Lane(ConnectionHealth owner, string name)
        {
            _owner = owner;
            Name = name;
        }

        public string Name { get; }

        /// <summary>Whether this lane's last attempt reached the server. Guarded by the owner.</summary>
        internal bool Healthy { get; set; } = true;

        /// <summary>An attempt failed and will be retried.</summary>
        public void Failed(string detail, int attempt) => _owner.Failed(this, detail, attempt);

        /// <summary>An attempt reached the server, or the failing work was abandoned.</summary>
        public void Recovered() => _owner.Recovered(this);
    }
}
