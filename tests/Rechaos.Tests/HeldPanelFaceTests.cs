using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The held-button helper's faces on the information panels (FND-UI-062, FND-UI-067): where the
/// rebuild draws them, which image, and what a release leaves.
/// </summary>
public sealed class HeldPanelFaceTests
{
    [Fact]
    public void TheHelperDrawsTheLitFaceInsideAndThePlainFaceOutside()
    {
        // FND-UI-062: the face lands at the control's corner at the size of its sheet image.
        var face = new Rectangle(161, 293, 49, 22);
        Assert.Equal((new Rectangle(161, 293, 50, 23), new Rectangle(0, 386, 50, 23)),
            HeldButtonFaces.Drawn(HeldButtonKind.Confirm, face, pointerInside: true));
        Assert.Equal((new Rectangle(161, 293, 50, 23), new Rectangle(50, 386, 50, 23)),
            HeldButtonFaces.Drawn(HeldButtonKind.Confirm, face, pointerInside: false));
        Assert.Equal((new Rectangle(137, 261, 50, 23), new Rectangle(0, 409, 50, 23)),
            HeldButtonFaces.Drawn(HeldButtonKind.Cancel, IdleGangWarningLayout.Cancel, pointerInside: true));
    }

    public static TheoryData<string, Rectangle, Rectangle> PanelFaces() => new()
    {
        // FND-UI-067: each panel's face test and the helper rectangle it passes, as (left, top).
        { "Gang information", GangInformationLayout.Ok, new Rectangle(137, 293, 50, 23) },
        { "Item Information", ItemInformationLayout.Ok, new Rectangle(161, 293, 50, 23) },
        { "Site Information", SiteInformationLayout.Ok, new Rectangle(161, 293, 50, 23) },
        { "Financial", FinanceLayout.Ok, new Rectangle(161, 293, 50, 23) },
        { "Gangs in Sector", SectorGangsLayout.Ok, new Rectangle(137, 293, 50, 23) },
        { "Player Rankings", PlayerRankingLayout.Ok, new Rectangle(137, 293, 50, 23) },
        { "Combat Results", CombatResultsLayout.Ok, new Rectangle(137, 293, 50, 23) },
        { "Hire comparison", HireComparisonLayout.Ok, new Rectangle(161, 293, 50, 23) },
        { "Game Information", GameInformationLayout.Ok, new Rectangle(161, 293, 50, 23) },
        { "Gang definition information", GangDefinitionInformationLayout.Ok, new Rectangle(161, 293, 50, 23) },
        { "Comlink View", ComlinkViewLayout.Ok, new Rectangle(137, 293, 50, 23) },
        { "Idle-gang warning OK", IdleGangWarningLayout.Ok, new Rectangle(137, 293, 50, 23) },
        { "Idle-gang warning Cancel", IdleGangWarningLayout.Cancel, new Rectangle(137, 261, 50, 23) },
    };

    [Theory]
    [MemberData(nameof(PanelFaces))]
    public void EachPanelTestsAFaceOnePixelSmallerThanTheHelpersRectangle(string panel, Rectangle face, Rectangle helper)
    {
        Assert.True(new Rectangle(helper.X, helper.Y, 49, 22) == face, $"{panel}: {face}");
        Assert.Equal(helper, HeldButtonFaces.Drawn(HeldButtonKind.Confirm, face, pointerInside: true).Destination);
    }

    [Fact]
    public void TheOutsideTestsFollowTheOriginal()
    {
        // FND-UI-067: Financial, Game Information and the Hire comparison test the full 344-pixel
        // panel; Item Information, Site Information and gang definition information the 320-pixel
        // one they are drawn in.
        Assert.Equal(new Rectangle(104, 124, 344, 209), SharedPanelLayout.Panel);
        Assert.Equal(new Rectangle(104, 124, 344, 209), GameInformationLayout.InputBounds);
        Assert.Equal(new Rectangle(128, 124, 320, 209), ItemInformationLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), SiteInformationLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), GangDefinitionInformationLayout.Panel);
    }

    [Fact]
    public void AReleaseOutsideLeavesThePlainFaceAndOneInsideCloses()
    {
        var closed = 0;
        var face = ItemInformationLayout.Ok;
        var game = GameHolding(face, () => closed++);

        Release(game, new Point(300, 300));
        Assert.Equal(0, closed);
        Assert.Null(Field("_pressedPanelFace").GetValue(game));
        Assert.Equal((face, ClientScreen.Title, HeldButtonKind.Confirm),
            ((Rectangle, ClientScreen, HeldButtonKind)?)Field("_releasedPanelFace").GetValue(game));

        game = GameHolding(face, () => closed++);
        Release(game, face.Center);
        Assert.Equal(1, closed);
    }

    private static ChaosGame GameHolding(Rectangle face, Action close)
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Field("_screens").SetValue(game, new ScreenRouter());
        Field("_pressedPanelFace").SetValue(game, (face, ClientScreen.Title, close));
        Field("_pressedPanelFaceKind").SetValue(game, (HeldButtonKind?)HeldButtonKind.Confirm);
        return game;
    }

    private static void Release(ChaosGame game, Point point) =>
        typeof(ChaosGame).GetMethod("CompletePointerRelease", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(game, [true, point, false]);

    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
}
