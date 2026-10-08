using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Rechaos.Game;

public enum StartupPlatform
{
    Windows,
    MacOS,
    Linux
}

public static class StartupFailureReporter
{
    public const string ApplicationTitle = "Chaos Overlords: New Chrome";

    public static string BuildUserMessage(Exception exception, string? assetRoot, string? logPath = null) =>
        BuildUserMessage(exception, assetRoot, logPath, CurrentPlatform);

    public static string BuildUserMessage(
        Exception exception,
        string? assetRoot,
        string? logPath,
        StartupPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var builder = new StringBuilder()
            .AppendLine($"{ApplicationTitle} could not start.")
            .AppendLine()
            .AppendLine(exception.Message);
        if (!string.IsNullOrWhiteSpace(assetRoot))
            builder.AppendLine().AppendLine($"Asset folder: {Path.GetFullPath(assetRoot)}");
        builder.AppendLine()
            .AppendLine(MissingAssetsHint(platform));
        if (!string.IsNullOrWhiteSpace(logPath))
            builder.AppendLine().AppendLine($"Technical details: {logPath}");
        return builder.ToString().TrimEnd();
    }

    public static StartupPlatform CurrentPlatform =>
        OperatingSystem.IsMacOS() ? StartupPlatform.MacOS
        : OperatingSystem.IsLinux() ? StartupPlatform.Linux
        : StartupPlatform.Windows;

    private static string MissingAssetsHint(StartupPlatform platform) => platform switch
    {
        StartupPlatform.MacOS =>
            "If the original game assets are missing, start the game again to import them, or run " +
            "“Contents/MacOS/Install Original Resources” inside the application bundle from Terminal " +
            "with the folder of your Chaos Overlords installation.",
        StartupPlatform.Linux =>
            "If the original game assets are missing, start the game again to import them (this needs " +
            "zenity or kdialog), or run “chaos-overlords-new-chrome-import” from a terminal with the " +
            "folder of your Chaos Overlords installation.",
        _ =>
            "If the original game assets are missing, run “Import Assets from Original Chaos Overlords” " +
            "from the Start menu, or reinstall and keep asset import selected."
    };

    /// <summary>What the player is told when the game stops during play.</summary>
    public static string BuildMainLoopMessage(
        Exception exception,
        string? recoverySavePath,
        string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var builder = new StringBuilder()
            .AppendLine($"{ApplicationTitle} stopped unexpectedly.")
            .AppendLine()
            .AppendLine(exception.Message)
            .AppendLine()
            .AppendLine(recoverySavePath is null
                ? "The match in progress could not be saved. Your last manual save is unchanged."
                : $"The match in progress was written to {recoverySavePath}. Your saved slots are unchanged.");
        if (!string.IsNullOrWhiteSpace(logPath))
            builder.AppendLine().AppendLine($"Technical details: {logPath}");
        return builder.ToString().TrimEnd();
    }

    public static void Report(
        Exception exception,
        string? assetRoot,
        RuntimeDiagnostics? diagnostics = null)
    {
        var logPath = diagnostics?.CaptureCrash(exception, "startup")
            ?? TryWriteLog(exception, assetRoot);
        Show(BuildUserMessage(exception, assetRoot, logPath), exception);
    }

    /// <summary>
    /// Reports a crash that happened after the game was running.
    /// </summary>
    /// <remarks>
    /// The two used to share one message. A player whose turn resolution threw on turn 30 was told
    /// the game "could not start" and advised to re-import their assets, and the crash log recorded
    /// the origin as "main-loop" for real startup failures too, so the log could not tell them apart
    /// either.
    /// </remarks>
    public static void ReportMainLoopFailure(
        Exception exception,
        string? recoverySavePath,
        RuntimeDiagnostics? diagnostics = null)
    {
        var logPath = diagnostics?.CaptureCrash(exception, "main-loop")
            ?? TryWriteLog(exception, null);
        Show(BuildMainLoopMessage(exception, recoverySavePath, logPath), exception);
    }

    private static void Show(string message, Exception exception)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(exception);
        if (OperatingSystem.IsWindows())
        {
            _ = MessageBoxW(IntPtr.Zero, message, ApplicationTitle, 0x10);
            return;
        }
        // A game started from Finder or a desktop menu has no terminal to read stderr in.
        try
        {
            NativeDialogs.TryCreate()?.ShowError(message);
        }
        catch (Exception dialogFailure) when (dialogFailure is System.ComponentModel.Win32Exception
            or InvalidOperationException)
        {
            Console.Error.WriteLine(dialogFailure.Message);
        }
    }

    private static string? TryWriteLog(Exception exception, string? assetRoot)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AssetRootResolver.ApplicationDataDirectory,
                "Logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory,
                $"crash-{DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)}-{Environment.ProcessId}.log");
            File.WriteAllText(path,
                $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
                $"Asset folder: {assetRoot ?? "(not resolved)"}{Environment.NewLine}" +
                exception);
            return path;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
