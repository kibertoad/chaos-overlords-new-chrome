using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

public static class ExtractedHelpStore
{
    public const int MaximumDocumentBytes = 2 * 1024 * 1024;
    private const int MaximumTopics = 512;
    private const int MaximumContentsEntries = 1024;
    private const int MaximumTopicCharacters = 128 * 1024;
    private const int MaximumTotalCharacters = 1024 * 1024;

    public static ExtractedHelpDocument? LoadOrNull(string assetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        var path = Path.Combine(assetRoot, "help", "contents.json");
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumDocumentBytes) return null;
            using var stream = file.OpenRead();
            var document = JsonSerializer.Deserialize<ExtractedHelpDocument>(stream);
            return IsValid(document) ? document : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException
                                          or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsValid(ExtractedHelpDocument? document)
    {
        if (document is null
            || document.FormatVersion != ExtractedHelpDocument.CurrentFormatVersion
            || string.IsNullOrWhiteSpace(document.Title)
            || document.Topics is null or { Count: 0 or > MaximumTopics }
            || document.Contents is null or { Count: > MaximumContentsEntries })
            return false;
        var ids = new HashSet<int>();
        var totalCharacters = 0;
        foreach (var topic in document.Topics)
        {
            if (topic.Id < 0 || !ids.Add(topic.Id) || string.IsNullOrWhiteSpace(topic.Title)
                || topic.Text is null || topic.Text.Length > MaximumTopicCharacters)
                return false;
            totalCharacters = checked(totalCharacters + topic.Title.Length + topic.Text.Length);
            if (totalCharacters > MaximumTotalCharacters) return false;
        }
        return document.Contents.All(entry =>
            entry.Level is >= 0 and <= 15
            && !string.IsNullOrWhiteSpace(entry.Label)
            && (entry.TopicId is null || ids.Contains(entry.TopicId.Value)));
    }
}
