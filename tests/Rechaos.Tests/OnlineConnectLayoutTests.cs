using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineConnectLayoutTests
{
    /// <summary>
    /// Every online screen stands in the same frame.
    /// </summary>
    /// <remarks>
    /// The point of the frame is that a player walking from the connect form to the browser and back
    /// finds the title, the status and the actions where they left them, so a screen that quietly
    /// moved one of them would undo it.
    /// </remarks>
    [Fact]
    public void TheFrameHoldsEveryScreensHeaderFooterAndActions()
    {
        var panel = OnlineScreenLayout.Panel;
        Assert.Equal(VirtualInput.Width / 2, panel.Center.X);
        Assert.True(OnlineScreenLayout.TitleY > panel.Y);
        Assert.True(OnlineScreenLayout.HeaderRuleY < OnlineScreenLayout.BodyTop);
        Assert.True(OnlineScreenLayout.FooterRuleY < OnlineScreenLayout.StatusY);
        Assert.All(
            new[]
            {
                OnlineScreenLayout.Action(0), OnlineScreenLayout.Action(1),
                OnlineScreenLayout.ThirdAction(0), OnlineScreenLayout.ThirdAction(1),
                OnlineScreenLayout.ThirdAction(2), OnlineScreenLayout.Nav(0),
                OnlineScreenLayout.Nav(1)
            },
            action => Assert.True(panel.Contains(action), $"{action} escapes the panel"));
        // The status line is drawn above the actions and clear of both the rule and the buttons.
        Assert.True(
            OnlineScreenLayout.StatusY + OriginalFontLayout.GlyphHeight
                <= OnlineScreenLayout.ActionY);
    }

    /// <summary>
    /// Nothing on any screen writes through the panel's own border.
    /// </summary>
    /// <remarks>
    /// The unfinished sessions and the seat picker used to put their status line four pixels above
    /// the bottom of a panel whose border is two pixels thick, so the words were drawn through it.
    /// </remarks>
    [Fact]
    public void TheLastLineOfEveryScreenStaysInsideThePanel()
    {
        var panel = OnlineScreenLayout.Panel;
        var lastRow = OnlineScreenLayout.ListRow(
            OnlineConnectLayout.DiscoveryTop, OnlineScreenLayout.ListRows - 1);
        Assert.True(lastRow.Bottom <= OnlineScreenLayout.FooterRuleY);
        Assert.True(
            OnlineScreenLayout.ListRow(
                OnlineConnectLayout.HistoryTop, OnlineScreenLayout.ListRows - 1).Bottom
                <= OnlineConnectLayout.HistoryNoteY);
        Assert.True(
            OnlineConnectLayout.HistoryNoteY + OriginalFontLayout.GlyphHeight
                <= OnlineScreenLayout.FooterRuleY);
        Assert.True(OnlineScreenLayout.ActionY + OnlineScreenLayout.ActionHeight < panel.Bottom - 2);
    }

    [Fact]
    public void FieldsLeaveRoomForEveryLabelAndJoinAction()
    {
        Assert.False(OnlineConnectLayout.JoinCode.Intersects(OnlineConnectLayout.PasteJoinCode));
        Assert.Equal(OnlineConnectLayout.JoinCode.Y, OnlineConnectLayout.PasteJoinCode.Y);
        Assert.True(OnlineConnectLayout.PasteJoinCode.Right <= OnlineScreenLayout.ContentRight);
        Assert.False(OnlineConnectLayout.Continue.Intersects(OnlineConnectLayout.Back));
        Assert.False(OnlineConnectLayout.Discover.Intersects(OnlineConnectLayout.Reconnect));
        // The ways elsewhere stand above the rule; the form's own action stands below it.
        Assert.True(OnlineConnectLayout.Discover.Bottom <= OnlineScreenLayout.FooterRuleY);
        Assert.True(OnlineConnectLayout.Continue.Y > OnlineScreenLayout.StatusY);
        Assert.All(OnlineConnectLayout.Fields.Zip(OnlineConnectLayout.Fields.Skip(1)), pair =>
            Assert.True(pair.First.Bottom + 16 <= pair.Second.Y));
    }

    /// <summary>
    /// The form asks its questions in the order they matter, and tabs through them the same way.
    /// </summary>
    /// <remarks>
    /// The role decides what the rest of the form means, so it leads; the server is last because
    /// almost nobody changes it, and it used to be the first thing the screen demanded.
    /// </remarks>
    [Fact]
    public void TheFormReadsFromWhatToDoDownToWhichServer()
    {
        Assert.True(OnlineConnectLayout.HostRole.Bottom <= OnlineConnectLayout.Name.Y);
        Assert.True(OnlineConnectLayout.Name.Bottom <= OnlineConnectLayout.JoinCode.Y);
        Assert.True(OnlineConnectLayout.JoinCode.Bottom <= OnlineConnectLayout.Password.Y);
        Assert.True(OnlineConnectLayout.Password.Bottom <= OnlineConnectLayout.Central.Y);
        Assert.Equal(
            new[]
            {
                OnlineConnectLayout.Name, OnlineConnectLayout.JoinCode,
                OnlineConnectLayout.Password, OnlineConnectLayout.Server
            },
            OnlineConnectLayout.Fields);
    }

    /// <summary>The service, its address and its health share one row without colliding.</summary>
    [Fact]
    public void TheServerRowCarriesTheChoiceAndTheAddressSideBySide()
    {
        Assert.Equal(OnlineConnectLayout.Central.Y, OnlineConnectLayout.Custom.Y);
        Assert.Equal(OnlineConnectLayout.Central.Y, OnlineConnectLayout.Server.Y);
        Assert.True(OnlineConnectLayout.Central.Right < OnlineConnectLayout.Custom.X);
        Assert.True(OnlineConnectLayout.Custom.Right < OnlineConnectLayout.Server.X);
        Assert.Equal(OnlineScreenLayout.ContentRight, OnlineConnectLayout.Server.Right);
        Assert.Equal(
            OnlineConnectLayout.ServerStatusY,
            OnlineConnectLayout.Central.Y - OnlineScreenLayout.CaptionOffset);
    }

    /// <summary>
    /// The listing choice is a pair on the row the join code uses, matching the pair above it.
    /// </summary>
    /// <remarks>
    /// The two halves are drawn one over the other, since a host has no code to type and a joining
    /// player has no session to list, so they have to cover the same ground.
    /// </remarks>
    [Fact]
    public void TheListingChoiceStandsWhereTheJoinCodeDoes()
    {
        Assert.Equal(
            (OnlineConnectLayout.HostRole.Width, OnlineConnectLayout.HostRole.Height),
            (OnlineConnectLayout.PublicChoice.Width, OnlineConnectLayout.PublicChoice.Height));
        Assert.Equal(OnlineConnectLayout.HostRole.X, OnlineConnectLayout.PublicChoice.X);
        Assert.Equal(OnlineConnectLayout.JoinRole.X, OnlineConnectLayout.PrivateChoice.X);
        Assert.Equal(OnlineConnectLayout.PublicChoice.Y, OnlineConnectLayout.PrivateChoice.Y);
        Assert.False(OnlineConnectLayout.PublicChoice.Intersects(OnlineConnectLayout.PrivateChoice));
        Assert.True(OnlineConnectLayout.PublicChoice.Intersects(OnlineConnectLayout.JoinCode));
    }

    [Fact]
    public void ConnectionErrorActionsStayInsideTheCenteredModal()
    {
        var panel = OnlineConnectLayout.ErrorPanel;
        Assert.Equal(320, panel.Center.X);
        Assert.True(panel.Contains(OnlineConnectLayout.CopyError));
        Assert.True(panel.Contains(OnlineConnectLayout.DismissError));
        Assert.False(OnlineConnectLayout.CopyError.Intersects(OnlineConnectLayout.DismissError));
    }

    /// <summary>
    /// The face picker shares the name's row without standing on anything.
    /// </summary>
    /// <remarks>
    /// The name gave up the width it needed, so the two have to add up to the same row the rest of
    /// the form is drawn on: an arrow either side of the face, clear of the name beside it and of
    /// the join code and paste button that come next.
    /// </remarks>
    [Fact]
    public void TheFacePickerSitsBesideTheNameWithoutCrowdingTheRowsAroundIt()
    {
        var face = OnlineConnectLayout.Portrait;
        Assert.Equal(OnlineConnectLayout.Name.Y, face.Y);
        Assert.Equal(OnlineConnectLayout.PortraitPrevious.Y, face.Y);
        Assert.Equal(OnlineConnectLayout.PortraitNext.Y, face.Y);
        Assert.True(OnlineConnectLayout.Name.Right < OnlineConnectLayout.PortraitPrevious.X);
        Assert.True(OnlineConnectLayout.PortraitPrevious.Right <= face.X);
        Assert.True(face.Right <= OnlineConnectLayout.PortraitNext.X);
        Assert.True(OnlineConnectLayout.PortraitNext.Right <= OnlineScreenLayout.ContentRight);
        Assert.All(
            new[]
            {
                OnlineConnectLayout.JoinCode, OnlineConnectLayout.PasteJoinCode,
                OnlineConnectLayout.PublicChoice, OnlineConnectLayout.PrivateChoice,
                OnlineConnectLayout.Password, OnlineConnectLayout.HostRole,
                OnlineConnectLayout.JoinRole
            },
            other =>
            {
                Assert.False(other.Intersects(face));
                Assert.False(other.Intersects(OnlineConnectLayout.PortraitPrevious));
                Assert.False(other.Intersects(OnlineConnectLayout.PortraitNext));
            });
    }

    /// <summary>
    /// The browser's three filters, its rows and its three actions share one set of columns.
    /// </summary>
    [Fact]
    public void TheBrowserPutsItsActionsOnTheColumnsItsFiltersUse()
    {
        for (var column = 0; column < DiscoveryFilters.Count; column++)
        {
            Assert.Equal(
                OnlineConnectLayout.DiscoveryFilter(column).X,
                OnlineScreenLayout.ThirdAction(column).X);
            Assert.Equal(
                OnlineConnectLayout.DiscoveryFilter(column).Width,
                OnlineScreenLayout.ThirdAction(column).Width);
        }
        Assert.False(
            OnlineConnectLayout.DiscoveryJoin.Intersects(OnlineConnectLayout.DiscoveryRefresh));
        Assert.False(
            OnlineConnectLayout.DiscoveryRefresh.Intersects(OnlineConnectLayout.DiscoveryBack));
        Assert.True(
            OnlineConnectLayout.DiscoveryFilter(DiscoveryFilters.Status).Bottom
                < OnlineConnectLayout.DiscoveryRow(0).Y);
    }

    /// <summary>Every list in the flow scrolls the same way and stacks its rows without a gap fault.</summary>
    [Theory]
    [InlineData(OnlineConnectLayout.DiscoveryTop)]
    [InlineData(OnlineConnectLayout.HistoryTop)]
    public void ListRowsStackWithoutOverlappingOrLeavingThePanel(int top)
    {
        var rows = Enumerable.Range(0, OnlineScreenLayout.ListRows)
            .Select(index => OnlineScreenLayout.ListRow(top, index))
            .ToArray();
        Assert.All(rows.Zip(rows.Skip(1)), pair =>
            Assert.True(pair.First.Bottom < pair.Second.Y));
        Assert.All(rows, row => Assert.True(OnlineScreenLayout.Panel.Contains(row)));
        Assert.True(rows[^1].Bottom <= OnlineScreenLayout.FooterRuleY);
    }

    /// <summary>
    /// The lobby roster stacks a face per seat and keeps clear of everything around it.
    /// </summary>
    /// <remarks>
    /// Six seats and the line counting the computer players that fill the rest share one column with
    /// the join code above them, so the rows are the thing that has to stay small.
    /// </remarks>
    [Fact]
    public void TheLobbyRosterFitsSixSeatsAndTheLineUnderThem()
    {
        var rows = Enumerable.Range(0, 6).Select(OnlineLobbyLayout.RosterPortrait).ToArray();
        Assert.All(rows.Zip(rows.Skip(1)), pair =>
            Assert.True(pair.First.Bottom <= pair.Second.Y));
        Assert.True(OnlineLobbyLayout.CopyCode.Bottom <= rows[0].Y);
        // The count of computer players is drawn on the row after the last seat, above the hint.
        Assert.True(
            OnlineLobbyLayout.RosterPortrait(6).Bottom <= OnlineLobbyLayout.WaitingHintY);
        Assert.True(OnlineLobbyLayout.WaitingHintY < OnlineScreenLayout.FooterRuleY);
        Assert.All(rows, row => Assert.True(row.Right < OnlineLobbyLayout.SettingsLeft));
        Assert.All(rows, row => Assert.False(row.Intersects(OnlineLobbyLayout.SessionName)));
        Assert.All(rows, row => Assert.False(row.Intersects(OnlineLobbyLayout.Setup)));
    }

    /// <summary>
    /// Every captioned control on the connect screen leaves room for its caption.
    /// </summary>
    /// <remarks>
    /// Two 30-high buttons stacked on the 38-pixel row pitch left the lower one's caption with
    /// nowhere to go, and its words were drawn through the bottom border of the button above it.
    /// </remarks>
    [Fact]
    public void ConnectScreenCaptionsClearWhateverStandsAboveThem() =>
        AssertCaptionsFit(
            captioned:
            [
                OnlineConnectLayout.HostRole, OnlineConnectLayout.Name,
                OnlineConnectLayout.Portrait, OnlineConnectLayout.JoinCode,
                OnlineConnectLayout.Password, OnlineConnectLayout.PublicChoice,
                OnlineConnectLayout.Central
            ],
            everything:
            [
                OnlineConnectLayout.HostRole, OnlineConnectLayout.JoinRole,
                OnlineConnectLayout.Name, OnlineConnectLayout.Portrait,
                OnlineConnectLayout.PortraitPrevious, OnlineConnectLayout.PortraitNext,
                OnlineConnectLayout.JoinCode, OnlineConnectLayout.PasteJoinCode,
                OnlineConnectLayout.PublicChoice, OnlineConnectLayout.PrivateChoice,
                OnlineConnectLayout.Password, OnlineConnectLayout.Central,
                OnlineConnectLayout.Custom, OnlineConnectLayout.Server,
                OnlineConnectLayout.Discover, OnlineConnectLayout.Reconnect,
                OnlineConnectLayout.Continue, OnlineConnectLayout.Back
            ]);

    /// <summary>The same, for the settings the lobby puts beside its roster.</summary>
    [Fact]
    public void LobbyCaptionsClearWhateverStandsAboveThem() =>
        AssertCaptionsFit(
            captioned:
            [
                OnlineLobbyLayout.SessionName, OnlineLobbyLayout.PublicChoice,
                OnlineLobbyLayout.LateJoinAllowed
            ],
            everything:
            [
                OnlineLobbyLayout.SessionName, OnlineLobbyLayout.PublicChoice,
                OnlineLobbyLayout.PrivateChoice, OnlineLobbyLayout.LateJoinAllowed,
                OnlineLobbyLayout.LateJoinRefused, OnlineLobbyLayout.Setup,
                OnlineLobbyLayout.Start, OnlineLobbyLayout.CopyCode, OnlineLobbyLayout.Leave
            ]);

    /// <summary>
    /// The lobby's summary stacks under the settings and above the button that changes them.
    /// </summary>
    /// <remarks>
    /// It is what everybody in the lobby reads about the match, so it has to be readable: four lines
    /// that do not run into the settings above them, into each other, or into CHANGE GAME RULES.
    /// </remarks>
    [Fact]
    public void TheLobbySummarySitsBetweenTheSettingsAndTheButtonThatChangesThem()
    {
        var rows = Enumerable.Range(0, 4).Select(OnlineLobbyLayout.SummaryRow).ToArray();
        Assert.True(
            OnlineLobbyLayout.LateJoinAllowed.Bottom <= OnlineLobbyLayout.SummaryCaptionY);
        Assert.True(
            OnlineLobbyLayout.SummaryCaptionY + OriginalFontLayout.GlyphHeight <= rows[0].Y);
        Assert.All(rows.Zip(rows.Skip(1)), pair =>
            Assert.True(pair.First.Bottom <= pair.Second.Y));
        Assert.True(rows[^1].Bottom <= OnlineLobbyLayout.Setup.Y);
        Assert.True(OnlineLobbyLayout.Setup.Bottom <= OnlineLobbyLayout.WaitingHintY);
        Assert.All(rows, row => Assert.Equal(OnlineScreenLayout.ContentRight, row.Right));
    }

    /// <summary>
    /// Asserts that no caption is drawn over anything.
    /// </summary>
    /// <remarks>
    /// Controls that overlap each other are the two halves of one place, drawn one at a time, so
    /// they are exempt from each other. Everything else on the screen has to stay clear.
    /// </remarks>
    private static void AssertCaptionsFit(Rectangle[] captioned, Rectangle[] everything)
    {
        foreach (var control in captioned)
        {
            var caption = new Rectangle(
                control.X, control.Y - OnlineScreenLayout.CaptionOffset,
                control.Width, OriginalFontLayout.GlyphHeight);
            Assert.All(
                everything.Where(other => other != control && !other.Intersects(control)),
                other => Assert.False(caption.Intersects(other),
                    $"a caption at {caption} is drawn over {other}"));
        }
    }
}
