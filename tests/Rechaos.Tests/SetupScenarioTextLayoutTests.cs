using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SetupScenarioTextLayoutTests
{
    // FND-SETUP-019: a line ends at the last space at or before its 37th character, and the next
    // starts after that space, so a double space leaves one at the end of the line.
    [Fact]
    public void ALineBreaksAtTheLastSpaceAndTheNextStartsAfterIt()
    {
        var first = new string('A', 10) + " " + new string('B', 21);
        Assert.Equal([first + " ", "NEXTWORD MORE"],
            SetupScenarioTextLayout.DescriptionLines(first + "  NEXTWORD MORE"));
        Assert.Equal(["ABCD EFGH IJKL MNOP QRST UVWX", "YZ0123.  NEXT WORD AND SO ON"],
            SetupScenarioTextLayout.DescriptionLines("ABCD EFGH IJKL MNOP QRST UVWX YZ0123.  NEXT WORD AND SO ON"));
        Assert.Equal(["ONE LINE"], SetupScenarioTextLayout.DescriptionLines("ONE LINE"));
    }

    // Each break takes one space, so the lines joined by a space give the whole description back
    // when none of it falls past the fifth line.
    [Fact]
    public void EveryDescriptionFitsTheBox()
    {
        foreach (var scenario in Enum.GetValues<ScenarioId>())
        {
            var description = ExecutableStrings.ScenarioDescription(scenario);
            var lines = SetupScenarioTextLayout.DescriptionLines(description);
            Assert.All(lines, line => Assert.InRange(line.Length, 0, SetupScenarioTextLayout.LineCells));
            Assert.Equal(description, string.Join(' ', lines));
        }
    }

    // FND-SETUP-014: card 0's bar is at (385, 95) and card 5's at (468, 243); a name of nine
    // characters starts 27 pixels left of x 45 of its card.
    [Fact]
    public void CardsFollowFndSetup014()
    {
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(385, 95, 9, 41), SetupPlayerCardArtLayout.ColourBar(0));
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(468, 243, 9, 41), SetupPlayerCardArtLayout.ColourBar(5));
        Assert.Equal(new Microsoft.Xna.Framework.Point(403, 153), SetupPlayerCardArtLayout.NameStart(0, 9));
    }
}
