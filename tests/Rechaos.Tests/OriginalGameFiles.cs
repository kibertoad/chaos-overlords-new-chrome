using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Files from a maintainer's copy of the original game, for tests that compare the rebuild with
/// it. They live under the directory named by <c>GAME_DIR</c>: one directory per build, named by
/// its build ID, laid out as the build entry's paths give them, with a file from a disc under a
/// directory named after the disc (<c>CD:</c>, <c>CD2:</c>). Captures, dumps and recordings that
/// cannot be committed, and the saves experiments start from, sit in <c>GAME_DIR/captures/</c>,
/// named by their xxh3 (<see cref="SpecHash"/>). A test whose file is absent is skipped, and a
/// file whose hash differs from the spec fails the test.
/// </summary>
public static partial class OriginalGameFiles
{
    public const string EnvironmentVariable = "GAME_DIR";

    [GeneratedRegex(@"^BLD-[A-Z][A-Z0-9.-]*$")]
    private static partial Regex BuildPattern();

    [GeneratedRegex(@"^(?<disc>CD[0-9]*):(?<path>.+)$")]
    private static partial Regex DiscPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}$")]
    private static partial Regex Xxh3Pattern();

    /// <summary>
    /// The full path of <paramref name="path"/> in <paramref name="build"/>, after checking it
    /// against <paramref name="xxh3"/> from the build entry. Skips the test when it is absent.
    /// </summary>
    public static string Require(string build, string path, string xxh3) =>
        Resolve(Environment.GetEnvironmentVariable(EnvironmentVariable), build, path, xxh3)
        ?? throw SkipMissing($"{build}/{path}");

    /// <summary>The full path of a capture, dump or recording named by its xxh3.</summary>
    public static string RequireCapture(string xxh3) =>
        ResolveCapture(Environment.GetEnvironmentVariable(EnvironmentVariable), xxh3)
        ?? throw SkipMissing($"captures/{xxh3}");

    internal static string? Resolve(string? root, string build, string path, string xxh3)
    {
        if (!BuildPattern().IsMatch(build))
        {
            throw new ArgumentException($"'{build}' is not a build ID.", nameof(build));
        }
        var disc = DiscPattern().Match(path);
        var relative = disc.Success ? $"{disc.Groups["disc"].Value}/{disc.Groups["path"].Value}" : path;
        return Verified(root, Path.Combine(build, Portable(relative)), xxh3);
    }

    internal static string? ResolveCapture(string? root, string xxh3) =>
        Verified(root, Path.Combine("captures", xxh3), xxh3);

    private static string? Verified(string? root, string relative, string xxh3)
    {
        if (!Xxh3Pattern().IsMatch(xxh3))
        {
            throw new ArgumentException($"'{xxh3}' is not a lower-case xxh3.", nameof(xxh3));
        }
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        var file = Path.Combine(root, relative);
        if (!File.Exists(file))
        {
            return null;
        }
        using var stream = File.OpenRead(file);
        var actual = SpecHash.Xxh3(stream);
        if (actual != xxh3)
        {
            throw new InvalidDataException($"{file} has xxh3 {actual}, but the spec gives {xxh3}.");
        }
        return file;
    }

    private static string Portable(string path)
    {
        var parts = path.Split('/');
        if (Path.IsPathRooted(path) || parts.Any(part => part is "" or "." or ".." || part.Contains('\\')))
        {
            throw new ArgumentException($"'{path}' is not a relative path with forward slashes.", nameof(path));
        }
        return Path.Combine(parts);
    }

    private static Exception SkipMissing(string what)
    {
        Assert.Skip($"{what} is not available under {EnvironmentVariable}.");
        return new UnreachableException();
    }
}
