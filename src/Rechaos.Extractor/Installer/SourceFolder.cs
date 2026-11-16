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

    public static async Task<SourceFolder> OpenAsync(string source)
    {
        if (!File.Exists(source)) return new SourceFolder(source, null);
        var temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rechaos-installer-{Guid.NewGuid():N}");
        try
        {
            Console.WriteLine($"Unpacking the game's folders from the GOG installer {System.IO.Path.GetFullPath(source)}...");
            var count = await GogInstaller.UnpackGameFoldersAsync(source, temporary);
            Console.WriteLine($"Unpacked {count} files.");
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
