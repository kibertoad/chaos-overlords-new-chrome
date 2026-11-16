namespace Rechaos.Extractor.Installer;

/// <summary>
/// The folder the extractor reads the original from. A folder given as the source is used as it
/// is; GOG's installer given as the source is unpacked into a temporary folder first, which is
/// deleted when this is disposed.
/// </summary>
public sealed class SourceFolder : IDisposable
{
    private readonly string? _temporary;

    private SourceFolder(string path, string? temporary)
    {
        Path = path;
        _temporary = temporary;
    }

    /// <summary>The folder that holds DATA, or DATA itself, as the user gave it.</summary>
    public string Path { get; }

    public bool IsUnpackedInstaller => _temporary is not null;

    /// <summary>
    /// Opens <paramref name="source"/>. An installer is unpacked beside <paramref name="output"/>
    /// when one is given, where the asset pack is staged as well, so a disk that cannot hold the
    /// unpacked files is reported as the output's problem (<see cref="OutputNotWritableException"/>)
    /// and the first-start import asks the player to free space there instead of choosing another
    /// source. Without an output the installer is unpacked into the system's temporary folder.
    /// </summary>
    public static async Task<SourceFolder> OpenAsync(string source, string? output = null)
    {
        if (!File.Exists(source)) return new SourceFolder(source, null);
        var temporary = output is not null
            ? AssetPackInstaller.WorkFolderBeside(output, "installer")
            : System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rechaos-installer-{Guid.NewGuid():N}");
        try
        {
            Console.WriteLine($"Unpacking the game's folders from the GOG installer {System.IO.Path.GetFullPath(source)}...");
            var count = await GogInstaller.UnpackGameFoldersAsync(source, temporary);
            Console.WriteLine($"Unpacked {count} files.");
        }
        catch (UnpackNotWritableException exception) when (output is not null)
        {
            Delete(temporary);
            throw new OutputNotWritableException(output, exception.InnerException!);
        }
        catch
        {
            Delete(temporary);
            throw;
        }
        return new SourceFolder(temporary, temporary);
    }

    public void Dispose()
    {
        if (_temporary is not null) Delete(_temporary);
    }

    private static void Delete(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover in the temporary folder is not worth failing an import that worked.
            Console.Error.WriteLine($"Could not delete the unpacked files in {folder}: {exception.Message}");
        }
    }
}
