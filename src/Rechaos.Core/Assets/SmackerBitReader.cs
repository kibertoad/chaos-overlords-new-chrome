namespace Rechaos.Core.Assets;

internal sealed class SmackerBitReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _bitOffset;

    public SmackerBitReader(ReadOnlyMemory<byte> data) => _data = data;

    public int BitsRemaining => checked((_data.Length * 8) - _bitOffset);

    public int ReadBit() => ReadBits(1);

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
        var leafCount = 0;
        var root = ReadNode(reader, 0, ref leafCount);
        if (reader.ReadBit() != 0)
            throw new InvalidDataException("Smacker Huffman tree is missing its end marker.");
        return new SmackerHuffmanTree(root);
    }

    public byte ReadValue(SmackerBitReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var node = _root;
        while (!node.IsLeaf)
            node = reader.ReadBit() == 0 ? node.Left! : node.Right!;
        return node.Value;
    }

    private static Node ReadNode(SmackerBitReader reader, int depth, ref int leafCount)
    {
        if (depth > MaximumDepth)
            throw new InvalidDataException("Smacker Huffman tree exceeds its depth limit.");
        if (reader.ReadBit() == 0)
        {
            if (++leafCount > MaximumLeaves)
                throw new InvalidDataException("Smacker Huffman tree exceeds its leaf limit.");
            return new Node(reader.ReadBits(8));
        }

        var left = ReadNode(reader, depth + 1, ref leafCount);
        var right = ReadNode(reader, depth + 1, ref leafCount);
        return new Node(left, right);
    }

    private sealed class Node
    {
        public Node(int value)
        {
            IsLeaf = true;
            Value = (byte)value;
        }

        public Node(Node left, Node right)
        {
            Left = left;
            Right = right;
        }

        public bool IsLeaf { get; }
        public byte Value { get; }
        public Node? Left { get; }
        public Node? Right { get; }
    }
}
