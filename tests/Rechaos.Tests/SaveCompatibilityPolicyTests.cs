using System.Text.RegularExpressions;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The save and replay compatibility policy in docs/NATIVE-SAVE-FORMAT.md, held where the release
/// workflow's tests run.
/// </summary>
/// <remarks>
/// From 1.0.0 every 1.x build must load the saves of every earlier 1.x release. A promise like that
/// is only kept if something fails when it is broken, so each stable release leaves a save of its
/// own format in tests/fixtures/stable-saves, every build loads all of them, and a stable version
/// with no fixture of the current format cannot pass.
/// </remarks>
public sealed class SaveCompatibilityPolicyTests
{
    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "stable-saves");

    private static IEnumerable<string> Fixtures => Directory.Exists(FixtureDirectory)
        ? Directory.EnumerateFiles(FixtureDirectory, "*.rchsave").Order(StringComparer.Ordinal)
        : [];

    [Fact]
    public void EverySaveFromAStableReleaseStillLoads()
    {
        var definitions = BundledOriginalData.Load();
        foreach (var fixture in Fixtures)
        {
            using var stream = File.OpenRead(fixture);
            var exception = Record.Exception(() => NativeSaveSerializer.Load(stream, definitions));
            Assert.True(exception is null,
                $"{Path.GetFileName(fixture)} was written by a stable release and no longer loads: {exception}");
        }
    }

    [Fact]
    public void AStableReleaseKeepsASaveOfItsOwnFormat()
    {
        var version = Version.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "version.txt")).Trim());
        // Development formats promise nothing. The 1.0.0 fixture is added while version.txt still
        // names the last development release, since the release workflow moves it to 1.0.0.
        if (version.Major < 1) return;
        var formats = Fixtures.Select(DeclaredFormat).ToArray();
        Assert.True(formats.Contains(NativeSaveSerializer.CurrentFormatVersion),
            $"Version {version} writes save format {NativeSaveSerializer.CurrentFormatVersion}, and "
            + "tests/fixtures/stable-saves holds no save of it. Add one written by this build before "
            + "releasing; the policy is in docs/NATIVE-SAVE-FORMAT.md.");
    }

    /// <summary>
    /// The format document states the versions it describes; it stood at 26 and 31 while the code
    /// wrote 40 and 53, so it is checked here, where a version bump runs.
    /// </summary>
    [Fact]
    public void TheFormatDocumentStatesTheCurrentVersions()
    {
        var document = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "NATIVE-SAVE-FORMAT.md"));
        var status = Regex.Match(document,
            @"Status: implemented save format version (?<save>\d+), replay format version (?<replay>\d+), state\s+fingerprint encoding version (?<hash>\d+)",
            RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(status.Success, "The status line of NATIVE-SAVE-FORMAT.md is not in the expected form.");
        Assert.Equal(
            (Save: NativeSaveSerializer.CurrentFormatVersion, Replay: MatchReplaySerializer.CurrentFormatVersion,
                Hash: MatchStateHasher.FormatVersion),
            (Save: int.Parse(status.Groups["save"].Value), Replay: int.Parse(status.Groups["replay"].Value),
                Hash: int.Parse(status.Groups["hash"].Value)));
        Assert.Contains($"`formatVersion: {NativeSaveSerializer.CurrentFormatVersion}`", document);
        Assert.Contains($"currently `{NativeSaveSerializer.CurrentFormatVersion}`", document);
    }

    private static int DeclaredFormat(string fixture)
    {
        using var bounded = new MemoryStream(File.ReadAllBytes(fixture));
        return NativeSaveSerializer.DeclaredFormatVersion(bounded, NativeSaveSerializer.CurrentFormatVersion)
            ?? throw new InvalidDataException($"{Path.GetFileName(fixture)} declares no format version.");
    }
}
