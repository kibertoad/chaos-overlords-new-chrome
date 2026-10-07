using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Rechaos.Core.GameModel;

namespace Rechaos.Multiplayer.Protocol;

/// <summary>
/// Where a sealed Comlink message belongs: the match, the turn it is sent in, its sender and the
/// one recipient its envelope is for.
/// </summary>
/// <remarks>
/// It is bound into the envelope as associated data, so an envelope copied into another match,
/// another turn, another sender's op or another recipient's letter no longer opens. The sender can
/// only send from its own slot (the server refuses anything else), which makes this the guarantee
/// that a message shown as from a player was sealed by that player's client for this reader.
/// </remarks>
/// <param name="MatchId">The server's id for the match.</param>
/// <param name="Turn">The turn the message is sent in, which is the turn its inbox entry records.</param>
/// <param name="Sender">The seat that sent it.</param>
/// <param name="Recipient">The seat this envelope is for.</param>
public readonly record struct ComlinkSealContext(
    string MatchId,
    int Turn,
    PlayerId Sender,
    PlayerId Recipient);

/// <summary>
/// Seals a Comlink message for one recipient, and opens one sealed for this client.
/// </summary>
/// <remarks>
/// <para>
/// ECIES over P-256: a fresh ephemeral key per envelope, ECDH with the recipient's published key,
/// HKDF-SHA256 to an AES-256 key and a GCM nonce, and AES-256-GCM over the text padded with zero
/// bytes to 160. The envelope is the ephemeral point (64 bytes, X then Y), the ciphertext (160) and
/// the tag (16): 240 bytes, the same for every message, so its length says nothing about the text.
/// Each derived key seals exactly one envelope, which is why the nonce can be derived with it.
/// </para>
/// <para>
/// Every primitive is in the .NET base library on every platform the game ships for, so this adds no
/// native dependency. The server never runs any of it; it checks only the envelope's length and
/// alphabet. See docs/MULTIPLAYER.md, "Comlink privacy", for what this does and does not protect.
/// </para>
/// </remarks>
public static class ComlinkSeal
{
    private const int CoordinateBytes = 32;
    private const int PointBytes = 2 * CoordinateBytes;
    private const int TextBytes = MatchLimits.ComlinkMessageCharacters;
    private const int TagBytes = 16;
    private const int KeyBytes = 32;
    private const int NonceBytes = 12;

    private static readonly byte[] Label = "chaos-overlords comlink 1"u8.ToArray();

