namespace Rechaos.OriginalProbe;

/// <summary>
/// 32-bit values the probe writes at <paramref name="Address"/> when the planning-entry function
/// starts to draw the console (FND-UI-040): the first at its first call, the second at its second,
/// and the last at every later call. The console then draws a number the match would not reach,
/// such as a score whose first glyph cell lies outside the glyph sheet (RULE-UI-004), over what an
/// earlier call drew. The write changes the match from then on, so a run that uses it is not
/// replayed. The calls are counted over every human's entries, so the probe takes it with one
/// human only.
/// </summary>
internal sealed record ProbeDrawValue(uint Address, IReadOnlyList<int> Values)
{
    public override string ToString() => $"draw_value 0x{Address:X8} {string.Join("/", Values)}";
}

/// <summary>
/// A left-button press and release the probe posts at a client point once the dump is taken
/// (<c>--search-clicks</c>, SearchClickRecord).
/// </summary>
internal sealed record ProbeClick(int X, int Y)
{
    public override string ToString() => $"({X}, {Y}) after the dump";
}

/// <summary>
/// One call of a planning entry panel (RULE-SETUP-008): Combat Results or Last Turn Events, the
/// roll count when it was called, and whether it showed its panel, which a call does when it
/// reaches its call of the panel-open helper (FND-UI-061). A call with nothing to show returns
/// at once.
/// </summary>
internal sealed record PanelRecord(string Panel, int AfterRoll, bool Shown);

/// <summary>
/// The values one Financial panel drew, in the order it drew them (FND-FINANCE-003), with the sector
/// the probe asked for and the sector the panel function was passed, -2 when it was not called.
/// </summary>
internal sealed record FinanceRecord(int Turn, int Sector, int PanelSector, List<int> Values);
