using System.Buffers.Binary;
using System.Text;

namespace Rechaos.Core.Assets;

/// <summary>A file of <c>DATA/DATA.Z</c> as its file entry describes it (FMT-DATA-005).</summary>
public sealed record InstallShieldArchiveEntry(
    string Directory,
    string Name,
    int ExpandedSize,
    int CompressedSize,
    int Offset,
    ushort Date,
    ushort Time,
    uint Attributes)
{
    /// <summary>The directory and name joined with a backslash, as the installation lays them out.</summary>
    public string Path => Directory.Length == 0 ? Name : Directory + "\\" + Name;
}

/// <summary>
/// The InstallShield 3 archive <c>DATA/DATA.Z</c> (FMT-DATA-005): a 255-byte header, the
/// compressed files back to back, then the directory entries and the file entries. The game never
/// reads it (FND-DATA-008); the rebuild reads it to list and expand the installer's files.
/// </summary>
public sealed class InstallShieldArchive
{
    public const uint Signature = 0x8C655D13;
    private const int HeaderSize = 0xFF;

    private readonly byte[] _bytes;

    private InstallShieldArchive(byte[] bytes, ushort date, ushort time, uint expandedTotal,
        IReadOnlyList<string> directories, IReadOnlyList<InstallShieldArchiveEntry> entries)
    {
        _bytes = bytes;
        Date = date;
        Time = time;
        ExpandedTotal = expandedTotal;
        Directories = directories;
        Entries = entries;
    }

    public ushort Date { get; }
    public ushort Time { get; }

    /// <summary>The header's sum of every file's expanded size (FND-DATA-010).</summary>
    public uint ExpandedTotal { get; }

    public IReadOnlyList<string> Directories { get; }
    public IReadOnlyList<InstallShieldArchiveEntry> Entries { get; }

    public static InstallShieldArchive Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < HeaderSize) throw new InvalidDataException("DATA.Z is shorter than its header.");
        var span = bytes.AsSpan();
        if (BinaryPrimitives.ReadUInt32LittleEndian(span) != Signature)
            throw new InvalidDataException("DATA.Z does not start with the InstallShield 3 signature.");
        var fileCount = BinaryPrimitives.ReadUInt16LittleEndian(span[0x0C..]);
        var date = BinaryPrimitives.ReadUInt16LittleEndian(span[0x0E..]);
        var time = BinaryPrimitives.ReadUInt16LittleEndian(span[0x10..]);
        var archiveSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x12..]);
        var expandedTotal = BinaryPrimitives.ReadUInt32LittleEndian(span[0x16..]);
        var directoryOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[0x29..]);
        var directoryCount = BinaryPrimitives.ReadUInt16LittleEndian(span[0x31..]);
        var fileOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[0x33..]);
        if (archiveSize != bytes.Length) throw new InvalidDataException("DATA.Z's recorded size is not its length.");

        var directories = new string[directoryCount];
        var position = directoryOffset;
        for (var index = 0; index < directoryCount; index++)
        {
            var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(span[(position + 2)..]);
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(span[(position + 4)..]);
            directories[index] = Encoding.Latin1.GetString(span.Slice(position + 6, nameLength));
            position += entrySize;
        }

        var entries = new InstallShieldArchiveEntry[fileCount];
        position = fileOffset;
        for (var index = 0; index < fileCount; index++)
        {
            var entry = span[position..];
            var directoryIndex = BinaryPrimitives.ReadUInt16LittleEndian(entry[0x01..]);
            var nameLength = entry[0x1D];
            var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(entry[0x0B..]);
            var compressedSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(entry[0x07..]);
            if (directoryIndex >= directoryCount || offset < HeaderSize || offset + compressedSize > directoryOffset)
                throw new InvalidDataException($"DATA.Z file entry {index} points outside the archive.");
            entries[index] = new InstallShieldArchiveEntry(
                directories[directoryIndex],
                Encoding.Latin1.GetString(entry.Slice(0x1E, nameLength)),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(entry[0x03..]),
                compressedSize,
                offset,
                BinaryPrimitives.ReadUInt16LittleEndian(entry[0x0F..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[0x11..]),
                BinaryPrimitives.ReadUInt32LittleEndian(entry[0x13..]));
            position += BinaryPrimitives.ReadUInt16LittleEndian(entry[0x17..]);
        }
        return new InstallShieldArchive(bytes, date, time, expandedTotal, directories, entries);
    }

    /// <summary>
    /// Expands a file's block. The stream has to end on the block's last byte and give the entry's
    /// expanded size, as every block of the shipped archive does (FND-DATA-010).
    /// </summary>
    public byte[] Expand(InstallShieldArchiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (output, consumed) = PkwareExplode.Expand(_bytes.AsSpan(entry.Offset, entry.CompressedSize));
        if (consumed != entry.CompressedSize || output.Length != entry.ExpandedSize)
            throw new InvalidDataException($"{entry.Path} does not expand to its recorded size.");
        return output;
    }
}
