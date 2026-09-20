using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Validation;

if (args.Length == 0)
{
    Usage();
    return 2;
}

try
{
    if (args[0] == "state-diff") return await StateDiffAsync(args);
    if (args[0] == "ai-tournament") return await AiTournamentAsync(args[1..]);
    Usage();
    return 2;
}
catch (Exception exception) when (exception is IOException or JsonException
    or InvalidDataException or ArgumentException or OverflowException)
{
    Console.Error.WriteLine($"Rechaos.Tools failed: {exception.Message}");
    return 2;
}

static async Task<int> StateDiffAsync(string[] arguments)
{
    if (arguments.Length is < 3 or > 5 ||
        (arguments.Length == 5 && arguments[3] != "--labels") || arguments.Length == 4)
    {
        Usage();
        return 2;
    }
    using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(arguments[1]));
    using var actual = JsonDocument.Parse(await File.ReadAllTextAsync(arguments[2]));
    IReadOnlyDictionary<string, string>? labels = null;
    if (arguments.Length == 5)
    {
        labels = JsonSerializer.Deserialize<Dictionary<string, string>>(
            await File.ReadAllTextAsync(arguments[4]))
            ?? throw new InvalidDataException("Label map is empty.");
    }

    var differences = JsonStateDiffer.Compare(expected.RootElement, actual.RootElement, labels);
    foreach (var difference in differences)
        Console.WriteLine($"{difference.Kind}: {difference.Label} ({difference.Path}): expected {difference.Expected ?? "<missing>"}, actual {difference.Actual ?? "<missing>"}");
    if (differences.Count == 0) Console.WriteLine("States match.");
    return differences.Count == 0 ? 0 : 1;
}

static async Task<int> AiTournamentAsync(string[] arguments)
{
    var options = TournamentOptions.Parse(arguments);
    var definitions = BundledOriginalData.Load();
    var cases = Enumerable.Range(0, options.Matches)
        .Select(index => new TournamentCase(
            index,
            options.Scenarios[index % options.Scenarios.Count],
            unchecked(options.FirstSeed + index),
            options.ReplayEvery > 0 && index % options.ReplayEvery == 0))
        .ToArray();
    var results = new ConcurrentBag<TournamentResult>();
    var failures = new ConcurrentBag<TournamentFailure>();
    var running = new ConcurrentDictionary<int, TournamentCase>();
    var completed = 0;
    var stopwatch = Stopwatch.StartNew();
    using var stopping = new CancellationTokenSource();

    var heartbeat = Task.Run(async () =>
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.HeartbeatSeconds));
        while (await timer.WaitForNextTickAsync(stopping.Token).ConfigureAwait(false))
        {
            Console.Error.WriteLine(
                $"heartbeat elapsed={stopwatch.Elapsed:c} complete={Volatile.Read(ref completed)}/{cases.Length} "
                + $"running={running.Count} failures={failures.Count}");
        }
    });

    try
    {
        await Parallel.ForEachAsync(cases, new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Workers
        }, (match, cancellationToken) =>
        {
            running[match.Index] = match;
            var matchWatch = Stopwatch.StartNew();
            try
            {
                var result = HeadlessMatchRunner.Run(
                    definitions,
                    new HeadlessMatchOptions(
                        match.Scenario, options.Duration, match.Seed, options.Policy,
                        options.Turns, match.VerifyReplay, options.ProgressEveryTurns),
                    options.Trace
                        ? progress => Console.Error.WriteLine(
                            $"trace index={match.Index} scenario={progress.Scenario} seed={progress.Seed} "
                            + $"turn={progress.Turn} boundaries={progress.PhaseBoundaries} "
                            + $"events={progress.EventCount} elapsed={matchWatch.Elapsed:c}")
                        : null,
                    cancellationToken);
                results.Add(new TournamentResult(
                    match.Index, match.Scenario, match.Seed,
                    result.State.Coordinator.Turn, result.State.Outcome?.Reason,
                    result.PhaseBoundaries, result.State.Events.Count,
                    result.State.Sectors.Count(sector => sector.Owner is not null),
                    result.State.Sectors.Count(sector => sector.Owner is { } owner
                        && result.State.FindPlayer(owner)?.Gangs.Any(gang =>
                            gang.IsActive && gang.SectorId == sector.Id) == true),
                    result.State.Players.Sum(player => player.Gangs.Count(gang => gang.IsActive)),
                    result.State.Events.Count(gameEvent =>
                        gameEvent.Kind == GameEventKind.CommandResolved
                        && gameEvent.Action == GangAction.Attack),
                    result.ReplayVerified, result.StateHash,
                    matchWatch.ElapsedMilliseconds));
            }
            catch (Exception exception)
            {
                failures.Add(new TournamentFailure(
                    match.Index, match.Scenario, match.Seed,
                    exception.GetType().Name, exception.Message));
            }
            finally
            {
                running.TryRemove(match.Index, out _);
                Interlocked.Increment(ref completed);
            }
            return ValueTask.CompletedTask;
        });
    }
    finally
    {
        await stopping.CancelAsync();
        try { await heartbeat; }
        catch (OperationCanceledException) { }
    }

    stopwatch.Stop();
    var report = new TournamentReport(
        SchemaVersion: 1,
        options.Policy,
        options.Duration,
        options.Turns,
        options.Workers,
        cases.Length,
        results.Count,
        failures.Count,
        results.Count(result => result.ReplayVerified),
        stopwatch.ElapsedMilliseconds,
        stopwatch.Elapsed.TotalSeconds > 0 ? results.Count / stopwatch.Elapsed.TotalSeconds : 0,
        results.OrderBy(result => result.Index).ToArray(),
        failures.OrderBy(failure => failure.Index).ToArray());
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    jsonOptions.Converters.Add(new JsonStringEnumConverter());
    Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
    return failures.IsEmpty ? 0 : 1;
}

