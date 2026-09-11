using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rechaos.Multiplayer.Protocol;

/// <summary>
/// Deterministic JSON: the exact text the server's order digests are taken over.
/// </summary>
/// <remarks>
/// <para>
/// The rules are the server's, in <c>packages/kernel/src/logic/canonical-json.ts</c>: object keys
/// sorted by UTF-16 code unit (which is <see cref="StringComparer.Ordinal"/>), no whitespace,
/// standard JSON string escaping, and integers written plainly.
/// </para>
/// <para>
/// The restriction to integers is the point. A client in another language has to reproduce this
/// text byte for byte to verify a digest, and a floating-point value's shortest round-trip
/// spelling is not portable: JavaScript writes <c>1e+21</c> where .NET writes <c>1E+21</c>. Any
/// value outside the portable set throws rather than hashing something a peer cannot reproduce.
/// </para>
/// </remarks>
public static class CanonicalJson
{
    /// <summary>The canonical text of a value already parsed as JSON.</summary>
    public static string Encode(JsonElement value)
    {
        var builder = new StringBuilder();
        Write(builder, value, string.Empty);
        return builder.ToString();
    }

    /// <summary>The canonical text as UTF-8 bytes, which is what a digest is taken over.</summary>
    public static byte[] EncodeUtf8(JsonElement value) => Encoding.UTF8.GetBytes(Encode(value));

    private static void Write(StringBuilder builder, JsonElement value, string path)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(builder, value, path);
                return;
            case JsonValueKind.Array:
                WriteArray(builder, value, path);
                return;
            case JsonValueKind.String:
                WriteString(builder, value.GetString() ?? string.Empty);
                return;
            case JsonValueKind.Number:
                WriteNumber(builder, value, path);
                return;
            case JsonValueKind.True:
                builder.Append("true");
                return;
            case JsonValueKind.False:
                builder.Append("false");
                return;
            case JsonValueKind.Null:
                builder.Append("null");
                return;
            case JsonValueKind.Undefined:
            default:
                throw new NonCanonicalValueException(path, $"unsupported kind {value.ValueKind}");
        }
    }

    private static void WriteObject(StringBuilder builder, JsonElement value, string path)
    {
        var properties = new List<JsonProperty>();
        foreach (var property in value.EnumerateObject()) properties.Add(property);
        properties.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        builder.Append('{');
        for (var index = 0; index < properties.Count; index++)
        {
            if (index > 0) builder.Append(',');
            var property = properties[index];
            WriteString(builder, property.Name);
            builder.Append(':');
            Write(builder, property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}");
        }
        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, JsonElement value, string path)
    {
        builder.Append('[');
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (index > 0) builder.Append(',');
            Write(builder, item, $"{path}[{index}]");
            index++;
        }
        builder.Append(']');
    }

    /// <summary>
    /// Numbers are safe integers or nothing.
    /// </summary>
    /// <remarks>
    /// The raw text is re-parsed rather than reprinted from the reader's own formatting so that
    /// <c>1.0</c>, <c>1e3</c> and <c>-0</c> are all refused: each of them is a number some writer
    /// spells differently, and the digest is over the spelling.
    /// </remarks>
    private static void WriteNumber(StringBuilder builder, JsonElement value, string path)
    {
        var raw = value.GetRawText();
        if (!long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)
            || raw.StartsWith("-0", StringComparison.Ordinal) && integer == 0)
        {
            throw new NonCanonicalValueException(
                path, $"{raw} is not an integer with one portable spelling");
        }
        if (integer is > MaxSafeInteger or < -MaxSafeInteger)
        {
            throw new NonCanonicalValueException(path, $"{raw} is outside the safe integer range");
        }
        builder.Append(integer.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <c>JSON.stringify</c>'s string escaping, which is narrower than what .NET writes by default.
    /// </summary>
    /// <remarks>
    /// Only the quote, the backslash and the control characters below U+0020 are escaped, and the
    /// five short forms are preferred where they exist. Everything else, non-ASCII included, is
    /// written literally: escaping more would still be valid JSON and a different byte sequence,
    /// which is a different digest.
    /// </remarks>
    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < ' ') builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
                    else builder.Append(character);
                    break;
            }
        }
        builder.Append('"');
    }

    /// <summary>2^53 - 1: the largest integer JavaScript holds exactly, and so the wire's ceiling.</summary>
    private const long MaxSafeInteger = 9_007_199_254_740_991;
}

/// <summary>A value with no portable JSON text, so no digest a peer could reproduce.</summary>
public sealed class NonCanonicalValueException(string path, string detail)
    : InvalidOperationException($"cannot canonicalize {(path.Length == 0 ? "value" : path)}: {detail}")
{
    /// <summary>Where in the document the offending value sits, e.g. <c>ops[0].gang</c>.</summary>
    public string Path { get; } = path;
}
