namespace Rechaos.Extractor.Installer;

/// <summary>
/// Decodes raw LZMA and LZMA2 data, the two compressions GOG's Inno Setup installer uses: LZMA for
/// its setup header and LZMA2 for the files it carries.
/// </summary>
/// <remarks>
/// The whole output stays in memory and serves as the dictionary, so the dictionary size the
/// stream declares is not needed. The largest file the import reads from the installer is a few
/// megabytes.
/// </remarks>
public static class LzmaDecoder
{
    /// <summary>
    /// Decodes a raw LZMA stream that starts with Inno Setup's five property bytes (the lc/lp/pb
    /// byte and the dictionary size) and has no stored length. Decoding stops at an end marker or
    /// where the input runs out, as liblzma's raw decoder does.
    /// </summary>
    public static byte[] DecodeLzma(ReadOnlySpan<byte> input, int maximumLength)
    {
        if (input.Length < 5) throw new InvalidDataException("The LZMA stream is shorter than its properties.");
        var output = new OutputWindow(maximumLength);
        var decoder = new LzmaState(PropertiesFrom(input[0]));
        var range = new RangeDecoder(input[5..]);
        decoder.DecodeUntilInputEnds(ref range, output);
        return output.ToArray();
    }

    /// <summary>
    /// Decodes a raw LZMA2 stream whose first byte is the dictionary-size property, up to the
    /// control byte 0 that ends it.
    /// </summary>
    public static byte[] DecodeLzma2(ReadOnlySpan<byte> input, int maximumLength)
    {
        if (input.Length < 1) throw new InvalidDataException("The LZMA2 stream has no dictionary property.");
        if (input[0] > 40) throw new InvalidDataException($"LZMA2 dictionary property {input[0]} is out of range.");
        var data = input[1..];
        var output = new OutputWindow(maximumLength);
        LzmaState? state = null;
        var position = 0;
        var first = true;
        while (true)
        {
            if (position >= data.Length) throw new InvalidDataException("The LZMA2 stream ended early.");
            int control = data[position++];
            if (control == 0) break;
            if (control is 1 or 2)
            {
                // An uncompressed chunk: 1 resets the dictionary, 2 keeps it.
                if (first && control != 1) throw new InvalidDataException("The first LZMA2 chunk does not reset the dictionary.");
                var size = ReadBigEndian16(data, ref position) + 1;
                if (position + size > data.Length) throw new InvalidDataException("An LZMA2 chunk runs past the input.");
                output.Append(data.Slice(position, size));
                position += size;
                first = false;
                continue;
            }
            if (control < 0x80) throw new InvalidDataException($"LZMA2 control byte {control} is not defined.");
            var unpacked = ((control & 0x1F) << 16) + ReadBigEndian16(data, ref position) + 1;
            var packed = ReadBigEndian16(data, ref position) + 1;
            var reset = (control >> 5) & 3;
            if (first && reset != 3) throw new InvalidDataException("The first LZMA2 chunk does not reset the dictionary.");
            if (reset >= 2)
            {
                if (position >= data.Length) throw new InvalidDataException("The LZMA2 stream ended early.");
                var properties = PropertiesFrom(data[position++]);
                if (properties.Lc + properties.Lp > 4)
                    throw new InvalidDataException("LZMA2 literal context bits are out of range.");
                state = new LzmaState(properties);
            }
            else if (state is null) throw new InvalidDataException("An LZMA2 chunk has no properties to continue from.");
            else if (reset == 1) state.Reset();
            if (position + packed > data.Length) throw new InvalidDataException("An LZMA2 chunk runs past the input.");
            var range = new RangeDecoder(data.Slice(position, packed));
            state!.Decode(ref range, output, output.Length + unpacked);
            if (!range.AtCleanEnd()) throw new InvalidDataException("An LZMA2 chunk does not end where its packed size says.");
            position += packed;
            first = false;
        }
        return output.ToArray();
    }

    private static int ReadBigEndian16(ReadOnlySpan<byte> data, ref int position)
    {
        if (position + 2 > data.Length) throw new InvalidDataException("The LZMA2 stream ended early.");
        var value = (data[position] << 8) | data[position + 1];
        position += 2;
        return value;
    }

