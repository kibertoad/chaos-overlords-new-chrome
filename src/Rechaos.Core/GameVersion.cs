using System.Reflection;

namespace Rechaos.Core;

/// <summary>
/// The version this build was stamped with, as the title screen and every report print it.
/// </summary>
/// <remarks>
/// The number comes from <c>version.txt</c> by way of the assembly's informational version, so the
/// title screen, a bug report, a diagnostics export, and the installer that delivered them all name
/// the same build. Nothing here reads a file at run time: a version that can go missing after
/// installation is a version a player cannot quote back.
/// </remarks>
public static class GameVersion
{
    /// <summary>What an unstamped build reports, rather than a made-up number.</summary>
    public const string Unknown = "unknown";

    /// <summary>The three-part version, for example <c>0.1.0</c>.</summary>
    public static string Current { get; } = Read(
        typeof(GameVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(GameVersion).Assembly.GetName().Version);

    /// <summary>The version as the title screen prints it, for example <c>V0.1.0</c>.</summary>
    public static string Display { get; } = Format(Current);

    /// <summary>Spells a version out for the title screen, including when there is none.</summary>
    internal static string Format(string version) =>
        version == Unknown ? "VERSION UNKNOWN" : "V" + version;

    /// <summary>Picks the version out of what the compiler stamped onto an assembly.</summary>
    /// <remarks>
    /// The informational version carries the three parts that were written down; the assembly
    /// version pads them to four and is only the fallback for a build that lost the attribute.
    /// Build metadata after a <c>+</c> is a source revision, not part of the version.
    /// </remarks>
    internal static string Read(string? informationalVersion, Version? assemblyVersion)
    {
        if (informationalVersion is not null)
        {
            var metadata = informationalVersion.IndexOf('+');
            var stamped = metadata < 0 ? informationalVersion : informationalVersion[..metadata];
            if (stamped.Trim() is { Length: > 0 } trimmed) return trimmed;
        }

        if (assemblyVersion is null) return Unknown;
        return assemblyVersion.Build < 0 ? assemblyVersion.ToString() : assemblyVersion.ToString(3);
    }
}
