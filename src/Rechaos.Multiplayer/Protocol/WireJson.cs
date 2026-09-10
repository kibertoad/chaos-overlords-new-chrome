using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Rechaos.Multiplayer.Protocol;

/// <summary>
/// The serializer settings every wire payload is read and written through.
/// </summary>
/// <remarks>
/// <para>
/// The generated records in <c>Rechaos.Multiplayer.Generated</c> name every JSON property
/// explicitly, so no naming policy is configured: one would only be able to disagree with them.
/// </para>
/// <para>
/// The encoder is the relaxed one. .NET escapes non-ASCII and HTML-sensitive characters by default,
/// which is valid JSON the server would accept — but a player's display name is the shortest path
/// from a lobby to a digest mismatch, and there is no reason for this side to spell a name
/// differently from every other client.
/// </para>
/// </remarks>
public static class WireJson
{
    /// <summary>Options for reading and writing every request, response and event body.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // The schemas are strict: an unknown field is refused rather than dropped. Refusing one
            // here too means a server that grew a field this client does not know about is noticed
            // at the boundary instead of silently ignored.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        return options;
    }

    /// <summary>
    /// Deserializes a body, turning an empty or malformed one into a protocol failure.
    /// </summary>
    /// <remarks>
    /// The document goes through <see cref="WireOrder.TagFirst"/> first, because the reader accepts
    /// a discriminated union only with its tag in front and the server has no reason to put it
    /// there.
    /// </remarks>
    public static T Read<T>(string body)
    {
        try
        {
            var node = WireOrder.TagFirst(JsonNode.Parse(body));
            return node.Deserialize<T>(Options)
                ?? throw new MultiplayerProtocolException($"the server sent an empty {typeof(T).Name}");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new MultiplayerProtocolException(
                $"the server sent a {typeof(T).Name} this client cannot read: {exception.Message}",
                exception);
        }
    }

    /// <summary>Serializes a request body.</summary>
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}

/// <summary>The server answered something this client cannot make sense of.</summary>
public sealed class MultiplayerProtocolException(string message, Exception? inner = null)
    : Exception(message, inner);
