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
    /// The strictness the schemas have is kept where it belongs: a view is refused when it carries
    /// a field this build does not know, because that is a server this client cannot fully read.
    /// </summary>
    [Fact]
    public void StillRefusesAnUnknownFieldOnAStrictView()
    {
        Assert.Throws<MultiplayerProtocolException>(() => WireJson.Read<SnapshotView>("""
            {"turn":1,"formatVersion":18,"stateHash":"aaaa","uploadedByPlayerId":"p1",
             "uploadedAt":"2026-09-10T12:00:00.000Z","body":"QUJD","smuggled":true}
            """));
    }
}
