using Rechaos.Core.Assets;
using Rechaos.Extractor;
using System.Reflection;
using System.Security.Cryptography;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalDataReaderTests
{
    [Fact]
    public void BundledTablesHaveCanonicalCountsAndRepresentativeRecords()
    {
        var data = BundledOriginalData.Load();
        Assert.Equal(22, data.Sites.Count);
        Assert.Equal(90, data.Gangs.Count);
        Assert.Equal(64, data.Items.Count);
        Assert.Equal("GYM", data.Sites[0].Name);
        Assert.Equal("RIGHT HANDS", data.Gangs[0].Name);
        Assert.Equal("METAL PIPE", data.Items[0].Name);
    }

    [Fact]
    public void BundledPayloadMatchesRecordedGenerationHash()
    {
        using var stream = typeof(BundledOriginalData).Assembly.GetManifestResourceStream(
            "Rechaos.Core.GameData.original-data.json");
        Assert.NotNull(stream);
        Assert.Equal(GameplayDataProvenance.BundledJsonSha256,
            Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    [Fact]
    public void BmpRepairRestoresMissingHeaderFields()
    {
        var bytes = new byte[54];
        bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        BmpRepair.Repair(bytes, 640, 460, 16);
        Assert.Equal(640, BitConverter.ToInt32(bytes, 18));
        Assert.Equal(460, BitConverter.ToInt32(bytes, 22));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 26));
        Assert.Equal(16, BitConverter.ToInt16(bytes, 28));
    }

    [Theory]
    [InlineData("PX00100", 588854, 640, 460)]
    [InlineData("PX04000", 69174, 720, 48)]
    [InlineData("PX07000", 65590, 512, 64)]
    [InlineData("PX00140", 176022, 312, 282)]
    [InlineData("PX00150", 24694, 220, 56)]
    public void DimensionsMatchDocumentedResources(string name, long bytes, int width, int height)
    {
        Assert.True(PxDimensions.TryGet(name, bytes, out var size));
        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
    }
}
