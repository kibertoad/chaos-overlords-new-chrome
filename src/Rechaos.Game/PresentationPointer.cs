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
/// The original selects the stock hourglass before it sets up a city, loads a game or resolves a
/// turn, and the stock arrow after. The rebuild does that work inside one update, where no pointer
/// message is handled either, so the hourglass stays up for as long as the work runs, as it does in
/// the original.
/// </remarks>
public sealed class PresentationPointer
{
    private readonly Action<PointerShape> _apply;
    private int _busyDepth;

    public PresentationPointer(Action<PointerShape> apply)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    /// <summary>Shows the hourglass until the returned scope is disposed.</summary>
    /// <remarks>
    /// Scopes nest: the arrow comes back when the outermost one ends, and a scope's end still puts
    /// the arrow back when the work inside it throws.
    /// </remarks>
    public IDisposable Busy()
    {
        if (_busyDepth++ == 0) _apply(PointerShape.Hourglass);
        return new BusyScope(this);
    }

    private void EndBusy()
    {
        if (--_busyDepth == 0) _apply(PointerShape.Arrow);
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