    private static (int Lc, int Lp, int Pb) PropertiesFrom(byte value)
    {
        if (value >= 9 * 5 * 5) throw new InvalidDataException($"LZMA property byte {value} is out of range.");
        var lc = value % 9;
        var lp = value / 9 % 5;
        var pb = value / 45;
        return (lc, lp, pb);
    }

    /// <summary>The decoded bytes, which are also the dictionary that matches copy from.</summary>
    private sealed class OutputWindow(int maximumLength)
    {
        private byte[] _buffer = new byte[Math.Min(maximumLength, 1 << 16)];

        public int Length { get; private set; }

        public byte Last(int distance) => Length >= distance ? _buffer[Length - distance] : (byte)0;

        public bool HasDistance(uint distance) => distance < (uint)Length;

        public void Put(byte value)
        {
            Reserve(1);
            _buffer[Length++] = value;
        }

        public void Copy(uint distance, int count)
        {
            Reserve(count);
            var from = Length - (int)distance - 1;
            // A match may overlap the bytes it writes, so it is copied byte by byte.
            for (var i = 0; i < count; i++) _buffer[Length++] = _buffer[from + i];
        }

        public void Append(ReadOnlySpan<byte> bytes)
        {
            Reserve(bytes.Length);
            bytes.CopyTo(_buffer.AsSpan(Length));
            Length += bytes.Length;
        }

        public byte[] ToArray() => _buffer.AsSpan(0, Length).ToArray();

        private void Reserve(int count)
        {
            if ((long)Length + count > maximumLength)
                throw new InvalidDataException($"The LZMA data decodes to more than {maximumLength} bytes.");
            if (Length + count <= _buffer.Length) return;
            var size = Math.Min(maximumLength, Math.Max(Length + count, _buffer.Length * 2));
            Array.Resize(ref _buffer, size);
        }
    }

    /// <summary>Thrown inside the range decoder when it needs a byte past the end of its input.</summary>
    private sealed class InputEndedException : Exception;

    private ref struct RangeDecoder
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;
        private uint _range;
        private uint _code;

        public RangeDecoder(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
            _range = uint.MaxValue;
            _code = 0;
            if (data.Length < 5) throw new InvalidDataException("An LZMA range coder needs five bytes to start.");
            if (data[_position++] != 0) throw new InvalidDataException("An LZMA range coder does not start with 0.");
            for (var i = 0; i < 4; i++) _code = (_code << 8) | data[_position++];
            if (_code == _range) throw new InvalidDataException("An LZMA range coder starts out of range.");
        }

        private void Normalize()
        {
            if (_range >= 1u << 24) return;
            if (_position >= _data.Length) throw new InputEndedException();
            _range <<= 8;
            _code = (_code << 8) | _data[_position++];
        }

        public int Bit(ref ushort probability)
        {
            Normalize();
            var bound = (_range >> 11) * probability;
            if (_code < bound)
            {
                _range = bound;
                probability += (ushort)((2048 - probability) >> 5);
                return 0;
            }
            _range -= bound;
            _code -= bound;
            probability -= (ushort)(probability >> 5);
            return 1;
        }

        public uint DirectBits(int count)
        {
            uint result = 0;
            for (var i = 0; i < count; i++)
            {
                Normalize();
                _range >>= 1;
                var bit = _code >= _range ? 1u : 0u;
                if (bit != 0) _code -= _range;
                result = (result << 1) | bit;
            }
            return result;
        }

        public int BitTree(ushort[] probabilities, int offset, int bits)
        {
            var symbol = 1;
            for (var i = 0; i < bits; i++) symbol = (symbol << 1) | Bit(ref probabilities[offset + symbol]);
            return symbol - (1 << bits);
        }

        public int ReverseBitTree(ushort[] probabilities, int offset, int bits)
        {
            var index = 1;
            var symbol = 0;
            for (var i = 0; i < bits; i++)
            {
                var bit = Bit(ref probabilities[offset + index]);
                index = (index << 1) | bit;
                symbol |= bit << i;
            }
            return symbol;
        }

