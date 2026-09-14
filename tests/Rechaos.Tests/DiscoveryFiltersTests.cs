using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class DiscoveryFiltersTests
{
    public static TheoryData<int> Filters =>
        new(Enumerable.Range(0, DiscoveryFilters.Count));

    [Theory]
    [MemberData(nameof(Filters))]
    public void EveryOptionHasALabelAndSurvivesBeingStoredAndReadBack(int filter)
    {
        for (var option = 0; option < DiscoveryFilters.OptionCount(filter); option++)
        {
            Assert.NotEqual(string.Empty, DiscoveryFilters.Label(filter, option));
            Assert.Equal(
                option,
                DiscoveryFilters.OptionOf(filter, DiscoveryFilters.ValueOf(filter, option)));
        }
    }

    [Fact]
    public void TheFirstOptionOfEachFilterIsTheOneThatKeepsEverything()
    {
        Assert.Equal("ALL STATES", DiscoveryFilters.Label(DiscoveryFilters.Status, 0));
        Assert.Equal("ALL MODES", DiscoveryFilters.Label(DiscoveryFilters.Scenario, 0));
        Assert.Equal("ALL AI", DiscoveryFilters.Label(DiscoveryFilters.Ai, 0));
        Assert.Equal(0, DiscoveryFilters.ValueOf(DiscoveryFilters.Status, 0));
        Assert.Equal(-1, DiscoveryFilters.ValueOf(DiscoveryFilters.Scenario, 0));
        Assert.Equal(-1, DiscoveryFilters.ValueOf(DiscoveryFilters.Ai, 0));
    }

    [Fact]
    public void ScenarioAndAiOptionsFollowTheOrderTheListingsAreMatchedIn()
    {
        Assert.Equal(
            (int)ScenarioId.KillEmAll,
            DiscoveryFilters.ValueOf(
                DiscoveryFilters.Scenario, (int)ScenarioId.KillEmAll + 1));
        Assert.Equal(
            ScenarioCatalog.Get(ScenarioId.KillEmAll).Name,
            DiscoveryFilters.Label(DiscoveryFilters.Scenario, (int)ScenarioId.KillEmAll + 1));
        Assert.Equal(
            DifficultyPresentation.Label(AiDifficulty.CrimeLord),
            DiscoveryFilters.Label(DiscoveryFilters.Ai, (int)AiDifficulty.CrimeLord + 1));
    }

    [Theory]
    [MemberData(nameof(Filters))]
    public void AnOpenDropdownStacksItsOptionsUnderItsButtonAndInsideThePanel(int filter)
    {
        var button = OnlineConnectLayout.DiscoveryFilter(filter);
        var menu = OnlineConnectLayout.DiscoveryFilterMenu(filter);
        var panel = new Rectangle(100, 72, 440, 372);
        Assert.Equal(button.X, menu.X);
        Assert.Equal(button.Bottom, menu.Y);
        Assert.Equal(button.Width, menu.Width);
        Assert.True(panel.Contains(menu), $"filter {filter} opens past the panel: {menu}");
        var count = DiscoveryFilters.OptionCount(filter);
        for (var option = 0; option < count; option++)
        {
            var row = OnlineConnectLayout.DiscoveryFilterOption(filter, option);
            Assert.True(menu.Contains(row), $"option {option} of filter {filter} escapes {menu}");
            Assert.Equal(
                menu.Y + option * OnlineConnectLayout.DiscoveryOptionHeight, row.Y);
        }
        Assert.Equal(
            menu.Bottom,
            OnlineConnectLayout.DiscoveryFilterOption(filter, count - 1).Bottom);
    }

    [Theory]
    [MemberData(nameof(Filters))]
    public void EveryLabelFitsItsButtonWithoutRunningUnderTheDropdownArrow(int filter)
    {
        var button = OnlineConnectLayout.DiscoveryFilter(filter);
        for (var option = 0; option < DiscoveryFilters.OptionCount(filter); option++)
        {
            var width = DiscoveryFilters.Label(filter, option).Length * 6;
            Assert.True(width + 32 <= button.Width,
                $"{DiscoveryFilters.Label(filter, option)} is too wide for {button.Width}px");
        }
    }

    [Fact]
    public void TheDropdownsDoNotOverlapEachOther()
    {
        for (var filter = 1; filter < DiscoveryFilters.Count; filter++)
            Assert.True(
                OnlineConnectLayout.DiscoveryFilter(filter - 1).Right
                    < OnlineConnectLayout.DiscoveryFilter(filter).X);
    }
}