    /// <summary>
    /// Seals <paramref name="text"/> for the holder of <paramref name="recipientPublicKey"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The text is not one the Send panel could have written (RULE-COMLINK-006), or it is blank,
    /// which RULE-COMLINK-003 never stores; or the key is not a P-256 public key.
    /// </exception>
    public static string Seal(string text, string recipientPublicKey, ComlinkSealContext context)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsSendable(text))
            throw new ArgumentException("A sealed Comlink message holds 1 to 160 characters from space to Z.", nameof(text));
        var recipientKey = ComlinkKeyPair.DecodePublicKey(recipientPublicKey);
        using var recipient = ComlinkKeyPair.ImportPublicKey(recipientKey);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = ephemeral.ExportParameters(includePrivateParameters: false).Q;
        var envelope = new byte[ComlinkEnvelope.Bytes];
        point.X.AsSpan().CopyTo(envelope.AsSpan(0, CoordinateBytes));
        point.Y.AsSpan().CopyTo(envelope.AsSpan(CoordinateBytes, CoordinateBytes));
        var secret = ephemeral.DeriveRawSecretAgreement(recipient.PublicKey);
        var plaintext = new byte[TextBytes];
        try
        {
            Encoding.ASCII.GetBytes(text, plaintext);
            using var cipher = Cipher(secret, envelope.AsSpan(0, PointBytes), recipientKey, out var nonce);
            cipher.Encrypt(
                nonce,
                plaintext,
                envelope.AsSpan(PointBytes, TextBytes),
                envelope.AsSpan(PointBytes + TextBytes, TagBytes),
                AssociatedData(context));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(plaintext);
        }
        return Convert.ToBase64String(envelope);
    }

    /// <summary>
    /// Opens an envelope sealed for <paramref name="recipient"/>, or answers null.
    /// </summary>
    /// <remarks>
    /// Null whenever the envelope does not open to a message the Send panel could have written: it
    /// was sealed to another key, it was moved from where <paramref name="context"/> says it belongs,
    /// it was tampered with, or a modified client sealed text the game would never send. Every
    /// client stores the same envelope either way, so what this answers changes only what this
    /// client shows.
    /// </remarks>
    public static string? Open(string envelope, ComlinkKeyPair recipient, ComlinkSealContext context)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        if (!ComlinkEnvelope.IsWellFormed(envelope)) return null;
        var bytes = Convert.FromBase64String(envelope);
        var plaintext = new byte[TextBytes];
        byte[]? secret = null;
        try
        {
            using var ephemeral = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = bytes[..CoordinateBytes],
                    Y = bytes[CoordinateBytes..PointBytes]
                }
            });
            secret = recipient.Key.DeriveRawSecretAgreement(ephemeral.PublicKey);
            using var cipher = Cipher(
                secret, bytes.AsSpan(0, PointBytes), recipient.PublicKeyBytes, out var nonce);
            cipher.Decrypt(
                nonce,
                bytes.AsSpan(PointBytes, TextBytes),
                bytes.AsSpan(PointBytes + TextBytes, TagBytes),
                plaintext,
                AssociatedData(context));
            return Unpad(plaintext);
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            // A point off the curve (which Windows reports as an unsupported curve), another key,
            // or a tag that does not match.
            return null;
        }
        finally
        {
            if (secret is not null) CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a message the Send panel could send and the inbox would
    /// store: 1 to 160 characters from space to <c>Z</c> (RULE-COMLINK-006), not all spaces
    /// (RULE-COMLINK-003).
    /// </summary>
    public static bool IsSendable(string text) =>
        text.Length is > 0 and <= TextBytes
        && IsComlinkCharacters(text)
        && !MatchState.IsBlankComlinkDraft(text);

    /// <summary>Whether every character is one the Send panel types, space to <c>Z</c> (RULE-COMLINK-006).</summary>
    public static bool IsComlinkCharacters(string text) =>
        text.All(character => character is >= ' ' and <= 'Z');

    /// <summary>The AES-GCM cipher and nonce one envelope is sealed under.</summary>
    /// <remarks>
    /// The salt binds the derived key to the ephemeral point and the recipient's public key, so the
    /// same shared secret can never be reached for another pair.
    /// </remarks>
    private static AesGcm Cipher(
        byte[] secret,
        ReadOnlySpan<byte> ephemeralPoint,
        ReadOnlySpan<byte> recipientPublicKey,
        out byte[] nonce)
    {
        var salt = new byte[ephemeralPoint.Length + recipientPublicKey.Length];
        ephemeralPoint.CopyTo(salt);
        recipientPublicKey.CopyTo(salt.AsSpan(ephemeralPoint.Length));
        var material = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, KeyBytes + NonceBytes, salt, Label);
        try
        {
            nonce = material[KeyBytes..];
            return new AesGcm(material.AsSpan(0, KeyBytes), TagBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material.AsSpan(0, KeyBytes));
        }
    }

    private static byte[] AssociatedData(ComlinkSealContext context)
    {
        ArgumentException.ThrowIfNullOrEmpty(context.MatchId);
        var match = Encoding.UTF8.GetBytes(context.MatchId);
        var data = new byte[Label.Length + 4 + match.Length + 4 + 1 + 1];
        var offset = 0;
        Label.CopyTo(data, offset);
        offset += Label.Length;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), match.Length);
        offset += 4;
        match.CopyTo(data, offset);
        offset += match.Length;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), context.Turn);
        offset += 4;
        data[offset++] = checked((byte)context.Sender.Value);
        data[offset] = checked((byte)context.Recipient.Value);
        return data;
    }

    /// <summary>The text inside a padded plaintext, or null when it is not one the game sends.</summary>
    private static string? Unpad(ReadOnlySpan<byte> plaintext)
    {
        var length = plaintext.IndexOf((byte)0);
        if (length < 0) length = plaintext.Length;
        if (plaintext[length..].ContainsAnyExcept((byte)0)) return null;
        var text = Encoding.ASCII.GetString(plaintext[..length]);
        return IsSendable(text) ? text : null;
    }
}

