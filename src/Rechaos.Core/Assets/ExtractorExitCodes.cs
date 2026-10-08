namespace Rechaos.Core.Assets;

/// <summary>
/// The exit codes of Rechaos.Extractor, shared with the game's first-start import, which tells
/// the player what to change from the code.
/// </summary>
public static class ExtractorExitCodes
{
    public const int Success = 0;

    /// <summary>The source is not a supported installation, or the extraction itself went wrong.</summary>
    public const int Failed = 1;

    /// <summary>The command line is not one the extractor accepts.</summary>
    public const int Usage = 2;

    /// <summary>
    /// The asset pack could not be written to the output folder: no permission, a read-only
    /// volume, or a full disk. Another source folder cannot fix this.
    /// </summary>
    public const int OutputNotWritable = 3;
}
