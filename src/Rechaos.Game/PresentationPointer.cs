using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>The two stock pointer shapes the original ever selects (RULE-UI-007).</summary>
public enum PointerShape
{
    Arrow = 0,
    Hourglass = 4
}

/// <summary>
/// Shows the hourglass around work that takes no input, and the arrow otherwise (RULE-UI-007).
/// </summary>
/// <remarks>
/// The original selects the stock hourglass before it sets up a city, loads a game, plans a
/// computer's turn or resolves a turn, and the stock arrow after. Between two stretches it takes
/// no input (EXP-UI-022), so from the Done press to the next human planning entry the player sees
/// the hourglass. The rebuild does a stretch inside one update and plans the computers in the next,
/// so between updates it shows the shape <see cref="Idle"/> gives: the hourglass while a
/// computer's planning is still to run. A music fade can hold that planning back while it
/// dispatches messages (FND-AUDIO-016), and a pointer moved then shows the arrow
/// (<see cref="PointerMoved"/>).
/// </remarks>
public sealed class PresentationPointer
{
    private readonly Action<PointerShape> _apply;
    private readonly Func<PointerShape> _idle;
    private int _busyDepth;
    private PointerShape? _shown;
    private bool _movedSinceWork;

    public PresentationPointer(Action<PointerShape> apply, Func<PointerShape>? idle = null)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _idle = idle ?? (() => PointerShape.Arrow);
    }

    /// <summary>
    /// The shape between updates of a local match: the hourglass when the active player of the
    /// planning phase is a computer, whose planning runs without taking input, and the arrow when
    /// a human plans or the match has ended.
    /// </summary>
    public static PointerShape Idle(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Outcome is null && state.Coordinator.Phase == TurnPhase.Command
            && state.Coordinator.ActivePlayer is { } player
            && state.FindPlayer(player)?.Setup.Controller == PlayerController.Computer
                ? PointerShape.Hourglass
                : PointerShape.Arrow;
    }

    /// <summary>Shows the hourglass until the returned scope is disposed.</summary>
    /// <remarks>
    /// Scopes nest: the idle shape comes back when the outermost one ends, and a scope's end still
    /// puts it back when the work inside it throws.
    /// </remarks>
    public IDisposable Busy()
    {
        if (_busyDepth++ == 0)
        {
            _movedSinceWork = false;
            Show(PointerShape.Hourglass);
        }
        return new BusyScope(this);
    }

    /// <summary>
    /// A pointer move the window handled while it dispatched messages: shows the arrow until the
    /// next busy scope, as the original's answer to each pointer message does (RULE-UI-007).
    /// </summary>
    public void PointerMoved()
    {
        _movedSinceWork = true;
        if (_busyDepth == 0) Show(PointerShape.Arrow);
    }

    /// <summary>Shows the idle shape unless a busy scope is open.</summary>
    public void Refresh()
    {
        if (_busyDepth == 0) Show(IdleShape());
    }

    private PointerShape IdleShape()
    {
        var shape = _idle();
        // A move counts only while the hourglass waits; once the idle shape is the arrow, the
        // next hourglass needs a move of its own to be replaced.
        if (shape == PointerShape.Arrow) _movedSinceWork = false;
        return _movedSinceWork ? PointerShape.Arrow : shape;
    }

    private void Show(PointerShape shape)
    {
        if (_shown == shape) return;
        // Remembered only once applied, so a shape that failed to apply is tried again.
        _apply(shape);
        _shown = shape;
    }

    private void EndBusy()
    {
        if (--_busyDepth == 0) Show(IdleShape());
    }

    private sealed class BusyScope(PresentationPointer owner) : IDisposable
    {
        private PresentationPointer? _owner = owner;

        public void Dispose()
        {
            _owner?.EndBusy();
            _owner = null;
        }
    }
}
