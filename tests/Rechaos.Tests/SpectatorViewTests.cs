using Microsoft.Xna.Framework;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using Xunit;
using static Rechaos.Tests.MultiplayerSpectatorSessionTests;

namespace Rechaos.Tests;

/// <summary>
/// The host's delay choice, what the spectator view says, and where it says it.
/// </summary>
/// <remarks>The spectator view is part of DEV-NET-001: online play has no original to compare with.</remarks>
public sealed class SpectatorViewTests
{
    [Fact]
    public void TheLobbyArrowsStepFromOffThroughEveryDelayWithoutWrapping()
    {
        Assert.Equal(SpectatorDelayChoice.Minimum, SpectatorDelayChoice.Step(null, 1));
        Assert.Null(SpectatorDelayChoice.Step(null, -1));
        Assert.Null(SpectatorDelayChoice.Step(SpectatorDelayChoice.Minimum, -1));
        Assert.Equal(3, SpectatorDelayChoice.Step(2, 1));
        Assert.Equal(SpectatorDelayChoice.Maximum, SpectatorDelayChoice.Step(SpectatorDelayChoice.Maximum, 1));
        Assert.Equal(19, SpectatorDelayChoice.Step(20, -1));
    }

    [Fact]
    public void TheClassicFacesChangeTheDelayAndLeaveOffAlone()
    {
        Assert.Null(SpectatorDelayChoice.Adjust(null, 1));
        Assert.Equal(SpectatorDelayChoice.Minimum, SpectatorDelayChoice.Adjust(SpectatorDelayChoice.Minimum, -1));
        Assert.Equal(SpectatorDelayChoice.Maximum, SpectatorDelayChoice.Adjust(SpectatorDelayChoice.Maximum, 1));
        Assert.Equal(6, SpectatorDelayChoice.Adjust(5, 1));
    }

    [Fact]
    public void TheBrowserSaysHowLateAMatchCanBeWatched()
    {
        Assert.Equal("WATCH 4 BEHIND", SpectatorDelayChoice.ListingLabel(4));
        Assert.Equal(string.Empty, SpectatorDelayChoice.ListingLabel(null));
    }

    [Fact]
    public void TheViewSaysWhichTurnItShowsAndWhichThePlayersAreOn()
    {
        var lines = SpectatorViewPresentation.Describe(
            ViewOf(MatchStatus.Running, currentTurn: 9, releasedTurn: 6),
            hasState: true, shownTurn: 6, isComplete: false, connected: true);

        Assert.Equal("TURN 7 OF 9", lines.Progress);
        Assert.Equal("2 TURNS BEHIND", lines.Delay);
        Assert.Equal("FOLLOWING THE MATCH", lines.Standing);
    }

    [Fact]
    public void TheViewSaysWhatItIsWaitingFor()
    {
        var started = SpectatorViewPresentation.Describe(
            ViewOf(MatchStatus.Running, currentTurn: 1, releasedTurn: 0),
            hasState: false, shownTurn: null, isComplete: false, connected: true);
        Assert.Equal("PLAYERS ON TURN 1", started.Progress);
        Assert.Equal("WAITING FOR THE STARTING CITY", started.Standing);

        var lost = SpectatorViewPresentation.Describe(
            ViewOf(MatchStatus.Running, currentTurn: 5, releasedTurn: 2),
            hasState: true, shownTurn: 2, isComplete: false, connected: false);
        Assert.Equal("CONNECTION LOST  RETRYING", lost.Standing);

        var finishing = SpectatorViewPresentation.Describe(
            ViewOf(MatchStatus.Finished, currentTurn: 8, releasedTurn: 7),
            hasState: true, shownTurn: 5, isComplete: false, connected: true);
        Assert.Equal("SHOWING THE LAST TURNS", finishing.Standing);

        var over = SpectatorViewPresentation.Describe(
            ViewOf(MatchStatus.Finished, currentTurn: 8, releasedTurn: 7),
            hasState: true, shownTurn: 7, isComplete: true, connected: true);
        Assert.Equal("THE MATCH IS OVER", over.Standing);
    }

