using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class UiNavigationTests
{
    [Fact]
    public void RouterStartsAtTitleAndBackReturnsThere()
    {
        var router = new ScreenRouter();

        Assert.Equal(ClientScreen.Title, router.Current);
        Assert.False(router.Back());
        router.Show(ClientScreen.Setup);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.City);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.Endgame);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.Handoff);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.City);
        router.Show(ClientScreen.Events);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
    }
}
