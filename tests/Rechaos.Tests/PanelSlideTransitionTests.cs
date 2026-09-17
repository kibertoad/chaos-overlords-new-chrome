using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class PanelSlideTransitionTests
{
    [Theory]
    [InlineData(ClientScreen.GameInfo)]
    [InlineData(ClientScreen.Hire)]
    [InlineData(ClientScreen.SectorGangs)]
    [InlineData(ClientScreen.Site)]
    [InlineData(ClientScreen.ItemInformation)]
    [InlineData(ClientScreen.Finance)]
    public void NativeAdjacentBufferPanelsUseRecovered320PixelTravel(ClientScreen screen)
    {
        var slide = new PanelSlideTransition();
        var start = TimeSpan.FromSeconds(4);

        slide.Begin(screen, start);

        Assert.Equal(PanelSlideTransition.AlternateStartOffset, slide.Offset(screen, start));
        Assert.Equal(160, slide.Offset(screen, start + PanelSlideTransition.Duration / 2));
    }
}
