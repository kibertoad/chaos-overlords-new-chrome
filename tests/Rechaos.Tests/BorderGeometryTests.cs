using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The outline strips must tile the border rather than overlap it. A translucent drag-target
/// highlight blends once per strip, so a corner covered twice reads darker than the edges that
/// meet there.
/// </summary>
public sealed class BorderGeometryTests
{
    public static TheoryData<int, int, int> Borders()
    {
        var data = new TheoryData<int, int, int>();
        foreach (var width in new[] { 0, 1, 2, 3, 4, 5, 40, 74 })
        foreach (var height in new[] { 0, 1, 2, 3, 4, 5, 28, 110 })
        foreach (var thickness in new[] { 0, 1, 2, 3 })
            data.Add(width, height, thickness);
        return data;
    }

    [Theory]
    [MemberData(nameof(Borders))]
    public void StripsCoverEveryBorderPixelExactlyOnce(int width, int height, int thickness)
    {
        var rectangle = new Rectangle(7, 13, width, height);
        Span<Rectangle> strips = stackalloc Rectangle[BorderGeometry.MaximumStrips];
        var count = BorderGeometry.Strips(rectangle, thickness, strips);

        var painted = new Dictionary<Point, int>();
        for (var strip = 0; strip < count; strip++)
        {
            var area = strips[strip];
            Assert.True(area.Width > 0 && area.Height > 0, $"Empty strip {strip}: {area}.");
            Assert.True(rectangle.Contains(area), $"Strip {strip} leaves the rectangle: {area}.");
            for (var y = area.Y; y < area.Bottom; y++)
            for (var x = area.X; x < area.Right; x++)
                painted[new Point(x, y)] = painted.GetValueOrDefault(new Point(x, y)) + 1;
        }

        Assert.All(painted, pixel => Assert.Equal(1, pixel.Value));
        Assert.Equal(ExpectedBorder(rectangle, thickness), painted.Keys.ToHashSet());
    }

    [Fact]
    public void SideStripsStopShortOfTheBandsTheyMeet()
    {
        Span<Rectangle> strips = stackalloc Rectangle[BorderGeometry.MaximumStrips];
        var count = BorderGeometry.Strips(new Rectangle(0, 0, 74, 110), 2, strips);

        Assert.Equal(4, count);
        Assert.Equal(new Rectangle(0, 0, 74, 2), strips[0]);
        Assert.Equal(new Rectangle(0, 2, 2, 106), strips[1]);
        Assert.Equal(new Rectangle(72, 2, 2, 106), strips[2]);
        Assert.Equal(new Rectangle(0, 108, 74, 2), strips[3]);
    }

    [Fact]
    public void NegativeThicknessIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var strips = new Rectangle[BorderGeometry.MaximumStrips];
            BorderGeometry.Strips(new Rectangle(0, 0, 10, 10), -1, strips);
        });
    }

    /// <summary>Every pixel of the rectangle within <paramref name="thickness"/> of an edge.</summary>
    private static HashSet<Point> ExpectedBorder(Rectangle rectangle, int thickness)
    {
        var border = new HashSet<Point>();
        if (thickness == 0) return border;
        for (var y = rectangle.Y; y < rectangle.Bottom; y++)
        for (var x = rectangle.X; x < rectangle.Right; x++)
            if (x - rectangle.X < thickness || rectangle.Right - 1 - x < thickness
                || y - rectangle.Y < thickness || rectangle.Bottom - 1 - y < thickness)
                border.Add(new Point(x, y));
        return border;
    }
}
