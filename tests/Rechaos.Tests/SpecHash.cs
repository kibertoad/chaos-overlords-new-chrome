using System.IO.Hashing;

namespace Rechaos.Tests;

/// <summary>
/// The hash the documentation standard uses for every file it names: the 128-bit form of xxHash3
/// (<c>XXH3_128bits</c>, which <c>xxhsum -H2</c> prints), as 32 lower-case hex digits in the byte
/// order of its canonical form.
/// </summary>
public static class SpecHash
{
    public static string Xxh3(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(XxHash128.Hash(bytes));

    public static string Xxh3(Stream stream)
    {
        var hash = new XxHash128();
        hash.Append(stream);
        return Convert.ToHexStringLower(hash.GetCurrentHash());
    }
}
