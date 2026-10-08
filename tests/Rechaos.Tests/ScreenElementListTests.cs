using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The element lists in tools/Rechaos.OriginalProbe/Screens, which the probe digests a capture
/// at (docs/validation/screen-captures.md). Each list names its screen entry, which has masks,
/// every rectangle lies inside the 640-by-460 drawing area (RULE-GFX-002),
/// and every element names a row of the entry's Drawn elements table: its name is the row's, or
/// the row's name (or that name's part before its own comma) followed by a comma and either an
/// index ("slot 0") or a field or variant the row's Element or Shows cell names as a whole word.
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
        // A capture digested at this list is compared under the screen's masks (ScreenCaptureTests).
        Assert.True(ScreenCaptureMasks.ByScreen.ContainsKey(screen), $"{screen} has no entry in ScreenCaptureMasks.");
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
            // The entry's title names the whole drawing area, so the frame is compared as one as well.
            var whole = name == EntryTitle(screen) && rect is [0, 0, ScreenFrame.Width, ScreenFrame.Height];
            Assert.True(whole || Cites(name, rows), $"{screen} {name} names no row of the entry's Drawn elements table.");
        }
    }

    [Theory]
    [InlineData("Offer portrait, slot 0", "Offer portrait, one per offer slot", "The portrait", true)]
    [InlineData("Panel, City", "Panel, City", "None", true)]
    [InlineData("Panel, Ctiy", "Panel, City", "None", false)]
    [InlineData("Panel", "Panel, City", "None", true)]
    [InlineData("Value fields, Gang Upkeep", "Value fields", "Gang Upkeep at y = 151", true)]
    [InlineData("Value fields, Gang Upkep", "Value fields", "Gang Upkeep at y = 151", false)]
    [InlineData("Value field, Gang Upkeep", "Value fields", "Gang Upkeep at y = 151", false)]
    [InlineData("Player totals, score", "Player totals", "Score and cash", true)]
    public void AnElementCitesTheRowItsNameBeginsWith(string element, string row, string shows, bool cites) =>
        Assert.Equal(cites, Cites(element, [(row, shows)]));

    // An element cites a row when its name is the row's, or when its name before the first comma is
    // the row's name or the row name's part before its own comma, and what follows the comma is an
    // index or a whole word or phrase of the row's Element or Shows cell.
    private static bool Cites(string element, IReadOnlyCollection<(string Name, string Shows)> rows)
    {
        var comma = element.IndexOf(", ", StringComparison.Ordinal);
        var stem = comma < 0 ? element : element[..comma];
        var part = comma < 0 ? null : element[(comma + 2)..];
        return rows.Any(row => row.Name == element
                               || ((row.Name == stem || row.Name.StartsWith(stem + ",", StringComparison.Ordinal))
                                   && (part is null || IndexPart.IsMatch(part)
                                       || NamesWhole(row.Name[stem.Length..], part) || NamesWhole(row.Shows, part))));
    }

    private static readonly Regex IndexPart = new(@"^[a-z]+ \d+$", RegexOptions.CultureInvariant);

    private static bool NamesWhole(string text, string part) =>
        Regex.IsMatch(text, $@"(?<!\w){Regex.Escape(part)}(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string EntryTitle(string screen) =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "spec", "screens", $"{screen}.md"))
            .First(line => line.StartsWith("title: ", StringComparison.Ordinal))["title: ".Length..];

    private static IReadOnlyList<(string Name, string Shows)> DrawnElementRows(string screen)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spec", "screens", $"{screen}.md"));
        var start = Array.FindIndex(lines, line => line.Trim() == "## Drawn elements");
        Assert.True(start >= 0, $"{screen} has no Drawn elements section.");
        var header = lines.Skip(start + 1).First(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Split('|').Select(cell => cell.Trim()).ToList();
        var shows = header.IndexOf("Shows");
        Assert.True(shows > 0, $"{screen}'s Drawn elements table has no Shows column.");
        var rows = new List<(string Name, string Shows)>();
        foreach (var line in lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal)) continue;
            var cells = line.Split('|');
            var cell = cells[1].Trim();
            if (cell != "Element" && !cell.StartsWith("---", StringComparison.Ordinal))
                rows.Add((cell, cells.Length > shows ? cells[shows].Trim() : ""));
        }
        return rows;
    }
}
