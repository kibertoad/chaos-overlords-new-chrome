using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EquipmentCommandListLayoutTests
{
    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 6)]
    [InlineData(12, 11)]
    public void CategoriesThatFitAlwaysKeepTheirFirstItemVisible(int itemCount, int selected)
    {
        Assert.Equal(0, EquipmentCommandLayout.FirstVisibleItem(itemCount, selected));
    }

    [Theory]
    [InlineData(13, 0, 0)]
    [InlineData(13, 12, 1)]
    [InlineData(20, 10, 5)]
    [InlineData(20, 19, 8)]
    public void OversizedCategoriesScrollOnlyAsNeeded(int itemCount, int selected, int expected)
    {
        Assert.Equal(expected, EquipmentCommandLayout.FirstVisibleItem(itemCount, selected));
    }
}
