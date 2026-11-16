using Rechaos.Extractor.Installer;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The LZMA and LZMA2 decoder the import uses to read GOG's installer, checked against streams
/// that liblzma (Python's <c>lzma</c> module) wrote from <see cref="Sample"/>.
/// </summary>
public sealed class LzmaDecoderTests
{
    // Inno Setup's five property bytes (lc 3, lp 0, pb 2, a 1 MiB dictionary), then the raw LZMA
    // stream liblzma wrote for Sample(3000), which ends with an end marker.
    private const string Lzma = "XQAAEAAAOhoABXInoJ9cmmYQd6+qOmQN5ZgL2DWWL1qA3F/qPygd6AxTZfI0PL6so/S7yM0VQYef9eBQBcfyGlIYU9GoTVgJfMp2dcKxj+HIG6XW4mcqphFiz+cEsiNeP3zfxFUhyznsWLX2JJ48j1ppD+uZ0aTYOF5jHGohM2R5Vy4Vk0nnEM9lmayagIUbNkjV57NEznp+skc59sD/nXEiaY6DwqobzmO8SZ+mfZsduMqQ52agjiZatXSzmVEc91YT4ivMFxL0wy/OKd+m7vE2A5vmKcgbXZY7IPiAAP80bUcaAUsUG/D5eQsn0eHYNP5/ahsuAuUz5UtS9Y3osn1U8K+yU52cJWc7QBZPi8+TD4nGpVP5j6ydhSsQu6Y4j1i2DCwS78zwja1dEDpKrUT7LEtn9gy/6hYvHNuzfR6GnSDakNv8I6NYNdTcMGUU1Xz964C4bca0KHJPqsXKgDpz4UeTiZYKVEAic/l6Jg3RVthaDiyxr1MCvm8lvJKy4hkFK79wdf/xeeMz3r7fsqd4cN627isq52H3B3Av88lXEF3WGZ2Xnr9x99K44HQQquZiI6sj4XudelazxDaZnR94dB4duDTHnngtG59xr0qzpqxXBqtUfu+3oDX5pmiVQDogbhWphLDhy37XgeAbaJtjK5FIhTrwWXkWduffTdLiz9tpbmn916UsaFOBBt0LLLXczuF9jePqBELzROUQXf0bfCvOOnw4GFvmLEUmCnmX2BJeCJX4GyMl4KEYvPoXqJz0ZmPvZbgmOIDpx1x04HFITvtgyQmQA4Aat1aM07zNHMBEW/YnTFtaTaiI9lDohFjZLaffUkDEgEeozv/IxzJY8uuq47Y4odjQlyvbWTWfxc/Ug2LYhw0OKQr6bLofagEIUxk6PByJ4mnQMp+kyAsPtyTmQd3WoNeOhtlUVFvUObCJ7k5b3hZwDW/pzOWbMD5HEQOA4gjiyKbE+oTQlfRRL8482GvH5HhfSAJ2J3g42kR3NrTxIzYq4XLm/YXqlKe4ovddMkmJ8NHPwK3Gt7Pzcs7DhzbFGIACqVGxyL91U2iFfADTo9GxVgqb9/DvYfFhutvCKgX/XglPCYg1NTDLA4iAJs7calPEkoleQNnuS5Cg+0hkYZxXMWjuYYeeGTqegV8RiuRYMfx5GrYm6owDX49Ro9fIcp/Iyzke1IiawmPioXp6+TUaFASuvTlUUVHAeKVwwqafhQ/md6Ikkte+2reqYYmNI126VUnx8OmQ8wbVHXSLHb/9oDNI";

    // The LZMA2 dictionary property for 1 MiB, then the raw LZMA2 stream liblzma wrote for
    // Sample(3000): one compressed chunk that resets the dictionary, and the end byte.
    private const string Lzma2 = "EOALtwOyXQA6GgAFciegn1yaZhB3r6o6ZA3lmAvYNZYvWoDcX+o/KB3oDFNl8jQ8vqyj9LvIzRVBh5/14FAFx/IaUhhT0ahNWAl8ynZ1wrGP4cgbpdbiZyqmEWLP5wSyI14/fN/EVSHLOexYtfYknjyPWmkP65nRpNg4XmMcaiEzZHlXLhWTSecQz2WZrJqAhRs2SNXns0TOen6yRzn2wP+dcSJpjoPCqhvOY7xJn6Z9mx24ypDnZqCOJlq1dLOZURz3VhPiK8wXEvTDL84p36bu8TYDm+YpyBtdljsg+IAA/zRtRxoBSxQb8Pl5CyfR4dg0/n9qGy4C5TPlS1L1jeiyfVTwr7JTnZwlZztAFk+Lz5MPicalU/mPrJ2FKxC7pjiPWLYMLBLvzPCNrV0QOkqtRPssS2f2DL/qFi8c27N9HoadINqQ2/wjo1g11NwwZRTVfP3rgLhtxrQock+qxcqAOnPhR5OJlgpUQCJz+XomDdFW2FoOLLGvUwK+byW8krLiGQUrv3B1//F54zPevt+yp3hw3rbuKyrnYfcHcC/zyVcQXdYZnZeev3H30rjgdBCq5mIjqyPhe516VrPENpmdH3h0Hh24NMeeeC0bn3GvSrOmrFcGq1R+77egNfmmaJVAOiBuFamEsOHLfteB4Btom2MrkUiFOvBZeRZ2599N0uLP22luaf3XpSxoU4EG3QsstdzO4X2N4+oEQvNE5RBd/Rt8K846fDgYW+YsRSYKeZfYEl4IlfgbIyXgoRi8+heonPRmY+9luCY4gOnHXHTgcUhO+2DJCZADgBq3VozTvM0cwERb9idMW1pNqIj2UOiEWNktp99SQMSAR6jO/8jHMljy66rjtjih2NCXK9tZNZ/Fz9SDYtiHDQ4pCvpsuh9qAQhTGTo8HIniadAyn6TICw+3JOZB3dag146G2VRUW9Q5sInuTlveFnANb+nM5ZswPkcRA4DiCOLIpsT6hNCV9FEvzjzYa8fkeF9IAnYneDjaRHc2tPEjNirhcub9heqUp7ii910ySYnw0c/Arca3s/NyzsOHNsUYgAKpUbHIv3VTaIV8ANOj0bFWCpv38O9h8WG628IqBf9eCU8JiDU1MMsDiIAmztxqU8SSiV5A2e5LkKD7SGRhnFcxaO5hh54ZOp6BXxGK5Fgx/HkatibqjANfj1Gj18hyn8jLOR7UiJrCY+Khenr5NRoUBK69OVRRUcB4pXDCpp+FD+Z3oiSS177at6phiY0jXbpVSfHw6ZDzBtUYYI0AAA==";

