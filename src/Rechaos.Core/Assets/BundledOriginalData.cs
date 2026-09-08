using System.Reflection;
using System.Text.Json;

namespace Rechaos.Core.Assets;

/// <summary>
/// Loads gameplay definitions decoded from the canonical GOG tables. The source
/// hashes and field-by-field verification status are documented in
/// docs/ORIGINAL-FILE-FORMATS.md. This payload is generated, not rebalanced.
/// </summary>
public static class BundledOriginalData
{
    public static OriginalData Load()
    {
        var assembly = typeof(BundledOriginalData).Assembly;
        const string name = "Rechaos.Core.GameData.original-data.json";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded gameplay data: {name}");
        return JsonSerializer.Deserialize<OriginalData>(stream)
            ?? throw new InvalidDataException("Bundled gameplay data is invalid.");
    }
}