    /// <summary>
    /// The panel covers the console's command buttons and nothing a spectator still reads: the map,
    /// the Overlord bar, the status console above it and the message line below it.
    /// </summary>
    [Fact]
    public void ThePanelCoversTheCommandButtonsAndLeavesTheCityInView()
    {
        var panel = SpectatorViewLayout.Panel;
        Assert.False(panel.Intersects(CityMapLayout.Bounds));
        for (var seat = 0; seat < 6; seat++)
            Assert.False(panel.Intersects(OverlordBarLayout.PortraitHit(seat)));
        Assert.True(panel.Y > StatusConsoleLayout.SectorValueY(4) + OriginalFontLayout.GlyphHeight);
        Assert.True(panel.Bottom <= 354, "the message line starts at y 354");
        Assert.True(panel.Right <= 640);

        Rectangle[] controls =
        [
            SpectatorViewLayout.FollowPrevious, SpectatorViewLayout.FollowName,
            SpectatorViewLayout.FollowNext, SpectatorViewLayout.Leave,
        ];
        Assert.All(controls, control => Assert.True(panel.Contains(control), $"{control} leaves the panel"));
        for (var first = 0; first < controls.Length; first++)
            for (var second = first + 1; second < controls.Length; second++)
                Assert.False(controls[first].Intersects(controls[second]));
        var lastLine = SpectatorViewLayout.LineY(SpectatorViewLayout.TextLines - 1) + 4
            + OriginalFontLayout.GlyphHeight;
        Assert.True(lastLine < SpectatorViewLayout.FollowCaptionY);
        Assert.True(SpectatorViewLayout.FollowCaptionY + OriginalFontLayout.GlyphHeight
            <= SpectatorViewLayout.FollowPrevious.Y);
        Assert.True(SpectatorViewLayout.TextLeft + SpectatorViewLayout.LineColumns * OriginalFontLayout.CellWidth
            <= panel.Right);
    }

    /// <summary>Every line the panel writes fits a panel line.</summary>
    [Fact]
    public void ThePanelsFixedLinesFitItsWidth()
    {
        string[] lines =
        [
            "WATCHING", "MAP DRAWN FOR", "TURN 999 OF 999", "20 TURNS BEHIND",
            "FOLLOWING THE MATCH", "SHOWING THE LAST TURNS", "THE MATCH WAS ABANDONED",
        ];
        Assert.All(lines, line => Assert.True(line.Length <= SpectatorViewLayout.LineColumns, line));
        Assert.True(SpectatorViewLayout.Footer.Length * OriginalFontLayout.CellWidth + 18
            <= CityMapLayout.Bounds.Right);
    }

    [Fact]
    public void TheListOfSpectatorsKeepsItsRowsAndButtonsInsideItsPanel()
    {
        var panel = SpectatorListLayout.Panel;
        var lastRow = SpectatorListLayout.Row(SpectatorListLayout.Rows - 1);
        Assert.True(panel.Contains(SpectatorListLayout.Row(0)));
        Assert.True(panel.Contains(lastRow));
        Assert.True(lastRow.Bottom <= SpectatorListLayout.StatusY);
        Assert.True(SpectatorListLayout.StatusY + OriginalFontLayout.GlyphHeight <= SpectatorListLayout.Remove.Y);
        Assert.True(panel.Contains(SpectatorListLayout.Remove));
        Assert.True(panel.Contains(SpectatorListLayout.Close));
        Assert.False(SpectatorListLayout.Remove.Intersects(SpectatorListLayout.Close));
    }

    [Fact]
    public void ArrivalsAndDeparturesAreAnnouncedByName()
    {
        Assert.Equal("EVE IS WATCHING", SpectatorAnnouncement.Joined("eve"));
        Assert.Equal("EVE STOPPED WATCHING", SpectatorAnnouncement.Left("Eve", removed: false));
        Assert.Equal("EVE WAS REMOVED FROM THE SPECTATORS", SpectatorAnnouncement.Left("EVE", removed: true));
        Assert.Equal("A SPECTATOR IS WATCHING", SpectatorAnnouncement.Joined("  "));
    }
}
