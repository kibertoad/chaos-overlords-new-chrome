namespace Rechaos.Game;

/// <summary>
/// RULE-UI-013, FND-PLATFORM-009: a save named on the command line, as a file association or a
/// drop on the program passes one, is opened at start, and such a start skips the intro.
/// </summary>
public static class StartupSave
{
    /// <summary>
    /// The save the command line names: its first argument that is no option and no option's
    /// value, or null when there is none or the start draws a reference frame.
    /// </summary>
    /// <remarks>The file is the rebuild's own save (DEV-SAVE-001).</remarks>
    public static string? PathFrom(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Contains("--reference-frame")) return null;
        for (var index = 0; index < args.Count; index++)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                if (args[index] == "--assets") index++;
                continue;
            }
            return args[index].Length == 0 ? null : Path.GetFullPath(args[index]);
        }
        return null;
    }
}
