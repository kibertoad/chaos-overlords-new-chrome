using System.Text.Json;
using Rechaos.Core.Validation;

if (args.Length is < 3 or > 5 || args[0] != "state-diff" ||
    (args.Length == 5 && args[3] != "--labels") || args.Length == 4)
{
    Console.Error.WriteLine("Usage: Rechaos.Tools state-diff <expected.json> <actual.json> [--labels <labels.json>]");
    return 2;
}

try
{
    using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
    using var actual = JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));
    IReadOnlyDictionary<string, string>? labels = null;
    if (args.Length == 5)
    {
        labels = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(args[4]))
            ?? throw new InvalidDataException("Label map is empty.");
    }

    var differences = JsonStateDiffer.Compare(expected.RootElement, actual.RootElement, labels);
    foreach (var difference in differences)
        Console.WriteLine($"{difference.Kind}: {difference.Label} ({difference.Path}): expected {difference.Expected ?? "<missing>"}, actual {difference.Actual ?? "<missing>"}");
    if (differences.Count == 0) Console.WriteLine("States match.");
    return differences.Count == 0 ? 0 : 1;
}
catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
{
    Console.Error.WriteLine($"State diff failed: {exception.Message}");
    return 2;
}
