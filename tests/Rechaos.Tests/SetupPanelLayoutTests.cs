using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SetupPanelLayoutTests
{
    // SCR-SETUP-001, FND-SETUP-013: the left panel's rectangles, pressed images and test order.
    [Fact]
    public void SetupPanelControlsMatchTheOriginalRectangles()
    {
        Assert.Equal(new Rectangle(80, 109, 110, 32), SetupPanelLayout.Scenarios[0]);
        Assert.Equal(new Rectangle(194, 179, 110, 32), SetupPanelLayout.Scenarios[5]);
        Assert.Equal(new Rectangle(80, 215, 110, 32), SetupPanelLayout.Scenarios[6]);
        Assert.Equal(new Rectangle(194, 250, 110, 32), SetupPanelLayout.Scenarios[9]);
        Assert.Equal(
        [
            new Rectangle(80, 285, 53, 23), new Rectangle(137, 285, 53, 23),
            new Rectangle(194, 285, 53, 23), new Rectangle(251, 285, 53, 23)
        ], SetupPanelLayout.Durations);
        Assert.Equal(new Rectangle(80, 284, 224, 24), SetupPanelLayout.DurationArea);
        Assert.Equal(
            Enumerable.Range(0, 4).Select(row => new Rectangle(80, 337 + 27 * row, 110, 24)),
            SetupPanelLayout.AiMentalities);

        Assert.Equal(new Rectangle(110, 128, 110, 32), SetupPanelLayout.PressedSource(
            new SetupPanelControl(SetupPanelControlKind.Scenario, 9)));
        Assert.Equal(new Rectangle(159, 256, 53, 24), SetupPanelLayout.PressedSource(
            new SetupPanelControl(SetupPanelControlKind.Duration, 3)));
        Assert.Equal(new Rectangle(0, 232, 110, 24), SetupPanelLayout.PressedSource(
            new SetupPanelControl(SetupPanelControlKind.AiMentality, 3)));
        Assert.Equal(new Rectangle(110, 160, 110, 24), SetupPanelLayout.PressedSource(
            new SetupPanelControl(SetupPanelControlKind.PlanningTime, 0)));
        Assert.Equal(new Rectangle(304, 138, 8, 16), SetupPanelLayout.LightSource);

        Assert.Equal(new SetupPanelControl(SetupPanelControlKind.Duration, 1),
            SetupPanelLayout.HitTest(new Point(137, 285), timed: true));
        Assert.Null(SetupPanelLayout.HitTest(new Point(137, 285), timed: false));
        Assert.Null(SetupPanelLayout.HitTest(new Point(134, 290), timed: true));
        Assert.Null(SetupPanelLayout.HitTest(new Point(190, 337), timed: true));
        Assert.Equal(new SetupPanelControl(SetupPanelControlKind.PlanningTime, 3),
            SetupPanelLayout.HitTest(new Point(303, 441), timed: true));
    }
}
