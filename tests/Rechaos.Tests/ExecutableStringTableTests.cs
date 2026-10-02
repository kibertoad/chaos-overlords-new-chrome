using System.Buffers.Binary;
using System.Text;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Compares every executable string the rebuild draws (<see cref="ExecutableStrings"/>) with the
/// STRING resources of BLD-GOG-EN-1.1's executable: the scenario names, Mentalities, time limits,
/// lengths and player labels of RULE-UI-009, the captions of SCR-EVENT-001 and the final-view
/// calendar companion of FND-UI-040.
/// </summary>
public sealed class ExecutableStringTableTests
{
    private const string Executable = "Chaos Overlords.exe";
    private const string ExecutableXxh3 = "a82da6843188901e1d4fce1c76a925ef";

    [Fact]
    public void DrawnStringsAreTheExecutablesForRuleUi009AndScrEvent001()
    {
        // needs: GAME_DIR
        var path = OriginalGameFiles.Require(OriginalFormatFiles.Build, Executable, ExecutableXxh3);
        var table = PeStringTable.Read(File.ReadAllBytes(path));

        var failures = ExecutableStrings.Drawn
            .Where(entry => !table.TryGetValue(entry.Key, out var text) || text != entry.Value)
            .Select(entry => $"STRING/{entry.Key}: the rebuild draws \"{entry.Value}\", the executable has "
                + (table.TryGetValue(entry.Key, out var text) ? $"\"{text}\"" : "no such string"))
            .ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void DrawnStringsCoverEveryNumberTheEntriesName()
    {
        // RULE-UI-009 reads 1 to 10 and 0x2E to 0x3C, SCR-EVENT-001 reads 33 to 44, FND-UI-040
        // reads 19.
        var expected = Enumerable.Range(1, 10)
            .Append(19)
            .Concat(Enumerable.Range(33, 12))
            .Concat(Enumerable.Range(0x2E, 0x3C - 0x2E + 1));
        Assert.Equal(expected.Order(), ExecutableStrings.Drawn.Keys.Order());
    }

    [Theory]
    [InlineData(0, 0, "NO EVENTS.")]
    [InlineData(1, 0, "POLICE CRACKDOWN.")]
    [InlineData(2, 0, "SECTOR CONTROL ATTAINED.")]
    [InlineData(3, 0, "SECTOR CONTROL LOST.")]
    [InlineData(4, 0, "SITE COOPERATION ACHIEVED.")]
    [InlineData(5, 0, "RESEARCH COMPLETED.")]
    [InlineData(6, 1, "INSUFFICIENT CASH TO BRIBE.")]
    [InlineData(6, 2, "INSUFFICIENT CASH TO EQUIP.")]
    [InlineData(6, 4, "INSUFFICIENT CASH TO HIRE.")]
    [InlineData(6, 3, null)]
    [InlineData(7, 0, "UNABLE TO HIRE, SECTOR AT CAPACITY.")]
    [InlineData(8, 0, "UNABLE TO HIRE, MAX GANGS REACHED.")]
    [InlineData(9, 0, "PLAYER HAS BEEN ELIMINATED.")]
    public void CaptionFollowsTheTableOfScrEvent001(int type, int arg1, string? expected)
    {
        Assert.Equal(expected, LastTurnEventPresentation.Caption(new LastTurnReportRecord(type, arg1, 0, 0)));
    }

    [Fact]
    public void ReaderRefusesAFileThatIsNotAPortableExecutable()
    {
        Assert.Throws<InvalidDataException>(() => PeStringTable.Read(new byte[64]));
        Assert.Throws<InvalidDataException>(() => PeStringTable.Read([]));
    }

    /// <summary>
    /// Reads the RT_STRING resources of a 32-bit PE file: blocks of 16 counted UTF-16 strings,
    /// block <c>n</c> holding strings <c>16 * (n - 1)</c> to <c>16 * n - 1</c>. Every offset is
    /// checked against the file.
    /// </summary>
    private static class PeStringTable
    {
        private const int StringResourceType = 6;

        public static IReadOnlyDictionary<int, string> Read(byte[] file)
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

            var strings = new Dictionary<int, string>();
            foreach (var (typeId, typeDirectory) in Entries(file, root, root))
            {
                if (typeId != StringResourceType) continue;
                foreach (var (blockId, languages) in Entries(file, root, typeDirectory))
                {
                    foreach (var (_, dataEntry) in Entries(file, root, languages))
                    {
                        var data = Offset(file, sectionTable, sections, Int32(file, dataEntry));
                        var size = Int32(file, dataEntry + 4);
                        var block = Span(file, data, size);
                        var position = 0;
                        for (var index = 0; index < 16; index++)
                        {
                            var length = BinaryPrimitives.ReadUInt16LittleEndian(Slice(block, position, 2));
                            position += 2;
                            if (length > 0)
                                strings[(blockId - 1) * 16 + index] =
                                    Encoding.Unicode.GetString(Slice(block, position, length * 2));
                            position += length * 2;
                        }
                    }
                }
            }
            return strings;
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

        private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> block, int offset, int length) =>
            offset >= 0 && length >= 0 && offset <= block.Length - length
                ? block.Slice(offset, length)
                : throw new InvalidDataException("A string runs past its block.");
    }
}
