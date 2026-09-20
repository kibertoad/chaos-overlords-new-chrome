using System.Text;

namespace Rechaos.Core.Assets;

public static class OriginalDataReader
{
    public static OriginalData Read(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var data = new OriginalData(
            ReadRecords(Path.Combine(dataDirectory, "SITES"), 62, ParseSite),
            ReadRecords(Path.Combine(dataDirectory, "GANGS"), 156, ParseGang),
            ReadRecords(Path.Combine(dataDirectory, "ITEMS"), 166, ParseItem));
        OriginalDataValidator.Validate(data);
        return data;
    }

    private static IReadOnlyList<T> ReadRecords<T>(string path, int size, Func<BinaryReader, T> parse)
    {
        using var stream = File.OpenRead(FindCaseInsensitive(path));
        if (stream.Length == 0 || stream.Length % size != 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} has an unexpected size ({stream.Length}).");

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        var result = new List<T>((int)(stream.Length / size));
        while (stream.Position < stream.Length)
        {
            var start = stream.Position;
            result.Add(parse(reader));
            if (stream.Position - start != size)
                throw new InvalidDataException($"Parser consumed {stream.Position - start} bytes, expected {size}.");
        }
        return result;
    }

    private static SiteDefinition ParseSite(BinaryReader reader) => new(
        Text(reader, 20), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(),
        reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), Stats(reader), reader.ReadInt16());

    private static GangDefinition ParseGang(BinaryReader reader)
    {
        var name = Text(reader, 30);
        var id = reader.ReadInt16();
        var description = Text(reader, 90);
        var force = reader.ReadInt16();
        var upkeep = reader.ReadInt16();
        var combat = reader.ReadInt16();
        var defense = reader.ReadInt16();
        var techLevel = reader.ReadInt16();
        return new GangDefinition(name, id, description, force, upkeep, techLevel, Stats(reader, combat, defense));
    }

    private static ItemDefinition ParseItem(BinaryReader reader)
    {
        var name = Text(reader, 30);
        var id = reader.ReadInt16();
        var description = Text(reader, 90);
        var type = reader.ReadInt16();
        var difficulty = reader.ReadInt16();
        var cost = reader.ReadInt16();
        var techLevel = reader.ReadInt16();
        var stats = Stats(reader);
        return new ItemDefinition(name, id, description, type, difficulty, cost, techLevel,
            stats, reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
    }

    private static Statistics Stats(BinaryReader reader, short? combat = null, short? defense = null) => new(
        combat ?? reader.ReadInt16(), defense ?? reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(),
        reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(),
        reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());

    private static string Text(BinaryReader reader, int length) =>
        Encoding.ASCII.GetString(reader.ReadBytes(length)).TrimEnd('\0', ' ');

    public static string FindCaseInsensitive(string path)
    {
        if (File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path) ?? ".";
        var name = Path.GetFileName(path);
        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Required original game file not found: {name}", path);
    }

    /// <summary>
    /// The directory at <paramref name="path"/> whatever case its name is stored in, or null.
    /// </summary>
    /// <remarks>
    /// The original CD and the GOG re-release differ in how they case `DATA`, `MUSIC` and the rest,
    /// and a case-sensitive file system takes that literally. <see cref="FindCaseInsensitive"/>
    /// already handles the files; without this the directories holding them were still matched
    /// exactly, so a lower-case install failed with a message about the wrong thing.
    /// </remarks>
    public static string? FindDirectoryCaseInsensitive(string path)
    {
        if (Directory.Exists(path)) return path;
        var parent = Path.GetDirectoryName(path);
        if (parent is null || !Directory.Exists(parent)) return null;
        var name = Path.GetFileName(path);
        return Directory.EnumerateDirectories(parent).FirstOrDefault(candidate =>
            string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Enumeration that matches a glob the way Windows does, on every platform.</summary>
    public static readonly EnumerationOptions CaseInsensitiveFiles = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = false
    };
}
