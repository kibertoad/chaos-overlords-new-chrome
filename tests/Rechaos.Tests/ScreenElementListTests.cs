using System.Text.Json;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The element lists in tools/Rechaos.OriginalProbe/Screens, which the probe digests a capture
/// at (docs/VALIDATION.md, "Screens against captures of the original"). Each list names its screen
/// entry, every rectangle lies inside the 640-by-460 drawing area (RULE-GFX-002), and every element
/// names a row of the entry's Drawn elements table, with an index or field after a comma.
/// </summary>
public sealed class ScreenElementListTests
{
    public static TheoryData<string> Lists()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Screens"), "SCR-*.json")
                     .Order(StringComparer.Ordinal))
            data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void EveryElementLiesInTheDrawingAreaAndCitesItsEntry(string screen)
    {
        using var list = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Screens", $"{screen}.json")));
        Assert.Equal(screen, list.RootElement.GetProperty("screen").GetString());
        var rows = DrawnElementRows(screen);
        var elements = list.RootElement.GetProperty("elements").EnumerateArray().ToArray();
        Assert.NotEmpty(elements);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            var name = element.GetProperty("element").GetString()!;
            Assert.True(names.Add(name), $"{screen} lists {name} twice.");
            var rect = element.GetProperty("rect").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            Assert.True(rect is [>= 0, >= 0, > 0, > 0]
                        && rect[0] + rect[2] <= ScreenFrame.Width && rect[1] + rect[3] <= ScreenFrame.Height,
                $"{screen} {name} [{string.Join(", ", rect)}] lies outside the drawing area.");
            Assert.True(Cites(name, rows), $"{screen} {name} names no row of the entry's Drawn elements table.");
        }
    }

    [Theory]
    [InlineData("Offer portrait, slot 0", "Offer portrait, one per offer slot", true)]
    [InlineData("Panel, City", "Panel, City", true)]
    [InlineData("Value fields, Gang Upkeep", "Value fields", true)]
    [InlineData("Value field, Gang Upkeep", "Value fields", false)]
    [InlineData("Panel", "Panel, City", true)]
    public void AnElementCitesTheRowItsNameBeginsWith(string element, string row, bool cites) =>
        Assert.Equal(cites, Cites(element, [row]));

    // An element cites a row when its name is the row's, when its name before the first comma is
    // the row's, or when the row's name continues that stem after a comma.
    private static bool Cites(string element, IReadOnlyCollection<string> rows)
    {
        var comma = element.IndexOf(", ", StringComparison.Ordinal);
        var stem = comma < 0 ? element : element[..comma];
        return rows.Any(row => row == element || row == stem
                               || row.StartsWith(stem + ",", StringComparison.Ordinal)
                               || element.StartsWith(row + ", ", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> DrawnElementRows(string screen)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "screens", $"{screen}.md"));
        var start = Array.FindIndex(lines, line => line.Trim() == "## Drawn elements");
        Assert.True(start >= 0, $"{screen} has no Drawn elements section.");
        var rows = new List<string>();
        foreach (var line in lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal)) continue;
            var cell = line.Split('|')[1].Trim();
            if (cell != "Element" && !cell.StartsWith("---", StringComparison.Ordinal)) rows.Add(cell);
        }
        return rows;
    }
}
