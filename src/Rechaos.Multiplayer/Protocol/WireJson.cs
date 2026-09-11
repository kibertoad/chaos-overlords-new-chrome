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
/// <para>
/// Reading and writing do not agree about how strict to be, and the asymmetry is the point. What this
/// client <em>sends</em> is held to the schemas exactly, because the server refuses anything else.
/// What it <em>reads</em> tolerates a field this build has never heard of and one the server left
/// out, because the server is deployed separately — self-hosted servers especially — and one additive
/// release would otherwise lock every existing client out of every match. The exception is a payload
/// whose digest is recomputed here: see <see cref="ReadExact{T}"/>.
/// </para>
/// </remarks>
public static class WireJson
{
    /// <summary>
    /// Options for writing every request, and for reading a payload that must round-trip exactly.
    /// </summary>
    /// <remarks>
    /// Exact means exact: no unknown field, no absent one. Every property of these records comes from
    /// a constructor parameter, so requiring them is what stops a field the schema declares from
    /// quietly defaulting — which for a reference type means a <c>null</c> in a property the whole
    /// codebase is entitled to assume is not null, surfacing much later and nowhere near the payload
    /// that caused it.
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = Create(
        JsonUnmappedMemberHandling.Disallow, requireEveryField: true);

    /// <summary>
    /// Options for reading a response or an event: an unknown field is skipped.
    /// </summary>
    /// <remarks>
    /// Deliberately not requiring every field, which is the other half of being forward compatible. A
    /// server is free to add an optional field, and a record built before it existed has to be able
    /// to read a payload that leaves it out. What is still refused is a null where the schema says
    /// there cannot be one: that is not a newer server, it is a wrong one.
    /// </remarks>
    private static JsonSerializerOptions TolerantOptions { get; } = Create(
        JsonUnmappedMemberHandling.Skip, requireEveryField: false);

    private static JsonSerializerOptions Create(
        JsonUnmappedMemberHandling unmapped,
        bool requireEveryField)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            UnmappedMemberHandling = unmapped,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            // A non-nullable property is one every caller may rely on, so a null arriving in one is
            // refused here rather than thrown hundreds of lines away from the payload that sent it.
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = requireEveryField,
        };
        return options;
    }

    /// <summary>
    /// Deserializes a body, turning an empty or malformed one into a protocol failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A field this build does not know about is skipped, so a server that has grown one keeps
    /// working with clients that predate it, and so is one the server left out. What is not tolerated
    /// is a <c>null</c> where the schema says there cannot be one — see
    /// <see cref="ReadExact{T}"/> for the payloads held to every field being present as well.
    /// </para>
    /// <para>
    /// The document goes through <see cref="WireOrder.TagFirst"/> first, because the reader accepts
    /// a discriminated union only with its tag in front and the server has no reason to put it
    /// there.
    /// </para>
    /// </remarks>
    public static T Read<T>(string body) => Read<T>(body, TolerantOptions);

    /// <summary>
    /// Deserializes a body that has to round-trip byte for byte, refusing any unknown field.
    /// </summary>
    /// <remarks>
    /// Only for a payload this client re-serializes to check a digest — the order documents inside a
    /// sealed set. Skipping a field there would drop it from the text the hash is taken over, so the
    /// digest could not match however sound the rest of the set was. Refusing the field names the
    /// cause; tolerating it would report a mismatch and leave nobody any the wiser.
    /// </remarks>
    public static T ReadExact<T>(string body) => Read<T>(body, Options);

    private static T Read<T>(string body, JsonSerializerOptions options)
    {
        try
        {
            var node = WireOrder.TagFirst(JsonNode.Parse(body));
            return node.Deserialize<T>(options)
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
