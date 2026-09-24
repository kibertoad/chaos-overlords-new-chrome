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
/// The first ask comes <see cref="FirstAsk"/> after the later of the deadline and the moment the
/// watch began, and then once per repeat interval for as long as the same deadline stays unsealed.
/// The watch begins again on a new deadline, a new planning turn, and after <see cref="Stop"/>.
/// Measuring from the watch as well as from the deadline is what gives a stream that has just come
/// back — after a lost connection, or a laptop that slept through the deadline — the time to deliver
/// the seal it is replaying, rather than cancelling it on the first frame to rebuild instead. The
/// planning turn is part of the key because a resolved turn opens the next one before the stream
/// has delivered that turn's deadline: for that moment the deadline on screen is still the sealed
/// turn's, long past, and it must not be taken for a seal that never came.
/// </para>
/// </remarks>
public sealed class OnlineOverdueSealWatchdog(TimeSpan repeat)
{
    /// <summary>How long past the deadline an ordinary seal is given before anyone asks.</summary>
    public static readonly TimeSpan FirstAsk = TimeSpan.FromSeconds(10);

    private int? _turn;
    private DateTimeOffset? _deadline;
    private TimeSpan? _nextAsk;

    /// <summary>
    /// Whether to resynchronise this frame.
    /// </summary>
    /// <param name="turn">The planning turn the deadline is shown for.</param>
    /// <param name="deadline">The open turn's deadline, or null when nothing is counting down.</param>
    /// <param name="serverNow">Now, on the server's clock the deadline is measured on.</param>
    /// <param name="monotonicNow">Now, on the clock the repeat is measured on.</param>
    public bool Advance(
        int turn, DateTimeOffset? deadline, DateTimeOffset serverNow, TimeSpan monotonicNow)
    {
        if (turn != _turn || deadline != _deadline)
        {
            _turn = turn;
            _deadline = deadline;
            _nextAsk = monotonicNow + FirstAsk;
        }
        if (deadline is not { } dueAt || serverNow - dueAt < FirstAsk) return false;
        if (_nextAsk is { } next && monotonicNow < next) return false;
        _nextAsk = monotonicNow + repeat;
        return true;
    }

    /// <summary>Forgets the deadline being watched; the next <see cref="Advance"/> watches afresh.</summary>
    public void Stop()
    {
        _turn = null;
        _deadline = null;
        _nextAsk = null;
    }
}
