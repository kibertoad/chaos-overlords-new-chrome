using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Rechaos.Multiplayer.Protocol;

/// <summary>
/// Moves a discriminated union's tag to the front of the object that carries it.
/// </summary>
/// <remarks>
/// <para>
/// <c>System.Text.Json</c> reads a polymorphic payload only when its type discriminator is the
/// first property, and refuses it outright when it is not. Nothing on the other side of this wire
/// promises that: the server is TypeScript, its events carry the log envelope before the tag, and
/// no JSON writer anywhere guarantees an order. A client that depended on it would work until the
/// day a field moved.
/// </para>
/// <para>
/// Reordering an object's properties changes nothing about what the JSON means, so this normalises
/// the order rather than replacing the reader. The names it hoists are read off the
/// <see cref="JsonPolymorphicAttribute"/> declarations the generator emits, so a new union needs
/// nothing added here.
/// </para>
/// </remarks>
public static class WireOrder
{
    /// <summary>
    /// Every discriminator name any generated union uses. Three today: <c>type</c>, <c>kind</c>,
    /// <c>op</c>.
    /// </summary>
    private static readonly HashSet<string> Discriminators = typeof(Generated.MatchEvent).Assembly
        .GetTypes()
        .Select(type => type.GetCustomAttribute<JsonPolymorphicAttribute>())
        .Select(attribute => attribute?.TypeDiscriminatorPropertyName)
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The same document with every union's tag first, at any depth.
    /// </summary>
    /// <remarks>
    /// It hoists by name and not by type, so an object that merely happens to have a
    /// <c>type</c> field is reordered too. That is harmless: the order of an object's properties is
    /// not part of what JSON says.
    /// </remarks>
    public static JsonNode? TagFirst(JsonNode? node)
    {
        switch (node)
        {
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var item = array[index];
                    array[index] = null;
                    array[index] = TagFirst(item);
                }
                return array;
            case JsonObject shape:
                return Reorder(shape);
            default:
                return node;
        }
    }

    private static JsonNode Reorder(JsonObject shape)
    {
        var properties = shape.ToArray();
        foreach (var property in properties) shape.Remove(property.Key);
        var reordered = new JsonObject();
        foreach (var property in properties.Where(entry => Discriminators.Contains(entry.Key)))
        {
            reordered[property.Key] = TagFirst(property.Value);
        }
        foreach (var property in properties.Where(entry => !Discriminators.Contains(entry.Key)))
        {
            reordered[property.Key] = TagFirst(property.Value);
        }
        return reordered;
    }
}
