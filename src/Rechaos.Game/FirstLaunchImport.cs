using System.Diagnostics;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

public enum AssetPackState
{
    Ready,
    Missing,
    Incompatible
}

public enum FirstLaunchImportResult
{
    Imported,
    Declined
}

/// <summary>What one run of the extractor reported.</summary>
public sealed record ExtractorRun(int ExitCode, string? FailureMessage);

/// <summary>
/// Imports the original assets the first time the game starts without them on macOS and Linux.
/// </summary>
/// <remarks>
/// The Windows installer imports the assets while it installs. The macOS <c>.pkg</c> and the Linux
/// <c>.deb</c> cannot: a package script runs as root, with no player to ask for a folder and no
/// home folder of theirs to write to. Both used to leave the player to run the import helper from
/// a terminal, and a player who started the game from Finder or a desktop menu without doing so
/// saw nothing at all, since the startup failure went to stderr. Here the game asks for the
/// folder itself and runs the bundled extractor on it, with the same arguments the helpers pass.
/// </remarks>
public static class FirstLaunchImport
{
    public const string ExtractorFileName = "Rechaos.Extractor";

    public static AssetPackState Inspect(string assetRoot)
    {
        var manifestPath = Path.Combine(assetRoot, "manifest.json");
        if (!File.Exists(manifestPath)) return AssetPackState.Missing;
        try
        {
            var manifest = JsonSerializer.Deserialize<AssetManifest>(File.ReadAllText(manifestPath));
            return manifest?.FormatVersion == AssetManifest.CurrentFormatVersion
                ? AssetPackState.Ready
                : AssetPackState.Incompatible;
        }
        catch (Exception exception) when (exception is JsonException or IOException
            or UnauthorizedAccessException)
        {
            return AssetPackState.Incompatible;
        }
    }

    /// <summary>
    /// The extractor packaged with the game: <c>Contents/Resources/Tools</c> in the macOS bundle,
    /// <c>Tools</c> beside <c>Game</c> in the Linux package and the portable archives.
    /// </summary>
    public static string? FindExtractor(string applicationDirectory)
    {
        string[] candidates =
        [
            Path.Combine(applicationDirectory, "..", "Resources", "Tools", ExtractorFileName),
            Path.Combine(applicationDirectory, "..", "Tools", ExtractorFileName)
        ];
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    public static string IntroMessage(AssetPackState state, string assetRoot) =>
        (state == AssetPackState.Incompatible
            ? "The imported game assets were made by an older version of " +
              $"{StartupFailureReporter.ApplicationTitle} and have to be imported again."
            : $"{StartupFailureReporter.ApplicationTitle} needs the art, sound, music and video of " +
              "the original Chaos Overlords, and does not include them.") +
        "\n\nChoose the folder of your Chaos Overlords installation, for example the one GOG " +
        "installed. The original files are only read, never changed. The imported copy is " +
        $"stored in {assetRoot}.";

    public static string FailureMessage(string source, ExtractorRun run) =>
        $"The assets could not be imported from {source}.\n\n" +
        (string.IsNullOrWhiteSpace(run.FailureMessage)
            ? $"The importer stopped with exit code {run.ExitCode}."
            : run.FailureMessage) +
        "\n\nChoose the folder that holds the original game's DATA folder.";

    public static FirstLaunchImportResult Run(
        AssetPackState state,
        string assetRoot,
        INativeDialogs dialogs,
        Func<string, ExtractorRun> extract)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(extract);
        var message = IntroMessage(state, assetRoot);
        var accept = "Choose Folder";
        while (true)
        {
            if (!dialogs.Ask(message, accept, "Quit")) return FirstLaunchImportResult.Declined;
            var source = dialogs.ChooseFolder("Choose your Chaos Overlords folder");
            if (source is null) return FirstLaunchImportResult.Declined;
            // osascript ends the path with a slash, and the extractor takes the parent of a picked
            // DATA folder as the installation, which a trailing slash would make DATA itself.
            source = Path.TrimEndingDirectorySeparator(source);
            ExtractorRun run;
            using (dialogs.ShowProgress("Importing the game assets. This can take a minute."))
                run = extract(source);
            if (run.ExitCode == 0 && Inspect(assetRoot) == AssetPackState.Ready)
                return FirstLaunchImportResult.Imported;
            message = FailureMessage(source, run);
            accept = "Choose Another Folder";
        }
    }

    /// <summary>Runs the packaged extractor as the import helpers do and waits for it.</summary>
    public static ExtractorRun RunExtractor(string extractorPath, string source, string assetRoot)
    {
        var startInfo = new ProcessStartInfo(extractorPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(source);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(assetRoot);
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The importer did not start.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(output, error);
            return new ExtractorRun(process.ExitCode, LastLine(error.Result));
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException)
        {
            return new ExtractorRun(-1, $"The importer at {extractorPath} could not be started: {exception.Message}");
        }
    }

    internal static string? LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
}
