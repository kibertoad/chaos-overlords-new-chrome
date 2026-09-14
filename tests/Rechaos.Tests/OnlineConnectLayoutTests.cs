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
        Assert.Equal(new Rectangle(120, 264, 240, 22), OnlineConnectLayout.JoinCode);
        Assert.Equal(new Rectangle(368, 260, 152, 24), OnlineConnectLayout.PasteJoinCode);
        Assert.Equal(new Rectangle(120, 302, 240, 22), OnlineConnectLayout.Password);
        // The host's two choices stand in the margin those two fields leave, stacked in the column
        // the paste button occupies while joining.
        foreach (var choice in new[] { OnlineConnectLayout.Visibility, OnlineConnectLayout.LateJoin })
        {
            Assert.Equal(
                (OnlineConnectLayout.PasteJoinCode.X, OnlineConnectLayout.PasteJoinCode.Width),
                (choice.X, choice.Width));
            Assert.False(choice.Intersects(OnlineConnectLayout.JoinCode));
            Assert.False(choice.Intersects(OnlineConnectLayout.Password));
        }
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
    /// Every captioned control leaves room for the caption above it.
    /// </summary>
    /// <remarks>
    /// Two 30-high buttons stacked on the 38-pixel row pitch left the lower one's caption with
    /// nowhere to go, and LATE JOIN was drawn through the bottom border of the button above it.
    /// </remarks>
    [Fact]
    public void CaptionedControlsClearWhateverStandsAboveThem()
    {
        var captioned = new[]
        {
            OnlineConnectLayout.Name, OnlineConnectLayout.JoinCode, OnlineConnectLayout.Password,
            OnlineConnectLayout.Visibility, OnlineConnectLayout.LateJoin
        };
        var everything = captioned.Concat(
        [
            OnlineConnectLayout.Central, OnlineConnectLayout.Server, OnlineConnectLayout.HostRole,
            OnlineConnectLayout.PasteJoinCode, OnlineConnectLayout.Continue
        ]).ToArray();

        foreach (var control in captioned)
        {
            var caption = new Rectangle(
                control.X, control.Y - OnlineConnectLayout.CaptionHeight,
                control.Width, OnlineConnectLayout.CaptionHeight);
            Assert.All(everything.Where(other => other != control),
                other => Assert.False(caption.Intersects(other)));
        }
    }
}
