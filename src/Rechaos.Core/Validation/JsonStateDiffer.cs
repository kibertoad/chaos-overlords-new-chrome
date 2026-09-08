using System.Text.Json;

namespace Rechaos.Core.Validation;

public enum StateDifferenceKind
{
    Added,
    Removed,
    Changed
}

public sealed record StateDifference(
    string Path,
    string Label,
    StateDifferenceKind Kind,
    string? Expected,
    string? Actual);

public static class JsonStateDiffer
{
    public static IReadOnlyList<StateDifference> Compare(
        JsonElement expected,
        JsonElement actual,
        IReadOnlyDictionary<string, string>? labels = null)
    {
        var differences = new List<StateDifference>();
        CompareValue(expected, actual, "$", labels, differences);
        return differences;
    }

    private static void CompareValue(
        JsonElement expected,
        JsonElement actual,
        string path,
        IReadOnlyDictionary<string, string>? labels,
        List<StateDifference> differences)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            Add(StateDifferenceKind.Changed, expected.GetRawText(), actual.GetRawText());
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(expected, actual, path, labels, differences);
                break;
            case JsonValueKind.Array:
                CompareArrays(expected, actual, path, labels, differences);
                break;
            default:
                if (!JsonElement.DeepEquals(expected, actual))
                    Add(StateDifferenceKind.Changed, expected.GetRawText(), actual.GetRawText());
                break;
        }
        return;

        void Add(StateDifferenceKind kind, string? expectedValue, string? actualValue) =>
            differences.Add(new StateDifference(path, ResolveLabel(path, labels), kind, expectedValue, actualValue));
    }

    private static void CompareObjects(
        JsonElement expected,
        JsonElement actual,
        string path,
        IReadOnlyDictionary<string, string>? labels,
        List<StateDifference> differences)
    {
        var expectedProperties = expected.EnumerateObject().ToDictionary(property => property.Name, StringComparer.Ordinal);
        var actualProperties = actual.EnumerateObject().ToDictionary(property => property.Name, StringComparer.Ordinal);
        foreach (var name in expectedProperties.Keys.Union(actualProperties.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var childPath = AppendProperty(path, name);
            var hasExpected = expectedProperties.TryGetValue(name, out var expectedProperty);
            var hasActual = actualProperties.TryGetValue(name, out var actualProperty);
            if (!hasExpected)
            {
                differences.Add(new StateDifference(childPath, ResolveLabel(childPath, labels),
                    StateDifferenceKind.Added, null, actualProperty.Value.GetRawText()));
            }
            else if (!hasActual)
            {
                differences.Add(new StateDifference(childPath, ResolveLabel(childPath, labels),
                    StateDifferenceKind.Removed, expectedProperty.Value.GetRawText(), null));
            }
            else
            {
                CompareValue(expectedProperty.Value, actualProperty.Value, childPath, labels, differences);
            }
        }
    }

    private static void CompareArrays(
        JsonElement expected,
        JsonElement actual,
        string path,
        IReadOnlyDictionary<string, string>? labels,
        List<StateDifference> differences)
    {
        var expectedItems = expected.EnumerateArray().ToArray();
        var actualItems = actual.EnumerateArray().ToArray();
        for (var index = 0; index < Math.Max(expectedItems.Length, actualItems.Length); index++)
        {
            var childPath = $"{path}[{index}]";
            if (index >= expectedItems.Length)
            {
                differences.Add(new StateDifference(childPath, ResolveLabel(childPath, labels),
                    StateDifferenceKind.Added, null, actualItems[index].GetRawText()));
            }
            else if (index >= actualItems.Length)
            {
                differences.Add(new StateDifference(childPath, ResolveLabel(childPath, labels),
                    StateDifferenceKind.Removed, expectedItems[index].GetRawText(), null));
            }
            else
            {
                CompareValue(expectedItems[index], actualItems[index], childPath, labels, differences);
            }
        }
    }

    private static string ResolveLabel(string path, IReadOnlyDictionary<string, string>? labels)
    {
        if (labels is null) return path;
        if (labels.TryGetValue(path, out var exact)) return exact;

        var parent = path;
        while ((parent = ParentPath(parent)).Length > 0)
            if (labels.TryGetValue(parent, out var label))
                return label + path[parent.Length..];
        return path;
    }

    private static string ParentPath(string path)
    {
        var bracket = path.LastIndexOf('[');
        var dot = path.LastIndexOf('.');
        var split = Math.Max(bracket, dot);
        return split <= 0 ? string.Empty : path[..split];
    }

    private static string AppendProperty(string path, string name) =>
        name.All(character => char.IsLetterOrDigit(character) || character == '_')
            ? $"{path}.{name}"
            : $"{path}['{name.Replace("'", "\\'")}']";
}
