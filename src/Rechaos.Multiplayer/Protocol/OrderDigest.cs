using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Protocol;

/// <summary>
/// The digests that let a client check what it fetched against what the seal announced.
/// </summary>
/// <remarks>
/// A player's <c>ordersHash</c> is SHA-256 over the canonical JSON of that player's document, and
/// the turn's <c>orderSetHash</c> is SHA-256 over <c>slot:ordersHash</c> lines joined by newlines in
/// slot order. Both are the server's definitions; reproducing them here is the whole point of
/// <see cref="CanonicalJson"/>.
/// </remarks>
public static class OrderDigest
{
    /// <summary>The digest of one player's order document.</summary>
    public static string OfDocument(OrderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var parsed = JsonDocument.Parse(WireJson.Write(document));
        return Sha256Hex(CanonicalJson.EncodeUtf8(parsed.RootElement));
    }

    /// <summary>The canonical text of a document, for a diagnostic that has to show the bytes.</summary>
    public static string CanonicalTextOf(OrderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var parsed = JsonDocument.Parse(WireJson.Write(document));
        return CanonicalJson.Encode(parsed.RootElement);
    }

    /// <summary>The digest of a sealed set, from the per-player digests it names.</summary>
    public static string OfSet(IEnumerable<SealedPlayerOrders> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var lines = players
            .OrderBy(entry => entry.Slot)
            .Select(entry => $"{entry.Slot}:{entry.OrdersHash}");
        return Sha256Hex(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
    }

    /// <summary>
    /// Whether a fetched sealed set is the one the seal announced.
    /// </summary>
    /// <remarks>
    /// Both halves are checked: each player's document has to hash to the <c>ordersHash</c> beside
    /// it, and those hashes have to fold to <paramref name="announcedOrderSetHash"/>. Checking only
    /// the fold would accept a set whose documents were swapped for others with the same digests
    /// recomputed, which is exactly the substitution the fold exists to catch.
    /// </remarks>
    public static bool Verifies(SealedOrdersView sealedOrders, string announcedOrderSetHash)
    {
        ArgumentNullException.ThrowIfNull(sealedOrders);
        foreach (var entry in sealedOrders.Players)
        {
            if (!DigestsMatch(OfDocument(entry.Orders), entry.OrdersHash)) return false;
        }
        return DigestsMatch(OfSet(sealedOrders.Players), announcedOrderSetHash);
    }

    /// <summary>Lowercase hex SHA-256, the only digest spelling the protocol carries.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// Whether two digests are the same.
    /// </summary>
    /// <remarks>
    /// An ordinary comparison, deliberately. Both sides are digests the server publishes to every
    /// member of the match, so there is no secret here for a timing difference to leak — and a
    /// constant-time compare of a public value costs two allocations per check while implying one.
    /// </remarks>
    private static bool DigestsMatch(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
