using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>Checks the panel slide-in against RULE-UI-003's procedure.</summary>
public sealed class PanelSlideTransitionTests
{
    [Theory]
    [InlineData(ClientScreen.GameInfo)]
    [InlineData(ClientScreen.Hire)]
    [InlineData(ClientScreen.Site)]
    [InlineData(ClientScreen.ItemInformation)]
    [InlineData(ClientScreen.Finance)]
    public void AlternateCropPanelsTravel320Pixels(ClientScreen screen) =>
        Assert.Equal(PanelSlideTransition.AlternateStartOffset, PanelSlideTransition.StartOffsetFor(screen));

    [Theory]
    [InlineData(ClientScreen.SectorGangs)]
    [InlineData(ClientScreen.Gang)]
    [InlineData(ClientScreen.Ranking)]
    [InlineData(ClientScreen.Events)]
    public void PrimaryPanelsTravel344Pixels(ClientScreen screen) =>
        Assert.Equal(PanelSlideTransition.StartOffset, PanelSlideTransition.StartOffsetFor(screen));

    [Theory]
    // A quarter of the count, at least 1, divides the travel; the step is at least 16.
    [InlineData(344, 0, 344)]
    [InlineData(344, 3, 344)]
    [InlineData(344, 4, 344)]
    [InlineData(344, 8, 172)]
    [InlineData(344, 40, 34)]
    [InlineData(344, 80, 17)]
    [InlineData(344, 84, 16)]
    [InlineData(344, 10000, 16)]
    [InlineData(320, 40, 32)]
    [InlineData(320, 80, 16)]
    [InlineData(320, 84, 16)]
    public void SlideStepFollowsTheBenchmarkArithmetic(int travel, int count, int expected) =>
        Assert.Equal(expected, PanelSlideTransition.SlideStep(travel, count));

    [Fact]
    public void OpeningCopiesStopAtTheLastWholeStepBelowTheTravel()
    {
        // 34 * 10 = 340 is the last whole step below 344; the final copy covers the 4 left over.
        Assert.Equal([310, 276, 242, 208, 174, 140, 106, 72, 38, 4, 0],
            PanelSlideTransition.SlideInOffsets(344, 34, slidePanels: true));
        // A step equal to the travel leaves the final copy alone.
        Assert.Equal([0], PanelSlideTransition.SlideInOffsets(344, 344, slidePanels: true));
        // With Slide Panels off only the final copy is made.
        Assert.Equal([0], PanelSlideTransition.SlideInOffsets(320, 16, slidePanels: false));
        // An exact multiple stops one step short: 320 = 20 * 16.
        var alternate = PanelSlideTransition.SlideInOffsets(320, 16, slidePanels: true);
        Assert.Equal(20, alternate.Count);
        Assert.Equal(304, alternate[0]);
        Assert.Equal(16, alternate[^2]);
    }

    [Fact]
    public void EachCopyIsShownForOneBenchmarkCopy()
    {
        var slide = new PanelSlideTransition();
        var start = TimeSpan.FromSeconds(4);

        slide.Begin(ClientScreen.City, ClientScreen.Site, start);

        Assert.Equal(304, slide.Offset(ClientScreen.Site, start));
        Assert.Equal(304, slide.Offset(ClientScreen.Site, start + CopyStart(1) - TimeSpan.FromTicks(1)));
        Assert.Equal(288, slide.Offset(ClientScreen.Site, start + CopyStart(1)));
        Assert.Equal(16, slide.Offset(ClientScreen.Site, start + CopyStart(18)));
        Assert.Equal(16, slide.Offset(ClientScreen.Site, start + CopyStart(19) - TimeSpan.FromTicks(1)));
        Assert.Equal(0, slide.Offset(ClientScreen.Site, start + PanelSlideTransition.DurationFor(320)));
        Assert.Equal(TimeSpan.FromTicks(20 * TimeSpan.TicksPerSecond / 84), PanelSlideTransition.DurationFor(320));
        Assert.Equal(TimeSpan.FromTicks(22 * TimeSpan.TicksPerSecond / 84), PanelSlideTransition.DurationFor(344));
    }

    private static TimeSpan CopyStart(int copy) => TimeSpan.FromTicks(
        (copy * TimeSpan.TicksPerSecond + PanelSlideTransition.NominalBlitBenchmarkCount - 1)
        / PanelSlideTransition.NominalBlitBenchmarkCount);

