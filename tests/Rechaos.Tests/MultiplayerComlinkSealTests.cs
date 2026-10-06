using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// The envelope a Comlink message travels in online (DEV-NET-001): only the recipient's key opens
/// it, only where it was sealed for, and every envelope has the same length whatever it holds.
/// </summary>
public sealed class MultiplayerComlinkSealTests
{
    private static readonly ComlinkSealContext Context = new("match-seal", 3, new PlayerId(0), new PlayerId(1));

    [Theory]
    [InlineData("A")]
    [InlineData("MEET AT DAWN, SECTOR 27.")]
    [InlineData(" LEADING SPACE")]
    public void TheRecipientOpensWhatWasSealedForIt(string text)
    {
        using var recipient = ComlinkKeyPair.Generate();

        var envelope = ComlinkSeal.Seal(text, recipient.PublicKey, Context);

        Assert.True(ComlinkEnvelope.IsWellFormed(envelope));
        Assert.Equal(ComlinkEnvelope.Characters, envelope.Length);
        Assert.Equal(text, ComlinkSeal.Open(envelope, recipient, Context));
    }

    [Fact]
    public void TheLongestMessageFitsAndTwoSealsOfOneTextDiffer()
    {
        using var recipient = ComlinkKeyPair.Generate();
        var text = new string('Z', MatchLimits.ComlinkMessageCharacters);

        var first = ComlinkSeal.Seal(text, recipient.PublicKey, Context);
        var second = ComlinkSeal.Seal(text, recipient.PublicKey, Context);

        Assert.NotEqual(first, second);
        Assert.Equal(first.Length, ComlinkSeal.Seal("A", recipient.PublicKey, Context).Length);
        Assert.Equal(text, ComlinkSeal.Open(first, recipient, Context));
        Assert.Equal(text, ComlinkSeal.Open(second, recipient, Context));
    }

    [Fact]
    public void AnotherKeyOrAnotherPlaceOpensNothing()
    {
        using var recipient = ComlinkKeyPair.Generate();
        using var other = ComlinkKeyPair.Generate();
        var envelope = ComlinkSeal.Seal("PRIVATE", recipient.PublicKey, Context);

        Assert.Null(ComlinkSeal.Open(envelope, other, Context));
        Assert.Null(ComlinkSeal.Open(envelope, recipient, Context with { MatchId = "match-other" }));
        Assert.Null(ComlinkSeal.Open(envelope, recipient, Context with { Turn = 4 }));
        Assert.Null(ComlinkSeal.Open(envelope, recipient, Context with { Sender = new PlayerId(2) }));
        Assert.Null(ComlinkSeal.Open(envelope, recipient, Context with { Recipient = new PlayerId(2) }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70)]
    [InlineData(239)]
    public void AnyChangedByteOpensNothing(int index)
    {
        using var recipient = ComlinkKeyPair.Generate();
        var bytes = Convert.FromBase64String(ComlinkSeal.Seal("PRIVATE", recipient.PublicKey, Context));
        bytes[index] ^= 0x01;

        Assert.Null(ComlinkSeal.Open(Convert.ToBase64String(bytes), recipient, Context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lower")]
    [InlineData("CAFÉ")]
    public void TextTheSendPanelCouldNotWriteIsNeverSealed(string text)
    {
        using var recipient = ComlinkKeyPair.Generate();

        Assert.False(ComlinkSeal.IsSendable(text));
        Assert.Throws<ArgumentException>(() => ComlinkSeal.Seal(text, recipient.PublicKey, Context));
    }

    [Fact]
    public void APrivateKeyRoundTripsAndOnlyP256PublicKeysAreAccepted()
    {
        using var original = ComlinkKeyPair.Generate();
        using var restored = ComlinkKeyPair.FromPrivateKey(original.ExportPrivateKey());
        using var p384 = System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP384);

        Assert.NotNull(restored);
        Assert.Equal(original.PublicKey, restored.PublicKey);
        Assert.Equal("PRIVATE", ComlinkSeal.Open(
            ComlinkSeal.Seal("PRIVATE", original.PublicKey, Context), restored, Context));
        Assert.Null(ComlinkKeyPair.FromPrivateKey("not a key"));
        Assert.Null(ComlinkKeyPair.FromPrivateKey(null));
        Assert.True(ComlinkKeyPair.IsPublicKey(original.PublicKey));
        Assert.False(ComlinkKeyPair.IsPublicKey(Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo())));
        Assert.False(ComlinkKeyPair.IsPublicKey(null));
        Assert.False(ComlinkKeyPair.IsPublicKey("MFkw"));
    }

    /// <summary>
    /// A seat a player left and another took is sealed to under the key its current holder
    /// published; a key announced after the roster was read replaces the one it had.
    /// </summary>
    [Fact]
    public void TheKeyringSealsToTheSeatsCurrentHolder()
    {
        using var own = ComlinkKeyPair.Generate();
        using var departed = ComlinkKeyPair.Generate();
        using var current = ComlinkKeyPair.Generate();
        using var renewed = ComlinkKeyPair.Generate();
        var keyring = new ComlinkKeyring("match-seal", new PlayerId(0), own);

        keyring.Learn([
            Player("p1", 0, WirePlayerStatus.Active, own.PublicKey),
            Player("p2", 1, WirePlayerStatus.Left, departed.PublicKey),
            Player("p3", 1, WirePlayerStatus.Active, current.PublicKey),
            Player("p4", 2, WirePlayerStatus.Active, null),
        ]);

        Assert.Equal(current.PublicKey, keyring.KeyFor(new PlayerId(1)));
        Assert.Null(keyring.KeyFor(new PlayerId(2)));
        Assert.Null(keyring.Seal(1, new PlayerId(0), [new PlayerId(1), new PlayerId(2)], "HI"));

        keyring.Learn("p3", renewed.PublicKey);
        keyring.Learn("p4", "not a key");

        Assert.Equal(renewed.PublicKey, keyring.KeyFor(new PlayerId(1)));
        Assert.Null(keyring.KeyFor(new PlayerId(2)));
        var letter = Assert.Single(keyring.Seal(1, new PlayerId(0), [new PlayerId(1)], "HI")!);
        Assert.Equal("HI", ComlinkSeal.Open(
            letter.Envelope, renewed, new ComlinkSealContext("match-seal", 1, new PlayerId(0), new PlayerId(1))));
    }

    [Fact]
    public void TheKeyringReadsItsOwnInboxAndPassesClearTextThrough()
    {
        using var own = ComlinkKeyPair.Generate();
        var keyring = new ComlinkKeyring("match-seal", new PlayerId(1), own);
        var envelope = ComlinkSeal.Seal("SEALED", own.PublicKey, Context);
        var sealedMessage = new ComlinkMessage(0, Context.Turn, Context.Sender, string.Empty, envelope);

        Assert.Equal("SEALED", keyring.Read(sealedMessage));
        Assert.Equal("SEALED", keyring.Read(sealedMessage));
        Assert.Null(keyring.Read(sealedMessage with { Turn = 9 }));
        Assert.Equal("CLEAR", keyring.Read(new ComlinkMessage(1, 3, new PlayerId(0), "CLEAR")));
    }

    private static PlayerView Player(string id, int slot, WirePlayerStatus status, string? key) =>
        new(id, slot, id.ToUpperInvariant(), PortraitId: slot, Status: status, IsHost: slot == 0, ComlinkKey: key);
}
