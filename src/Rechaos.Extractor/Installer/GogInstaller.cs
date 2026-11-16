using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor.Installer;

/// <summary>
/// Reads the game's folders out of GOG's offline installer (SRC-INSTALLER-GOG) without running it,
/// so a player on macOS or Linux can import the assets from the file they downloaded.
/// </summary>
/// <remarks>
/// The installer is an Inno Setup 5.5.0 (Unicode) installer that carries its files in the
/// executable itself. Only that one installer is read: its SHA-256 is checked before anything
/// else, and the layouts below are the ones Inno Setup 5.5.0 writes, with no other version's
/// variants. Any other file is refused with a message that points at innoextract instead.
/// </remarks>
public static class GogInstaller
{
    /// <summary>The SHA-256 of setup_chaos_overlords_2.1.0.17.exe, which SRC-INSTALLER-GOG records.</summary>
    public const string Sha256 = "73ac35b075ae16c8a8add4290e6b8beb516cf25468242201e129c57170a31e7b";

    public const string FileName = GogInstallerFile.Name;

    /// <summary>The installation folders the import reads, as the installer names them under <c>{app}</c>.</summary>
    public static readonly IReadOnlyList<string> GameFolders = ["DATA", "HELP", "MUSIC"];

    private const long InstallerLength = 85_307_496;
    private const string SetupDataVersion = "Inno Setup Setup Data (5.5.0) (u)";
    private const int SetupHeaderMaximumLength = 16 << 20;
    private const int FileMaximumLength = 64 << 20;
    private static readonly byte[] OffsetTableMagic = [.. "rDlPtS"u8, 0xCD, 0xE6, 0xD7, 0x7B, 0x0B, 0x2A];
    private static readonly byte[] ChunkMagic = [.. "zlb"u8, 0x1A];

