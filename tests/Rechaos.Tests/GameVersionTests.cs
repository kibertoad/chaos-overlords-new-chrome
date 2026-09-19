using System.Reflection;
using Rechaos.Core;
using Rechaos.Extractor;
using Rechaos.Game;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The release process keeps one number honest: what <c>version.txt</c> says, what the assemblies
/// were stamped with, and what the game shows a player are the same.
/// </summary>
public sealed class GameVersionTests
{
    [Fact]
    public void ReportsTheVersionTheRepositoryWroteDown()
    {
        var written = ReadRepositoryVersion();

        Assert.Matches(@"^\d+\.\d+\.\d+$", written);
        Assert.Equal(written, GameVersion.Current);
    }

    /// <summary>
    /// A bug report names the assembly that composed it, a crash log the one that caught it; both
    /// are only useful if every assembly in a build carries the same stamp.
    /// </summary>
    [Fact]
    public void StampsEveryShippedAssemblyWithThatVersion()
    {
        Assembly[] shipped =
        [
            typeof(GameVersion).Assembly,
            typeof(ChaosGame).Assembly,
            typeof(ExtractorProgram).Assembly,
            typeof(MultiplayerProtocolVersion).Assembly
        ];

        foreach (var assembly in shipped)
            Assert.Equal(GameVersion.Current, GameVersion.Read(
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion,
                assembly.GetName().Version));
    }

    [Fact]
    public void ReadsThePlainVersionOutOfAStampWithASourceRevisionOnIt()
    {
        Assert.Equal("0.1.0", GameVersion.Read("0.1.0+1a2b3c4", null));
        Assert.Equal("0.1.0", GameVersion.Read(" 0.1.0 ", null));
    }

    [Fact]
    public void FallsBackToTheAssemblyVersionAndThenToNothingAtAll()
    {
        Assert.Equal("2.3.4", GameVersion.Read(null, new Version(2, 3, 4, 5)));
        Assert.Equal("2.3", GameVersion.Read(string.Empty, new Version(2, 3)));
        Assert.Equal(GameVersion.Unknown, GameVersion.Read(null, null));
    }

    [Fact]
    public void PrintsTheVersionTheWayTheTitleScreenShowsIt()
    {
        Assert.Equal("V0.1.0", GameVersion.Format("0.1.0"));
        Assert.Equal("VERSION UNKNOWN", GameVersion.Format(GameVersion.Unknown));
        Assert.Equal(GameVersion.Format(GameVersion.Current), GameVersion.Display);
    }

    /// <summary>
    /// Reads the file the build stamped from, rather than trusting the stamp to describe itself.
    /// </summary>
    private static string ReadRepositoryVersion()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "version.txt");
            if (File.Exists(candidate)) return File.ReadAllText(candidate).Trim();
        }

        throw new FileNotFoundException(
            $"No version.txt above the test binaries at '{AppContext.BaseDirectory}'.");
    }
}
