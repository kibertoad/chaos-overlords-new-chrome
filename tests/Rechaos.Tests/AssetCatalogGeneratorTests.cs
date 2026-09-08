using Rechaos.Core.Assets;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class AssetCatalogGeneratorTests
{
    [Fact]
    public void CatalogContainsEveryManifestEntryInStableOrder()
    {
        var manifest = new AssetManifest(
            AssetManifest.CurrentFormatVersion,
            "test",
            new string('a', 64),
            DateTimeOffset.UnixEpoch,
            [
                new ExtractedAsset("raw/px08/PX00002", 20, new string('2', 64),
                    "DATA/PX08/PX00002", "application/x-chaos-px08", new AssetConversion("copy")),
                new ExtractedAsset("images/PX00001.bmp", 10, new string('1', 64),
                    "DATA/PX16/PX00001", "image/bmp", new AssetConversion("bmp-header-repair", 2, 2, 16, "RGB555"))
            ],
            "1.2.3");

        var catalog = AssetCatalogGenerator.Generate(manifest);

        Assert.Contains("Asset count: 2", catalog);
        Assert.Contains("2x2x16 (RGB555)", catalog);
        Assert.Contains("Palette and transparency unresolved", catalog);
        Assert.True(catalog.IndexOf("images/PX00001.bmp", StringComparison.Ordinal) <
                    catalog.IndexOf("raw/px08/PX00002", StringComparison.Ordinal));
        Assert.Equal(2, catalog.Split('\n').Count(line => line.StartsWith("| `", StringComparison.Ordinal)));
    }
}
