namespace Rechaos.Game;

/// <summary>
/// Notices an online turn whose deadline has passed without the server's seal arriving.
/// </summary>
/// <remarks>
/// <para>
/// A client never seals an online turn itself; when the countdown reaches zero it shows
/// "SEALING" and waits for the server. The resolution watchdog only covered the turn every seat
/// had finished, so a turn that ran out of time while the player was still planning was covered
/// by nothing: a seal the server was late with, or one the event stream never delivered, left the
/// countdown on zero until the player ended the turn by hand. A resynchronisation settles both —
/// the rebuild reads the match view, which also has the server seal a turn it is overdue on.
/// </para>
/// <para>
/// The first ask comes after <see cref="FirstAsk"/>, long enough for an ordinary seal to land, and
/// then once per repeat interval for as long as the same deadline stays unsealed. A new
/// deadline — the next turn, or a restarted clock — starts over.
/// </para>
/// </remarks>
public sealed class OnlineOverdueSealWatchdog(TimeSpan repeat)
{
    /// <summary>How long past the deadline an ordinary seal is given before anyone asks.</summary>
    public static readonly TimeSpan FirstAsk = TimeSpan.FromSeconds(10);

    private DateTimeOffset? _deadline;
    private TimeSpan? _nextAsk;

    /// <summary>
    /// Whether to resynchronise this frame.
    /// </summary>
    /// <param name="deadline">The open turn's deadline, or null when nothing is counting down.</param>
    /// <param name="serverNow">Now, on the server's clock the deadline is measured on.</param>
    /// <param name="monotonicNow">Now, on the clock the repeat is measured on.</param>
    public bool Advance(DateTimeOffset? deadline, DateTimeOffset serverNow, TimeSpan monotonicNow)
    {
        if (deadline != _deadline)
        {
            _deadline = deadline;
            _nextAsk = null;
        }
        if (deadline is not { } dueAt || serverNow - dueAt < FirstAsk) return false;
        if (_nextAsk is { } next && monotonicNow < next) return false;
        _nextAsk = monotonicNow + repeat;
        return true;
    }

    /// <summary>Forgets the deadline being watched.</summary>
    public void Stop()
    {
        _deadline = null;
        _nextAsk = null;
    }
}
