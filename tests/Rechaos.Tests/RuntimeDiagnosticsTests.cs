using System.IO.Compression;
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

    [Fact]
    public void ExportIncludesSessionsAndPrivacyFilteredCrashSummaries()
    {
        using var diagnostics = RuntimeDiagnostics.Open(_directory.FullName);
        diagnostics.Write("screen.changed", new Dictionary<string, string?>
        {
            ["from"] = "Title",
            ["to"] = "Options"
        });
        const string privateMessage = @"C:\Users\Alice\Secret\save.rchsave";
        diagnostics.CaptureCrash(ThrownException(privateMessage), "test");
        diagnostics.Write("legacy.error", new Dictionary<string, string?>
        {
            ["error"] = new InvalidOperationException(privateMessage).ToString(),
            ["private"] = "must not be exported"
        });
        var destination = Path.Combine(_directory.FullName, "exports");

        var result = diagnostics.Export(destination);

        Assert.True(result.Succeeded, result.ErrorCode);
        var path = Assert.IsType<string>(result.Path);
        using var archive = ZipFile.OpenRead(path);
        var manifestEntry = Assert.Single(archive.Entries, entry => entry.FullName == "manifest.json");
        var manifest = JsonSerializer.Deserialize<DiagnosticsExportManifest>(ReadEntry(manifestEntry));
        Assert.NotNull(manifest);
        Assert.Equal(DiagnosticsExport.CurrentFormatVersion, manifest.FormatVersion);
        Assert.Equal(1, manifest.SessionLogCount);
        Assert.Equal(1, manifest.CrashSummaryCount);
        var session = Assert.Single(archive.Entries,
            entry => entry.FullName.StartsWith("sessions/", StringComparison.Ordinal));
        var sessionText = ReadEntry(session);
        Assert.Contains("diagnostics.export.requested", sessionText, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException", sessionText, StringComparison.Ordinal);
        Assert.DoesNotContain(privateMessage, sessionText, StringComparison.Ordinal);
        Assert.DoesNotContain("must not be exported", sessionText, StringComparison.Ordinal);
        var crash = Assert.Single(archive.Entries,
            entry => entry.FullName.StartsWith("crashes/", StringComparison.Ordinal));
        var summary = ReadEntry(crash);
        Assert.Contains("Exception type: System.InvalidOperationException", summary,
            StringComparison.Ordinal);
        Assert.Contains("Stack:", summary, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrownException), summary, StringComparison.Ordinal);
        Assert.DoesNotContain(privateMessage, summary, StringComparison.Ordinal);
        Assert.DoesNotContain("save.rchsave", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RuntimeDiagnosticsTests.cs", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepeatedExportsNeverOverwriteAnExistingBundle()
    {
        using var diagnostics = RuntimeDiagnostics.Open(_directory.FullName);
        var destination = Path.Combine(_directory.FullName, "exports");

        var first = diagnostics.Export(destination);
        var second = diagnostics.Export(destination);

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(second.Succeeded, second.ErrorCode);
        Assert.NotEqual(first.Path, second.Path);
        Assert.True(File.Exists(first.Path));
        Assert.True(File.Exists(second.Path));
    }

    [Fact]
    public void DisabledDiagnosticsFailExportWithoutThrowing()
    {
        using var diagnostics = RuntimeDiagnostics.Open("\0");

        var result = diagnostics.Export(Path.Combine(_directory.FullName, "exports"));

        Assert.False(result.Succeeded);
        Assert.Equal("DiagnosticsUnavailable", result.ErrorCode);
        Assert.Null(result.Path);
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static Exception ThrownException(string message)
    {
        try { throw new InvalidOperationException(message); }
        catch (Exception exception) { return exception; }
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
