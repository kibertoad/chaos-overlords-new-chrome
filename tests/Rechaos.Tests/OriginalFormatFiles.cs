using System.Text.RegularExpressions;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The shipped files a format entry lists, resolved through <see cref="OriginalGameFiles"/>. The
/// entry's <c>files:</c> patterns are matched against the paths of the build entry, so the tests
/// check exactly the files the spec names, each against the xxh3 the build entry gives.
/// </summary>
public static partial class OriginalFormatFiles
{
    public const string Build = "BLD-GOG-EN-1.1";

    public sealed record ListedFile(string Path, string Xxh3)
    {
        /// <summary>The last part of the path, as the build entry spells it.</summary>
        public string Name => Path[(Path.LastIndexOf('/') + 1)..];
    }

    public sealed record ResolvedFile(string Path, string Name, string FullPath)
    {
        public byte[] ReadAllBytes() => File.ReadAllBytes(FullPath);
    }

    [GeneratedRegex(@"^\s*-\s*path:\s*(?<path>.+?)\s*$")]
    private static partial Regex PathLine();

    [GeneratedRegex(@"^\s*xxh3:\s*(?<hash>[0-9a-f]{32})\s*$")]
    private static partial Regex HashLine();

    [GeneratedRegex(@"^files:\s*\[(?<list>.*)\]\s*$")]
    private static partial Regex FilesLine();

    [GeneratedRegex("\"(?<pattern>[^\"]+)\"")]
    private static partial Regex QuotedPattern();

    /// <summary>The files of <see cref="Build"/> that match the format entry's <c>files:</c>.</summary>
    public static IReadOnlyList<ListedFile> Listed(string formatId)
    {
        var patterns = FrontMatter(SpecPath("formats", formatId + ".md"))
            .Select(line => FilesLine().Match(line))
            .Where(match => match.Success)
            .SelectMany(match => QuotedPattern().Matches(match.Groups["list"].Value))
            .Select(match => Glob(match.Groups["pattern"].Value))
            .ToArray();
        Assert.NotEmpty(patterns);
        var files = BuildFiles().Where(file => patterns.Any(pattern => pattern.IsMatch(file.Path))).ToArray();
        Assert.NotEmpty(files);
        return files;
    }

    /// <summary>
    /// Every file the format entry lists, checked against its hash. Skips the test when
    /// <c>GAME_DIR</c> does not hold them.
    /// </summary>
    public static IReadOnlyList<ResolvedFile> Require(string formatId) =>
        Listed(formatId)
            .Select(file => new ResolvedFile(file.Path, file.Name,
                OriginalGameFiles.Require(Build, file.Path, file.Xxh3)))
            .ToArray();

    /// <summary>Runs <paramref name="check"/> on every file and fails once with every failure.</summary>
    public static void CheckEach(IEnumerable<ResolvedFile> files, Action<ResolvedFile> check)
    {
        var failures = new List<string>();
        foreach (var file in files)
        {
            try
            {
                check(file);
            }
            catch (Exception exception) when (exception is not Xunit.Sdk.SkipException)
            {
                failures.Add($"{file.Path}: {exception.Message}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<ListedFile> BuildFiles()
    {
        string? path = null;
        foreach (var line in FrontMatter(SpecPath("builds", Build + ".md")))
        {
            var pathMatch = PathLine().Match(line);
            if (pathMatch.Success)
            {
                path = pathMatch.Groups["path"].Value.Trim('"', '\'');
                continue;
            }
            var hashMatch = HashLine().Match(line);
            if (hashMatch.Success && path is not null)
            {
                yield return new ListedFile(path, hashMatch.Groups["hash"].Value);
                path = null;
            }
        }
    }

    private static IEnumerable<string> FrontMatter(string file)
    {
        var lines = File.ReadAllLines(file);
        Assert.Equal("---", lines[0]);
        return lines.Skip(1).TakeWhile(line => line != "---");
    }

    private static string SpecPath(string kind, string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "spec", kind, file);
        Assert.True(File.Exists(path), $"{path} was not copied beside the test binaries.");
        return path;
    }

    /// <summary>A <c>files:</c> pattern: <c>*</c> stays inside one directory, and case counts.</summary>
    private static Regex Glob(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$");
}
