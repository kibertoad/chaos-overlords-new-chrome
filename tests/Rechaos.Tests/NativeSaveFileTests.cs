using System.Buffers.Binary;
using System.Text;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>The compressed file a native save is stored in.</summary>
public sealed class NativeSaveFileTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("rechaos-save-file-");

    public void Dispose() => _directory.Delete(recursive: true);

    private string SavePath => Path.Combine(_directory.FullName, "match.rchsave");

    [Fact]
    public void ASaveIsWrittenCompressedAndLoadsToTheSameState()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        var json = NativeSaveStore.Serialize(match);

        NativeSaveStore.SaveAtomic(SavePath, match);

        var file = File.ReadAllBytes(SavePath);
        Assert.Equal("RCHN"u8.ToArray(), file[..4]);
        Assert.Equal(json.Length, BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8, 4)));
        Assert.True(file.Length * 4 < json.Length, $"{file.Length} bytes stored for {json.Length} of JSON.");
        Assert.Equal(json, Encoding.UTF8.GetBytes(NativeSaveFileText.Read(SavePath)));
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(match),
            MatchStateHasher.ComputeFingerprint(NativeSaveStore.Load(SavePath, match.Definitions)));
    }

    [Fact]
    public void ACapturedSnapshotIsWrittenCompressedToo()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        var json = NativeSaveStore.Serialize(match);

        NativeSaveStore.SaveAtomic(SavePath, json, match.Definitions, trustExistingPrimary: false);

        Assert.Equal("RCHN"u8.ToArray(), File.ReadAllBytes(SavePath)[..4]);
        Assert.Equal(json, Encoding.UTF8.GetBytes(NativeSaveFileText.Read(SavePath)));
    }

    /// <summary>Earlier builds wrote the JSON bare, and a save they wrote still loads.</summary>
    [Fact]
    public void ABareJsonSaveFromAnEarlierBuildStillLoads()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        File.WriteAllBytes(SavePath, NativeSaveStore.Serialize(match));

        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(match),
            MatchStateHasher.ComputeFingerprint(NativeSaveStore.Load(SavePath, match.Definitions)));
    }

    [Fact]
    public void AFileDeclaringMoreThanTheLimitIsRefusedBeforeDecompressing()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        NativeSaveStore.SaveAtomic(SavePath, match);
        var file = File.ReadAllBytes(SavePath);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(8, 4), NativeSaveSerializer.MaximumSaveBytes + 1);
        File.WriteAllBytes(SavePath, file);

        var error = Assert.Throws<InvalidDataException>(() => NativeSaveStore.Load(SavePath, match.Definitions));
        Assert.Contains("unusable size", error.Message);
    }

    [Fact]
    public void ABodyThatExpandsPastItsDeclaredLengthIsRefused()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        NativeSaveStore.SaveAtomic(SavePath, match);
        var file = File.ReadAllBytes(SavePath);
        var declared = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8, 4));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(8, 4), declared - 1);
        File.WriteAllBytes(SavePath, file);

        var error = Assert.Throws<InvalidDataException>(() => NativeSaveStore.Load(SavePath, match.Definitions));
        Assert.Contains("beyond its declared size", error.Message);
    }

    [Fact]
    public void ATruncatedBodyIsDamageAndNotADiskFailure()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        NativeSaveStore.SaveAtomic(SavePath, match);
        var file = File.ReadAllBytes(SavePath);
        File.WriteAllBytes(SavePath, file[..(file.Length / 2)]);

        Assert.Throws<InvalidDataException>(() => NativeSaveStore.Load(SavePath, match.Definitions));
    }

    [Fact]
    public void ACorruptBodyIsReportedAsDamage()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        NativeSaveStore.SaveAtomic(SavePath, match);
        var file = File.ReadAllBytes(SavePath);
        Array.Fill(file, (byte)0xFF, NativeSaveStore.FileHeaderBytes, file.Length - NativeSaveStore.FileHeaderBytes);
        File.WriteAllBytes(SavePath, file);

        Assert.Throws<InvalidDataException>(() => NativeSaveStore.Load(SavePath, match.Definitions));
    }

    [Fact]
    public void AnUnknownCodecIsRefused()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        NativeSaveStore.SaveAtomic(SavePath, match);
        var file = File.ReadAllBytes(SavePath);
        file[4] = 2;
        File.WriteAllBytes(SavePath, file);

        var error = Assert.Throws<InvalidDataException>(() => NativeSaveStore.Load(SavePath, match.Definitions));
        Assert.Contains("codec 2", error.Message);
    }
}
