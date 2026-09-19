using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangStatisticModifierTooltipTests
{
    private static Statistics Stats(
        short combat = 0,
        short defense = 0,
        short stealth = 0) =>
        new(combat, defense, stealth, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void TheBreakdownNamesEverySourceAndEndsWithTheEffectiveValue()
    {
        var lines = GangStatisticModifierTooltip.Lines(InformationEffect.Combat, Stats(combat: 3),
        [
            new GangStatisticsModifier(GangModifierSource.Weapon, "Uzi", Stats(combat: 2)),
            new GangStatisticsModifier(GangModifierSource.Armor, "Flak Jacket", Stats(defense: 4)),
            new GangStatisticsModifier(GangModifierSource.Site, "Chop Shop", Stats(combat: -1))
        ]);

        Assert.Equal(["BASE 3", "+2 UZI (WEAPON)", "-1 CHOP SHOP (SITE)", "TOTAL 4"], lines);
    }

    [Fact]
    public void SourcesThatLeaveTheHoveredStatisticAloneAreNotListed()
    {
        var lines = GangStatisticModifierTooltip.Lines(InformationEffect.Stealth, Stats(stealth: 1),
        [
            new GangStatisticsModifier(GangModifierSource.Miscellaneous, "Cloak", Stats(stealth: 5)),
            new GangStatisticsModifier(GangModifierSource.Weapon, "Uzi", Stats(combat: 2))
        ]);

        Assert.Equal(["BASE 1", "+5 CLOAK (MISC)", "TOTAL 6"], lines);
    }

    [Fact]
    public void AnUnmodifiedStatisticShowsNoBreakdownAtAll()
    {
        var lines = GangStatisticModifierTooltip.Lines(InformationEffect.Combat, Stats(combat: 3),
            [new GangStatisticsModifier(GangModifierSource.Armor, "Flak Jacket", Stats(defense: 4))]);

        Assert.Empty(lines);
    }

    [Fact]
    public void BreakdownLinesStayInsideTheHoverPanel()
    {
        var lines = GangStatisticModifierTooltip.Lines(InformationEffect.Combat, Stats(combat: 3),
            [new GangStatisticsModifier(GangModifierSource.Site,
                new string('X', 120), Stats(combat: 9))]);
        var bounds = HoverTooltipLayout.Bounds(new Point(320, 230), lines);

        Assert.True(bounds.Width <= VirtualInput.Width - 16);
        Assert.All(lines, line => Assert.True(
            line.Length * OriginalFontLayout.CellWidth + 16 <= bounds.Width));
        Assert.All(lines, line => Assert.Equal(line.ToUpperInvariant(), line));
    }

    [Fact]
    public void HoveringAGangStatisticAppendsTheBreakdownToItsDescription()
    {
        var point = new Point(200, GangInformationLayout.StatisticY(0) + 2);
        var description = InformationEffectTooltips.GangAt(point);

        var explained = InformationEffectTooltips.GangAt(point,
            effect => GangStatisticModifierTooltip.Lines(effect, Stats(combat: 3),
                [new GangStatisticsModifier(GangModifierSource.Weapon, "Uzi", Stats(combat: 2))]));

        Assert.Equal("COMBAT", description[0]);
        Assert.Equal(description, explained.Take(description.Count));
        Assert.Equal(["BASE 3", "+2 UZI (WEAPON)", "TOTAL 5"], explained.Skip(description.Count));
    }

    [Fact]
    public void FieldsWithoutModifiersKeepTheirPlainDescription()
    {
        IReadOnlyList<string> Breakdown(InformationEffect effect) => ["UNREACHABLE"];

        var force = InformationEffectTooltips.GangAt(
            new Point(SharedPanelLayout.X(96), SharedPanelLayout.Y(92)), Breakdown);

        Assert.Equal("FORCE", force[0]);
        Assert.DoesNotContain("UNREACHABLE", force);
    }
}
