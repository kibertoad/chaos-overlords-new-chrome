namespace Rechaos.Core.Assets;

public sealed record SmackerDecodedVideoFrame(
    int Width,
    int Height,
    ReadOnlyMemory<byte> ColorIndices);

/// <summary>Stateful decoder for the four-block Smacker v2 video representation.</summary>
public sealed class SmackerVideoDecoder
{
    private static readonly int[] BlockRuns =
    [
        1, 2, 3, 4, 5, 6, 7, 8,
        9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24,
        25, 26, 27, 28, 29, 30, 31, 32,
        33, 34, 35, 36, 37, 38, 39, 40,
        41, 42, 43, 44, 45, 46, 47, 48,
        49, 50, 51, 52, 53, 54, 55, 56,
        57, 58, 59, 128, 256, 512, 1024, 2048
    ];

    private readonly int _width;
    private readonly int _height;
    private readonly SmackerBigHuffmanTree _monochromeMap;
    private readonly SmackerBigHuffmanTree _monochromeColor;
    private readonly SmackerBigHuffmanTree _fullBlock;
    private readonly SmackerBigHuffmanTree _blockType;
    private readonly byte[] _colorIndices;

    private SmackerVideoDecoder(
        int width,
        int height,
        SmackerBigHuffmanTree monochromeMap,
        SmackerBigHuffmanTree monochromeColor,
        SmackerBigHuffmanTree fullBlock,
        SmackerBigHuffmanTree blockType)
    {
        _width = width;
        _height = height;
        _monochromeMap = monochromeMap;
        _monochromeColor = monochromeColor;
        _fullBlock = fullBlock;
        _blockType = blockType;
        _colorIndices = new byte[checked(width * height)];
    }

    public static SmackerVideoDecoder Create(Stream stream, SmackerVideoMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Smacker tree decoding requires a readable, seekable stream.", nameof(stream));
        if (metadata.Width % 4 != 0 || metadata.Height % 4 != 0)
            throw new NotSupportedException("Smacker dimensions must be divisible into 4x4 blocks.");
        if ((metadata.Flags & ~1u) != 0)
            throw new NotSupportedException("Interlaced or line-doubled Smacker video is not supported.");
        if (metadata.TreeBytes > SmackerVideoReader.MaximumTreeBytes)
            throw new InvalidDataException("Smacker Huffman trees exceed the allocation limit.");

        stream.Position = metadata.TreeOffset;
        var treeData = new byte[checked((int)metadata.TreeBytes)];
        stream.ReadExactly(treeData);
        var reader = new SmackerBitReader(treeData);
        var skippedTrees = 0;
        var map = ReadCodebook(reader, metadata.TreeAllocationSizes.MonochromeMap, ref skippedTrees);
        var color = ReadCodebook(reader, metadata.TreeAllocationSizes.MonochromeColor, ref skippedTrees);
        var full = ReadCodebook(reader, metadata.TreeAllocationSizes.FullBlock, ref skippedTrees);
        var type = ReadCodebook(reader, metadata.TreeAllocationSizes.BlockType, ref skippedTrees);
        if (skippedTrees == 4)
            throw new InvalidDataException("Smacker video omits all four Huffman trees.");
        if (!reader.RemainingBitsAreZero())
            throw new InvalidDataException("Smacker Huffman tree region contains nonzero trailing data.");
        return new SmackerVideoDecoder(metadata.Width, metadata.Height, map, color, full, type);
    }

    public SmackerDecodedVideoFrame Decode(SmackerFramePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var reader = new SmackerBitReader(packet.VideoData);
        _monochromeMap.ResetHistory();
        _monochromeColor.ResetHistory();
        _fullBlock.ResetHistory();
        _blockType.ResetHistory();

        var blockWidth = _width / 4;
        var blockCount = checked(blockWidth * (_height / 4));
        var block = 0;
        while (block < blockCount)
        {
            var type = _blockType.ReadValue(reader);
            var run = BlockRuns[(type >> 2) & 0x3f];
            if (run > blockCount - block)
                throw new InvalidDataException("Smacker block run exceeds the frame dimensions.");
            switch (type & 3)
            {
                case 0:
                    for (var index = 0; index < run; index++)
                        DecodeMonochromeBlock(reader, blockWidth, block++);
                    break;
                case 1:
                    for (var index = 0; index < run; index++)
                        DecodeFullBlock(reader, blockWidth, block++);
                    break;
                case 2:
                    block += run;
                    break;
                case 3:
                    var color = (byte)(type >> 8);
                    for (var index = 0; index < run; index++)
                        DecodeFillBlock(color, blockWidth, block++);
                    break;
            }
        }

        return new SmackerDecodedVideoFrame(_width, _height, _colorIndices.ToArray());
    }

    private static SmackerBigHuffmanTree ReadCodebook(
        SmackerBitReader reader,
        uint allocationBytes,
        ref int skippedTrees)
    {
        if (reader.ReadBit() != 0)
            return SmackerBigHuffmanTree.ReadAfterStartMarker(reader, allocationBytes);
        skippedTrees++;
        return SmackerBigHuffmanTree.Constant(0);
    }