    [Fact]
    public void TheDetailedSectorScreenNeitherSlidesNorPlaysPanelSounds()
    {
        Assert.False(PanelSlideTransition.IsPanel(ClientScreen.Sector));
        Assert.False(PanelSlideTransition.ShouldAnimate(ClientScreen.City, ClientScreen.Sector));
        Assert.Empty(AudioRouting.PanelTransitionSounds(ClientScreen.City, ClientScreen.Sector, true));
        Assert.Equal([GeneralSoundSlot.PanelOpen],
            AudioRouting.PanelTransitionSounds(ClientScreen.Sector, ClientScreen.Gang, true));
        Assert.Equal([GeneralSoundSlot.PanelClose],
            AudioRouting.PanelTransitionSounds(ClientScreen.Gang, ClientScreen.Sector, true));
    }

    [Fact]
    public void GangsInSectorAndTheFullGangPanelTravelTheSharedWidth()
    {
        // RULE-UI-003, SCR-UI-005, SCR-GANG-002: primary panels travel 344 pixels.
        Assert.Equal(PanelSlideTransition.StartOffset,
            PanelSlideTransition.StartOffsetFor(ClientScreen.SectorGangs));
        Assert.Equal(PanelSlideTransition.StartOffset,
            PanelSlideTransition.StartOffsetFor(ClientScreen.Gang));
        // SCR-GANG-001: the compact panel an order panel opens is an alternate one.
        Assert.Equal(PanelSlideTransition.AlternateStartOffset,
            PanelSlideTransition.StartOffsetFor(ClientScreen.Gang, compactGangPanel: true));

        var slide = new PanelSlideTransition();
        var start = TimeSpan.FromSeconds(4);
        slide.Begin(ClientScreen.Commands, ClientScreen.Gang, start, compactGangPanel: true);
        // The first copy already shows one step of the 320-pixel travel.
        var travel = PanelSlideTransition.AlternateStartOffset;
        Assert.Equal(PanelSlideTransition.SlideInOffsets(travel,
                PanelSlideTransition.SlideStep(travel, PanelSlideTransition.NominalBlitBenchmarkCount),
                slidePanels: true)[0],
            slide.Offset(ClientScreen.Gang, start));
    }

    /// <summary>
    /// RULE-UI-003: each copy draws only the panel's left travel - offset columns, ending at
    /// x 448, over rows 124 to 333.
    /// </summary>
    [Theory]
    [InlineData(ClientScreen.Items, false, 104, 344)]
    [InlineData(ClientScreen.Finance, false, 128, 320)]
    [InlineData(ClientScreen.Gang, false, 104, 344)]
    [InlineData(ClientScreen.Gang, true, 128, 320)]
    public void EachCopyShowsOnlyThePanelsLeftColumnsUpToX448(
        ClientScreen screen, bool compact, int left, int travel)
    {
        var panel = PanelSlideTransition.PanelAreaFor(screen, compact);
        Assert.Equal(new Rectangle(left, 124, travel, 209), panel);

        var step = PanelSlideTransition.SlideStep(travel, PanelSlideTransition.NominalBlitBenchmarkCount);
        foreach (var offset in PanelSlideTransition.SlideInOffsets(travel, step, slidePanels: true))
        {
            var visible = PanelSlideTransition.VisibleArea(panel, offset);
            Assert.Equal(448, visible.Right);
            Assert.Equal(travel - offset, visible.Width);
            Assert.Equal(left + offset, visible.Left);
            Assert.Equal(124, visible.Top);
            Assert.Equal(333, visible.Bottom);
        }
        // The first copy of a 344-pixel slide shows the 16 columns of one step.
        Assert.Equal(new Rectangle(432, 124, 16, 209), PanelSlideTransition.VisibleArea(
            PanelSlideTransition.PanelAreaFor(ClientScreen.Items), 344 - 16));
    }

    [Fact]
    public void TheSlideRemembersTheScreenThePanelOpenedFrom()
    {
        var slide = new PanelSlideTransition();
        slide.Begin(ClientScreen.Sector, ClientScreen.Finance, TimeSpan.FromSeconds(1));

        Assert.Equal(ClientScreen.Sector, slide.Previous);
        Assert.Equal(new Rectangle(128, 124, 320, 209), slide.PanelArea);
    }

    [Fact]
    public void TheClipRectangleFollowsTheWindowScale()
    {
        var doubled = new Viewport(0, 0, 1280, 920);
        Assert.Equal(new Rectangle(864, 248, 32, 418),
            VirtualInput.ToPhysical(doubled, new Rectangle(432, 124, 16, 209)));

        // A wider window centres the 640-by-460 screen and keeps the edges on whole pixels.
        var wide = new Viewport(0, 0, 1000, 460);
        Assert.Equal(new Rectangle(180 + 432, 124, 16, 209),
            VirtualInput.ToPhysical(wide, new Rectangle(432, 124, 16, 209)));
    }
}
