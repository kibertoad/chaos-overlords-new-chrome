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
/// <para>
/// An object is rebuilt only when it actually carries a tag that is not already first. Everything
/// else — which is most of a payload — is walked and left alone, because a rebuild means detaching
/// and re-parenting every property of every object in the document to fix the few that need it.
/// Should this ever become worth removing altogether, the way to do it is a
/// <c>JsonConverter</c> per union that reads the tag wherever it sits, which would also save parsing
/// each payload into a node tree before deserializing it.
/// </para>
/// </remarks>
public static class WireOrder
{
    /// <summary>
    /// Every discriminator name any generated union uses. Three today: <c>type</c>, <c>kind</c>,
    /// <c>op</c>.
    /// </summary>
    /// <remarks>
    /// Declared attributes only (<c>inherit: false</c>): a union's members inherit the attribute from
    /// their base, and counting them would say the same three names several times over.
    /// </remarks>
    private static readonly HashSet<string> Discriminators = typeof(Generated.MatchEvent).Assembly
        .GetTypes()
        .Select(type => type.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false))
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
    public static JsonNode? TagFirst(JsonNode? node) => node switch
    {
        JsonArray array => Normalise(array),
        JsonObject shape => Normalise(shape),
        _ => node,
    };

    /// <summary>
    /// Normalises each element, replacing only the ones that came back as a different node.
    /// </summary>
    /// <remarks>
    /// Assigning a node back into the slot it already occupies throws — a node may have one parent —
    /// so an element normalised in place is left where it is rather than reassigned.
    /// </remarks>
    private static JsonArray Normalise(JsonArray array)
    {
        for (var index = 0; index < array.Count; index++)
        {
            var item = array[index];
            var normalised = TagFirst(item);
            if (ReferenceEquals(item, normalised)) continue;
            array[index] = null;
            array[index] = normalised;
        }
        return array;
    }

    /// <summary>The same object, with its properties normalised and its tag hoisted if it has one.</summary>
    private static JsonNode Normalise(JsonObject shape)
    {
        // Snapshot the names: normalising a property can replace it, and an object cannot be written
        // to while it is being enumerated.
        var names = new string[shape.Count];
        var index = 0;
        var tag = -1;
        foreach (var property in shape)
        {
            if (tag < 0 && Discriminators.Contains(property.Key)) tag = index;
            names[index++] = property.Key;
        }

        foreach (var name in names)
        {
            var child = shape[name];
            var normalised = TagFirst(child);
            if (!ReferenceEquals(child, normalised)) shape[name] = normalised;
        }

        // No tag, or one already in front: the reader will take it as it stands.
        return tag <= 0 ? shape : Hoist(shape, names, tag);
    }

    /// <summary>The object again with <paramref name="tag"/>'s property first, everything else in order.</summary>
    private static JsonObject Hoist(JsonObject shape, string[] names, int tag)
    {
        var values = new JsonNode?[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            values[index] = shape[names[index]];
            // Removing detaches the value, which is what lets it be re-parented below.
            shape.Remove(names[index]);
        }
        var reordered = new JsonObject { [names[tag]] = values[tag] };
        for (var index = 0; index < names.Length; index++)
        {
            if (index != tag) reordered[names[index]] = values[index];
        }
        return reordered;
    }
}
