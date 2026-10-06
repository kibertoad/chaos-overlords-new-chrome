using System.Buffers.Binary;
using System.Text;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Compares every executable string the rebuild draws (<see cref="ExecutableStrings"/>) with the
/// STRING resources of BLD-GOG-EN-1.1's executable: the scenario names, Mentalities, time limits,
/// lengths and player labels of RULE-UI-009, the captions of SCR-EVENT-001, the item types of
/// FND-UI-013, the final-view calendar companion of FND-UI-040 and the scenario descriptions of
/// FND-SETUP-013.
/// </summary>
public sealed class ExecutableStringTableTests
{
    [Fact]
    public void DrawnStringsAreTheExecutablesForRuleUi009AndScrEvent001()
    {
        // needs: GAME_DIR
        var table = PeStringTable.Read(ExecutableResources.RequireExecutable());

        var failures = ExecutableStrings.Drawn
            .Where(entry => !table.TryGetValue(entry.Key, out var text) || text != entry.Value)
            .Select(entry => $"STRING/{entry.Key}: the rebuild draws \"{entry.Value}\", the executable has "
                + (table.TryGetValue(entry.Key, out var text) ? $"\"{text}\"" : "no such string"))
            .ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheScoreCaptionIsTheStringFndAwards004Reads()
    {
        // needs: GAME_DIR
        var file = ExecutableResources.RequireExecutable();
        var bytes = Enumerable.Range(0, ExecutableStrings.ScoreCaption.Length + 1)
            .Select(offset => (byte)ExecutableResources.ImageInt32(file, 0x00487704u + (uint)offset))
            .ToArray();

        Assert.Equal(ExecutableStrings.ScoreCaption + "\0", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void DrawnStringsCoverEveryNumberTheEntriesName()
    {
        // RULE-UI-009 reads 1 to 10 and 0x2E to 0x3C, SCR-EVENT-001 reads 33 to 44, FND-UI-040
        // reads 19, FND-UI-013 reads 25 to 29 for the five item types, FND-UI-049 reads 30 to 32 and
        // FND-SETUP-013 reads 95 to 104.
        var expected = Enumerable.Range(1, 10)
            .Append(19)
            .Concat(Enumerable.Range(25, 5))
            .Concat(Enumerable.Range(30, 3))
            .Concat(Enumerable.Range(33, 12))
            .Concat(Enumerable.Range(0x2E, 0x3C - 0x2E + 1))
            .Concat(Enumerable.Range(95, 10));
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
    /// checked against its block.
    /// </summary>
    private static class PeStringTable
    {
        private const int StringResourceType = 6;

        public static IReadOnlyDictionary<int, string> Read(byte[] file)
        {
            var strings = new Dictionary<int, string>();
            foreach (var (blockId, data) in ExecutableResources.Read(file, StringResourceType))
            {
                ReadOnlySpan<byte> block = data;
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
            return strings;
        }

        private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> block, int offset, int length) =>
            offset >= 0 && length >= 0 && offset <= block.Length - length
                ? block.Slice(offset, length)
                : throw new InvalidDataException("A string runs past its block.");
    }
}
