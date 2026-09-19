using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Rechaos.Core;

namespace Rechaos.Game;

public sealed record DiagnosticsExportResult(
    bool Succeeded,
    string? Path,
    string? ErrorCode);

public sealed record DiagnosticsExportManifest(
    int FormatVersion,
    DateTimeOffset CreatedUtc,
    string ProductVersion,
    int SessionLogCount,
    int CrashSummaryCount,
    string PrivacyNotice);

/// <summary>Creates a bounded, shareable diagnostics archive without raw exception messages.</summary>
public static class DiagnosticsExport
{
    public const int CurrentFormatVersion = 1;
    public const long MaximumCrashReportBytes = 4 * 1024 * 1024;
    private const int MaximumStackFrames = 128;
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly HashSet<string> ExportedFieldNames = new(StringComparer.Ordinal)
    {
        "version", "platform", "from", "to", "helpAvailable", "combatSounds",
        "generalSounds", "combatAnimations", "scenario", "duration",
        "configuredPlayers", "computerPlayers", "mentality", "seed", "player",
        "turnBefore", "turnAfter", "turn", "phase", "commands", "file", "origin",
        "exceptionType", "error", "reason"
    };

    public static DiagnosticsExportResult Create(
        string logDirectory,
        string destinationDirectory,
        DateTimeOffset? createdUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        string? temporaryPath = null;
        try
        {
            logDirectory = Path.GetFullPath(logDirectory);
            destinationDirectory = Path.GetFullPath(destinationDirectory);
            Directory.CreateDirectory(destinationDirectory);
            var created = createdUtc ?? DateTimeOffset.UtcNow;
            var finalPath = UniqueExportPath(destinationDirectory, created);
            temporaryPath = finalPath + ".tmp";
            var sessions = SelectFiles(
                logDirectory,
                "session-*.jsonl",
                RuntimeDiagnostics.MaximumSessionLogs,
                RuntimeDiagnostics.MaximumSessionBytes);
            var crashes = SelectFiles(
                logDirectory,
                "crash-*.log",
                RuntimeDiagnostics.MaximumCrashReports,
                MaximumCrashReportBytes);

            using (var stream = new FileStream(
                       temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteManifest(archive, created, sessions.Count, crashes.Count);
                foreach (var session in sessions)
                    WriteSessionLog(archive, session);
                foreach (var crash in crashes)
                    WriteCrashSummary(archive, crash);
            }
            File.Move(temporaryPath, finalPath);
            temporaryPath = null;
            return new DiagnosticsExportResult(true, finalPath, null);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or NotSupportedException
                                          or ArgumentException
                                          or InvalidDataException)
        {
            return new DiagnosticsExportResult(false, null, exception.GetType().Name);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException)
                {
                    // A failed best-effort export must not affect the game.
                }
            }
        }
    }

    private static IReadOnlyList<FileInfo> SelectFiles(
        string directory,
        string pattern,
        int maximumCount,
        long maximumLength) =>
        new DirectoryInfo(directory)
            .EnumerateFiles(pattern, SearchOption.TopDirectoryOnly)
            .Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0
                           && file.Length <= maximumLength)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .Take(maximumCount)
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();

    private static string UniqueExportPath(string directory, DateTimeOffset created)
    {
        var stem = $"diagnostics-{created:yyyyMMddTHHmmssfffZ}-{Environment.ProcessId}";
        for (var suffix = 0; suffix < 100; suffix++)
        {
            var name = suffix == 0 ? stem : $"{stem}-{suffix}";
            var path = Path.Combine(directory, name + ".zip");
            if (!File.Exists(path) && !File.Exists(path + ".tmp")) return path;
        }
        throw new IOException("Cannot allocate a unique diagnostics export name.");
    }

    private static void WriteManifest(
        ZipArchive archive,
        DateTimeOffset created,
        int sessionCount,
        int crashCount)
    {
        var manifest = new DiagnosticsExportManifest(
            CurrentFormatVersion,
            created,
            GameVersion.Current,
            sessionCount,
            crashCount,
            "Player names, commands, saves, asset paths, exception messages, and source paths are not included.");
        WriteTextEntry(
            archive,
            "manifest.json",
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteSessionLog(ZipArchive archive, FileInfo source)
    {
        var entry = archive.CreateEntry($"sessions/{source.Name}", CompressionLevel.Optimal);
        using var inputStream = new FileStream(
            source.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(inputStream, Utf8, detectEncodingFromByteOrderMarks: true);
        using var writer = new StreamWriter(entry.Open(), Utf8);
        while (reader.ReadLine() is { } line)
        {
            RuntimeDiagnosticEntry? diagnostic;
            try { diagnostic = JsonSerializer.Deserialize<RuntimeDiagnosticEntry>(line); }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                continue;
            }
            if (diagnostic is null) continue;
            var fields = diagnostic.Fields?
                .Where(field => ExportedFieldNames.Contains(field.Key))
                .ToDictionary(
                    field => field.Key,
                    field => SanitizeExportedField(field.Key, field.Value),
                    StringComparer.Ordinal);
            var safe = diagnostic with
            {
                Event = SafeToken(diagnostic.Event),
                Fields = fields
            };
            writer.WriteLine(JsonSerializer.Serialize(safe));
        }
    }

    private static void WriteCrashSummary(ZipArchive archive, FileInfo source)
    {
        var lines = File.ReadAllLines(source.FullName);
        var summary = new StringBuilder();
        foreach (var prefix in new[] { "Timestamp (UTC):", "Version:", "Origin:", "Session log:" })
        {
            var header = lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (header is not null) summary.AppendLine(header);
        }

        var exceptionLine = lines.FirstOrDefault(IsExceptionTypeLine);
        if (exceptionLine is not null)
        {
            var separator = exceptionLine.IndexOf(':');
            summary.Append("Exception type: ")
                .AppendLine(separator < 0 ? exceptionLine.Trim() : exceptionLine[..separator].Trim());
        }

        var frames = lines
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("at ", StringComparison.Ordinal))
            .Select(RemoveSourcePath)
            .Take(MaximumStackFrames)
            .ToArray();
        if (frames.Length > 0)
        {
            summary.AppendLine("Stack:");
            foreach (var frame in frames) summary.Append("  ").AppendLine(frame);
        }
        WriteTextEntry(
            archive,
            $"crashes/{Path.GetFileNameWithoutExtension(source.Name)}.summary.txt",
            summary.ToString());
    }

    private static bool IsExceptionTypeLine(string line)
    {
        line = line.Trim();
        if (line.Length == 0 || line.StartsWith("at ", StringComparison.Ordinal)) return false;
        var separator = line.IndexOf(':');
        var candidate = separator < 0 ? line : line[..separator];
        return candidate.EndsWith("Exception", StringComparison.Ordinal)
               && candidate.All(character => char.IsLetterOrDigit(character)
                                             || character is '.' or '_' or '+' or '`');
    }

    private static string RemoveSourcePath(string frame)
    {
        var sourceIndex = frame.IndexOf(" in ", StringComparison.Ordinal);
        return sourceIndex < 0 ? frame : frame[..sourceIndex];
    }

    private static string? SanitizeExportedField(string key, string? value)
    {
        if (value is null) return null;
        if (key == "file") return Path.GetFileName(value.Replace('\\', '/'));
        if (key == "error") return ExceptionName(value);
        return SafeToken(value);
    }

    private static string ExceptionName(string value)
    {
        var firstLine = value.Split(['\r', '\n'], 2)[0].Trim();
        var separator = firstLine.IndexOf(':');
        var candidate = separator < 0 ? firstLine : firstLine[..separator];
        return candidate.EndsWith("Exception", StringComparison.Ordinal)
               && IsSafeToken(candidate)
            ? candidate
            : "Error";
    }

    private static string SafeToken(string value) =>
        value.Length <= RuntimeDiagnostics.MaximumFieldCharacters
        && IsSafeToken(value)
            ? value
            : "[redacted]";

    private static bool IsSafeToken(string value) =>
        value.All(character => char.IsLetterOrDigit(character)
                               || character is ' ' or '.' or '_' or '-' or '+' or '`');

    private static void WriteTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Utf8);
        writer.Write(content);
    }
}
