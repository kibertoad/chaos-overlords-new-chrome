using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

public static class ExtractedHelpStore
{
    public const int MaximumDocumentBytes = 2 * 1024 * 1024;
    private const int MaximumTopics = 512;
    private const int MaximumContentsEntries = 1024;
    private const int MaximumContexts = 2048;
    private const int MaximumRunsPerTopic = 4096;
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
            || document.Contents is null or { Count: > MaximumContentsEntries }
            || document.Contexts is null or { Count: > MaximumContexts })
            return false;
        var ids = new HashSet<int>();
        var linkHashes = new HashSet<uint>();
        var totalCharacters = 0;
        var previousTopicOffset = -1;
        foreach (var topic in document.Topics)
        {
            if (topic.Id < 0 || !ids.Add(topic.Id) || string.IsNullOrWhiteSpace(topic.Title)
                || topic.Text is null || topic.Text.Length > MaximumTopicCharacters)
                return false;
            if (topic.TopicOffset < previousTopicOffset
                || topic.Runs is null or { Count: > MaximumRunsPerTopic }
                || !topic.Runs.All(run =>
                    !string.IsNullOrEmpty(run.Text)
                    && run.HalfPoints is > 0 and <= 144
                    && (!run.Popup || run.LinkHash is not null)))
                return false;
            previousTopicOffset = topic.TopicOffset;
            if (!string.Equals(string.Concat(topic.Runs.Select(run => run.Text)),
                    topic.Text, StringComparison.Ordinal))
                return false;
            foreach (var hash in topic.Runs.Where(run => run.LinkHash is not null)
                         .Select(run => run.LinkHash!.Value))
                linkHashes.Add(hash);
            totalCharacters = checked(totalCharacters + topic.Title.Length + topic.Text.Length);
            if (totalCharacters > MaximumTotalCharacters) return false;
        }
        if (!document.Contents.All(entry =>
            entry.Level is >= 0 and <= 15
            && !string.IsNullOrWhiteSpace(entry.Label)
            && (entry.TopicId is null || ids.Contains(entry.TopicId.Value))
            && (entry.ContextName is null or { Length: > 0 and <= 255 })))
            return false;
        var hashes = new HashSet<uint>();
        var numericIds = new HashSet<uint>();
        var validContexts = document.Contexts.All(context =>
            context.TargetOffset >= 0
            && (context.Hash is not null ^ context.NumericId is not null)
            && (context.Name is null or { Length: > 0 and <= 255 })
            && (context.Name is null || context.Hash is not null)
            && (context.Hash is not { } hash || hashes.Add(hash))
            && (context.NumericId is not { } numericId || numericIds.Add(numericId)));
        return validContexts && linkHashes.All(hashes.Contains);
    }
}
