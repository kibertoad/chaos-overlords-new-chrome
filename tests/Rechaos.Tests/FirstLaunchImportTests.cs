using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class FirstLaunchImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rechaos-first-launch-{Guid.NewGuid():N}");

    private string AssetRoot => Path.Combine(_root, "Assets");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void InspectTellsMissingIncompatibleAndReadyPacksApart()
    {
        Assert.Equal(AssetPackState.Missing, FirstLaunchImport.Inspect(AssetRoot));

        WriteManifest(AssetManifest.CurrentFormatVersion - 1);
        Assert.Equal(AssetPackState.Incompatible, FirstLaunchImport.Inspect(AssetRoot));

        File.WriteAllText(Path.Combine(AssetRoot, "manifest.json"), "{ not json");
        Assert.Equal(AssetPackState.Incompatible, FirstLaunchImport.Inspect(AssetRoot));

        WriteManifest(AssetManifest.CurrentFormatVersion);
        Assert.Equal(AssetPackState.Ready, FirstLaunchImport.Inspect(AssetRoot));
    }

    [Fact]
    public void ImportsFromTheChosenFolderWithoutItsTrailingSlash()
    {
        var dialogs = new FakeDialogs(picks: [1], folders: ["/games/Chaos Overlords/"]);
        var sources = new List<string>();

        var result = FirstLaunchImport.Run(AssetPackState.Missing, AssetRoot, dialogs, source =>
        {
            sources.Add(source);
            WriteManifest(AssetManifest.CurrentFormatVersion);
            return new ExtractorRun(0, null);
        });

        Assert.Equal(FirstLaunchImportResult.Imported, result);
        Assert.Equal(["/games/Chaos Overlords"], sources);
        Assert.Contains(AssetRoot, dialogs.Messages[0], StringComparison.Ordinal);
        Assert.Equal(1, dialogs.ProgressShown);
    }

    [Fact]
    public void ImportsFromTheChosenInstaller()
    {
        var installer = "/home/player/Downloads/" + GogInstallerFile.Name;
        var dialogs = new FakeDialogs(picks: [0], folders: [], installers: [installer]);
        var sources = new List<string>();

        var result = FirstLaunchImport.Run(AssetPackState.Missing, AssetRoot, dialogs, source =>
        {
            sources.Add(source);
            WriteManifest(AssetManifest.CurrentFormatVersion);
            return new ExtractorRun(0, null);
        });

        Assert.Equal(FirstLaunchImportResult.Imported, result);
        Assert.Equal([installer], sources);
        Assert.Equal(
            [FirstLaunchImport.ChooseInstallerLabel, FirstLaunchImport.ChooseFolderLabel, FirstLaunchImport.QuitLabel],
            dialogs.Choices[0]);
        Assert.Contains(GogInstallerFile.Name, dialogs.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void OffersAnotherFolderAfterAFailedImport()
    {
        var dialogs = new FakeDialogs(picks: [1, 1], folders: ["/wrong", "/right"]);

        var result = FirstLaunchImport.Run(AssetPackState.Missing, AssetRoot, dialogs, source =>
        {
            if (source == "/wrong")
                return new ExtractorRun(1, "Extraction failed: DATA/PX16 is missing.");
            WriteManifest(AssetManifest.CurrentFormatVersion);
            return new ExtractorRun(0, null);
        });

        Assert.Equal(FirstLaunchImportResult.Imported, result);
        Assert.Contains("/wrong", dialogs.Messages[1], StringComparison.Ordinal);
        Assert.Contains("DATA/PX16 is missing", dialogs.Messages[1], StringComparison.Ordinal);
        Assert.Equal(dialogs.Choices[0], dialogs.Choices[1]);
    }

    [Fact]
    public void ASuccessfulExitWithoutAUsablePackCountsAsAFailure()
    {
        var dialogs = new FakeDialogs(picks: [1, null], folders: ["/games"]);

        var result = FirstLaunchImport.Run(AssetPackState.Missing, AssetRoot, dialogs,
            _ => new ExtractorRun(0, null));

        Assert.Equal(FirstLaunchImportResult.Declined, result);
        Assert.Equal(2, dialogs.Messages.Count);
    }

    [Fact]
    public void AnOutputThatCannotBeWrittenIsRetriedWithTheSameFolder()
    {
        var dialogs = new FakeDialogs(picks: [1, 0], folders: ["/games"]);
        var sources = new List<string>();

        var result = FirstLaunchImport.Run(AssetPackState.Missing, AssetRoot, dialogs, source =>
        {
            sources.Add(source);
            if (sources.Count == 1)
                return new ExtractorRun(ExtractorExitCodes.OutputNotWritable, "Cannot write the asset pack: disk full.");
            WriteManifest(AssetManifest.CurrentFormatVersion);
            return new ExtractorRun(0, null);
        });

        Assert.Equal(FirstLaunchImportResult.Imported, result);
        Assert.Equal(["/games", "/games"], sources);
        Assert.Equal([FirstLaunchImport.TryAgainLabel, FirstLaunchImport.QuitLabel], dialogs.Choices[1]);
        Assert.Contains(AssetRoot, dialogs.Messages[1], StringComparison.Ordinal);
        Assert.Contains("disk full", dialogs.Messages[1], StringComparison.Ordinal);
        Assert.DoesNotContain("DATA folder", dialogs.Messages[1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheInspectionKeepsTheErrorTheStartupReports()
    {
        Assert.IsType<FileNotFoundException>(AssetPackInspection.Of(AssetRoot).Problem);

        WriteManifest(AssetManifest.CurrentFormatVersion - 1);
        Assert.IsType<InvalidDataException>(AssetPackInspection.Of(AssetRoot).Problem);

        File.WriteAllText(Path.Combine(AssetRoot, "manifest.json"), "{ not json");
        var unreadable = AssetPackInspection.Of(AssetRoot);
        Assert.IsAssignableFrom<JsonException>(unreadable.Problem);
        Assert.ThrowsAny<JsonException>(unreadable.ThrowIfNotReady);

        WriteManifest(AssetManifest.CurrentFormatVersion);
        var ready = AssetPackInspection.Of(AssetRoot);
        Assert.Null(ready.Problem);
        ready.ThrowIfNotReady();
    }

    [Fact]
    public void QuittingOrCancellingThePickerRunsNoImport()
    {
        var calls = 0;
        ExtractorRun Extract(string _)
        {
            calls++;
            return new ExtractorRun(0, null);
        }

        Assert.Equal(FirstLaunchImportResult.Declined, FirstLaunchImport.Run(
            AssetPackState.Missing, AssetRoot, new FakeDialogs(picks: [null], folders: []), Extract));
        Assert.Equal(FirstLaunchImportResult.Declined, FirstLaunchImport.Run(
            AssetPackState.Missing, AssetRoot, new FakeDialogs(picks: [1], folders: [null]), Extract));
        Assert.Equal(FirstLaunchImportResult.Declined, FirstLaunchImport.Run(
            AssetPackState.Missing, AssetRoot, new FakeDialogs(picks: [0], folders: [], installers: [null]), Extract));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void AnIncompatiblePackIsExplainedAsAnotherVersionOrUnreadable()
    {
        var message = FirstLaunchImport.IntroMessage(AssetPackState.Incompatible, AssetRoot);

        Assert.Contains("another version", message, StringComparison.Ordinal);
        Assert.Contains("cannot be read", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASuccessfulExitWithoutAPackIsNotReportedAsAnExitCode()
    {
        var message = FirstLaunchImport.FailureMessage("/games", new ExtractorRun(0, null));

        Assert.Contains("left no usable asset pack", message, StringComparison.Ordinal);
        Assert.DoesNotContain("exit code", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Resources/Tools")]
    [InlineData("Tools")]
    public void FindsTheExtractorInTheMacBundleAndTheLinuxLayout(string toolsDirectory)
    {
        var applicationDirectory = Path.Combine(_root, "Contents", "MacOS");
        Directory.CreateDirectory(applicationDirectory);
        Assert.Null(FirstLaunchImport.FindExtractor(applicationDirectory));

        var extractor = Path.Combine(_root, "Contents", toolsDirectory, FirstLaunchImport.ExtractorFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(extractor)!);
        File.WriteAllText(extractor, string.Empty);

        Assert.Equal(Path.GetFullPath(extractor), FirstLaunchImport.FindExtractor(applicationDirectory));
    }

    [Fact]
    public void KeepsTheExtractorsLastErrorLine()
    {
        Assert.Equal("Extraction failed: bad table.",
            FirstLaunchImport.LastLine("Checking...\nExtraction failed: bad table.\n"));
        Assert.Null(FirstLaunchImport.LastLine(string.Empty));
    }

    private void WriteManifest(int formatVersion)
    {
        Directory.CreateDirectory(AssetRoot);
        File.WriteAllText(Path.Combine(AssetRoot, "manifest.json"), JsonSerializer.Serialize(
            new AssetManifest(formatVersion, "test", "fingerprint", DateTimeOffset.UnixEpoch, [])));
    }

    private sealed class FakeDialogs(int?[] picks, string?[] folders, string?[]? installers = null) : INativeDialogs
    {
        private int _pick;
        private int _folder;
        private int _installer;

        public List<string> Messages { get; } = [];

        public List<IReadOnlyList<string>> Choices { get; } = [];

        public int ProgressShown { get; private set; }

        public int? Choose(string message, IReadOnlyList<string> choices)
        {
            Messages.Add(message);
            Choices.Add(choices);
            return picks[_pick++];
        }

        public string? ChooseFolder(string title) => folders[_folder++];

        public string? ChooseInstaller(string title) => installers![_installer++];

        public void ShowError(string message) => Messages.Add(message);

        public IDisposable ShowProgress(string message)
        {
            ProgressShown++;
            return new Handle();
        }

        private sealed class Handle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
