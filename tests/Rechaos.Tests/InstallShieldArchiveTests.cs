using System.Buffers.Binary;
using System.Text;
using Rechaos.Core.Assets;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Reads <c>DATA/DATA.Z</c> as FMT-DATA-005 lays it out and expands its blocks, first on built
/// archives and then on the shipped file against FND-DATA-009 and FND-DATA-010.
/// </summary>
public sealed class InstallShieldArchiveTests
{
    /// <summary>The test stream of zlib's contrib/blast: coded literals, a 1024-byte dictionary.</summary>
    private static readonly byte[] BlastTestStream = [0x00, 0x04, 0x82, 0x24, 0x25, 0x8F, 0x80, 0x7F];

    [Fact]
    public void TheBlastTestStreamExpands()
    {
        var (output, consumed) = PkwareExplode.Expand(BlastTestStream);

        Assert.Equal("AIAIAIAIAIAIA", Encoding.ASCII.GetString(output));
        Assert.Equal(BlastTestStream.Length, consumed);
    }

    [Fact]
    public void AStreamCutShortIsRefused() =>
        Assert.Throws<InvalidDataException>(() => PkwareExplode.Expand(BlastTestStream.AsSpan(0, 5)));

    [Fact]
    public void AStreamWithAnUnknownDictionaryIsRefused() =>
        Assert.Throws<InvalidDataException>(() => PkwareExplode.Expand(new byte[] { 0x00, 0x07, 0xFF }));

    [Fact]
    public void ABuiltArchiveListsAndExpandsItsFile()
    {
        var archive = InstallShieldArchive.Read(BuildArchive(BlastTestStream, "DATA", "TEST.TXT", 13));

        Assert.Equal(["", "DATA"], archive.Directories);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal("DATA\\TEST.TXT", entry.Path);
        Assert.Equal(0xFF, entry.Offset);
        Assert.Equal(0x20u, entry.Attributes);
        Assert.Equal("AIAIAIAIAIAIA", Encoding.ASCII.GetString(archive.Expand(entry)));
    }

    [Fact]
    public void AnEntryWhoseRecordedSizeDiffersIsRefused()
    {
        var archive = InstallShieldArchive.Read(BuildArchive(BlastTestStream, "", "TEST.TXT", 12));

        Assert.Throws<InvalidDataException>(() => archive.Expand(archive.Entries[0]));
    }

    [Fact]
    public void AFileWithoutTheSignatureIsRefused()
    {
        var bytes = BuildArchive(BlastTestStream, "", "TEST.TXT", 13);
        bytes[0] ^= 1;

        Assert.Throws<InvalidDataException>(() => InstallShieldArchive.Read(bytes));
    }

    /// <summary>
    /// FND-DATA-009 and FND-DATA-010: 459 files in five directories, every block expanding to its
    /// recorded size on its last byte, 449 equal to the installed file, nine that differ and
    /// <c>README.DOC</c>, which is not installed.
    /// </summary>
    [Fact]
    public void TheShippedArchiveExpandsToTheInstalledFiles()
    {
        var file = Assert.Single(OriginalFormatFiles.Require("FMT-DATA-005"));
        var archive = InstallShieldArchive.Read(file.ReadAllBytes());
        var installation = Path.GetDirectoryName(Path.GetDirectoryName(file.FullPath))!;

        Assert.Equal(459, archive.Entries.Count);
        Assert.Equal(5, archive.Directories.Count);
        Assert.Equal(32_320_236u, archive.ExpandedTotal);
        Assert.Equal(archive.ExpandedTotal, (uint)archive.Entries.Sum(entry => (long)entry.ExpandedSize));
        var equal = 0;
        var differing = new List<string>();
        var missing = new List<string>();
        foreach (var entry in archive.Entries)
        {
            var expanded = archive.Expand(entry);
            var installed = FindInstalled(installation, entry.Path);
            if (installed is null) missing.Add(entry.Path);
            else if (File.ReadAllBytes(installed).AsSpan().SequenceEqual(expanded)) equal++;
            else differing.Add(entry.Path.ToUpperInvariant());
        }

        Assert.Equal(449, equal);
        Assert.Equal(["README.DOC"], missing.Select(path => path.ToUpperInvariant()));
        Assert.Equal(
            new[]
            {
                "CHAOS OVERLORDS.EXE",
                "DATA\\PX08\\PX00128", "DATA\\PX08\\PX00129", "DATA\\PX08\\PX05010", "DATA\\PX08\\PX05017",
                "DATA\\PX16\\PX00128", "DATA\\PX16\\PX00129", "DATA\\PX16\\PX05010", "DATA\\PX16\\PX05017"
            }.Order(),
            differing.Order());
    }

    private static string? FindInstalled(string root, string archivePath)
    {
        var current = root;
        foreach (var part in archivePath.Split('\\'))
        {
            var match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(candidate => string.Equals(
                    Path.GetFileName(candidate), part, StringComparison.OrdinalIgnoreCase));
            if (match is null) return null;
            current = match;
        }
        return File.Exists(current) ? current : null;
    }

    /// <summary>One file in one directory besides the root, laid out as FMT-DATA-005 gives.</summary>
    private static byte[] BuildArchive(byte[] block, string directory, string name, int expandedSize)
    {
        var directories = new List<byte>();
        foreach (var directoryName in new[] { "", directory }.Distinct())
        {
            var nameBytes = Encoding.ASCII.GetBytes(directoryName);
            var entry = new byte[6 + nameBytes.Length + 5];
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(0), (ushort)(directoryName == directory ? 1 : 0));
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(2), (ushort)entry.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(4), (ushort)nameBytes.Length);
            nameBytes.CopyTo(entry, 6);
            directories.AddRange(entry);
        }
        var directoryCount = directory.Length == 0 ? 1 : 2;
        var fileName = Encoding.ASCII.GetBytes(name);
        var fileEntry = new byte[0x1E + fileName.Length + 13];
        BinaryPrimitives.WriteUInt16LittleEndian(fileEntry.AsSpan(0x01), (ushort)(directoryCount - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(fileEntry.AsSpan(0x03), (uint)expandedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(fileEntry.AsSpan(0x07), (uint)block.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(fileEntry.AsSpan(0x0B), 0xFF);
        BinaryPrimitives.WriteUInt32LittleEndian(fileEntry.AsSpan(0x13), 0x20);
        BinaryPrimitives.WriteUInt16LittleEndian(fileEntry.AsSpan(0x17), (ushort)fileEntry.Length);
        fileEntry[0x1D] = (byte)fileName.Length;
        fileName.CopyTo(fileEntry, 0x1E);

        var directoryOffset = 0xFF + block.Length;
        var fileOffset = directoryOffset + directories.Count;
        var bytes = new byte[fileOffset + fileEntry.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), InstallShieldArchive.Signature);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x0C), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x12), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x16), (uint)expandedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x29), (uint)directoryOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x2D), (uint)directories.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x31), (ushort)directoryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x33), (uint)fileOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x37), (ushort)fileEntry.Length);
        block.CopyTo(bytes, 0xFF);
        directories.CopyTo(bytes, directoryOffset);
        fileEntry.CopyTo(bytes, fileOffset);
        return bytes;
    }
}
