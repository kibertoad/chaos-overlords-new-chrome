using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;

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

    /// <summary>
    /// Whether a listing survives the filters as they currently stand.
    /// </summary>
    /// <param name="statusFilter">The stored <see cref="Status"/> value: 0 keeps every state.</param>
    /// <param name="scenarioFilter">The stored <see cref="Scenario"/> value, or -1 for any.</param>
    /// <param name="aiFilter">The stored <see cref="Ai"/> value, or -1 for any.</param>
    /// <param name="status">The state the server listed the session in.</param>
    /// <param name="settings">
    /// The session's settings, or null when this build cannot read the blob it carries.
    /// </param>
    /// <remarks>
    /// Unread settings are not a reason to hide a session. A blob written by a build with a
    /// scenario or a mentality this one does not have still describes a session that can be joined
    /// — the setup screen leaves such a blob alone and the match reports it properly at start — so
    /// it only falls out of the list when a filter is actually asking about what it could not read.
    /// That keeps the unfiltered list what its name says: every public session the server has.
    /// </remarks>
    public static bool Matches(
        int statusFilter,
        int scenarioFilter,
        int aiFilter,
        MatchStatus status,
        MultiplayerGameSettings? settings)
    {
        if (statusFilter == 1 && status != MatchStatus.Lobby) return false;
        if (statusFilter == 2 && status != MatchStatus.Running) return false;
        if (scenarioFilter < 0 && aiFilter < 0) return true;
        if (settings is not { } known) return false;
        return (scenarioFilter < 0 || (int)known.Scenario == scenarioFilter)
            && (aiFilter < 0 || (int)known.AiMentality == aiFilter);
    }
}
