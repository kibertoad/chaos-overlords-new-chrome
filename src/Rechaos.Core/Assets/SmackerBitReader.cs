namespace Rechaos.Core.Assets;

internal sealed class SmackerBitReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _bitOffset;

    public SmackerBitReader(ReadOnlyMemory<byte> data) => _data = data;

    public int BitsRemaining => checked((_data.Length * 8) - _bitOffset);

    public int ReadBit() => ReadBits(1);

    public bool RemainingBitsAreZero()
    {
        while (BitsRemaining > 0)
            if (ReadBit() != 0) return false;
        return true;
    }

    public int ReadBits(int count)
    {
        if (count is < 0 or > 24)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (BitsRemaining < count)
            throw new InvalidDataException("Smacker bitstream is truncated.");

        var result = 0;
        var span = _data.Span;
        for (var index = 0; index < count; index++, _bitOffset++)
            result |= ((span[_bitOffset >> 3] >> (_bitOffset & 7)) & 1) << index;
        return result;
    }
}

internal sealed class SmackerHuffmanTree
{
    private const int MaximumDepth = 27;
    private const int MaximumLeaves = 256;
    private readonly Node _root;

    private SmackerHuffmanTree(Node root) => _root = root;

    public static SmackerHuffmanTree ReadDelimited(SmackerBitReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.ReadBit() != 1)
            throw new InvalidDataException("Smacker Huffman tree is missing its start marker.");
        return ReadAfterStartMarker(reader);
    }

    public static SmackerHuffmanTree ReadAfterStartMarker(SmackerBitReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var entries = new List<Entry>();
        ReadEntries(reader, 0, entries);
        if (reader.ReadBit() != 0)
            throw new InvalidDataException("Smacker Huffman tree is missing its end marker.");
        return new SmackerHuffmanTree(BuildCanonicalTree(entries));
    }

    public static SmackerHuffmanTree Constant(byte value) => new(new Node(value));

    public byte ReadValue(SmackerBitReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var node = _root;
        while (!node.IsLeaf)
            node = (reader.ReadBit() == 0 ? node.Left : node.Right)
                ?? throw new InvalidDataException("Smacker Huffman code is not assigned.");
        return node.Value;
    }

    private static void ReadEntries(SmackerBitReader reader, int depth, List<Entry> entries)
    {
        if (depth > MaximumDepth)
            throw new InvalidDataException("Smacker Huffman tree exceeds its depth limit.");
        if (reader.ReadBit() == 0)
        {
            if (entries.Count >= MaximumLeaves)
                throw new InvalidDataException("Smacker Huffman tree exceeds its leaf limit.");
            entries.Add(new Entry((byte)reader.ReadBits(8), depth));
            return;
        }

        ReadEntries(reader, depth + 1, entries);
        ReadEntries(reader, depth + 1, entries);
    }

    private static Node BuildCanonicalTree(IReadOnlyList<Entry> entries)
    {
        if (entries.Count == 1 && entries[0].Length == 0)
            return new Node(entries[0].Value);
        var root = new Node();
        ulong code = 0;
        foreach (var entry in entries)
        {
            if (entry.Length is < 1 or > MaximumDepth)
                throw new InvalidDataException("Smacker Huffman code has an invalid length.");
            var increment = 1UL << (32 - entry.Length);
            if ((code & (increment - 1)) != 0 || code + increment > 1UL << 32)
                throw new InvalidDataException("Smacker Huffman code lengths are invalid.");

            var node = root;
            for (var bitIndex = 0; bitIndex < entry.Length; bitIndex++)
            {
                if (node.IsLeaf)
                    throw new InvalidDataException("Smacker Huffman codes overlap.");
                var bit = (int)((code >> (31 - bitIndex)) & 1);
                if (bitIndex == entry.Length - 1)
                {
                    var existing = bit == 0 ? node.Left : node.Right;
                    if (existing is not null)
                        throw new InvalidDataException("Smacker Huffman codes overlap.");
                    if (bit == 0) node.Left = new Node(entry.Value);
                    else node.Right = new Node(entry.Value);
                }
                else
                {
                    var next = bit == 0 ? node.Left : node.Right;
                    if (next is null)
                    {
                        next = new Node();
                        if (bit == 0) node.Left = next;
                        else node.Right = next;
                    }
                    node = next;
                }
            }
            code += increment;
        }
        return root;
    }

    private sealed class Node
    {
        public Node()
        {
        }

        public Node(int value)
        {
            IsLeaf = true;
            Value = (byte)value;
        }

        public bool IsLeaf { get; }
        public byte Value { get; }
        public Node? Left { get; set; }
        public Node? Right { get; set; }
    }

    private readonly record struct Entry(byte Value, int Length);
}
