using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The three filters over the public session list, described as option lists rather than as
/// counters.
/// </summary>
/// <remarks>
/// Each filter is stored as a single integer, but the two enum-backed ones use -1 for "any" while
/// the status filter starts at 0, so the stored value and the row a dropdown draws are not the same
/// number. Everything that converts between them lives here, so a screen only ever deals in option
/// indices.
/// </remarks>
public static class DiscoveryFilters
{
    public const int Status = 0;
    public const int Scenario = 1;
    public const int Ai = 2;

    /// <summary>How many filters there are, and so how many dropdowns the discovery screen has.</summary>
    public const int Count = 3;

    private static readonly string[] StatusLabels = ["ALL STATES", "WAITING", "ONGOING"];

    public static int OptionCount(int filter) => filter switch
    {
        Status => StatusLabels.Length,
        Scenario => Enum.GetValues<ScenarioId>().Length + 1,
        Ai => Enum.GetValues<AiDifficulty>().Length + 1,
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    public static string Label(int filter, int option) => filter switch
    {
        Status => StatusLabels[option],
        Scenario => option == 0
            ? "ALL MODES"
            : ScenarioCatalog.Get((ScenarioId)(option - 1)).Name,
        Ai => option == 0
            ? "ALL AI"
            : DifficultyPresentation.Label((AiDifficulty)(option - 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    /// <summary>The dropdown row that shows a stored filter value.</summary>
    public static int OptionOf(int filter, int value) => filter == Status ? value : value + 1;

    /// <summary>The value to store when a dropdown row is chosen.</summary>
    public static int ValueOf(int filter, int option) => filter == Status ? option : option - 1;
}