    /// <summary>
    /// Writes every file the installer puts under <c>{app}\DATA</c>, <c>{app}\HELP</c> and
    /// <c>{app}\MUSIC</c> to the same relative path under <paramref name="destination"/>, after
    /// checking each against the SHA-1 the installer stores for it. Returns how many it wrote.
    /// </summary>
    public static async Task<int> UnpackGameFoldersAsync(string installerPath, string destination)
    {
        var installer = await ReadVerifiedAsync(installerPath);
        var setup = ReadSetup(installer);
        destination = Path.GetFullPath(destination);
        var written = 0;
        byte[]? chunk = null;
        long chunkOffset = -1;
        foreach (var file in setup.Files)
        {
            if (GameFolderPath(file.Destination) is not { } relative) continue;
            if (file.Location >= setup.DataEntries.Count)
                throw new InvalidDataException($"The installer has no data for {file.Destination}.");
            var data = setup.DataEntries[(int)file.Location];
            // Checked for every file, since files that share a chunk reuse the one already read.
            CheckStorage(data);
            if (data.ChunkOffset != chunkOffset)
            {
                chunk = ReadChunk(installer, setup, data);
                chunkOffset = data.ChunkOffset;
            }
            if (data.FileOffset + data.FileSize > (ulong)chunk!.Length)
                throw new InvalidDataException($"{file.Destination} runs past the end of its chunk in the installer.");
            var bytes = chunk.AsSpan((int)data.FileOffset, (int)data.FileSize);
            if (!SHA1.HashData(bytes).AsSpan().SequenceEqual(data.Sha1))
                throw new InvalidDataException($"{file.Destination} in the installer does not match its checksum.");
            var target = Path.GetFullPath(Path.Combine(destination, relative));
            if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException($"{file.Destination} would be written outside {destination}.");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, chunk.AsMemory((int)data.FileOffset, (int)data.FileSize));
            }
            // The installer was read whole before this loop, so an I/O failure here is the
            // destination's: a full disk, a lost permission, a volume that went away.
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                throw new UnpackNotWritableException(destination, exception);
            }
            written++;
        }
        if (written == 0) throw new InvalidDataException("The installer holds none of the game's folders.");
        return written;
    }

    /// <summary>
    /// The path under the installation of a file the import reads, with the platform's
    /// separators, or null for a file outside <see cref="GameFolders"/>.
    /// </summary>
    public static string? GameFolderPath(string destination)
    {
        var parts = destination.Split('\\');
        if (parts.Length < 3 || !parts[0].Equals("{app}", StringComparison.OrdinalIgnoreCase)) return null;
        if (!GameFolders.Contains(parts[1], StringComparer.OrdinalIgnoreCase)) return null;
        if (parts.Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"The installer names an unusable path, {destination}.");
        return Path.Combine(parts[1..]);
    }

    private static async Task<byte[]> ReadVerifiedAsync(string installerPath)
    {
        var length = new FileInfo(installerPath).Length;
        if (length != InstallerLength) throw NotTheInstaller(installerPath, $"it is {length:N0} bytes long");
        var installer = await File.ReadAllBytesAsync(installerPath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(installer));
        if (hash != Sha256) throw NotTheInstaller(installerPath, $"its SHA-256 is {hash}");
        return installer;
    }

    private static InvalidDataException NotTheInstaller(string path, string reason) =>
        new($"{Path.GetFileName(path)} is not GOG's installer {FileName} ({reason}). Unpack another " +
            "version with innoextract, or install it, and choose the folder that holds DATA instead.");

    private static Setup ReadSetup(byte[] installer)
    {
        var offsets = ReadOffsetTable(installer);
        var version = Encoding.ASCII.GetString(installer, (int)offsets.Header, 64).TrimEnd('\0');
        if (version != SetupDataVersion)
            throw new InvalidDataException($"The installer's setup data is '{version}', not '{SetupDataVersion}'.");
        var headerBlock = ReadBlock(installer, offsets.Header + 64, out var dataBlockStart);
        var header = new SetupReader(LzmaDecoder.DecodeLzma(headerBlock, SetupHeaderMaximumLength));
        var dataBlock = ReadBlock(installer, dataBlockStart, out _);
        var dataEntries = new SetupReader(LzmaDecoder.DecodeLzma(dataBlock, SetupHeaderMaximumLength));
        return ParseSetup(header, dataEntries, offsets.Data);
    }

    private readonly record struct Offsets(long Header, long Data);

    /// <summary>
    /// The setup loader's table of where the setup data and the files start, which follows the
    /// signature <c>rDlPtS</c> and its six check bytes.
    /// </summary>
    private static Offsets ReadOffsetTable(byte[] installer)
    {
        var start = installer.AsSpan().IndexOf(OffsetTableMagic);
        if (start < 0) throw new InvalidDataException("The installer has no setup loader table.");
        // Revision, total size, setup program offset, its size and CRC-32, then the two offsets
        // the reader needs, and a CRC-32 of everything before it.
        var table = installer.AsSpan(start, OffsetTableMagic.Length + 8 * 4);
        var fields = table[OffsetTableMagic.Length..];
        var revision = BinaryPrimitives.ReadUInt32LittleEndian(fields);
        var stored = BinaryPrimitives.ReadUInt32LittleEndian(fields[28..]);
        if (revision != 1 || Crc32.HashToUInt32(table[..^4]) != stored)
            throw new InvalidDataException("The installer's setup loader table is damaged.");
        var header = BinaryPrimitives.ReadUInt32LittleEndian(fields[20..]);
        var data = BinaryPrimitives.ReadUInt32LittleEndian(fields[24..]);
        if (header + 64 > installer.Length || data >= installer.Length)
            throw new InvalidDataException("The installer's setup loader table points past its end.");
        return new Offsets(header, data);
    }

    /// <summary>
    /// One compressed block of setup data: a CRC-32, the stored size and a compressed flag, then
    /// the stored bytes in 4,096-byte pieces, each after the CRC-32 of its bytes.
    /// </summary>
    private static byte[] ReadBlock(byte[] installer, long start, out long end)
    {
        if (start + 9 > installer.Length) throw new InvalidDataException("A setup data block starts past the installer's end.");
        var head = installer.AsSpan((int)start, 9);
        if (Crc32.HashToUInt32(head[4..]) != BinaryPrimitives.ReadUInt32LittleEndian(head))
            throw new InvalidDataException("A setup data block header is damaged.");
        var storedSize = BinaryPrimitives.ReadUInt32LittleEndian(head[4..]);
        if (head[8] != 1) throw new InvalidDataException("A setup data block is not compressed.");
        var position = start + 9;
        end = position + storedSize;
        if (end > installer.Length) throw new InvalidDataException("A setup data block runs past the installer's end.");
        var output = new MemoryStream((int)storedSize);
        while (position < end)
        {
            if (position + 4 >= end) throw new InvalidDataException("A setup data block ends inside a piece.");
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(installer.AsSpan((int)position, 4));
            position += 4;
            var piece = installer.AsSpan((int)position, (int)Math.Min(4096, end - position));
            if (Crc32.HashToUInt32(piece) != crc) throw new InvalidDataException("A setup data block is damaged.");
            output.Write(piece);
            position += piece.Length;
        }
        return output.ToArray();
    }

    private sealed record Setup(IReadOnlyList<FileEntry> Files, IReadOnlyList<DataEntry> DataEntries, long DataStart);

    private sealed record FileEntry(string Destination, uint Location);

    private sealed record DataEntry(
        uint FirstSlice, uint LastSlice, long ChunkOffset, ulong FileOffset, ulong FileSize, ulong ChunkSize,
        byte[] Sha1, ushort Flags);

    // Data entry flags, in the order Inno Setup 5.5.0 stores them.
    private const ushort CallInstructionOptimized = 1 << 4;
    private const ushort ChunkEncrypted = 1 << 6;
    private const ushort ChunkCompressed = 1 << 7;

    // Header compression values: stored, zlib, bzip2, LZMA, LZMA2.
    private const byte Lzma2Compression = 4;

    /// <summary>
    /// Walks Inno Setup 5.5.0's setup header to the file entries, skipping what comes before them,
    /// and reads the data entries from the second block.
    /// </summary>
    private static Setup ParseSetup(SetupReader header, SetupReader dataEntries, long dataStart)
    {
        header.SkipStrings(31);
        var languages = header.U32();
        var messages = header.U32();
        var permissions = header.U32();
        var types = header.U32();
        var components = header.U32();
        var tasks = header.U32();
        var directories = header.U32();
        var files = header.U32();
        var dataEntryCount = header.U32();
        header.Skip(7 * 4); // Icons, INI, registry, delete, uninstall delete, run and uninstall run.
        header.Skip(20); // The Windows version range.
        header.Skip(3 * 4); // Background colours and the wizard image's.
        header.Skip(20 + 8); // Password SHA-1 and salt.
        header.Skip(8 + 4); // Extra disk space, slices per disk.
        header.Skip(5); // Uninstall log mode, directory warning, privileges, language dialog and detection.
        var compression = header.U8();
        header.Skip(2 + 2 + 8 + 6); // Architectures, page options, uninstall size, 46 option flags.

        for (var i = 0; i < languages; i++)
        {
            header.SkipStrings(10);
            header.Skip(4 + 4 * 4 + 1); // Language ID, four font sizes, right-to-left.
        }
        for (var i = 0; i < messages; i++)
        {
            header.SkipStrings(2);
            header.Skip(4);
        }
        for (var i = 0; i < permissions; i++) header.SkipStrings(1);
        for (var i = 0; i < types; i++)
        {
            header.SkipStrings(4);
            header.Skip(20 + 1 + 1 + 8); // Versions, flags, type, size.
        }
        for (var i = 0; i < components; i++)
        {
            header.SkipStrings(5);
            header.Skip(8 + 4 + 1 + 20 + 1 + 8); // Disk space, level, used, versions, flags, size.
        }
        for (var i = 0; i < tasks; i++)
        {
            header.SkipStrings(6);
            header.Skip(4 + 1 + 20 + 1); // Level, used, versions, flags.
        }
        for (var i = 0; i < directories; i++)
        {
            header.SkipStrings(7);
            header.Skip(4 + 20 + 2 + 1); // Attributes, versions, permission, flags.
        }
        var fileEntries = new List<FileEntry>((int)files);
        for (var i = 0; i < files; i++)
        {
            header.SkipStrings(1); // Source.
            var destination = header.UnicodeString();
            header.SkipStrings(8); // Font, assembly and the six condition strings.
            header.Skip(20); // Versions.
            var location = header.U32();
            header.Skip(4 + 8 + 2 + 4 + 1); // Attributes, external size, permission, 32 flags, type.
            fileEntries.Add(new FileEntry(destination, location));
        }

        const int dataEntryLength = 4 + 4 + 4 + 8 + 8 + 8 + 20 + 8 + 4 + 4 + 2;
        if (dataEntries.Length != dataEntryCount * dataEntryLength)
            throw new InvalidDataException("The installer's data entries are not the length Inno Setup 5.5.0 writes.");
        var data = new List<DataEntry>((int)dataEntryCount);
        for (var i = 0; i < dataEntryCount; i++)
        {
            var firstSlice = dataEntries.U32();
            var lastSlice = dataEntries.U32();
            var chunkOffset = dataEntries.U32();
            var fileOffset = dataEntries.U64();
            var fileSize = dataEntries.U64();
            var chunkSize = dataEntries.U64();
            var sha1 = dataEntries.Bytes(20);
            dataEntries.Skip(8 + 4 + 4); // File time, file version.
            var flags = dataEntries.U16();
            data.Add(new DataEntry(firstSlice, lastSlice, chunkOffset, fileOffset, fileSize, chunkSize, sha1, flags));
        }
        if (data.Any(entry => (entry.Flags & ChunkCompressed) != 0) && compression != Lzma2Compression)
            throw new InvalidDataException($"The installer's files use compression {compression}, not LZMA2.");
        return new Setup(fileEntries, data, dataStart);
    }

    /// <summary>Refuses a file kept in another slice, in an encrypted chunk or with the executable filter.</summary>
    private static void CheckStorage(DataEntry data)
    {
        if (data.FirstSlice != 0 || data.LastSlice != 0)
            throw new InvalidDataException("The installer keeps a file outside its executable.");
        if ((data.Flags & ChunkEncrypted) != 0)
            throw new InvalidDataException("The installer's files are encrypted.");
        if ((data.Flags & CallInstructionOptimized) != 0)
            throw new InvalidDataException("A game file in the installer is stored with the executable filter.");
    }

    /// <summary>A chunk of file data: the signature <c>zlb</c> and 0x1A, then the stored or LZMA2 bytes.</summary>
    private static byte[] ReadChunk(byte[] installer, Setup setup, DataEntry data)
    {
        var start = setup.DataStart + data.ChunkOffset;
        if (start + ChunkMagic.Length + (long)data.ChunkSize > installer.Length)
            throw new InvalidDataException("A chunk of the installer runs past its end.");
        if (!installer.AsSpan((int)start, ChunkMagic.Length).SequenceEqual(ChunkMagic))
            throw new InvalidDataException("A chunk of the installer has no chunk signature.");
        var stored = installer.AsSpan((int)start + ChunkMagic.Length, (int)data.ChunkSize);
        return (data.Flags & ChunkCompressed) != 0
            ? LzmaDecoder.DecodeLzma2(stored, FileMaximumLength)
            : stored.ToArray();
    }

    /// <summary>Little-endian fields and length-prefixed strings of decompressed setup data.</summary>
    private sealed class SetupReader(byte[] bytes)
    {
        private int _position;

        public int Length => bytes.Length;

        public byte U8() => Take(1)[0];

        public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

        public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

        public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

        public byte[] Bytes(int count) => Take(count).ToArray();

        public void Skip(int count) => Take(count);

        public void SkipStrings(int count)
        {
            for (var i = 0; i < count; i++) Take(checked((int)U32()));
        }

        public string UnicodeString() => Encoding.Unicode.GetString(Take(checked((int)U32())));

        private ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || _position + count > bytes.Length)
                throw new InvalidDataException("The installer's setup data ends early.");
            var span = bytes.AsSpan(_position, count);
            _position += count;
            return span;
        }
    }
}

/// <summary>The unpacked files could not be written to the folder the installer is unpacked into.</summary>
public sealed class UnpackNotWritableException(string destination, Exception inner)
    : IOException($"Cannot write the unpacked installer to {destination}: {inner.Message}", inner);
