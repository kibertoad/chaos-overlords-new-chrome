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
                new ExtractedAsset("images/PX00150.bmp", 10, new string('4', 64),
                    "DATA/PX16/PX00150", "image/bmp", new AssetConversion("bmp-header-repair", 2, 2, 16, "RGB555")),
                new ExtractedAsset("images/PX00129.bmp", 10, new string('6', 64),
                    "DATA/PX16/PX00129", "image/bmp", new AssetConversion("bmp-header-repair", 2, 2, 16, "RGB555")),
                new ExtractedAsset("images/PX00300.bmp", 10, new string('5', 64),
                    "DATA/PX16/PX00300", "image/bmp", new AssetConversion("bmp-header-repair", 2, 2, 16, "RGB555")),
                new ExtractedAsset("video/MVINTRO.smk", 30, new string('3', 64),
                    "DATA/MVINTRO", "video/x-smacker", new AssetConversion("copy")),
                new ExtractedAsset("images/PX00001.bmp", 10, new string('1', 64),
                    "DATA/PX16/PX00001", "image/bmp", new AssetConversion("bmp-header-repair", 2, 2, 16, "RGB555"))
            ],
            "1.2.3");

        var catalog = AssetCatalogGenerator.Generate(manifest);

        Assert.Contains("Asset count: 6", catalog);
        Assert.Contains("2x2x16 (RGB555)", catalog);
        Assert.Contains("Palette and transparency unresolved", catalog);
        Assert.Contains("RGB555; native exact maximum-white key", catalog);
        Assert.Contains("RGB555; native mixed opaque, pattern-mask, and exact-white copies", catalog);
        Assert.Contains("RGB555; native opaque copy (black retained)", catalog);
        Assert.Contains("1,150 frames; 100 ms/frame; packed PCM 22050 Hz 8-bit stereo", catalog);
        Assert.True(catalog.IndexOf("images/PX00001.bmp", StringComparison.Ordinal) <
                    catalog.IndexOf("raw/px08/PX00002", StringComparison.Ordinal));
        Assert.DoesNotContain("\r", catalog);
        Assert.Equal(6, catalog.Split('\n').Count(line => line.StartsWith("| `", StringComparison.Ordinal)));
    }
}
