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
/// The objective, Mentality and planning limit the probe writes once the preference loader has
/// returned, in a run that copies the setup screen (<c>--setup-capture</c> or <c>--setup-steps</c>),
/// so the screen opens with them (RULE-OPTIONS-001, RULE-SETUP-002). <see cref="GogInstallation"/>
/// is what GOG's installer stores (SRC-INSTALLER-GOG), and the default; <see cref="Initialized"/>
/// is what the executable's data holds when the registry key is absent (FND-OPTIONS-001), which the
/// runs made before the option existed showed.
/// </summary>
internal sealed record ProbeSetupPreferences(int Scenario, int Mentality, int PlanningLimit)
{
    public static readonly ProbeSetupPreferences GogInstallation = new(4, 0, 0);
    public static readonly ProbeSetupPreferences Initialized = new(0, 1, 0);

    public static ProbeSetupPreferences Parse(string value) => value.Split(':') switch
    {
        [var scenario, var mentality, var limit]
            when Number(scenario, out var s) && s <= 9
                 && Number(mentality, out var m) && m <= 3
                 && Number(limit, out var l) && l <= 3 => new(s, m, l),
        _ => throw new ArgumentException(
            $"--setup-preferences takes <scenario 0-9>:<mentality 0-3>:<planning limit 0-3>, not {value}."),
    };

    // Digits only, as the rebuild's --setup-preferences reads them: no sign, space or separator.
    private static bool Number(string value, out int number) => int.TryParse(value,
        System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number);

    public override string ToString() => $"{Scenario}:{Mentality}:{PlanningLimit}";
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
