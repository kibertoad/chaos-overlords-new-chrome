using System.Text.Json;
using Rechaos.Core.Validation;
using Xunit;

namespace Rechaos.Tests;

public sealed class JsonStateDifferTests
{
    [Fact]
    public void ReportsStableLabeledStructuralDifferences()
    {
        using var expected = JsonDocument.Parse("""{"turn":1,"players":[{"cash":500}],"removed":true}""");
        using var actual = JsonDocument.Parse("""{"turn":2,"players":[{"cash":475},{"cash":100}],"added":false}""");
        var labels = new Dictionary<string, string>
        {
            ["$.turn"] = "Turn number",
            ["$.players"] = "Players"
        };

        var differences = JsonStateDiffer.Compare(expected.RootElement, actual.RootElement, labels);

        Assert.Collection(differences,
            difference => Assert.Equal(StateDifferenceKind.Added, difference.Kind),
            difference => Assert.Equal("Players[0].cash", difference.Label),
            difference => Assert.Equal("Players[1]", difference.Label),
            difference => Assert.Equal(StateDifferenceKind.Removed, difference.Kind),
            difference => Assert.Equal("Turn number", difference.Label));
        Assert.Equal(["$.added", "$.players[0].cash", "$.players[1]", "$.removed", "$.turn"],
            differences.Select(difference => difference.Path));
    }

    [Fact]
    public void EquivalentObjectsMatchRegardlessOfPropertyOrder()
    {
        using var expected = JsonDocument.Parse("""{"a":1,"b":[true,null]}""");
        using var actual = JsonDocument.Parse("""{"b":[true,null],"a":1}""");

        Assert.Empty(JsonStateDiffer.Compare(expected.RootElement, actual.RootElement));
    }
}
