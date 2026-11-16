using System.Diagnostics;

namespace Rechaos.Game;

/// <summary>The few dialogs the game shows before it has a window of its own.</summary>
public interface INativeDialogs
{
    /// <summary>
    /// Shows <paramref name="message"/> with a button for each of <paramref name="choices"/>, two or
    /// three of them, the last of which declines. Returns the index of the button the player picked,
    /// or null when they declined or closed the dialog.
    /// </summary>
    int? Choose(string message, IReadOnlyList<string> choices);

    /// <summary>The folder the player picked, or null when they cancelled.</summary>
    string? ChooseFolder(string title);

    /// <summary>The installer file the player picked, or null when they cancelled.</summary>
    string? ChooseInstaller(string title);

    void ShowError(string message);

    /// <summary>Shows <paramref name="message"/> until the returned handle is disposed.</summary>
    IDisposable ShowProgress(string message);
}

/// <summary>
/// Dialogs drawn by tools the desktop already has: <c>osascript</c> on macOS, <c>zenity</c> or
/// <c>kdialog</c> on Linux. Windows shows its startup error with <c>MessageBoxW</c> and imports
/// the assets in its installer, so it gets none.
/// </summary>
public static class NativeDialogs
{
    public static INativeDialogs? TryCreate()
    {
        if (OperatingSystem.IsMacOS())
            return FindOnPath("osascript") is { } osascript ? new AppleScriptDialogs(osascript) : null;
        if (!OperatingSystem.IsLinux()) return null;
        // Without a display, zenity and kdialog exit with the same code a cancel does, so a start
        // from a terminal or over SSH would read as the player quitting and print nothing.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return null;
        if (FindOnPath("zenity") is { } zenity) return new ZenityDialogs(zenity);
        if (FindOnPath("kdialog") is { } kdialog) return new KDialogDialogs(kdialog);
        return null;
    }

    private static string? FindOnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);

    internal static (int ExitCode, string Output) Run(string tool, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        Process? started;
        try
        {
            started = Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            // FindOnPath only checks that the file exists. A tool that cannot run shows nothing,
            // which reads as a cancel, rather than replacing the startup error with this one.
            Console.Error.WriteLine($"{tool} could not be started: {exception.Message}");
            return (-1, string.Empty);
        }
        using var process = started ?? throw new InvalidOperationException($"{tool} did not start.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        error.Wait();
        return (process.ExitCode, output.TrimEnd('\r', '\n'));
    }

    internal static IDisposable Start(string tool, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        try
        {
            return new KillOnDispose(Process.Start(startInfo));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new KillOnDispose(null);
        }
    }

    private sealed class KillOnDispose(Process? process) : IDisposable
    {
        public void Dispose()
        {
            if (process is null) return;
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException)
            {
                // It closed on its own between the check and the kill.
            }
            process.Dispose();
        }
    }
}

/// <summary>
/// macOS dialogs through <c>osascript</c>. The texts travel as script arguments, so nothing in them
/// is read as AppleScript. <c>activate</c> brings the dialog in front of Finder, which it would
/// otherwise open behind, since the game has no window yet.
/// </summary>
internal sealed class AppleScriptDialogs(string osascript) : INativeDialogs
{
    // The buttons arrive declining one first and the default last, the order macOS draws them in
    // from the left. The script prints the label of the button picked.
    private static readonly string[] ChooseScript =
    [
        "on run argv",
        "activate",
        "set labels to items 3 thru -1 of argv",
        "button returned of (display alert (item 1 of argv) message (item 2 of argv) buttons labels default button (count of labels) cancel button 1)",
        "end run"
    ];

    private static readonly string[] ChooseFolderScript =
    [
        "on run argv",
        "activate",
        "POSIX path of (choose folder with prompt (item 1 of argv))",
        "end run"
    ];

    private static readonly string[] ChooseFileScript =
    [
        "on run argv",
        "activate",
        "POSIX path of (choose file with prompt (item 1 of argv))",
        "end run"
    ];

    private static readonly string[] ErrorScript =
    [
        "on run argv",
        "activate",
        "display alert (item 1 of argv) message (item 2 of argv) as critical buttons {\"OK\"} default button 1",
        "end run"
    ];

    private static readonly string[] ProgressScript =
    [
        "on run argv",
        "activate",
        "display dialog (item 1 of argv) with title (item 2 of argv) buttons {\"Hide\"} default button 1 giving up after 3600",
        "end run"
    ];

