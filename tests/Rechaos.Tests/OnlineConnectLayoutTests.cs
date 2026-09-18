using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineConnectLayoutTests
{
    [Fact]
    public void FieldsLeaveRoomForEveryLabelAndJoinAction()
    {
        Assert.Equal(new Rectangle(120, 188, 190, 26), OnlineConnectLayout.HostRole);
        Assert.Equal(new Rectangle(120, 264, 300, 22), OnlineConnectLayout.JoinCode);
        Assert.Equal(new Rectangle(428, 260, 92, 30), OnlineConnectLayout.PasteJoinCode);
        Assert.Equal(new Rectangle(120, 302, 400, 22), OnlineConnectLayout.Password);
        Assert.Equal(new Rectangle(120, 340, 190, 30), OnlineConnectLayout.Continue);
        Assert.Equal(new Rectangle(330, 340, 190, 30), OnlineConnectLayout.Discover);
        Assert.Equal(new Rectangle(120, 378, 190, 30), OnlineConnectLayout.Reconnect);
        Assert.Equal(new Rectangle(330, 378, 190, 30), OnlineConnectLayout.Back);
        Assert.Equal((416, 436),
            (OnlineConnectLayout.ServerStatusY, OnlineConnectLayout.StatusY));
        Assert.All(OnlineConnectLayout.Fields.Zip(OnlineConnectLayout.Fields.Skip(1)), pair =>
            Assert.True(pair.First.Bottom + 16 <= pair.Second.Y));
    }

    /// <summary>
    /// The listing choice is a pair on the row the join code uses, matching the pairs above it.
    /// </summary>
    /// <remarks>
    /// The two halves are drawn one over the other, since a host has no code to type and a joining
    /// player has no session to list, so they have to cover the same ground.
    /// </remarks>
    [Fact]
    public void TheListingChoiceStandsWhereTheJoinCodeDoes()
    {
        Assert.Equal(
            (OnlineConnectLayout.Central.Width, OnlineConnectLayout.Central.Height),
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
                OnlineConnectLayout.Name, OnlineConnectLayout.JoinCode,
                OnlineConnectLayout.Password, OnlineConnectLayout.PublicChoice
            ],
            everything:
            [
                OnlineConnectLayout.Central, OnlineConnectLayout.Custom, OnlineConnectLayout.Server,
                OnlineConnectLayout.HostRole, OnlineConnectLayout.JoinRole,
                OnlineConnectLayout.Name, OnlineConnectLayout.JoinCode,
                OnlineConnectLayout.PasteJoinCode, OnlineConnectLayout.PublicChoice,
                OnlineConnectLayout.PrivateChoice, OnlineConnectLayout.Password,
                OnlineConnectLayout.Continue, OnlineConnectLayout.Discover
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
                control.X, control.Y - OnlineConnectLayout.CaptionOffset,
                control.Width, OriginalFontLayout.GlyphHeight);
            Assert.All(
                everything.Where(other => other != control && !other.Intersects(control)),
                other => Assert.False(caption.Intersects(other)));
        }
    }
}
