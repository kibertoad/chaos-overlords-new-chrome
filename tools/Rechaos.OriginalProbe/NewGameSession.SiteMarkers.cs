namespace Rechaos.OriginalProbe;

/// <summary>
/// One city redraw (FND-SEARCH-006): the viewing player and each site marker it drew as
/// definition, sector, ordinal and controlled flag.
/// </summary>
internal sealed record CityMarkers(int Viewer, List<int[]> Markers);

internal sealed partial class NewGameSession
{
    private CityMarkers? _redraw;
    private CityMarkers? _lastRedraw;

    // FND-SEARCH-006: each city redraw's markers, kept once the redraw returns; the dump keeps the
    // last complete redraw, whichever human it was drawn for, with that viewer. FND-SEARCH-007: both
    // callers push the viewer as a full dword, so it is read unmasked.
    private void OnCityRedraw(BreakContext context)
    {
        var redraw = new CityMarkers(context.Argument(0), []);
        _redraw = redraw;
        _process.SetBreakpoint(context.ReturnAddress, _ =>
        {
            if (_redraw == redraw) _lastRedraw = redraw;
            _redraw = null;
        }, oneShot: true);
    }

    // FND-SEARCH-007: both calls push the definition sign-extended from its byte, the sector and the
    // ordinal from dword locals, and the controlled flag as an immediate 1 or 0, so all four are full
    // dwords with no leftover high bits and are read unmasked.
    private void OnSiteMarker(BreakContext context) =>
        _redraw?.Markers.Add([context.Argument(0), context.Argument(1), context.Argument(2), context.Argument(3)]);
}
