using System.Text;
using System.Text.Json;

namespace Rechaos.Game;

/// <summary>
/// Writes bounded, local-only runtime diagnostics. Logs are never uploaded and
/// intentionally exclude player names, commands, save contents, and asset paths.
/// </summary>
public sealed class RuntimeDiagnostics : IDisposable
{
    public const int MaximumSessionLogs = 5;
    public const int MaximumCrashReports = 10;
    public const long MaximumSessionBytes = 1024 * 1024;
    private const int MaximumFields = 16;
    private const int MaximumFieldCharacters = 256;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private long _sequence;
    private bool _disposed;

    private RuntimeDiagnostics(string? logDirectory, string? sessionLogPath, StreamWriter? writer)
    {
        LogDirectory = logDirectory;
        SessionLogPath = sessionLogPath;
        _writer = writer;
    }

    public string? LogDirectory { get; }
    public string? SessionLogPath { get; }
    public bool Enabled => _writer is not null;

    public static RuntimeDiagnostics OpenDefault() => Open(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AssetRootResolver.ApplicationDataDirectory,
        "Logs"));

    public static RuntimeDiagnostics Open(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        try
        {
            logDirectory = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(logDirectory);
            var path = CreateUniquePath(logDirectory, "session", ".jsonl");
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var diagnostics = new RuntimeDiagnostics(logDirectory, path, writer);
            diagnostics.Write("application.started", new Dictionary<string, string?>
            {
                ["version"] = typeof(RuntimeDiagnostics).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["platform"] = Environment.OSVersion.Platform.ToString()
            });
            Prune(logDirectory, "session-*.jsonl", MaximumSessionLogs);
            Prune(logDirectory, "crash-*.log", MaximumCrashReports);
            return diagnostics;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                          or NotSupportedException or ArgumentException)
        {
            return new RuntimeDiagnostics(null, null, null);
        }
    }

    public void Write(string eventName, IReadOnlyDictionary<string, string?>? fields = null)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return;
        lock (_gate)
        {
            if (_disposed || _writer is null) return;
            try
            {
                if (_writer.BaseStream.Position >= MaximumSessionBytes) return;
                var safeFields = Sanitize(fields);
                var entry = new RuntimeDiagnosticEntry(
                    DateTimeOffset.UtcNow,
                    _sequence++,
                    Limit(eventName),
                    safeFields);
                _writer.WriteLine(JsonSerializer.Serialize(entry));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ObjectDisposedException or NotSupportedException)
            {
                DisableWriter();
            }
        }
    }

    public string? CaptureCrash(Exception exception, string origin)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write("application.crashed", new Dictionary<string, string?>
        {
            ["origin"] = origin,
            ["exceptionType"] = exception.GetType().FullName
        });
        if (LogDirectory is null) return null;
        try
        {
            var path = CreateUniquePath(LogDirectory, "crash", ".log");
            var report = new StringBuilder()
                .AppendLine($"Timestamp (UTC): {DateTimeOffset.UtcNow:O}")
                .AppendLine($"Version: {typeof(RuntimeDiagnostics).Assembly.GetName().Version}")
                .AppendLine($"Origin: {Limit(origin)}")
                .AppendLine($"Session log: {Path.GetFileName(SessionLogPath)}")
                .AppendLine()
                .Append(exception)
                .ToString();
            File.WriteAllText(path, report, new UTF8Encoding(false));
            Prune(LogDirectory, "crash-*.log", MaximumCrashReports);
            return path;
        }
        catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException
                                               or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        Write("application.stopped");
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            DisableWriter();
        }
    }

    private static string CreateUniquePath(string directory, string prefix, string extension)
    {
        var stem = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Environment.ProcessId}";
        for (var suffix = 0; suffix < 100; suffix++)
        {
            var name = suffix == 0 ? stem : $"{stem}-{suffix}";
            var path = Path.Combine(directory, name + extension);
            if (!File.Exists(path)) return path;
        }
        throw new IOException($"Cannot allocate a unique {prefix} log name.");
    }

    private static void Prune(string directory, string pattern, int retain)
    {
        try
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles(pattern)
                         .OrderByDescending(file => file.LastWriteTimeUtc)
                         .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                         .Skip(retain))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Logging must never prevent the game from running.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Logging must never prevent the game from running.
        }
    }

    private static string Limit(string? value)
    {
        value ??= string.Empty;
        return value.Length <= MaximumFieldCharacters
            ? value
            : value[..MaximumFieldCharacters];
    }

    private static IReadOnlyDictionary<string, string?>? Sanitize(
        IReadOnlyDictionary<string, string?>? fields)
    {
        if (fields is null) return null;
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in fields.Take(MaximumFields))
            result[Limit(field.Key)] = Limit(field.Value);
        return result;
    }

    private void DisableWriter()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // Nothing else can be done if the diagnostic sink is unavailable.
        }
        _writer = null;
    }
}

public sealed record RuntimeDiagnosticEntry(
    DateTimeOffset TimestampUtc,
    long Sequence,
    string Event,
    IReadOnlyDictionary<string, string?>? Fields);
