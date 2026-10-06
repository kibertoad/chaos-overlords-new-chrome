namespace Rechaos.OriginalProbe;

/// <summary>
/// One click of <c>--search-clicks</c> and what followed it: whether the Search panel was open, the
/// active player, and the whole search_filters table, 22 bytes for each of the six players
/// (FND-SEARCH-001).
/// </summary>
internal sealed record SearchClickRecord(int X, int Y, bool PanelOpen, int ActivePlayer, List<int> Filters);

internal sealed partial class NewGameSession
{
    private readonly List<SearchClickRecord> _searchClicks = [];

    // RULE-SEARCH-001: once the dump is taken, the probe posts each click to the window as a
    // player's press, lets the original handle it, and keeps the filter table, the active player and
    // whether the Search handler fn_00448E32 (FND-SEARCH-002) is running. The clicks come after the
    // dump: a filter changes only the markers the city draws, never the match.
    private bool RecordSearchClicks(IntPtr window)
    {
        var open = false;
        _process.SetBreakpoint(OriginalAddresses.SearchPanel, context =>
        {
            open = true;
            _process.SetBreakpoint(context.ReturnAddress, _ => open = false, oneShot: true);
        });
        foreach (var (x, y) in settings.SearchClicks!)
        {
            Click(window, x, y);
            _process.Pump(TimeSpan.FromSeconds(0.5));
            if (_process.Exited) return false;
            var player = _process.ReadInt32(OriginalAddresses.ActivePlayer);
            var filters = _process.Read(OriginalAddresses.SearchFilters, 6 * OriginalAddresses.SiteDefinitionCount);
            _searchClicks.Add(new SearchClickRecord(x, y, open, player, filters.Select(value => (int)value).ToList()));
        }
        return true;
    }
}