/// <summary>
/// One seat's Comlink key pair: the private half stays with the player's client, the public half is
/// published for the other seats to seal messages to.
/// </summary>
/// <remarks>
/// P-256, which every platform's base cryptography library carries. The private key is kept as
/// PKCS#8 in the client's recovery record beside the membership token, because a seat resumed
/// without it can no longer open what was sealed to it.
/// </remarks>
public sealed class ComlinkKeyPair : IDisposable
{
    /// <summary>
    /// The 27 bytes every P-256 SubjectPublicKeyInfo starts with: the algorithm, the curve and the
    /// uncompressed-point marker. The server holds published keys to the same prefix.
    /// </summary>
    private static readonly byte[] PublicKeyPrefix = Convert.FromHexString(
        "3059301306072A8648CE3D020106082A8648CE3D03010703420004");

    /// <summary>The bytes of a P-256 SubjectPublicKeyInfo.</summary>
    private const int PublicKeyLength = 91;

    private ComlinkKeyPair(ECDiffieHellman key)
    {
        Key = key;
        PublicKeyBytes = key.ExportSubjectPublicKeyInfo();
        PublicKey = Convert.ToBase64String(PublicKeyBytes);
    }

    internal ECDiffieHellman Key { get; }

    internal byte[] PublicKeyBytes { get; }

    /// <summary>The public half, as the server stores it and the other seats seal to it.</summary>
    public string PublicKey { get; }

    /// <summary>A fresh key pair, for a seat that has none yet.</summary>
    public static ComlinkKeyPair Generate() =>
        new(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256));

    /// <summary>The key pair a recovery record kept, or null when what it kept is not one.</summary>
    public static ComlinkKeyPair? FromPrivateKey(string? privateKey)
    {
        if (string.IsNullOrEmpty(privateKey)) return null;
        ECDiffieHellman? key = null;
        try
        {
            key = ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out var read);
            var pair = new ComlinkKeyPair(key);
            if (pair.PublicKeyBytes.Length == PublicKeyLength
                && pair.PublicKeyBytes.AsSpan().StartsWith(PublicKeyPrefix))
            {
                return pair;
            }
            pair.Dispose();
            return null;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            key?.Dispose();
            return null;
        }
    }

    /// <summary>The private half as PKCS#8 in base64, for the recovery record.</summary>
    public string ExportPrivateKey() => Convert.ToBase64String(Key.ExportPkcs8PrivateKey());

    /// <summary>Whether <paramref name="publicKey"/> is a P-256 public key this client can seal to.</summary>
    public static bool IsPublicKey(string? publicKey)
    {
        try
        {
            using var key = ImportPublicKey(DecodePublicKey(publicKey));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The bytes of a published key, held to the one shape the server accepts.</summary>
    internal static byte[] DecodePublicKey(string? publicKey)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(publicKey ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("A Comlink key is base64.", nameof(publicKey), exception);
        }
        if (bytes.Length != PublicKeyLength || !bytes.AsSpan().StartsWith(PublicKeyPrefix))
            throw new ArgumentException("A Comlink key is a P-256 SubjectPublicKeyInfo.", nameof(publicKey));
        return bytes;
    }

    /// <summary>A published key, imported; a point off the curve is refused here.</summary>
    internal static ECDiffieHellman ImportPublicKey(byte[] publicKey)
    {
        var key = ECDiffieHellman.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(publicKey, out _);
            return key;
        }
        catch (CryptographicException exception)
        {
            key.Dispose();
            throw new ArgumentException("A Comlink key is not a point on P-256.", nameof(publicKey), exception);
        }
    }

    public void Dispose() => Key.Dispose();
}
