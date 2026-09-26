using Xunit;

namespace Rechaos.Tests;

public sealed class SpecHashTests
{
    // XXH3_128bits of empty input in canonical byte order, as `xxhsum -H2` prints it.
    private const string Empty = "99aa06d3014798d86001c324468d497f";

    [Fact]
    public void HashesInCanonicalByteOrder() => Assert.Equal(Empty, SpecHash.Xxh3([]));

    [Fact]
    public void StreamAndSpanGiveTheSameHash()
    {
        var bytes = Enumerable.Range(0, 5000).Select(value => (byte)value).ToArray();

        Assert.Equal(SpecHash.Xxh3(bytes), SpecHash.Xxh3(new MemoryStream(bytes)));
    }
}
