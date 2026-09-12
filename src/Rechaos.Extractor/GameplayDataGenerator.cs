using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

/// <summary>Deterministic generator for the checked-in non-expressive gameplay data.</summary>
public static class GameplayDataGenerator
{
    public const int FormatVersion = 1;

    public static string Serialize(OriginalData data)
    {
        OriginalDataValidator.Validate(data);
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n";
    }

    public static async Task WriteAsync(OriginalData data, string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        var fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath, Serialize(data), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
