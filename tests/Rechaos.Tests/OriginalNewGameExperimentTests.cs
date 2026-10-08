using System.Text.Json;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // The replayed experiments, one per line and grouped by kind, so that experiments of different
    // kinds are added in different places.
    private static readonly string[] Experiments =
    [
        "EXP-SETUP-001",
        "EXP-SETUP-002",
        "EXP-SETUP-003",
        "EXP-SETUP-004",
        "EXP-SETUP-005",

        "EXP-TURN-001",
        "EXP-TURN-002",
        "EXP-TURN-003",
        "EXP-TURN-004",
        "EXP-TURN-005",
        "EXP-TURN-006",
        "EXP-TURN-007",
        "EXP-TURN-008",
        "EXP-TURN-009",
        "EXP-TURN-010",
        "EXP-TURN-011",
        "EXP-TURN-012",
        "EXP-TURN-013",
        "EXP-TURN-014",
        "EXP-TURN-015",
        "EXP-TURN-016",
        "EXP-TURN-017",
        "EXP-TURN-018",
        "EXP-TURN-019",
        "EXP-TURN-020",
        "EXP-TURN-021",
        "EXP-TURN-022",
        "EXP-TURN-023",
        "EXP-TURN-024",
        "EXP-TURN-025",
        "EXP-TURN-026",
        "EXP-TURN-027",
        "EXP-TURN-028",
        "EXP-TURN-029",
        "EXP-TURN-030",
        "EXP-TURN-031",
        "EXP-TURN-032",
        "EXP-TURN-033",
        "EXP-TURN-034",
        "EXP-TURN-035",
        "EXP-TURN-037",
        "EXP-TURN-038",
        "EXP-TURN-039",
        "EXP-TURN-040",
        "EXP-TURN-041",
        "EXP-TURN-042",
        "EXP-TURN-043",
        "EXP-TURN-044",
        "EXP-TURN-045",
        "EXP-TURN-046",
        "EXP-TURN-047",
        "EXP-TURN-048",
        "EXP-TURN-049",
        "EXP-TURN-050",
        "EXP-TURN-051",
        "EXP-TURN-052",
        "EXP-TURN-053",
        "EXP-TURN-054",
        "EXP-TURN-055",
        "EXP-TURN-056",
        "EXP-TURN-057",
        "EXP-TURN-058",
        "EXP-TURN-059",
        "EXP-TURN-060",
        "EXP-TURN-061",
        "EXP-TURN-062",
        "EXP-TURN-063",
        "EXP-TURN-064",
        "EXP-TURN-065",
        "EXP-TURN-066",
        "EXP-TURN-067",
        "EXP-TURN-068",
        "EXP-TURN-069",
        "EXP-TURN-070",
        "EXP-TURN-071",
        "EXP-TURN-072",
        "EXP-TURN-073",
        "EXP-TURN-074",
        "EXP-TURN-075",
        "EXP-TURN-076",
        "EXP-TURN-077",
        "EXP-TURN-078",
        "EXP-TURN-079",
        "EXP-TURN-080",
        "EXP-TURN-081",
        "EXP-TURN-082",
        "EXP-TURN-083",
        "EXP-TURN-084",
        "EXP-TURN-085",
        "EXP-TURN-086",
        "EXP-TURN-087",
        "EXP-TURN-088",
        "EXP-TURN-089",
        "EXP-TURN-090",
        "EXP-TURN-091",
        "EXP-TURN-093",
        "EXP-TURN-094",
        "EXP-TURN-095",
        "EXP-TURN-096",
        "EXP-TURN-097",
        "EXP-TURN-098",
        "EXP-TURN-099",
        "EXP-TURN-100",
        "EXP-TURN-101",
        "EXP-TURN-102",
        "EXP-TURN-103",
        "EXP-TURN-104",
        "EXP-TURN-105",
        "EXP-TURN-109",
        "EXP-TURN-110",
        "EXP-TURN-111",
        "EXP-TURN-114",
        "EXP-TURN-115",
        "EXP-TURN-116",
        "EXP-TURN-117",

        "EXP-UI-001",
        "EXP-UI-003",
        "EXP-UI-004",
        "EXP-UI-005",
        "EXP-UI-006",
        "EXP-UI-007",
        "EXP-UI-008",
        "EXP-UI-009",
        "EXP-UI-010",
        "EXP-UI-011",
        "EXP-UI-012",
        "EXP-UI-013",
        "EXP-UI-014",
        "EXP-UI-016",
        "EXP-UI-017",
        "EXP-UI-018",
        "EXP-UI-019",
        "EXP-UI-020",
        "EXP-UI-021",
        "EXP-UI-022",
        "EXP-UI-023",
        "EXP-UI-024",
        "EXP-UI-025",
        "EXP-UI-026",
        "EXP-UI-029",
        "EXP-UI-030",
        "EXP-UI-032",
        "EXP-UI-034",
        "EXP-UI-035",
        "EXP-UI-036",
        "EXP-UI-041",
        "EXP-UI-042",
        "EXP-UI-043",
        "EXP-UI-044",
        "EXP-UI-046",
        "EXP-UI-047",
        "EXP-UI-048",
        "EXP-UI-049",
        "EXP-UI-054",

        "EXP-EQUIP-001",
        "EXP-EQUIP-002",
        "EXP-EQUIP-003",

        "EXP-ATTACK-001",
        "EXP-ATTACK-002",
        "EXP-ATTACK-003",

        "EXP-SEARCH-001",
        "EXP-SEARCH-002",

        "EXP-HIRE-001",
        "EXP-HIRE-002",

        "EXP-COMBAT-001",
        "EXP-COMBAT-002",
        "EXP-COMBAT-003",
        "EXP-COMBAT-004",
        "EXP-COMBAT-005",
        "EXP-COMBAT-006",
        "EXP-COMBAT-007",
        "EXP-COMBAT-008",
        "EXP-COMBAT-009",

        "EXP-AUDIO-001",

        "EXP-VIDEO-001",
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, RecordedRun[]>> Recorded =
        new(() => Experiments.ToDictionary(experiment => experiment, LoadRuns));

    // Runs the rebuild does not replay, with the first roll that differs.
    // DEV-AI-002: in EXP-TURN-083 a family-7 gang of player 2 plans Influence in neutral sector 10,
    // which the original resolves and the rebuild gives no command, so the rebuild rolls ten dice
    // fewer in that turn's Instant phase and reaches the combat dice early.
    private static readonly Dictionary<(string Experiment, int Run), int> KnownDivergences = new()
    {
        [("EXP-TURN-083", 0)] = 1420,
    };

    public static TheoryData<string, int> MatchingRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, run) in MatchingRunKeys()) data.Add(experiment, run);
        return data;
    }

    private static IEnumerable<(string Experiment, int Run)> MatchingRunKeys() =>
        from experiment in Experiments
        from run in Enumerable.Range(0, Recorded.Value[experiment].Length)
        where !KnownDivergences.ContainsKey((experiment, run))
        select (experiment, run);

    // Each row starts from its run's replay, so the rows play theirs ahead on a few workers, in CI
    // the longest matches first. The replays land in the cache other tests and ScreenCaptureTests
    // read as well.
    private static readonly RowPrefetch<(string Experiment, int Run), Replay> MatchingReplays =
        new(MatchingRunKeys, key => Replayed(Run(key.Experiment, key.Run)), ReplayCost);

    // How long a run's replay takes, for the prefetches that queue the longest first in CI.
    private static int ReplayCost((string Experiment, int Run) key) => Run(key.Experiment, key.Run).DoneCount;

    public static TheoryData<string, int> Runs()
    {
        var data = new TheoryData<string, int>();
        foreach (var experiment in Experiments)
            for (var run = 0; run < Recorded.Value[experiment].Length; run++)
                data.Add(experiment, run);
        return data;
    }

    private static bool IsActive(MatchState match, PlayerId player) =>
        match.FindPlayer(player)!.Status == PlayerStatus.Active;

    private static RecordedRun Run(string experiment, int run) => Recorded.Value[experiment][run];

    /// <summary>
    /// Whether the runs of <paramref name="experiment"/> are replayed. A run that changes the
    /// match from outside, such as EXP-UI-002's <c>--draw-values</c>, is not.
    /// </summary>
    internal static bool IsReplayed(string experiment) => Experiments.Contains(experiment);

    /// <summary>The rebuild's match after replaying a recorded run to its endpoint.</summary>
    internal static MatchState ReplayedMatch(string experiment, int run) => Replayed(Run(experiment, run)).Match;
    internal static int RecordedTerm(string experiment, int run, string term, int index) =>
        Run(experiment, run).Term(term, index);

    private static RecordedRun[] LoadRuns(string experiment)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "spec", "experiments", $"{experiment}.json")));
        var inputs = fixture.RootElement.GetProperty("inputs");
        return fixture.RootElement.GetProperty("runs").EnumerateArray()
            .Select(run => new RecordedRun(run, inputs)).ToArray();
    }
}
