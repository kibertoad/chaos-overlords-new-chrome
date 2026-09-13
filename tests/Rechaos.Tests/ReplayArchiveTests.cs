using System.Buffers.Binary;
using System.Text;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The compressed container a journal travels in, beside a save and inside a bug report.
/// </summary>
public sealed class ReplayArchiveTests
{
    [Fact]
    public void RoundTripsAJournalAndReplaysToTheSameState()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        recorder.FinishUpkeep();
        foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);

        var archive = ReplayArchive.Pack(recorder);
        var replayed = ReplayArchive.LoadAndReplay(archive, recorder.State.Definitions);

        Assert.Equal(
            MatchStateHasher.ComputeSha256(recorder.State),
            MatchStateHasher.ComputeSha256(replayed));
    }

    /// <summary>
    /// The point of the container: a journal is JSON, and JSON of this shape compresses hard.
    /// </summary>
    [Fact]
    public void CompressesAJournalToAFractionOfItsJson()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        for (var turn = 0; turn < 6; turn++)
        {
            recorder.FinishUpkeep();
            foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);
            while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
                recorder.FinishExecutionPhase();
            foreach (var player in recorder.State.Players) recorder.FinishHire(player.Id);
            recorder.FinishPlayerElimination();
        }
        using var json = new MemoryStream();
        MatchReplaySerializer.Save(json, recorder);

        var archive = ReplayArchive.Pack(recorder, ReplayArchiveEffort.Smallest);
        var header = ReplayArchive.ReadHeader(archive);

        Assert.Equal(ReplayArchiveCodec.Brotli, header.Codec);
        Assert.Equal(json.Length, header.UncompressedBytes);
        Assert.True(
            archive.Length * 2 < json.Length,
            $"expected the archive to more than halve {json.Length} bytes, got {archive.Length}");
    }

    /// <summary>A payload too small for framing to pay for itself is stored as it is.</summary>
    [Fact]
    public void StoresATinyPayloadUncompressed()
    {
        var payload = Encoding.UTF8.GetBytes("{}");

        var archive = ReplayArchive.Pack(payload);

        Assert.Equal(ReplayArchiveCodec.None, ReplayArchive.ReadHeader(archive).Codec);
        Assert.Equal(payload, ReplayArchive.Unpack(archive));
    }

    [Fact]
    public void RefusesSomethingThatIsNotAnArchive()
    {
        Assert.Throws<InvalidDataException>(
            () => ReplayArchive.ReadHeader(Encoding.UTF8.GetBytes("not an archive at all!!")));
        Assert.Throws<InvalidDataException>(() => ReplayArchive.ReadHeader(new byte[4]));
    }

    /// <summary>
    /// A codec byte this build does not write is refused rather than handed to a decompressor.
    /// </summary>
    [Fact]
    public void RefusesACodecThisBuildCannotRead()
    {
        var archive = ReplayArchive.Pack(Encoding.UTF8.GetBytes(new string('x', 4_096)));
        archive[8] = (byte)ReplayArchiveCodec.Zstd;

        Assert.Throws<InvalidDataException>(() => ReplayArchive.Unpack(archive));
    }

    /// <summary>
    /// A small archive claiming a small payload cannot expand past it.
    /// </summary>
    /// <remarks>
    /// The declared length is what the reader allocates and what it stops at, so an archive that
    /// under-declares is caught with one byte read rather than with a gigabyte written.
    /// </remarks>
    [Fact]
    public void RefusesAnArchiveThatExpandsBeyondItsDeclaredSize()
    {
        var archive = ReplayArchive.Pack(Encoding.UTF8.GetBytes(new string('x', 64 * 1024)));
        BinaryPrimitives.WriteInt32LittleEndian(archive.AsSpan(12, 4), 1_024);

        Assert.Throws<InvalidDataException>(() => ReplayArchive.Unpack(archive));
    }

    /// <summary>
    /// Reading from a stream decides on the header, not on a file already in memory.
    /// </summary>
    /// <remarks>
    /// The span overload can only be handed an archive somebody has loaded whole, which is the one
    /// thing a guard cannot undo: a reader that allocates first has already paid whatever the file
    /// asked for. This one reads sixteen bytes and decides on those.
    /// </remarks>
    [Fact]
    public void UnpacksFromAStreamWithoutReadingItWhole()
    {
        var payload = Encoding.UTF8.GetBytes(new string('x', 64 * 1024));
        using var archive = new MemoryStream(ReplayArchive.Pack(payload), writable: false);

        Assert.Equal(payload, ReplayArchive.Unpack(archive));
    }

    /// <summary>A stored payload round-trips through the stream reader too.</summary>
    [Fact]
    public void UnpacksAnUncompressedPayloadFromAStream()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        using var archive = new MemoryStream(ReplayArchive.Pack(payload), writable: false);

        Assert.Equal(payload, ReplayArchive.Unpack(archive));
    }

    /// <summary>
    /// A stream longer than any archive could be is refused before a byte of it is read.
    /// </summary>
    /// <remarks>
    /// Nothing bounds the <em>compressed</em> bytes: the header's declared length only says what
    /// comes out. So the container states its own ceiling, and a seekable source is measured against
    /// it first — which is what keeps a hostile companion file a refusal rather than an allocation.
    /// </remarks>
    [Fact]
    public void RefusesAStreamLargerThanAnArchiveCanBe()
    {
        using var oversized = new OverstatedLengthStream(ReplayArchive.MaximumArchiveBytes + 1L);

        var refusal = Assert.Throws<InvalidDataException>(() => ReplayArchive.Unpack(oversized));

        Assert.Equal(0, oversized.BytesRead);
        Assert.Contains("larger than", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefusesAStreamThatEndsMidPayload()
    {
        var archive = ReplayArchive.Pack(Encoding.UTF8.GetBytes(new string('x', 64 * 1024)));
        using var truncated = new MemoryStream(archive[..(archive.Length / 2)], writable: false);

        Assert.Throws<InvalidDataException>(() => ReplayArchive.Unpack(truncated));
    }

    [Fact]
    public void RefusesAStreamThatEndsMidHeader()
    {
        using var stump = new MemoryStream(new byte[ReplayArchive.HeaderBytes - 1], writable: false);

        Assert.Throws<InvalidDataException>(() => ReplayArchive.Unpack(stump));
    }

    /// <summary>An empty, seekable stand-in that claims a length no archive could have.</summary>
    private sealed class OverstatedLengthStream(long length) : Stream
    {
        public int BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            BytesRead += count;
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush()
        {
        }
    }

    [Fact]
    public void FingerprintIsTheLowercaseHexDigestOfTheArchive()
    {
        var archive = ReplayArchive.Pack(Encoding.UTF8.GetBytes("{}"));

        var fingerprint = ReplayArchive.Fingerprint(archive);

        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        Assert.Equal(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(archive)),
            fingerprint);
    }
}
