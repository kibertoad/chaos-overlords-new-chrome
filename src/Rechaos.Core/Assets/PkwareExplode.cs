namespace Rechaos.Core.Assets;

/// <summary>
/// Expands a PKWARE Data Compression Library implode stream, the compression of every block of
/// <c>DATA/DATA.Z</c> (FMT-DATA-005, FND-DATA-010). The decoder follows the published description
/// of the format that zlib's <c>contrib/blast</c> implements.
/// </summary>
public static class PkwareExplode
{
    private const int MaximumCodeBits = 13;
    private const int EndOfStreamLength = 519;

    // The fixed Huffman code lengths, each byte a length in its low four bits and a repeat
    // count less one in its high four.
    private static readonly byte[] LiteralLengths =
    [
        11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8, 9, 7, 6, 7, 8, 7, 6,
        55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5, 7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7,
        9, 8, 25, 11, 8, 11, 9, 12, 8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24,
        10, 27, 44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45, 44, 173
    ];
    private static readonly byte[] LengthLengths = [2, 35, 36, 53, 38, 23];
    private static readonly byte[] DistanceLengths = [2, 20, 53, 230, 247, 151, 248];
    private static readonly short[] LengthBase = [3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264];
    private static readonly byte[] LengthExtra = [0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly Huffman LiteralCode = new(LiteralLengths, 256);
    private static readonly Huffman LengthCode = new(LengthLengths, 16);
    private static readonly Huffman DistanceCode = new(DistanceLengths, 64);

    /// <summary>The expanded bytes, and how many bytes of <paramref name="source"/> the stream used.</summary>
    public static (byte[] Output, int Consumed) Expand(ReadOnlySpan<byte> source)
    {
        var reader = new BitReader(source);
        var codedLiterals = reader.Bits(8);
        var dictionaryBits = reader.Bits(8);
        if (codedLiterals > 1) throw new InvalidDataException("An implode stream codes its literals with 0 or 1.");
        if (dictionaryBits is < 4 or > 6) throw new InvalidDataException("An implode dictionary has 4 to 6 bits.");
        var output = new List<byte>(source.Length * 4);
        while (true)
        {
            if (reader.Bits(1) != 0)
            {
                var symbol = LengthCode.Decode(ref reader);
                var length = LengthBase[symbol] + reader.Bits(LengthExtra[symbol]);
                if (length == EndOfStreamLength) break;
                var low = length == 2 ? 2 : dictionaryBits;
                var distance = (DistanceCode.Decode(ref reader) << low) + reader.Bits(low) + 1;
                if (distance > output.Count)
                    throw new InvalidDataException("An implode copy reaches before the start of the output.");
                var start = output.Count - distance;
                for (var index = 0; index < length; index++)
                    output.Add(output[start + index]);
            }
            else
            {
                output.Add((byte)(codedLiterals == 1 ? LiteralCode.Decode(ref reader) : reader.Bits(8)));
            }
        }
        return (output.ToArray(), reader.Consumed);
    }

    private ref struct BitReader(ReadOnlySpan<byte> source)
    {
        private readonly ReadOnlySpan<byte> _source = source;
        private int _next;
        private int _buffer;
        private int _count;

        public readonly int Consumed => _next;

        public int Bits(int need)
        {
            var value = _buffer;
            while (_count < need)
            {
                if (_next >= _source.Length) throw new InvalidDataException("The implode stream ends early.");
                value |= _source[_next++] << _count;
                _count += 8;
            }
            _buffer = value >> need;
            _count -= need;
            return value & ((1 << need) - 1);
        }
    }

    private sealed class Huffman
    {
        private readonly short[] _count = new short[MaximumCodeBits + 1];
        private readonly short[] _symbol;

        public Huffman(byte[] compact, int symbols)
        {
            var lengths = new byte[symbols];
            var next = 0;
            foreach (var entry in compact)
                for (var repeat = (entry >> 4) + 1; repeat > 0; repeat--)
                    lengths[next++] = (byte)(entry & 15);
            if (next != symbols) throw new InvalidOperationException("A code table does not cover its symbols.");
            foreach (var length in lengths) _count[length]++;
            var offsets = new short[MaximumCodeBits + 1];
            for (var length = 1; length < MaximumCodeBits; length++)
                offsets[length + 1] = (short)(offsets[length] + _count[length]);
            _symbol = new short[symbols];
            for (var symbol = 0; symbol < symbols; symbol++)
                if (lengths[symbol] != 0)
                    _symbol[offsets[lengths[symbol]]++] = (short)symbol;
        }

        // The format stores each code with its bits inverted, first bit first.
        public int Decode(ref BitReader reader)
        {
            int code = 0, first = 0, index = 0;
            for (var length = 1; length <= MaximumCodeBits; length++)
            {
                code |= reader.Bits(1) ^ 1;
                var count = _count[length];
                if (code - first < count) return _symbol[index + code - first];
                index += count;
                first = (first + count) << 1;
                code <<= 1;
            }
            throw new InvalidDataException("An implode code is longer than 13 bits.");
        }
    }
}
