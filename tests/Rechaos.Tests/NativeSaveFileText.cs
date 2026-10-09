using System.Text;
using Rechaos.Core.Persistence;

namespace Rechaos.Tests;

/// <summary>Edits the snapshot JSON inside a compressed native save file.</summary>
internal static class NativeSaveFileText
{
    public static string Read(string path)
    {
        using var file = File.OpenRead(path);
        using var json = NativeSaveStore.ReadFile(file);
        return Encoding.UTF8.GetString(json.ToArray());
    }

    /// <summary>Rewrites the JSON and compresses it again, answering the new file length.</summary>
    public static long Rewrite(string path, Func<string, string> edit)
    {
        var json = Encoding.UTF8.GetBytes(edit(Read(path)));
        using (var file = File.Create(path))
            NativeSaveStore.WriteFile(file, json);
        return new FileInfo(path).Length;
    }
}