    // The cancel button ends the script with error -128, so only the other buttons exit with 0.
    public int? Choose(string message, IReadOnlyList<string> choices)
    {
        var (exitCode, output) = NativeDialogs.Run(osascript, Script(ChooseScript,
            [StartupFailureReporter.ApplicationTitle, message, .. choices.Reverse()]));
        var picked = choices.Take(choices.Count - 1).ToList().IndexOf(output);
        return exitCode == 0 && picked >= 0 ? picked : null;
    }

    public string? ChooseFolder(string title)
    {
        var (exitCode, output) = NativeDialogs.Run(osascript, Script(ChooseFolderScript, title));
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public string? ChooseInstaller(string title)
    {
        var (exitCode, output) = NativeDialogs.Run(osascript, Script(ChooseFileScript, title));
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public void ShowError(string message) =>
        NativeDialogs.Run(osascript, Script(ErrorScript, StartupFailureReporter.ApplicationTitle, message));

    public IDisposable ShowProgress(string message) =>
        NativeDialogs.Start(osascript, Script(ProgressScript, message, StartupFailureReporter.ApplicationTitle));

    private static IEnumerable<string> Script(string[] lines, params string[] arguments) =>
        lines.SelectMany(line => new[] { "-e", line }).Concat(arguments);
}

/// <summary>Linux dialogs through <c>zenity</c>. <c>--no-markup</c> keeps paths and messages literal.</summary>
internal sealed class ZenityDialogs(string zenity) : INativeDialogs
{
    private static readonly string Title = $"--title={StartupFailureReporter.ApplicationTitle}";

    // The first choice is the OK button and the last the cancel button. The ones between are extra
    // buttons, which exit like cancel but print their label.
    public int? Choose(string message, IReadOnlyList<string> choices)
    {
        var middle = choices.Skip(1).Take(choices.Count - 2).ToList();
        var (exitCode, output) = NativeDialogs.Run(zenity,
        [
            "--question", "--no-markup", "--width=480", Title, $"--text={message}",
            $"--ok-label={choices[0]}", $"--cancel-label={choices[^1]}",
            .. middle.Select(label => $"--extra-button={label}")
        ]);
        if (exitCode == 0) return 0;
        var picked = middle.IndexOf(output);
        return picked >= 0 ? picked + 1 : null;
    }

    public string? ChooseFolder(string title)
    {
        var (exitCode, output) = NativeDialogs.Run(zenity,
            ["--file-selection", "--directory", $"--title={title}"]);
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public string? ChooseInstaller(string title)
    {
        var (exitCode, output) = NativeDialogs.Run(zenity,
        [
            "--file-selection", $"--title={title}",
            "--file-filter=GOG installer | *.exe *.EXE", "--file-filter=All files | *"
        ]);
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public void ShowError(string message) =>
        NativeDialogs.Run(zenity, ["--error", "--no-markup", "--width=480", Title, $"--text={message}"]);

    public IDisposable ShowProgress(string message) =>
        NativeDialogs.Start(zenity, ["--progress", "--pulsate", "--no-cancel", Title,
            $"--text={message}"]);
}

/// <summary>Linux dialogs through KDE's <c>kdialog</c>, for desktops without zenity.</summary>
internal sealed class KDialogDialogs(string kdialog) : INativeDialogs
{
    private const string TitleFlag = "--title";

    // Two choices are a yes/no question; three a yes/no/cancel one, which exits 0, 1 or 2.
    public int? Choose(string message, IReadOnlyList<string> choices)
    {
        if (choices.Count == 2)
            return NativeDialogs.Run(kdialog, [TitleFlag, StartupFailureReporter.ApplicationTitle,
                "--yesno", message, "--yes-label", choices[0], "--no-label", choices[1]]).ExitCode == 0 ? 0 : null;
        var exitCode = NativeDialogs.Run(kdialog, [TitleFlag, StartupFailureReporter.ApplicationTitle,
            "--yesnocancel", message, "--yes-label", choices[0], "--no-label", choices[1],
            "--cancel-label", choices[2]]).ExitCode;
        return exitCode is 0 or 1 ? exitCode : null;
    }

    public string? ChooseFolder(string title)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var (exitCode, output) = NativeDialogs.Run(kdialog,
            [TitleFlag, title, "--getexistingdirectory", home]);
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public string? ChooseInstaller(string title)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var (exitCode, output) = NativeDialogs.Run(kdialog,
            [TitleFlag, title, "--getopenfilename", home, "*.exe *.EXE|GOG installer"]);
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public void ShowError(string message) =>
        NativeDialogs.Run(kdialog, [TitleFlag, StartupFailureReporter.ApplicationTitle, "--error", message]);

    public IDisposable ShowProgress(string message) =>
        NativeDialogs.Start(kdialog, [TitleFlag, StartupFailureReporter.ApplicationTitle,
            "--passivepopup", message, "3600"]);
}
