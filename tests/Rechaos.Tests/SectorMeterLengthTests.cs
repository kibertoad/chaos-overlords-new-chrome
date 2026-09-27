using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>Checks the detailed-sector meter lengths against RULE-UI-005's procedure.</summary>
public sealed class SectorMeterLengthTests
{
    /// <summary>RULE-UI-005 <c>site_meter_length</c>.</summary>
    private static int SiteMeterLength(int progress, int resistance) =>
        resistance == 0 ? 100 : progress * 100 / resistance;

    [Theory]
    // The rebuild stores the Resistance still to overcome; progress is base minus remaining.
    [InlineData(3, 1, 66)]
    [InlineData(3, 2, 33)]
    [InlineData(3, 0, 100)]
    [InlineData(3, 3, 0)]
    [InlineData(7, 1, 85)]
    [InlineData(9, 8, 11)]
    [InlineData(0, 0, 100)]
    public void SiteMeterTruncatesThePercentOvercome(int resistance, int remaining, int expected)
    {
        Assert.Equal(expected, SiteMeterLength(resistance - remaining, resistance));
        Assert.Equal(expected, SectorDetailLayout.SiteControlWidth(resistance, remaining));
    }

    [Fact]
    public void SiteMeterMatchesTheProcedureForEveryProgressUpToTwenty()
    {
        for (var resistance = 0; resistance <= 20; resistance++)
        for (var progress = 0; progress <= resistance; progress++)
            Assert.Equal(SiteMeterLength(progress, resistance),
                SectorDetailLayout.SiteControlWidth(resistance, resistance - progress));
    }

    [Fact]
    public void ForceMeterGrowsSixPixelsPerPoint()
    {
        for (var force = 0; force <= 10; force++)
            Assert.Equal(force * 6, SectorGangCardLayout.ForceWidth(force));
    }
}
