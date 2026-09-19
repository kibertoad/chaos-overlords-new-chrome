using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
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
        var panel = OnlineScreenLayout.Panel;
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

    /// <summary>The settings a session hosted by this build carries.</summary>
    private static MultiplayerGameSettings Settings(
        ScenarioId scenario = ScenarioId.KillEmAll,
        AiDifficulty mentality = AiDifficulty.CrimeLord) =>
        new(scenario, GameDuration.SixMonths, mentality,
            [.. Enumerable.Range(0, MatchLimits.PlayerCount).Select(slot => (short)slot)]);

    [Theory]
    [InlineData(MatchStatus.Lobby)]
    [InlineData(MatchStatus.Running)]
    public void TheUnfilteredListKeepsEverySessionTheServerReturned(MatchStatus status)
    {
        Assert.True(DiscoveryFilters.Matches(0, -1, -1, status, Settings()));
        Assert.True(DiscoveryFilters.Matches(0, -1, -1, status, settings: null));
    }

    [Fact]
    public void TheStatusFilterKeepsOnlyTheStateItNames()
    {
        Assert.True(DiscoveryFilters.Matches(1, -1, -1, MatchStatus.Lobby, Settings()));
        Assert.False(DiscoveryFilters.Matches(1, -1, -1, MatchStatus.Running, Settings()));
        Assert.True(DiscoveryFilters.Matches(2, -1, -1, MatchStatus.Running, Settings()));
        Assert.False(DiscoveryFilters.Matches(2, -1, -1, MatchStatus.Lobby, Settings()));
    }

    [Fact]
    public void TheStatusFilterAloneStillKeepsSessionsWhoseSettingsCouldNotBeRead()
    {
        Assert.True(DiscoveryFilters.Matches(1, -1, -1, MatchStatus.Lobby, settings: null));
        Assert.False(DiscoveryFilters.Matches(1, -1, -1, MatchStatus.Running, settings: null));
    }

    [Fact]
    public void TheScenarioAndAiFiltersKeepOnlyTheValuesTheyName()
    {
        var settings = Settings(ScenarioId.KillEmAll, AiDifficulty.CrimeLord);
        Assert.True(DiscoveryFilters.Matches(
            0, (int)ScenarioId.KillEmAll, -1, MatchStatus.Lobby, settings));
        Assert.False(DiscoveryFilters.Matches(
            0, (int)ScenarioId.Greed, -1, MatchStatus.Lobby, settings));
        Assert.True(DiscoveryFilters.Matches(
            0, -1, (int)AiDifficulty.CrimeLord, MatchStatus.Lobby, settings));
        Assert.False(DiscoveryFilters.Matches(
            0, -1, (int)AiDifficulty.Goon, MatchStatus.Lobby, settings));
    }

    [Fact]
    public void OnlyAFilterThatAsksAboutTheSettingsDropsASessionThatCouldNotBeRead()
    {
        Assert.False(DiscoveryFilters.Matches(
            0, (int)ScenarioId.KillEmAll, -1, MatchStatus.Lobby, settings: null));
        Assert.False(DiscoveryFilters.Matches(
            0, -1, (int)AiDifficulty.CrimeLord, MatchStatus.Lobby, settings: null));
    }
}
