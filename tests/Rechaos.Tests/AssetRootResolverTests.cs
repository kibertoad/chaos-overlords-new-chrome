using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class AssetRootResolverTests
{
    [Fact]
    public void UsesAdjacentAssetsForPortableAndWindowsInstallerLayouts()
    {
        var path = AssetRootResolver.Resolve("/application", "/user-data", true);

        Assert.Equal(Path.Combine("/application", "Assets"), path);
    }

    [Fact]
    public void UsesWritablePerUserAssetsWhenApplicationDirectoryHasNone()
    {
        var path = AssetRootResolver.Resolve("/application", "/user-data", false);

        Assert.Equal(
            Path.Combine("/user-data", AssetRootResolver.ApplicationDataDirectory, "Assets"),
            path);
    }

    [Theory]
    [InlineData("", "/user-data")]
    [InlineData("/application", "")]
    public void RejectsMissingRoots(string applicationDirectory, string localApplicationData)
    {
        Assert.Throws<ArgumentException>(() =>
            AssetRootResolver.Resolve(applicationDirectory, localApplicationData, false));
    }
}
