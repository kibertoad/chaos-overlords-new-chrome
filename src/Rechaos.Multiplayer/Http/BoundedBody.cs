using System.Buffers;
using System.Text;

namespace Rechaos.Multiplayer.Http;

/// <summary>
/// Reads a response body up to a ceiling, whatever client it arrived on.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HttpClient.MaxResponseContentBufferSize"/> belongs to the client, and the client is
/// the caller's: one built with <c>new HttpClient()</c> buffers two gigabytes before any parser
/// sees a byte. Sending with <see cref="HttpCompletionOption.ResponseHeadersRead"/> and reading the
/// body through here bounds it regardless of how the client was configured.
/// </para>
/// <para>
/// The buffer grows with what arrives rather than being allocated at the ceiling, so a three-field
/// receipt costs what it weighs, not the megabytes a legitimate snapshot would.
/// </para>
/// </remarks>
internal static class BoundedBody
{
    private const int ChunkBytes = 16 * 1024;

    /// <summary>
    /// The body as text, or null when it is longer than <paramref name="maximumBytes"/>.
    /// </summary>
    /// <remarks>
    /// Decoded as UTF-8 whatever charset the headers name. Every body this library reads is JSON,
    /// which is UTF-8 by definition, and a proxy page naming a charset .NET does not know would
    /// otherwise throw <see cref="InvalidOperationException"/> out of
    /// <see cref="HttpContent.ReadAsStringAsync()"/> — a failure no retry policy classifies.
    /// </remarks>
    internal static async Task<string?> ReadStringAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (content.Headers.ContentLength > maximumBytes) return null;

        await using var stream = await content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        try
        {
            while (true)
            {
                // One byte past the ceiling is all it takes to know the body is over it.
                var room = (int)Math.Min(chunk.Length, maximumBytes - buffer.Length + 1);
                var read = await stream.ReadAsync(chunk.AsMemory(0, room), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > maximumBytes) return null;
                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var preamble = Encoding.UTF8.Preamble;
        if (bytes.StartsWith(preamble)) bytes = bytes[preamble.Length..];
        return Encoding.UTF8.GetString(bytes);
    }
}
