using System.Text.Json;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The digest a client verifies is taken over canonical JSON, so this side has to produce the
/// server's bytes exactly. The vectors are the ones pinned in
/// <c>multiplayer/packages/kernel/test/logic.spec.ts</c>; if one ever changes, both change.
/// </summary>
public sealed class MultiplayerCanonicalJsonTests
{
    private static string Canonical(string json)
    {
        using var document = JsonDocument.Parse(json);
        return CanonicalJson.Encode(document.RootElement);
    }

    [Fact]
    public void SortsKeysByCodeUnitTheWayOrdinalComparisonDoes()
    {
        Assert.Equal(
            """{"A":3,"Z":1,"a":2,"Ä":5,"é":4}""",
            Canonical("""{"Z":1,"a":2,"A":3,"é":4,"Ä":5}"""));
    }

    [Fact]
    public void WritesNoWhitespaceAndNestsInTheSameOrder()
    {
        Assert.Equal(
            """{"a":[1,{"b":false,"c":null}],"d":"x"}""",
            Canonical("""{ "d": "x", "a": [ 1, { "c": null, "b": false } ] }"""));
    }

    /// <summary>
    /// Only integers, booleans, null and JSON-escaped strings have one spelling every language
    /// agrees on. A float's shortest round-trip text does not: JavaScript writes <c>1e+21</c> where
    /// .NET writes <c>1E+21</c>, and a digest is a digest of the spelling.
    /// </summary>
    [Theory]
    [InlineData("""{"x":1.5}""")]
    [InlineData("""{"x":1e21}""")]
    [InlineData("""{"x":-0}""")]
    [InlineData("""{"x":1.0}""")]
    [InlineData("""{"x":9007199254740993}""")]
    public void RefusesValuesAnotherLanguageWouldWriteDifferently(string json)
    {
        Assert.Throws<NonCanonicalValueException>(() => Canonical(json));
    }

    /// <summary>
    /// .NET escapes far more than JavaScript by default, and every extra escape is a different byte
    /// sequence and so a different digest.
    /// </summary>
    [Fact]
    public void EscapesExactlyWhatJavaScriptEscapesAndNothingElse()
    {
        Assert.Equal(
            "{\"k\":\"a\\\"b\\\\c\\nd\\u0001e<f&gé\"}",
            Canonical("{\"k\":\"a\\\"b\\\\c\\nd\\u0001e<f&g\\u00e9\"}"));
    }

    /// <summary>
    /// The golden document, its canonical text and its digest, pinned on both sides of the wire.
    /// This is what makes a C# client able to verify a set a TypeScript server sealed.
    /// </summary>
    [Fact]
    public void ReproducesTheServersGoldenOrderDigest()
    {
        var document = new OrderDocument(1, [
            new SubmitCommandOp(0, 7, 10, new SectorTarget(27), Repeat: false, SecondaryTarget: null),
            new QueueHireOp(0, 44, 27),
        ]);

        Assert.Equal(
            """{"ops":[{"action":10,"gang":7,"op":"submitCommand","player":0,"repeat":false,"secondaryTarget":null,"target":{"id":27,"kind":"sector"}},{"gangDefinitionId":44,"op":"queueHire","player":0,"sectorId":27}],"schemaVersion":1}""",
            OrderDigest.CanonicalTextOf(document));
        Assert.Equal(
            "1e8d923be158821a974c60903be48d010f7499bdadc3510f6ea3714abadd6631",
            OrderDigest.OfDocument(document));
    }

    /// <summary>Two documents that differ only in serializer ordering are the same document.</summary>
    [Fact]
    public void HashesEqualDocumentsEquallyWhateverOrderTheyWereWrittenIn()
    {
        Assert.Equal(
            Canonical("""{"schemaVersion":1,"ops":[{"op":"x","args":{"a":1,"b":2}}]}"""),
            Canonical("""{"ops":[{"args":{"b":2,"a":1},"op":"x"}],"schemaVersion":1}"""));
    }

    /// <summary>
    /// The set digest folds the per-player ones in slot order, so a client can check a fetched set
    /// against the digest the seal announced without trusting the order it arrived in.
    /// </summary>
    [Fact]
    public void FoldsTheSealedSetInSlotOrderWhateverOrderItArrivesIn()
    {
        var first = new SealedPlayerOrders("p1", 0, Document(1), OrderDigest.OfDocument(Document(1)));
        var second = new SealedPlayerOrders("p2", 1, Document(2), OrderDigest.OfDocument(Document(2)));

        Assert.Equal(OrderDigest.OfSet([first, second]), OrderDigest.OfSet([second, first]));

        var view = new SealedOrdersView(1, OrderDigest.OfSet([first, second]), [first, second]);
        Assert.True(OrderDigest.Verifies(view, view.OrderSetHash));
        Assert.False(OrderDigest.Verifies(view, OrderDigest.OfSet([first])));
    }

    /// <summary>
    /// A set whose documents were swapped for others, with the per-player digests recomputed to
    /// match, still fails: the fold is over the digests the seal froze, not over whatever arrived.
    /// </summary>
    [Fact]
    public void RefusesASetWhoseDocumentsWereSubstituted()
    {
        var honest = new SealedPlayerOrders("p1", 0, Document(1), OrderDigest.OfDocument(Document(1)));
        var announced = OrderDigest.OfSet([honest]);
        var swapped = new SealedPlayerOrders("p1", 0, Document(9), OrderDigest.OfDocument(Document(9)));

        Assert.False(OrderDigest.Verifies(new SealedOrdersView(1, announced, [swapped]), announced));
    }

    private static OrderDocument Document(int gang) =>
        new(1, [new CancelCommandOp(0, gang)]);
}
