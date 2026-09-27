using System.Buffers.Binary;
using System.Text;
using Rechaos.Core.Assets;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Decodes the shipped definition tables with <see cref="OriginalDataReader"/> and checks them
/// against FMT-DATA-001 (SITES), FMT-DATA-002 (Gangs) and FMT-DATA-003 (ITEMS). Every number the
/// reader returns is compared with the 16-bit value at the offset the entry gives, so a field the
/// reader takes from the wrong place fails here.
/// </summary>
public sealed class OriginalDataTableFileTests
{
    [Fact]
    public void SitesDecodeAsTheTwentyTwoRecordsOfFmtData001()
    {
        var file = Single("FMT-DATA-001");
        var bytes = file.ReadAllBytes();
        var sites = Read(file).Sites;

        // FMT-DATA-001: 22 records of 62 bytes, nothing before or after them.
        Assert.Equal(22 * 62, bytes.Length);
        Assert.Equal(22, sites.Count);
        var failures = new List<string>();
        for (var index = 0; index < sites.Count; index++)
        {
            var site = sites[index];
            var record = bytes.AsSpan(index * 62, 62);
            Check(failures, $"site {index} name", NameField(record[..20]), site.Name);
            var numbers = new short[]
            {
                site.Id, site.Resistance, site.Support, site.Frequency, site.Tolerance, site.Cash
            }.Concat(StatisticsInEntryOrder(site.Stats)).Append(site.Special).ToArray();
            CompareNumbers(failures, $"site {index}", record, 0x14, numbers);
            Check(failures, $"site {index} id", (short)index, site.Id);
            // The modifiers the entry gives as 0 in every record.
            Check(failures, $"site {index} combat", (short)0, site.Stats.Combat);
            Check(failures, $"site {index} control", (short)0, site.Stats.Control);
            Check(failures, $"site {index} blade", (short)0, site.Stats.Blade);
            Check(failures, $"site {index} martial_arts", (short)0, site.Stats.MartialArts);
            if (site.Special is < 0 or > 3) failures.Add($"site {index} special {site.Special} is not in the table");
        }
        // FMT-DATA-001, special: 0 in 19 records, and 1, 2 and 3 in one record each.
        Check(failures, "records with special 0", 19, sites.Count(site => site.Special == 0));
        foreach (var value in new short[] { 1, 2, 3 })
            Check(failures, $"records with special {value}", 1, sites.Count(site => site.Special == value));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void GangsDecodeAsTheNinetyRecordsOfFmtData002()
    {
        var file = Single("FMT-DATA-002");
        var bytes = file.ReadAllBytes();
        var gangs = Read(file).Gangs;

        // FMT-DATA-002: 90 records of 156 bytes.
        Assert.Equal(90 * 156, bytes.Length);
        Assert.Equal(90, gangs.Count);
        var failures = new List<string>();
        for (var index = 0; index < gangs.Count; index++)
        {
            var gang = gangs[index];
            var record = bytes.AsSpan(index * 156, 156);
            Check(failures, $"gang {index} name", NameField(record[..30]), gang.Name);
            CompareNumbers(failures, $"gang {index}", record, 0x1E, [gang.Id]);
            Check(failures, $"gang {index} description", DescriptionField(record.Slice(0x20, 90)), gang.Description);
            var stats = gang.Stats;
            CompareNumbers(failures, $"gang {index}", record, 0x7A,
            [
                gang.Force, gang.Upkeep, stats.Combat, stats.Defense, gang.TechLevel, stats.Stealth,
                stats.Detect, stats.Chaos, stats.Control, stats.Heal, stats.Influence, stats.Research,
                stats.Strength, stats.Blade, stats.Range, stats.Fighting, stats.MartialArts
            ]);
            Check(failures, $"gang {index} id", (short)index, gang.Id);
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ItemsDecodeAsTheSixtyFourRecordsOfFmtData003()
    {
        var file = Single("FMT-DATA-003");
        var bytes = file.ReadAllBytes();
        var items = Read(file).Items;

        // FMT-DATA-003: 64 records of 166 bytes; 0 to 52 are items, 53 to 63 blank.
        Assert.Equal(64 * 166, bytes.Length);
        Assert.Equal(64, items.Count);
        var failures = new List<string>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var record = bytes.AsSpan(index * 166, 166);
            var blank = index >= 53;
            Check(failures, $"item {index} name", blank ? "" : NameField(record[..30]), item.Name);
            Check(failures, $"item {index} description", DescriptionField(record.Slice(0x20, 90)), item.Description);
            var numbers = new[] { item.Type, item.ResearchDifficulty, item.Cost, item.TechLevel }
                .Concat(StatisticsInEntryOrder(item.Stats))
                .Concat([item.AttackAnimation, item.HitAnimation, item.Sound, item.CombatPortraitFrame])
                .ToArray();
            CompareNumbers(failures, $"item {index}", record, 0x1E, [item.Id]);
            CompareNumbers(failures, $"item {index}", record, 0x7A, numbers);
            if (blank)
            {
                // A blank record: 30 spaces, 90 spaces, type 99 and every other number 0.
                if (record[..30].IndexOfAnyExcept((byte)' ') >= 0) failures.Add($"item {index} name is not 30 spaces");
                if (record.Slice(0x20, 90).IndexOfAnyExcept((byte)' ') >= 0)
                    failures.Add($"item {index} description is not 90 spaces");
                Check(failures, $"item {index} type", (short)99, item.Type);
                if (numbers.Skip(1).Append(item.Id).Any(value => value != 0))
                    failures.Add($"item {index} has a number other than 0");
                continue;
            }
            Check(failures, $"item {index} id", (short)index, item.Id);
            if (item.Type is not (0 or 1 or 2 or 3 or 4)) failures.Add($"item {index} type {item.Type} is not in the table");
            InRange(failures, $"item {index} cost", item.Cost, 0, 45);
            InRange(failures, $"item {index} tech_level", item.TechLevel, 0, 10);
            InRange(failures, $"item {index} attack_animation", item.AttackAnimation, 0, 26);
            InRange(failures, $"item {index} hit_animation", item.HitAnimation, 0, 19);
            InRange(failures, $"item {index} sound", item.Sound, 0, 17);
            InRange(failures, $"item {index} combat_portrait_frame", item.CombatPortraitFrame, 0, 14);
            // The modifiers the entry gives as 0 in every record.
            Check(failures, $"item {index} blade", (short)0, item.Stats.Blade);
            Check(failures, $"item {index} fighting", (short)0, item.Stats.Fighting);
            Check(failures, $"item {index} martial_arts", (short)0, item.Stats.MartialArts);
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static OriginalFormatFiles.ResolvedFile Single(string formatId) =>
        Assert.Single(OriginalFormatFiles.Require(formatId));

    /// <summary>
    /// The reader takes the three tables from one directory. All three are checked against their
    /// hashes first, so it reads the files the spec describes.
    /// </summary>
    private static OriginalData Read(OriginalFormatFiles.ResolvedFile file)
    {
        foreach (var id in new[] { "FMT-DATA-001", "FMT-DATA-002", "FMT-DATA-003" })
            Assert.Equal(Path.GetDirectoryName(file.FullPath), Path.GetDirectoryName(Single(id).FullPath));
        return OriginalDataReader.Read(Path.GetDirectoryName(file.FullPath)!);
    }

    /// <summary>The statistics in the order FMT-DATA-001 and FMT-DATA-003 lay them out.</summary>
    private static short[] StatisticsInEntryOrder(Statistics stats) =>
    [
        stats.Combat, stats.Defense, stats.Stealth, stats.Detect, stats.Chaos, stats.Control,
        stats.Heal, stats.Influence, stats.Research, stats.Strength, stats.Blade, stats.Range,
        stats.Fighting, stats.MartialArts
    ];

    /// <summary>
    /// A name field: text, one NUL, then spaces to the end of the field. Returns the C string, or
    /// a description of the problem that cannot equal a decoded name.
    /// </summary>
    private static string NameField(ReadOnlySpan<byte> field)
    {
        var nul = field.IndexOf((byte)0);
        if (nul < 1) return $"<no NUL after text in {Convert.ToHexString(field)}>";
        if (field[(nul + 1)..].IndexOfAnyExcept((byte)' ') >= 0) return "<bytes other than spaces after the NUL>";
        return Encoding.ASCII.GetString(field[..nul]);
    }

    /// <summary>A description field: text padded with spaces, with no NUL byte.</summary>
    private static string DescriptionField(ReadOnlySpan<byte> field) =>
        field.IndexOf((byte)0) >= 0
            ? "<NUL in description>"
            : Encoding.ASCII.GetString(field).TrimEnd(' ');

    private static void CompareNumbers(List<string> failures, string label, ReadOnlySpan<byte> record, int offset,
        short[] decoded)
    {
        for (var index = 0; index < decoded.Length; index++)
        {
            var at = offset + index * 2;
            var stored = BinaryPrimitives.ReadInt16LittleEndian(record.Slice(at, 2));
            if (stored != decoded[index])
                failures.Add($"{label} offset 0x{at:X2}: file holds {stored}, reader gave {decoded[index]}");
        }
    }

    private static void Check<T>(List<string> failures, string label, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            failures.Add($"{label}: expected {expected}, got {actual}");
    }

    private static void InRange(List<string> failures, string label, short value, int minimum, int maximum)
    {
        if (value < minimum || value > maximum) failures.Add($"{label} {value} is outside {minimum} to {maximum}");
    }
}