        /// <summary>
        /// True once every input byte is read and the code is back to 0, LZMA's clean end. The
        /// byte that the last symbol's normalization still owes is read first, as LzmaDec does
        /// after its last symbol; reading it now instead of before the next bit changes nothing.
        /// </summary>
        public bool AtCleanEnd()
        {
            if (_position < _data.Length) Normalize();
            return _position == _data.Length && _code == 0;
        }
    }

    /// <summary>The adaptive probabilities and match state of one LZMA stream.</summary>
    private sealed class LzmaState
    {
        private const int States = 12;
        private const int PositionStatesMaximum = 16;
        private const int EndPositionModelIndex = 14;
        private const int FullDistances = 128;
        private const int MatchMinimumLength = 2;
        private const ushort InitialProbability = 1024;

        private readonly int _lc;
        private readonly int _lp;
        private readonly int _pb;
        private readonly ushort[] _isMatch = new ushort[States * PositionStatesMaximum];
        private readonly ushort[] _isRep = new ushort[States];
        private readonly ushort[] _isRepG0 = new ushort[States];
        private readonly ushort[] _isRepG1 = new ushort[States];
        private readonly ushort[] _isRepG2 = new ushort[States];
        private readonly ushort[] _isRep0Long = new ushort[States * PositionStatesMaximum];
        private readonly ushort[] _positionSlot = new ushort[4 * 64];
        private readonly ushort[] _specialPosition = new ushort[FullDistances - EndPositionModelIndex];
        private readonly ushort[] _align = new ushort[16];
        private readonly LengthDecoder _length = new();
        private readonly LengthDecoder _repeatLength = new();
        private readonly ushort[] _literal;
        private int _state;
        private uint _rep0, _rep1, _rep2, _rep3;

        public LzmaState((int Lc, int Lp, int Pb) properties)
        {
            (_lc, _lp, _pb) = properties;
            _literal = new ushort[0x300 << (_lc + _lp)];
            Reset();
        }

        public void Reset()
        {
            foreach (var table in new[]
                     {
                         _isMatch, _isRep, _isRepG0, _isRepG1, _isRepG2, _isRep0Long, _positionSlot,
                         _specialPosition, _align, _literal
                     })
                Array.Fill(table, InitialProbability);
            _length.Reset();
            _repeatLength.Reset();
            _state = 0;
            _rep0 = _rep1 = _rep2 = _rep3 = 0;
        }

        /// <summary>Decodes until <paramref name="output"/> holds <paramref name="end"/> bytes.</summary>
        public void Decode(ref RangeDecoder range, OutputWindow output, int end)
        {
            try
            {
                while (output.Length < end)
                    if (!DecodeSymbol(ref range, output, end))
                        throw new InvalidDataException("An LZMA2 chunk holds an end marker.");
            }
            catch (InputEndedException)
            {
                throw new InvalidDataException("An LZMA2 chunk ended before its stated length.");
            }
        }

        /// <summary>Decodes until an end marker or until a symbol would need input that is not there.</summary>
        public void DecodeUntilInputEnds(ref RangeDecoder range, OutputWindow output)
        {
            try
            {
                while (!range.AtCleanEnd() && DecodeSymbol(ref range, output, int.MaxValue))
                {
                }
            }
            catch (InputEndedException)
            {
                // The symbol that wanted more input is incomplete and wrote nothing.
            }
        }

        /// <summary>Decodes one literal or match; false at the end marker.</summary>
        private bool DecodeSymbol(ref RangeDecoder range, OutputWindow output, int end)
        {
            var positionState = output.Length & ((1 << _pb) - 1);
            if (range.Bit(ref _isMatch[(_state << 4) + positionState]) == 0)
            {
                DecodeLiteral(ref range, output);
                return true;
            }

            int length;
            if (range.Bit(ref _isRep[_state]) == 1)
            {
                if (!output.HasDistance(0)) throw new InvalidDataException("An LZMA repeat comes before any data.");
                if (range.Bit(ref _isRepG0[_state]) == 0)
                {
                    if (range.Bit(ref _isRep0Long[(_state << 4) + positionState]) == 0)
                    {
                        if (!output.HasDistance(_rep0))
                            throw new InvalidDataException("An LZMA repeat reaches back before the start of the data.");
                        if (output.Length + 1 > end) throw new InvalidDataException("An LZMA repeat runs past the end of its chunk.");
                        _state = _state < 7 ? 9 : 11;
                        output.Put(output.Last((int)_rep0 + 1));
                        return true;
                    }
                }
                else
                {
                    uint distance;
                    if (range.Bit(ref _isRepG1[_state]) == 0)
                    {
                        distance = _rep1;
                    }
                    else
                    {
                        if (range.Bit(ref _isRepG2[_state]) == 0)
                        {
                            distance = _rep2;
                        }
                        else
                        {
                            distance = _rep3;
                            _rep3 = _rep2;
                        }
                        _rep2 = _rep1;
                    }
                    _rep1 = _rep0;
                    _rep0 = distance;
                }
                length = _repeatLength.Decode(ref range, positionState);
                _state = _state < 7 ? 8 : 11;
            }
            else
            {
                _rep3 = _rep2;
                _rep2 = _rep1;
                _rep1 = _rep0;
                length = _length.Decode(ref range, positionState);
                _state = _state < 7 ? 7 : 10;
                _rep0 = DecodeDistance(ref range, length);
                if (_rep0 == uint.MaxValue) return false;
            }

            if (!output.HasDistance(_rep0))
                throw new InvalidDataException("An LZMA match reaches back before the start of the data.");
            var count = length + MatchMinimumLength;
            if (output.Length + count > end) throw new InvalidDataException("An LZMA match runs past the end of its chunk.");
            output.Copy(_rep0, count);
            return true;
        }

        private void DecodeLiteral(ref RangeDecoder range, OutputWindow output)
        {
            var previous = output.Last(1);
            var context = ((output.Length & ((1 << _lp) - 1)) << _lc) + (previous >> (8 - _lc));
            var offset = 0x300 * context;
            var symbol = 1;
            if (_state >= 7)
            {
                // After a match the literal is coded against the byte the last match would have copied.
                int matchByte = output.Last((int)_rep0 + 1);
                while (symbol < 0x100)
                {
                    var matchBit = (matchByte >> 7) & 1;
                    matchByte <<= 1;
                    var bit = range.Bit(ref _literal[offset + 0x100 + (matchBit << 8) + symbol]);
                    symbol = (symbol << 1) | bit;
                    if (matchBit != bit) break;
                }
            }
            while (symbol < 0x100) symbol = (symbol << 1) | range.Bit(ref _literal[offset + symbol]);
            output.Put((byte)symbol);
            _state = _state < 4 ? 0 : _state < 10 ? _state - 3 : _state - 6;
        }

        private uint DecodeDistance(ref RangeDecoder range, int length)
        {
            var lengthState = Math.Min(length, 3);
            var slot = range.BitTree(_positionSlot, lengthState * 64, 6);
            if (slot < 4) return (uint)slot;
            var directBits = (slot >> 1) - 1;
            var distance = (uint)((2 | (slot & 1)) << directBits);
            if (slot < EndPositionModelIndex)
                return distance + (uint)range.ReverseBitTree(_specialPosition, (int)distance - slot - 1, directBits);
            distance += range.DirectBits(directBits - 4) << 4;
            return distance + (uint)range.ReverseBitTree(_align, 0, 4);
        }
    }

    private sealed class LengthDecoder
    {
        private ushort _choice;
        private ushort _choice2;
        private readonly ushort[] _low = new ushort[16 << 3];
        private readonly ushort[] _middle = new ushort[16 << 3];
        private readonly ushort[] _high = new ushort[256];

        public void Reset()
        {
            _choice = _choice2 = 1024;
            Array.Fill(_low, (ushort)1024);
            Array.Fill(_middle, (ushort)1024);
            Array.Fill(_high, (ushort)1024);
        }

        public int Decode(ref RangeDecoder range, int positionState)
        {
            if (range.Bit(ref _choice) == 0) return range.BitTree(_low, positionState << 3, 3);
            if (range.Bit(ref _choice2) == 0) return 8 + range.BitTree(_middle, positionState << 3, 3);
            return 16 + range.BitTree(_high, 0, 8);
        }
    }
}
