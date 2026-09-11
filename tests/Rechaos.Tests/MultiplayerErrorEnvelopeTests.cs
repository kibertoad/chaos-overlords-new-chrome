using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Every refusal the server makes arrives in one envelope, and the client branches on the reason
/// inside it rather than on the status: several refusals share a status and mean different things.
/// </summary>
public sealed class MultiplayerErrorEnvelopeTests
{
    [Fact]
    public void ReadsTheCodeMessageAndReason()
    {
        var envelope = WireJson.Read<ErrorEnvelope>("""
            {"error":{"code":"conflict","message":"The turn is not open","details":{"reason":"turn_not_open"},"requestId":"r1"}}
            """);

        Assert.Equal(ErrorCode.Conflict, envelope.Error.Code);
        Assert.Equal("The turn is not open", envelope.Error.Message);
        Assert.Equal("turn_not_open", envelope.Error.Details?.Reason);
        Assert.Equal("r1", envelope.Error.RequestId);
    }

    /// <summary>
    /// `details` is an open bag: the reason is the field a client branches on, and the contract
    /// validator puts its issue list beside it. Refusing the body over a field this build has never
    /// heard of would lose the reason too — the one thing the envelope exists to carry.
    /// </summary>
    [Fact]
    public void KeepsTheReasonWhenDetailsCarryMoreThanItDeclares()
    {
        var envelope = WireJson.Read<ErrorEnvelope>("""
            {"error":{"code":"validation_failed","message":"Request failed contract validation",
             "details":{"reason":"invalid_request","issues":[{"message":"expected a lowercase hex SHA-256","path":["stateHash"]}]},
             "requestId":"r2"}}
            """);

        Assert.Equal("invalid_request", envelope.Error.Details?.Reason);
        Assert.True(envelope.Error.Details?.AdditionalProperties?.ContainsKey("issues"));
    }

    /// <summary>A refusal with nothing to add carries no details at all, and that is not an error.</summary>
    [Fact]
    public void ReadsAnEnvelopeWithNoDetails()
    {
        var envelope = WireJson.Read<ErrorEnvelope>("""
            {"error":{"code":"unauthorized","message":"Send the player token as a Bearer credential"}}
            """);

        Assert.Equal(ErrorCode.Unauthorized, envelope.Error.Code);
        Assert.Null(envelope.Error.Details);
        Assert.Null(envelope.Error.RequestId);
    }

    /// <summary>
    /// An ordinary view tolerates a field this build does not know, and keeps the rest.
    /// </summary>
    /// <remarks>
    /// Forward compatibility where it costs nothing: the client does nothing with a field it cannot
    /// name, and refusing the whole view over one would lock every client out of a server that had
    /// grown it.
    /// </remarks>
    [Fact]
    public void SkipsAnUnknownFieldOnAView()
    {
        var view = WireJson.Read<SnapshotView>("""
            {"turn":1,"formatVersion":16,"stateHash":"aaaa","uploadedByPlayerId":"p1",
             "uploadedAt":"2026-09-10T12:00:00.000Z","body":"QUJD","smuggled":true}
            """);

        Assert.Equal(1, view.Turn);
        Assert.Equal("QUJD", view.Body);
    }

    /// <summary>
    /// A payload whose digest is recomputed here is still read exactly.
    /// </summary>
    /// <remarks>
    /// This is the one place strictness is load-bearing rather than defensive. The order documents in
    /// a sealed set are re-serialized to check the hash the seal announced, so a field dropped on the
    /// way in could not be written back out — the digest would not match however sound the set was.
    /// Refusing the field says which one; tolerating it would report a mismatch and explain nothing.
    /// </remarks>
    [Fact]
    public void StillRefusesAnUnknownFieldWhereTheDigestDependsOnIt()
    {
        Assert.Throws<MultiplayerProtocolException>(() => WireJson.ReadExact<SealedOrdersView>("""
            {"turn":1,"orderSetHash":"aaaa",
             "players":[{"playerId":"p1","slot":0,"ordersHash":"bbbb",
                         "orders":{"version":1,"ops":[],"smuggled":true}}]}
            """));
    }
}
