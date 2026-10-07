using System.Diagnostics;
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

    public static AssetPackState Inspect(string assetRoot) => AssetPackInspection.Of(assetRoot).State;

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
            ? "The imported game assets were made by another version of " +
              $"{StartupFailureReporter.ApplicationTitle} or cannot be read, and have to be imported again."
            : $"{StartupFailureReporter.ApplicationTitle} needs the art, sound, music and video of " +
              "the original Chaos Overlords, and does not include them.") +
        "\n\nChoose the folder of your Chaos Overlords installation, for example the one GOG " +
        "installed. The original files are only read, never changed. The imported copy is " +
        $"stored in {assetRoot}.";

    public static string OutputFailureMessage(string assetRoot, ExtractorRun run) =>
        $"The imported assets could not be written to {assetRoot}.\n\n" +
        (!string.IsNullOrWhiteSpace(run.FailureMessage) ? run.FailureMessage + "\n\n" : "") +
        "Make sure the disk has free space and that you can write to that folder, then try again.";

    public static string FailureMessage(string source, ExtractorRun run) =>
        $"The assets could not be imported from {source}.\n\n" +
        (!string.IsNullOrWhiteSpace(run.FailureMessage) ? run.FailureMessage
            : run.ExitCode == 0 ? "The importer finished, but left no usable asset pack."
            : $"The importer stopped with exit code {run.ExitCode}.") +
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
        // A source that was fine but could not be written out is used again, so the player who
        // freed space or fixed a permission is not asked for the folder a second time.
        string? retrySource = null;
        while (true)
        {
            if (!dialogs.Ask(message, accept, "Quit")) return FirstLaunchImportResult.Declined;
            var source = retrySource ?? dialogs.ChooseFolder("Choose your Chaos Overlords folder");
            if (source is null) return FirstLaunchImportResult.Declined;
            // osascript ends the path with a slash, and the extractor takes the parent of a picked
            // DATA folder as the installation, which a trailing slash would make DATA itself.
            source = Path.TrimEndingDirectorySeparator(source);
            ExtractorRun run;
            using (dialogs.ShowProgress("Importing the game assets. This can take a minute."))
                run = extract(source);
            if (run.ExitCode == ExtractorExitCodes.Success && Inspect(assetRoot) == AssetPackState.Ready)
                return FirstLaunchImportResult.Imported;
            if (run.ExitCode == ExtractorExitCodes.OutputNotWritable)
            {
                message = OutputFailureMessage(assetRoot, run);
                accept = "Try Again";
                retrySource = source;
                continue;
            }
            message = FailureMessage(source, run);
            accept = "Choose Another Folder";
            retrySource = null;
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
