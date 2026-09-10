using System.Text.Json;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class RuntimeDiagnosticsTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("rechaos-logs-");

    [Fact]
    public void SessionLogRecordsOrderedStructuredEvents()
    {
        string path;
        using (var diagnostics = RuntimeDiagnostics.Open(_directory.FullName))
        {
            Assert.True(diagnostics.Enabled);
            path = Assert.IsType<string>(diagnostics.SessionLogPath);
            diagnostics.Write("screen.changed", new Dictionary<string, string?>
            {
                ["from"] = "Title",
                ["to"] = "Help"
            });
        }

        var entries = File.ReadLines(path)
            .Select(line => JsonSerializer.Deserialize<RuntimeDiagnosticEntry>(line)!)
            .ToArray();
        Assert.Equal(["application.started", "screen.changed", "application.stopped"],
            entries.Select(entry => entry.Event));
        Assert.Equal([0, 1, 2], entries.Select(entry => entry.Sequence));
        Assert.Equal("Help", entries[1].Fields!["to"]);
    }

    [Fact]
    public void CrashReportIsUniqueAndLinksToSessionLog()
    {
        using var diagnostics = RuntimeDiagnostics.Open(_directory.FullName);

        var first = diagnostics.CaptureCrash(new InvalidOperationException("first"), "test");
        var second = diagnostics.CaptureCrash(new InvalidOperationException("second"), "test");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        var report = File.ReadAllText(first);
        var sessionLogName = Path.GetFileName(Assert.IsType<string>(diagnostics.SessionLogPath));
        Assert.Contains("Origin: test", report, StringComparison.Ordinal);
        Assert.Contains(sessionLogName, report, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: first", report, StringComparison.Ordinal);
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