    private void DecodeMonochromeBlock(SmackerBitReader reader, int blockWidth, int block)
    {
        var colors = _monochromeColor.ReadValue(reader);
        var map = _monochromeMap.ReadValue(reader);
        var low = (byte)colors;
        var high = (byte)(colors >> 8);
        var output = BlockOffset(blockWidth, block);
        for (var row = 0; row < 4; row++, output += _width, map >>= 4)
            for (var column = 0; column < 4; column++)
                _colorIndices[output + column] = (map & (1 << column)) != 0 ? high : low;
    }

    private void DecodeFullBlock(SmackerBitReader reader, int blockWidth, int block)
    {
        var output = BlockOffset(blockWidth, block);
        for (var row = 0; row < 4; row++, output += _width)
        {
            var right = _fullBlock.ReadValue(reader);
            var left = _fullBlock.ReadValue(reader);
            _colorIndices[output] = (byte)left;
            _colorIndices[output + 1] = (byte)(left >> 8);
            _colorIndices[output + 2] = (byte)right;
            _colorIndices[output + 3] = (byte)(right >> 8);
        }
    }

    private void DecodeFillBlock(byte color, int blockWidth, int block)
    {
        var output = BlockOffset(blockWidth, block);
        for (var row = 0; row < 4; row++, output += _width)
            _colorIndices.AsSpan(output, 4).Fill(color);
    }

    private int BlockOffset(int blockWidth, int block) =>
        checked(((block / blockWidth) * _width * 4) + ((block % blockWidth) * 4));
}

internal sealed class SmackerBigHuffmanTree
{
    private const int MaximumDepth = 500;
    private readonly Node _root;
    private readonly Node[] _history;

    private SmackerBigHuffmanTree(Node root, Node[] history)
    {
        _root = root;
        _history = history;
    }

    public static SmackerBigHuffmanTree Constant(ushort value)
    {
        var root = new Node(value);
        return new SmackerBigHuffmanTree(root, [new Node(0), new Node(0), new Node(0)]);
    }

    public static SmackerBigHuffmanTree ReadAfterStartMarker(
        SmackerBitReader reader,
        uint allocationBytes)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (allocationBytes > SmackerVideoReader.MaximumTreeAllocationBytes)
            throw new InvalidDataException("Smacker Huffman tree exceeds its allocation limit.");
        var maximumNodes = checked((int)((allocationBytes + 3L) / 4));
        if (maximumNodes == 0)
            throw new InvalidDataException("Smacker Huffman tree has no node allocation.");

        var lowBytes = ReadByteTree(reader);
        var highBytes = ReadByteTree(reader);
        var escapes = new ushort[3];
        for (var index = 0; index < escapes.Length; index++)
            escapes[index] = (ushort)reader.ReadBits(16);

        var history = new Node?[3];
        var nodeCount = 0;
        var root = ReadNode(
            reader, lowBytes, highBytes, escapes, history, 0, maximumNodes, ref nodeCount);
        if (reader.ReadBit() != 0)
            throw new InvalidDataException("Smacker Huffman tree is missing its end marker.");
        return new SmackerBigHuffmanTree(root,
            history.Select(node => node ?? new Node(0)).ToArray()!);
    }

    public void ResetHistory()
    {
        foreach (var node in _history) node.Value = 0;
    }

    public ushort ReadValue(SmackerBitReader reader)
    {
        var node = _root;
        while (!node.IsLeaf)
            node = (reader.ReadBit() == 0 ? node.Left : node.Right)
                ?? throw new InvalidDataException("Smacker Huffman code is not assigned.");
        var value = node.Value;
        if (value != _history[0].Value)
        {
            _history[2].Value = _history[1].Value;
            _history[1].Value = _history[0].Value;
            _history[0].Value = value;
        }
        return value;
    }

    private static SmackerHuffmanTree ReadByteTree(SmackerBitReader reader) =>
        reader.ReadBit() == 0
            ? SmackerHuffmanTree.Constant(0)
            : SmackerHuffmanTree.ReadAfterStartMarker(reader);

    private static Node ReadNode(
        SmackerBitReader reader,
        SmackerHuffmanTree lowBytes,
        SmackerHuffmanTree highBytes,
        IReadOnlyList<ushort> escapes,
        Node?[] history,
        int depth,
        int maximumNodes,
        ref int nodeCount)
    {
        if (depth > MaximumDepth)
            throw new InvalidDataException("Smacker Huffman tree exceeds its depth limit.");
        if (++nodeCount > maximumNodes)
            throw new InvalidDataException("Smacker Huffman tree exceeds its node allocation.");
        if (reader.ReadBit() == 0)
        {
            var value = (ushort)(lowBytes.ReadValue(reader) | (highBytes.ReadValue(reader) << 8));
            var node = new Node(value);
            for (var index = 0; index < escapes.Count; index++)
            {
                if (value != escapes[index]) continue;
                node.Value = 0;
                history[index] = node;
                break;
            }
            return node;
        }

        var left = ReadNode(
            reader, lowBytes, highBytes, escapes, history, depth + 1, maximumNodes, ref nodeCount);
        var right = ReadNode(
            reader, lowBytes, highBytes, escapes, history, depth + 1, maximumNodes, ref nodeCount);
        return new Node(left, right);
    }

    private sealed class Node
    {
        public Node(ushort value)
        {
            IsLeaf = true;
            Value = value;
        }

        public Node(Node left, Node right)
        {
            Left = left;
            Right = right;
        }

        public bool IsLeaf { get; }
        public ushort Value { get; set; }
        public Node? Left { get; }
        public Node? Right { get; }
    }
}
