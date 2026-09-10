using System.Globalization;
using System.Text;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>
/// Reads server-sent events off a response body into match events.
/// </summary>
/// <remarks>
/// Frames are separated by a blank line; <c>data:</c> carries the JSON event and <c>id:</c> its
/// sequence number. Comment lines (the keepalive) are skipped. Either line ending is allowed, as
/// the event-stream format says.
/// </remarks>
public static class EventStreamParser
{
    /// <summary>Match events from one connection's body, until the server closes it.</summary>
    public static async IAsyncEnumerable<MatchEvent> ReadAsync(
        Stream body,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var frame = new StringBuilder();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                // The connection ended. A frame with no blank line after it was cut mid-write, and
                // delivering half an event is worse than resuming from the last complete one.
                yield break;
            }
            if (line.Length > 0)
            {
                frame.Append(line).Append('\n');
                continue;
            }
            var parsed = ParseFrame(frame.ToString());
            frame.Clear();
            if (parsed is not null) yield return parsed;
        }
    }

    /// <summary>
    /// One frame, or null when it carried no data (a keepalive comment).
    /// </summary>
    /// <remarks>
    /// The <c>id:</c> field is reconciled with the <c>seq</c> inside the JSON rather than ignored.
    /// Both are written from the same number, so a disagreement means the frame was mangled in
    /// transit or the server is not the one this client thinks it is. Resuming from the wrong
    /// number would skip events in silence, so the frame is refused instead.
    /// </remarks>
    internal static MatchEvent? ParseFrame(string frame)
    {
        var data = new StringBuilder();
        string? id = null;
        var sawData = false;
        foreach (var line in frame.Split('\n'))
        {
            if (line.Length == 0 || line[0] == ':') continue;
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (sawData) data.Append('\n');
                data.Append(line.AsSpan(5).TrimStart());
                sawData = true;
            }
            else if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                id = line.AsSpan(3).Trim().ToString();
            }
        }
        if (!sawData) return null;

        var @event = WireJson.Read<MatchEvent>(data.ToString());
        if (id is not null
            && int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var framed)
            && framed != @event.Seq)
        {
            throw new MultiplayerProtocolException(
                $"event stream frame id {framed} disagrees with its payload seq {@event.Seq}");
        }
        return @event;
    }
}