static void Usage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  Rechaos.Tools state-diff <expected.json> <actual.json> [--labels <labels.json>]");
    Console.Error.WriteLine("  Rechaos.Tools ai-tournament [--matches N] [--turns N] [--workers N]");
    Console.Error.WriteLine("      [--policy original|advanced] [--scenarios objectives|all|NAME,...]");
    Console.Error.WriteLine("      [--seed N] [--replay-every N] [--progress-every N]");
    Console.Error.WriteLine("      [--heartbeat-seconds N] [--trace]");
}

file sealed record TournamentOptions(
    int Matches,
    int Turns,
    int Workers,
    AiPolicyMode Policy,
    IReadOnlyList<ScenarioId> Scenarios,
    int FirstSeed,
    int ReplayEvery,
    int ProgressEveryTurns,
    int HeartbeatSeconds,
    bool Trace,
    GameDuration Duration)
{
    private static readonly ScenarioId[] Objectives =
    [
        ScenarioId.KillEmAll, ScenarioId.Big40, ScenarioId.Eliminate,
        ScenarioId.Siege, ScenarioId.BigMan, ScenarioId.Armageddon
    ];

    public static TournamentOptions Parse(string[] arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var trace = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            var name = arguments[index];
            if (name == "--trace")
            {
                trace = true;
                continue;
            }
            if (!name.StartsWith("--", StringComparison.Ordinal) || index + 1 >= arguments.Length)
                throw new ArgumentException($"Unknown or incomplete option '{name}'.");
            if (!values.TryAdd(name, arguments[++index]))
                throw new ArgumentException($"Option '{name}' was supplied more than once.");
        }

        foreach (var name in values.Keys)
        {
            if (name is not ("--matches" or "--turns" or "--workers" or "--policy"
                or "--scenarios" or "--seed" or "--replay-every" or "--progress-every"
                or "--heartbeat-seconds"))
                throw new ArgumentException($"Unknown option '{name}'.");
        }

        var matches = Positive(values, "--matches", 60);
        var turns = Positive(values, "--turns", 40);
        var workers = Positive(values, "--workers", Math.Min(2, Environment.ProcessorCount));
        if (matches > 100_000)
            throw new ArgumentOutOfRangeException(nameof(arguments), "Matches cannot exceed 100,000.");
        if (turns > 10_000)
            throw new ArgumentOutOfRangeException(nameof(arguments), "Turns cannot exceed 10,000.");
        if (workers > 16)
            throw new ArgumentOutOfRangeException(nameof(arguments), "Workers cannot exceed 16.");
        var replayEvery = Nonnegative(values, "--replay-every", 10);
        var progressEvery = Positive(values, "--progress-every", 10);
        var heartbeatSeconds = Positive(values, "--heartbeat-seconds", 5);
        var seed = Integer(values, "--seed", 1977);
        var policyText = values.GetValueOrDefault("--policy", "original");
        var policy = policyText.ToLowerInvariant() switch
        {
            "original" => AiPolicyMode.Original,
            "advanced" => AiPolicyMode.Advanced,
            _ => throw new ArgumentException($"Unknown AI policy '{policyText}'.")
        };
        var scenarios = ParseScenarios(values.GetValueOrDefault("--scenarios", "objectives"));
        return new TournamentOptions(
            matches, turns, workers, policy, scenarios, seed, replayEvery,
            progressEvery, heartbeatSeconds, trace, GameDuration.FourYears);
    }

    private static IReadOnlyList<ScenarioId> ParseScenarios(string text)
    {
        if (text.Equals("objectives", StringComparison.OrdinalIgnoreCase)) return Objectives;
        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
            return Enum.GetValues<ScenarioId>();
        var scenarios = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // Enum.TryParse accepts any integer that fits the underlying type, so `--scenarios 99`
            // parsed happily and every match then failed inside the runner.
            .Select(value => Enum.TryParse<ScenarioId>(value, ignoreCase: true, out var scenario)
                             && Enum.IsDefined(scenario)
                ? scenario
                : throw new ArgumentException($"Unknown scenario '{value}'."))
            .Distinct()
            .ToArray();
        return scenarios.Length > 0
            ? scenarios
            : throw new ArgumentException("At least one scenario is required.");
    }

    private static int Positive(IReadOnlyDictionary<string, string> values, string name, int fallback)
    {
        var value = Integer(values, name, fallback);
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(name, "Value must be positive.");
    }

    private static int Nonnegative(IReadOnlyDictionary<string, string> values, string name, int fallback)
    {
        var value = Integer(values, name, fallback);
        return value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(name, "Value cannot be negative.");
    }

    // Command-line integers are invariant, not whatever the operator's regional settings say: a
    // culture-sensitive parse reads "1,000" differently from one machine to the next.
    private static int Integer(IReadOnlyDictionary<string, string> values, string name, int fallback) =>
        values.TryGetValue(name, out var text)
            ? int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"Option '{name}' requires an integer.")
            : fallback;
}

file sealed record TournamentCase(int Index, ScenarioId Scenario, int Seed, bool VerifyReplay);
file sealed record TournamentResult(
    int Index,
    ScenarioId Scenario,
    int Seed,
    int FinalTurn,
    MatchEndReason? Outcome,
    int PhaseBoundaries,
    int EventCount,
    int ControlledSectors,
    int DefendedControlledSectors,
    int ActiveGangs,
    int ResolvedAttacks,
    bool ReplayVerified,
    string StateHash,
    long ElapsedMilliseconds);
file sealed record TournamentFailure(
    int Index,
    ScenarioId Scenario,
    int Seed,
    string ErrorType,
    string Message);
file sealed record TournamentReport(
    int SchemaVersion,
    AiPolicyMode Policy,
    GameDuration Duration,
    int TurnHorizon,
    int Workers,
    int RequestedMatches,
    int CompletedMatches,
    int FailedMatches,
    int ReplayVerifiedMatches,
    long ElapsedMilliseconds,
    double MatchesPerSecond,
    IReadOnlyList<TournamentResult> Results,
    IReadOnlyList<TournamentFailure> Failures);