    [Fact]
    public void DecodesAnLzmaStreamWithInnoSetupsProperties()
    {
        Assert.Equal(Sample(3000), LzmaDecoder.DecodeLzma(Convert.FromBase64String(Lzma), 1 << 20));
    }

    [Fact]
    public void AnLzmaStreamCutShortYieldsTheSymbolsBeforeTheCut()
    {
        var stream = Convert.FromBase64String(Lzma);
        var decoded = LzmaDecoder.DecodeLzma(stream.AsSpan(0, stream.Length / 2), 1 << 20);

        Assert.NotEmpty(decoded);
        Assert.True(decoded.Length < 3000);
        Assert.Equal(Sample(3000)[..decoded.Length], decoded);
    }

    [Fact]
    public void AnLzmaStreamLongerThanTheLimitIsRefused()
    {
        Assert.Throws<InvalidDataException>(() =>
            LzmaDecoder.DecodeLzma(Convert.FromBase64String(Lzma), 2999));
    }

    [Fact]
    public void DecodesAnLzma2Stream()
    {
        Assert.Equal(Sample(3000), LzmaDecoder.DecodeLzma2(Convert.FromBase64String(Lzma2), 1 << 20));
    }

    [Fact]
    public void DecodesUncompressedLzma2ChunksAndAnLzmaChunkAfterThem()
    {
        var compressed = Convert.FromBase64String(Lzma2);
        var stored = "stored before the compressed chunk."u8.ToArray();
        // The compressed chunk resets the dictionary (0xE0); after a stored chunk it keeps it
        // instead and only resets the state and properties (0xC0), with the same bits otherwise.
        // LZMA's contexts depend on the position modulo 4 and on the byte before, so the stored
        // bytes come to 36 and end with a newline, which gives the same contexts as a start.
        var lzmaChunk = compressed[1..];
        lzmaChunk[0] = (byte)(0xC0 | (lzmaChunk[0] & 0x1F));
        byte[] stream =
        [
            compressed[0],
            0x01, 0x00, (byte)(stored.Length - 1), .. stored,
            0x02, 0x00, 0x00, (byte)'\n',
            .. lzmaChunk
        ];

        var decoded = LzmaDecoder.DecodeLzma2(stream, 1 << 20);

        Assert.Equal([.. stored, (byte)'\n', .. Sample(3000)], decoded);
    }

    [Fact]
    public void AnLzma2StreamThatDoesNotStartWithADictionaryResetIsRefused()
    {
        var compressed = Convert.FromBase64String(Lzma2);
        compressed[1] = (byte)(0xC0 | (compressed[1] & 0x1F));

        Assert.Throws<InvalidDataException>(() => LzmaDecoder.DecodeLzma2(compressed, 1 << 20));
    }

    [Fact]
    public void ATruncatedLzma2StreamIsRefused()
    {
        var compressed = Convert.FromBase64String(Lzma2);

        Assert.Throws<InvalidDataException>(() =>
            LzmaDecoder.DecodeLzma2(compressed.AsSpan(0, compressed.Length - 40), 1 << 20));
    }

    /// <summary>Runs of repeated text, which LZMA codes as matches, between runs of noise, which it codes as literals.</summary>
    private static byte[] Sample(int length)
    {
        var text = "the gang moves into the next sector and the sector pays its tax "u8;
        var bytes = new byte[length];
        var state = 12345u;
        for (var i = 0; i < length; i++)
        {
            if (i / 400 % 3 == 2)
            {
                state = unchecked(state * 1103515245 + 12345);
                bytes[i] = (byte)(state >> 16);
            }
            else
            {
                bytes[i] = text[i * 3 / 2 % text.Length];
            }
        }
        return bytes;
    }
}
