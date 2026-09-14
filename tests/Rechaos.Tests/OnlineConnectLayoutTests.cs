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
        Assert.Equal(new Rectangle(120, 378, 190, 30), OnlineConnectLayout.Discover);
        Assert.Equal(new Rectangle(330, 378, 190, 30), OnlineConnectLayout.Reconnect);
        Assert.Equal((416, 436),
            (OnlineConnectLayout.ServerStatusY, OnlineConnectLayout.StatusY));
        Assert.All(OnlineConnectLayout.Fields.Zip(OnlineConnectLayout.Fields.Skip(1)), pair =>
            Assert.True(pair.First.Bottom + 16 <= pair.Second.Y));
    }
}
