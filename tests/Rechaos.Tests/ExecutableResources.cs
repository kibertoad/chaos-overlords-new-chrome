using System.Buffers.Binary;

namespace Rechaos.Tests;

/// <summary>
/// The resources of BLD-GOG-EN-1.1's executable, read from the file's bytes as a 32-bit PE file.
/// The file is never loaded or run, so this works on every platform. Every offset is checked
/// against the file.
/// </summary>
internal static class ExecutableResources
{
    public const string Executable = "Chaos Overlords.exe";
    public const string ExecutableXxh3 = "a82da6843188901e1d4fce1c76a925ef";

    /// <summary>The executable's bytes, checked against the build entry. Skips without them.</summary>
    public static byte[] RequireExecutable() =>
        File.ReadAllBytes(OriginalGameFiles.Require(OriginalFormatFiles.Build, Executable, ExecutableXxh3));

    /// <summary>
    /// Every resource of <paramref name="type"/>, as its ID and the data of one language, in
    /// directory order. An ID with several languages appears once per language.
    /// </summary>
    public static IReadOnlyList<(int Id, byte[] Data)> Read(byte[] file, int type)
    {
        var header = Int32(file, 0x3C);
        if (!Span(file, 0, 2).SequenceEqual("MZ"u8) || !Span(file, header, 4).SequenceEqual("PE\0\0"u8))
            throw new InvalidDataException("Not a PE file.");
        var sections = UInt16(file, header + 6);
        var optional = header + 24;
        var optionalSize = UInt16(file, header + 20);
        if (UInt16(file, optional) != 0x10B) throw new InvalidDataException("Not a 32-bit PE file.");
        var resourceRva = Int32(file, optional + 96 + 2 * 8);
        var sectionTable = optional + optionalSize;
        var root = Offset(file, sectionTable, sections, resourceRva);

        var resources = new List<(int, byte[])>();
        foreach (var (typeId, typeDirectory) in Entries(file, root, root))
        {
            if (typeId != type) continue;
            foreach (var (id, languages) in Entries(file, root, typeDirectory))
            {
                foreach (var (_, dataEntry) in Entries(file, root, languages))
                {
                    var data = Offset(file, sectionTable, sections, Int32(file, dataEntry));
                    resources.Add((id, Span(file, data, Int32(file, dataEntry + 4)).ToArray()));
                }
            }
        }
        return resources;
    }

    private static IEnumerable<(int Id, int Target)> Entries(byte[] file, int root, int directory)
    {
        var count = UInt16(file, directory + 12) + UInt16(file, directory + 14);
        for (var index = 0; index < count; index++)
        {
            var entry = directory + 16 + index * 8;
            var id = Int32(file, entry);
            var target = Int32(file, entry + 4) & 0x7FFFFFFF;
            yield return (id, root + target);
        }
    }

    private static int Offset(byte[] file, int sectionTable, int sections, int rva)
    {
        for (var index = 0; index < sections; index++)
        {
            var section = sectionTable + index * 40;
            var address = Int32(file, section + 12);
            var rawSize = Int32(file, section + 16);
            if (rva >= address && rva < address + rawSize)
                return Int32(file, section + 20) + rva - address;
        }
        throw new InvalidDataException($"RVA 0x{rva:X} is in no section.");
    }

    private static int UInt16(byte[] file, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(Span(file, offset, 2));

    private static int Int32(byte[] file, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(Span(file, offset, 4));

    private static ReadOnlySpan<byte> Span(byte[] file, int offset, int length) =>
        offset >= 0 && length >= 0 && offset <= file.Length - length
            ? file.AsSpan(offset, length)
            : throw new InvalidDataException($"{length} bytes at 0x{offset:X} lie outside the file.");
}
