using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Rechaos.Game;

/// <summary>
/// Whether a settings or recovery file on disk was written by a build newer than this one, asked
/// before each save so the save does not replace it.
/// </summary>
/// <remarks>
/// <para>
/// The question has to be asked at save time rather than remembered from the load, because the
/// newer build may have written the file since. Asking it by parsing the whole file put a full read
/// and a <see cref="JsonDocument"/> on every save, and the multiplayer recovery history is saved on
/// every online turn. So the answer is remembered against the file's length and last write time,
/// and the file is read again only when either has changed: a file this build wrote or read itself
/// costs one <see cref="FileInfo"/> per save.
/// </para>
/// <para>
/// When it is read, only its top-level properties are walked, with every nested value skipped, and
/// the walk stops as soon as it has what it needs.
/// </para>
/// </remarks>
internal sealed class NewerBuildFileGuard(
    string versionProperty,
    int currentVersion,
    string? requiredProperty = null)
{
    private readonly record struct FileStamp(DateTime LastWriteUtc, long Length);

    /// <summary>The files last seen not to be newer, by full path.</summary>
    private readonly ConcurrentDictionary<string, FileStamp> _knownCurrent =
        new(StringComparer.Ordinal);

    private readonly byte[] _versionName = Encoding.UTF8.GetBytes(versionProperty);
    private readonly byte[]? _requiredName =
        requiredProperty is null ? null : Encoding.UTF8.GetBytes(requiredProperty);

    /// <summary>
    /// Whether the file at <paramref name="path"/> clearly comes from a newer build. Anything short
    /// of that — no file, one too large to be ours, a lock, bytes that do not parse — is not a
    /// reason to refuse the save.
    /// </summary>
    public bool IsNewer(string path, long maximumBytes)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > maximumBytes) return false;
            var key = file.FullName;
            var stamp = new FileStamp(file.LastWriteTimeUtc, file.Length);
            if (_knownCurrent.TryGetValue(key, out var known) && known == stamp) return false;
            if (IsNewer(File.ReadAllBytes(path))) return true;
            _knownCurrent[key] = stamp;
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Remembers a file this build has just written, or read as its own, so the next save need not
    /// read it.
    /// </summary>
    public void NoteCurrent(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Exists) _knownCurrent[file.FullName] = new FileStamp(file.LastWriteTimeUtc, file.Length);
        }
        catch
        {
            // Only a cache: the next save reads the file instead.
        }
    }

    private bool IsNewer(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
        int? version = null;
        var required = _requiredName is null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isVersion = reader.ValueTextEquals(_versionName);
            var isRequired = _requiredName is not null && reader.ValueTextEquals(_requiredName);
            if (!reader.Read()) return false;
            if (isVersion)
            {
                version = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number)
                    ? number
                    : null;
            }
            if (isRequired) required = true;
            if (required && version is not null) break;
            reader.Skip();
        }
        return required && version > currentVersion;
    }
}
