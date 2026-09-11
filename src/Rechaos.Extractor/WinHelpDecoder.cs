using System.Buffers.Binary;
using System.Text;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public static class WinHelpDecoder
{
    public const int MaximumHelpBytes = 4 * 1024 * 1024;
    public const int MaximumContentsBytes = 64 * 1024;
    private const uint HelpMagic = 0x00035f3f;
    private const ushort BTreeMagic = 0x293b;
    private const int FileHeaderSize = 9;
    private const int BTreeHeaderSize = 38;
    private const int TopicBlockHeaderSize = 12;
    private const int TopicLinkSize = 21;
    private const int MaximumTopics = 512;
    private const int MaximumPhrases = 16_512;
    private const int MaximumTopicTextBytes = 128 * 1024;

    public static ExtractedHelpDocument Decode(string helpPath, string contentsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helpPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentsPath);
        var help = ReadBounded(helpPath, MaximumHelpBytes);
        var contents = ReadBounded(contentsPath, MaximumContentsBytes);
        return Decode(help, contents);
    }

    public static ExtractedHelpDocument Decode(ReadOnlyMemory<byte> help, ReadOnlyMemory<byte> contents)
    {
        if (help.Length is < 16 or > MaximumHelpBytes)
            throw new InvalidDataException("WinHelp file size is outside the supported bounds.");
        if (contents.Length > MaximumContentsBytes)
            throw new InvalidDataException("WinHelp contents file is too large.");

        var container = new Container(help);
        var system = container.ReadStream("|SYSTEM");
        if (system.Length < 12)
            throw new InvalidDataException("WinHelp |SYSTEM stream is truncated.");
        var minorVersion = ReadUInt16(system.Span, 2);
        var flags = ReadUInt16(system.Span, 10);
        if (minorVersion < 16 || flags is not (4 or 8))
            throw new InvalidDataException(
                $"Unsupported WinHelp topic encoding (minor {minorVersion}, flags {flags}).");

        var phrases = ReadHallPhrases(container);
        var topics = ReadTopics(container.ReadStream("|TOPIC"), flags, phrases);
        var contexts = container.TryReadStream("|CONTEXT", out var contextStream)
            ? ReadContextMap(contextStream.Span)
            : new Dictionary<uint, int>();
        var contextIds = container.TryReadStream("|CTXOMAP", out var contextIdStream)
            ? ReadContextIds(contextIdStream.Span)
            : new Dictionary<uint, int>();
        var parsedContents = ParseContents(contents.Span);
        var listedTopics = parsedContents.Count(entry => entry.Reference is not null);
        if (listedTopics > topics.Count)
            throw new InvalidDataException("WinHelp contents references more topics than were decoded.");

        var nextTopic = 0;
        var contextNames = new Dictionary<uint, string>();
        var contentsEntries = parsedContents.Select(entry =>
        {
            int? topicId = null;
            if (entry.Reference is not null)
            {
                topicId = topics[nextTopic++].Id;
                if (contexts.Count > 0)
                {
                    var hash = CalculateContextHash(entry.Reference);
                    if (!contexts.ContainsKey(hash))
                        throw new InvalidDataException(
                            $"WinHelp contents context {entry.Reference} is missing.");
                    contextNames.TryAdd(hash, entry.Reference);
                }
            }
            return new ExtractedHelpContentsEntry(
                entry.Level, entry.Label, topicId, entry.Reference);
        }).ToArray();

        var listedIds = contentsEntries.Where(entry => entry.TopicId is not null)
            .Select(entry => entry.TopicId!.Value).ToHashSet();
        topics = topics.Select(topic => topic with
        {
            ListedInContents = listedIds.Contains(topic.Id)
        }).Select(ApplyGameplayClarifications).ToList();
        var extractedContexts = contexts
            .Select(entry => new ExtractedHelpContext(
                contextNames.GetValueOrDefault(entry.Key), entry.Key, null, entry.Value))
            .Concat(contextIds.Select(entry =>
                new ExtractedHelpContext(null, null, entry.Key, entry.Value)))
            .OrderBy(context => context.TargetOffset)
            .ThenBy(context => context.Hash is null ? 1 : 0)
            .ThenBy(context => context.Hash)
            .ThenBy(context => context.NumericId)
            .ToArray();

        return new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            ReadContentsTitle(contents.Span) ?? "Chaos Overlords Help",
            topics,
            contentsEntries,
            extractedContexts);
    }

    public static uint CalculateContextHash(string contextName)
    {
        ArgumentNullException.ThrowIfNull(contextName);
        if (contextName.Length == 0) return 1;
        var hash = 0u;
        foreach (var character in contextName)
        {
            var value = character switch
            {
                '0' => 10,
                >= '1' and <= '9' => character - '0',
                >= 'A' and <= 'Z' => character - 'A' + 0x11,
                >= 'a' and <= 'z' => character - 'a' + 0x11,
                '.' => 0x0c,
                '_' => 0x0d,
                _ => throw new ArgumentException(
                    "Context name contains a character unsupported by HC31.", nameof(contextName))
            };
            hash = unchecked(hash * 43 + (uint)value);
        }
        return hash;
    }

    private static ExtractedHelpTopic ApplyGameplayClarifications(ExtractedHelpTopic topic)
    {
        if (!string.Equals(topic.Title, "Attack...", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(topic.Title, "Attack…", StringComparison.OrdinalIgnoreCase))
            return topic;

        const string clarification =
            "NEW CHROME CLARIFICATION\n" +
            "Attack Roll = gang Combat + current Force - defender Defense. " +
            "Combat is simultaneous, so a gang eliminated during the round still attacks " +
            "using the Force it had at the start of the round.";
        return topic with { Text = $"{topic.Text.TrimEnd()}\n\n{clarification}" };
    }

    public static byte[] DecompressLz77(ReadOnlySpan<byte> input, int maximumOutputBytes)
    {
        if (maximumOutputBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        var window = new byte[0x1000];
        var output = new List<byte>(Math.Min(input.Length * 2, maximumOutputBytes));
        var inputOffset = 0;
        var windowPosition = 0;
        var mask = 0;
        var bits = 0;
        while (inputOffset < input.Length)
        {
            if (mask == 0)
            {
                bits = input[inputOffset++];
                mask = 1;
            }

            if ((bits & mask) != 0)
            {
                if (inputOffset + 2 > input.Length) break;
                var word = BinaryPrimitives.ReadUInt16LittleEndian(input[inputOffset..]);
                inputOffset += 2;
                var length = (word >> 12) + 3;
                var backPosition = windowPosition - (word & 0x0fff) - 1;
                for (var index = 0; index < length; index++)
                {
                    EnsureOutputBound(output.Count, maximumOutputBytes);
                    var value = window[backPosition++ & 0x0fff];
                    window[windowPosition++ & 0x0fff] = value;
                    output.Add(value);
                }
            }
            else
            {
                if (inputOffset >= input.Length) break;
                EnsureOutputBound(output.Count, maximumOutputBytes);
                var value = input[inputOffset++];
                window[windowPosition++ & 0x0fff] = value;
                output.Add(value);
            }
            mask = mask << 1 & 0xff;
        }
        return output.ToArray();
    }

    public static byte[] DecompressHall(
        ReadOnlySpan<byte> input,
        IReadOnlyList<byte[]> phrases,
        int maximumOutputBytes)
    {
        if (maximumOutputBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        var output = new List<byte>(Math.Min(input.Length * 3, maximumOutputBytes));
        var offset = 0;
        while (offset < input.Length)
        {
            var value = input[offset++];
            if ((value & 1) == 0)
            {
                AppendPhrase(value / 2);
            }
            else if ((value & 3) == 1)
            {
                if (offset >= input.Length) break;
                AppendPhrase(128 + value / 4 * 256 + input[offset++]);
            }
            else if ((value & 7) == 3)
            {
                Append(input.Slice(offset, Math.Min(value / 8 + 1, input.Length - offset)));
                offset += Math.Min(value / 8 + 1, input.Length - offset);
            }
            else if ((value & 15) == 7)
            {
                AppendRepeated((byte)' ', value / 16 + 1);
            }
            else
            {
                AppendRepeated(0, value / 16 + 1);
            }
        }
        return output.ToArray();

        void AppendPhrase(int index)
        {
            if (index < 0 || index >= phrases.Count)
                throw new InvalidDataException("WinHelp text references an invalid phrase.");
            Append(phrases[index]);
        }

        void Append(ReadOnlySpan<byte> bytes)
        {
            if (output.Count > maximumOutputBytes - bytes.Length)
                throw new InvalidDataException("WinHelp decompression exceeded its output bound.");
            foreach (var value in bytes) output.Add(value);
        }

        void AppendRepeated(byte value, int count)
        {
            if (output.Count > maximumOutputBytes - count)
                throw new InvalidDataException("WinHelp decompression exceeded its output bound.");
            for (var index = 0; index < count; index++) output.Add(value);
        }
    }

    private static List<ExtractedHelpTopic> ReadTopics(
        ReadOnlyMemory<byte> topicStream,
        ushort flags,
        IReadOnlyList<byte[]> phrases)
    {
        var physicalBlockSize = flags == 8 ? 2048 : 4096;
        var blockCount = (topicStream.Length + physicalBlockSize - 1) / physicalBlockSize;
        var blocks = new byte[blockCount][];
        var firstLinks = new int[blockCount];
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var start = blockIndex * physicalBlockSize;
            var length = Math.Min(physicalBlockSize, topicStream.Length - start);
            if (length < TopicBlockHeaderSize) break;
            var block = topicStream.Span.Slice(start, length);
            firstLinks[blockIndex] = ReadInt32(block, 4);
            blocks[blockIndex] = DecompressLz77(
                block[TopicBlockHeaderSize..], 0x4000);
        }

        var topics = new List<MutableTopic>();
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            if (blocks[blockIndex] is not { } block) continue;
            var topicPosition = blockIndex * 0x4000 + TopicBlockHeaderSize;
            var firstLinkOffset = firstLinks[blockIndex] - topicPosition;
            if (firstLinkOffset < 0 || firstLinkOffset >= block.Length) continue;

            byte[] parseData = block;
            if (blockIndex + 1 < blockCount && blocks[blockIndex + 1] is { } nextBlock)
            {
                var nextTopicPosition = (blockIndex + 1) * 0x4000 + TopicBlockHeaderSize;
                var continuation = firstLinks[blockIndex + 1] - nextTopicPosition;
                if (continuation > 0)
                {
                    if (continuation > nextBlock.Length)
                        throw new InvalidDataException("WinHelp topic continuation is out of bounds.");
                    parseData = [.. block, .. nextBlock.AsSpan(0, continuation)];
                }
            }
            ReadTopicLinks(parseData, topicPosition, firstLinkOffset, phrases, topics);
        }

        return topics.Where(topic => !string.IsNullOrWhiteSpace(topic.Title)
                                     || !string.IsNullOrWhiteSpace(topic.Text.ToString()))
            .Take(MaximumTopics)
            .Select((topic, index) => new ExtractedHelpTopic(
                index,
                string.IsNullOrWhiteSpace(topic.Title) ? $"Additional topic {index + 1}" : topic.Title,
                NormalizeText(topic.Text.ToString()),
                false))
            .ToList();
    }

    private static void ReadTopicLinks(
        ReadOnlySpan<byte> data,
        int topicPosition,
        int firstLinkOffset,
        IReadOnlyList<byte[]> phrases,
        List<MutableTopic> topics)
    {
        var offset = firstLinkOffset;
        while (offset + TopicLinkSize <= data.Length)
        {
            var rawBlockSize = ReadUInt32(data, offset);
            var rawDecompressedLength = ReadUInt32(data, offset + 4);
            var rawNextBlock = ReadUInt32(data, offset + 12);
            var rawData1Length = ReadUInt32(data, offset + 16);
            var recordType = data[offset + 20];
            if (rawBlockSize is 0 or > 32_768 || rawDecompressedLength > MaximumTopicTextBytes
                || rawData1Length < TopicLinkSize || rawData1Length > rawBlockSize)
                break;
            var blockSize = (int)rawBlockSize;
            var decompressedLength = (int)rawDecompressedLength;
            var data1Length = (int)rawData1Length;
            var nextBlock = rawNextBlock <= int.MaxValue ? (int)rawNextBlock : -1;
            if (offset > data.Length - blockSize) break;

            var data1 = data.Slice(offset + TopicLinkSize, data1Length - TopicLinkSize);
            var storedData2 = data.Slice(offset + data1Length, blockSize - data1Length);
            var data2 = decompressedLength <= storedData2.Length
                ? storedData2[..decompressedLength].ToArray()
                : DecompressHall(storedData2, phrases, MaximumTopicTextBytes);

            if (recordType == 0x02)
            {
                var titleEnd = data2.AsSpan().IndexOf((byte)0);
                var title = DecodeWindows1252(titleEnd < 0 ? data2 : data2.AsSpan(0, titleEnd));
                topics.Add(new MutableTopic(title));
                if (topics.Count > MaximumTopics)
                    throw new InvalidDataException("WinHelp topic count exceeds the supported bound.");
            }
            else if (recordType == 0x20 && topics.Count > 0)
            {
                AppendDisplayText(topics[^1].Text, data1, data2);
                if (topics[^1].Text.Length > MaximumTopicTextBytes)
                    throw new InvalidDataException("WinHelp topic text exceeds the supported bound.");
            }

            if (nextBlock <= 0) break;
            var relativeOffset = nextBlock - topicPosition;
            if (relativeOffset <= offset || relativeOffset >= data.Length) break;
            offset = relativeOffset;
        }
    }

    private static void AppendDisplayText(
        StringBuilder output,
        ReadOnlySpan<byte> data1,
        ReadOnlySpan<byte> data2)
    {
        var commandOffset = ParagraphCommandsOffset(data1);
        var textOffset = 0;
        var iterations = 0;
        while (iterations++ < data1.Length + data2.Length + 16)
        {
            var terminator = data2[textOffset..].IndexOf((byte)0);
            var length = terminator < 0 ? data2.Length - textOffset : terminator;
            output.Append(DecodeWindows1252(data2.Slice(textOffset, length)));
            textOffset += length + (terminator < 0 ? 0 : 1);
            if (commandOffset >= data1.Length) break;
            var command = data1[commandOffset];
            if (command == 0xff) break;
            commandOffset = SkipFormattingCommand(data1, commandOffset, output);
            if (terminator < 0 && textOffset >= data2.Length) break;
        }
        output.AppendLine().AppendLine();
    }

    private static int ParagraphCommandsOffset(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        _ = ReadCompressedLong(data, ref offset);
        _ = ReadCompressedWord(data, ref offset);
        Require(data, offset, 6);
        offset += 4;
        var bits = ReadUInt16(data, offset);
        offset += 2;
        if ((bits & 0x0001) != 0) _ = ReadCompressedLong(data, ref offset);
        foreach (var mask in new ushort[] { 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040 })
            if ((bits & mask) != 0) _ = ReadCompressedInt(data, ref offset);
        if ((bits & 0x0100) != 0)
        {
            Require(data, offset, 3);
            offset += 3;
        }
        if ((bits & 0x0200) != 0)
        {
            var tabs = ReadCompressedInt(data, ref offset);
            if (tabs is < 0 or > 64) throw new InvalidDataException("Invalid WinHelp tab count.");
            for (var index = 0; index < tabs; index++)
            {
                var tab = ReadCompressedWord(data, ref offset);
                if ((tab & 0x4000) != 0) _ = ReadCompressedWord(data, ref offset);
            }
        }
        return offset;
    }

    private static int SkipFormattingCommand(ReadOnlySpan<byte> data, int offset, StringBuilder output)
    {
        var command = data[offset];
        switch (command)
        {
            case 0x80:
                return Advance(data, offset, 3);
            case 0x81:
                output.AppendLine();
                return offset + 1;
            case 0x82:
                output.AppendLine().AppendLine();
                return offset + 1;
            case 0x83:
                output.Append(' ');
                return offset + 1;
            case 0x8b:
                output.Append(' ');
                return offset + 1;
            case 0x8c:
                output.Append('-');
                return offset + 1;
            case 0x86:
            case 0x87:
            case 0x88:
                return SkipPicture(data, offset);
            case 0xc8:
            case 0xcc:
            case 0xea:
            case 0xeb:
            case 0xee:
            case 0xef:
                Require(data, offset + 1, 2);
                return Advance(data, offset, 3 + ReadInt16(data, offset + 1));
            case 0xe0:
            case 0xe1:
            case 0xe2:
            case 0xe3:
            case 0xe6:
            case 0xe7:
                return Advance(data, offset, 5);
            default:
                return offset + 1;
        }
    }

    private static int SkipPicture(ReadOnlySpan<byte> data, int offset)
    {
        Require(data, offset, 2);
        var kind = data[offset + 1];
        var cursor = offset + 2;
        var size = ReadCompressedLong(data, ref cursor);
        if (kind == 0x22) _ = ReadCompressedWord(data, ref cursor);
        return Advance(data, cursor, size);
    }

    private static Dictionary<uint, int> ReadContextMap(ReadOnlySpan<byte> data)
    {
        var entries = ReadFixedBTreeLeaves(data, 8, "context");
        var result = new Dictionary<uint, int>();
        foreach (var entry in entries)
        {
            var hash = ReadUInt32(entry);
            var topicOffset = ReadInt32(entry, 4);
            if (topicOffset < 0 || !result.TryAdd(hash, topicOffset))
                throw new InvalidDataException("WinHelp context entry is invalid.");
        }
        return result;
    }

    private static Dictionary<uint, int> ReadContextIds(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) throw new InvalidDataException("WinHelp context-id map is truncated.");
        var count = ReadUInt16(data);
        Require(data, 2, checked(count * 8));
        var result = new Dictionary<uint, int>();
        for (var index = 0; index < count; index++)
        {
            var offset = 2 + index * 8;
            var contextId = ReadUInt32(data, offset);
            var topicOffset = ReadInt32(data, offset + 4);
            if (topicOffset < 0 || !result.TryAdd(contextId, topicOffset))
                throw new InvalidDataException("WinHelp context-id entry is invalid.");
        }
        return result;
    }

    private static IReadOnlyList<byte[]> ReadFixedBTreeLeaves(
        ReadOnlySpan<byte> tree,
        int entrySize,
        string label)
    {
        if (tree.Length < BTreeHeaderSize)
            throw new InvalidDataException($"WinHelp {label} B-tree is truncated.");
        if (ReadUInt16(tree) != BTreeMagic)
            throw new InvalidDataException($"WinHelp {label} B-tree magic is invalid.");
        var pageSize = ReadUInt16(tree, 4);
        var page = ReadInt16(tree, 26);
        var totalPages = ReadInt16(tree, 30);
        var levels = ReadInt16(tree, 32);
        var totalEntries = ReadInt32(tree, 34);
        if (pageSize is < 64 or > 8192 || totalPages is <= 0 or > 4096
            || levels is <= 0 or > 16 || totalEntries is < 0 or > 4096)
            throw new InvalidDataException($"WinHelp {label} B-tree metadata is invalid.");
        Require(tree, BTreeHeaderSize, checked(pageSize * totalPages));
        for (var level = 1; level < levels; level++)
        {
            var indexPage = BTreePage(tree, page, pageSize, totalPages, label);
            Require(indexPage, 0, 6);
            page = ReadInt16(indexPage, 4);
        }

        var result = new List<byte[]>(totalEntries);
        var visited = new HashSet<int>();
        while (page != -1)
        {
            if (page < 0 || page >= totalPages || !visited.Add(page))
                throw new InvalidDataException($"WinHelp {label} B-tree leaf chain is invalid.");
            var leaf = BTreePage(tree, page, pageSize, totalPages, label);
            Require(leaf, 0, 8);
            var count = ReadInt16(leaf, 2);
            if (count < 0 || count > totalEntries || 8 + count * entrySize > leaf.Length)
                throw new InvalidDataException($"WinHelp {label} B-tree entry count is invalid.");
            for (var index = 0; index < count; index++)
                result.Add(leaf.Slice(8 + index * entrySize, entrySize).ToArray());
            page = ReadInt16(leaf, 6);
        }
        if (result.Count != totalEntries)
            throw new InvalidDataException(
                $"WinHelp {label} B-tree entry count does not match its header.");
        return result;
    }

    private static ReadOnlySpan<byte> BTreePage(
        ReadOnlySpan<byte> tree,
        int index,
        int pageSize,
        int totalPages,
        string label)
    {
        if (index < 0 || index >= totalPages)
            throw new InvalidDataException($"WinHelp {label} B-tree page is out of bounds.");
        return tree.Slice(BTreeHeaderSize + index * pageSize, pageSize);
    }

    private static IReadOnlyList<byte[]> ReadHallPhrases(Container container)
    {
        var index = container.ReadStream("|PhrIndex").Span;
        var image = container.ReadStream("|PhrImage").Span;
        if (index.Length < 28) throw new InvalidDataException("WinHelp phrase index is truncated.");
        var entries = ReadInt32(index, 4);
        var imageSize = ReadInt32(index, 12);
        var compressedImageSize = ReadInt32(index, 16);
        var bits = ReadUInt16(index, 24) & 0x0f;
        if (entries is < 0 or > MaximumPhrases || imageSize is < 0 or > MaximumHelpBytes
            || compressedImageSize != image.Length || bits > 15)
            throw new InvalidDataException("WinHelp phrase metadata is invalid.");
        var phraseData = imageSize == compressedImageSize
            ? image.ToArray()
            : DecompressLz77(image, imageSize);
        if (phraseData.Length < imageSize)
            throw new InvalidDataException("WinHelp phrase image is truncated.");

        var bitReader = new DwordBitReader(index[28..]);
        var offsets = new int[entries + 1];
        for (var entry = 0; entry < entries; entry++)
        {
            var length = 1;
            while (bitReader.Read()) length += 1 << bits;
            for (var bit = 0; bit < bits; bit++)
                if (bitReader.Read()) length += 1 << bit;
            offsets[entry + 1] = checked(offsets[entry] + length);
            if (offsets[entry + 1] > phraseData.Length)
                throw new InvalidDataException("WinHelp phrase offset is out of bounds.");
        }
        return Enumerable.Range(0, entries)
            .Select(entry => phraseData[offsets[entry]..offsets[entry + 1]])
            .ToArray();
    }

    private static List<ContentsEntry> ParseContents(ReadOnlySpan<byte> bytes)
    {
        var entries = new List<ContentsEntry>();
        foreach (var sourceLine in DecodeWindows1252(bytes).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = sourceLine.Trim();
            if (line.Length == 0 || line[0] is ':' or ';') continue;
            var separator = line.IndexOf(' ');
            if (separator <= 0 || !int.TryParse(line[..separator], out var rawLevel))
                continue;
            var body = line[(separator + 1)..].Trim();
            var targetSeparator = body.IndexOf('=');
            var label = (targetSeparator < 0 ? body : body[..targetSeparator]).Trim();
            if (label.Length == 0) continue;
            entries.Add(new ContentsEntry(
                Math.Clamp(rawLevel - 1, 0, 15),
                label,
                targetSeparator < 0 ? null : body[(targetSeparator + 1)..].Trim()));
        }
        return entries;
    }

    private static string? ReadContentsTitle(ReadOnlySpan<byte> bytes)
    {
        foreach (var sourceLine in DecodeWindows1252(bytes).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = sourceLine.Trim();
            if (line.StartsWith(":Title ", StringComparison.OrdinalIgnoreCase))
                return line[7..].Trim();
        }
        return null;
    }

    private static string NormalizeText(string text)
    {
        var lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        var result = new List<string>(lines.Length);
        var previousBlank = true;
        foreach (var sourceLine in lines)
        {
            var line = string.Join(' ', sourceLine.Split(
                [' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
            var blank = line.Length == 0;
            if (!blank || !previousBlank) result.Add(line);
            previousBlank = blank;
        }
        return string.Join('\n', result).Trim();
    }

    private static string DecodeWindows1252(ReadOnlySpan<byte> bytes)
    {
        const string controls = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ";
        return string.Create(bytes.Length, bytes.ToArray(), (characters, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var value = source[index];
                characters[index] = value is >= 0x80 and <= 0x9f
                    ? controls[value - 0x80]
                    : (char)value;
            }
        });
    }

    private static byte[] ReadBounded(string path, int maximumBytes)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("Required WinHelp source is missing.", path);
        if (file.Length > maximumBytes) throw new InvalidDataException("WinHelp source is too large.");
        return File.ReadAllBytes(path);
    }

    private static int ReadCompressedWord(ReadOnlySpan<byte> data, ref int offset)
    {
        Require(data, offset, 1);
        if ((data[offset] & 1) == 0) return data[offset++] >> 1;
        Require(data, offset, 2);
        var value = ReadUInt16(data, offset) >> 1;
        offset += 2;
        return value;
    }

    private static int ReadCompressedInt(ReadOnlySpan<byte> data, ref int offset)
    {
        Require(data, offset, 1);
        if ((data[offset] & 1) == 0) return (data[offset++] >> 1) - 0x40;
        Require(data, offset, 2);
        var value = (ReadUInt16(data, offset) >> 1) - 0x4000;
        offset += 2;
        return value;
    }

    private static int ReadCompressedLong(ReadOnlySpan<byte> data, ref int offset)
    {
        Require(data, offset, 1);
        if ((data[offset] & 1) == 0)
        {
            Require(data, offset, 2);
            var value = (ReadUInt16(data, offset) >> 1) - 0x4000;
            offset += 2;
            return value;
        }
        Require(data, offset, 4);
        var result = (long)(ReadUInt32(data, offset) >> 1) - 0x40000000L;
        offset += 4;
        return checked((int)result);
    }

    private static int Advance(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (count < 0) throw new InvalidDataException("WinHelp command length is invalid.");
        Require(data, offset, count);
        return offset + count;
    }

    private static void EnsureOutputBound(int currentCount, int maximumOutputBytes)
    {
        if (currentCount >= maximumOutputBytes)
            throw new InvalidDataException("WinHelp decompression exceeded its output bound.");
    }

    private static void Require(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException("WinHelp structure is truncated.");
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset = 0)
    {
        Require(data, offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
    }

    private static short ReadInt16(ReadOnlySpan<byte> data, int offset)
    {
        Require(data, offset, 2);
        return BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset = 0)
    {
        Require(data, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset = 0) =>
        unchecked((int)ReadUInt32(data, offset));

    private sealed record ContentsEntry(int Level, string Label, string? Reference);

    private sealed class MutableTopic(string title)
    {
        public string Title { get; } = title;
        public StringBuilder Text { get; } = new();
    }

    private ref struct DwordBitReader(ReadOnlySpan<byte> source)
    {
        private readonly ReadOnlySpan<byte> _source = source;
        private int _offset;
        private uint _value;
        private uint _mask;

        public bool Read()
        {
            _mask <<= 1;
            if (_mask == 0)
            {
                Require(_source, _offset, 4);
                _value = ReadUInt32(_source, _offset);
                _offset += 4;
                _mask = 1;
            }
            return (_value & _mask) != 0;
        }
    }

    private sealed class Container
    {
        private readonly ReadOnlyMemory<byte> _data;
        private readonly Dictionary<string, int> _streams;

        public Container(ReadOnlyMemory<byte> data)
        {
            _data = data;
            var span = data.Span;
            if (ReadUInt32(span) != HelpMagic)
                throw new InvalidDataException("Source is not a supported WinHelp file.");
            var directoryOffset = ReadInt32(span, 4);
            var recordedSize = ReadInt32(span, 12);
            if (recordedSize != data.Length)
                throw new InvalidDataException("WinHelp recorded size does not match the file.");
            _streams = ReadDirectory(directoryOffset);
        }

        public ReadOnlyMemory<byte> ReadStream(string name)
        {
            if (!_streams.TryGetValue(name, out var offset))
                throw new InvalidDataException($"Required WinHelp stream {name} is missing.");
            Require(_data.Span, offset, FileHeaderSize);
            var reserved = ReadInt32(_data.Span, offset);
            var used = ReadInt32(_data.Span, offset + 4);
            if (reserved < FileHeaderSize || used < 0 || used > reserved - FileHeaderSize)
                throw new InvalidDataException($"WinHelp stream {name} has invalid bounds.");
            Require(_data.Span, offset + FileHeaderSize, used);
            return _data.Slice(offset + FileHeaderSize, used);
        }

        public bool TryReadStream(string name, out ReadOnlyMemory<byte> stream)
        {
            if (!_streams.ContainsKey(name))
            {
                stream = default;
                return false;
            }
            stream = ReadStream(name);
            return true;
        }

        private Dictionary<string, int> ReadDirectory(int directoryOffset)
        {
            Require(_data.Span, directoryOffset, FileHeaderSize + BTreeHeaderSize);
            var used = ReadInt32(_data.Span, directoryOffset + 4);
            Require(_data.Span, directoryOffset + FileHeaderSize, used);
            var tree = _data.Span.Slice(directoryOffset + FileHeaderSize, used);
            if (ReadUInt16(tree) != BTreeMagic)
                throw new InvalidDataException("WinHelp directory B-tree magic is invalid.");
            var pageSize = ReadUInt16(tree, 4);
            var rootPage = ReadInt16(tree, 26);
            var totalPages = ReadInt16(tree, 30);
            var levels = ReadInt16(tree, 32);
            var totalEntries = ReadInt32(tree, 34);
            if (pageSize is < 64 or > 8192 || totalPages is <= 0 or > 4096
                || levels is <= 0 or > 16 || totalEntries is < 0 or > 4096)
                throw new InvalidDataException("WinHelp directory B-tree metadata is invalid.");
            Require(tree, BTreeHeaderSize, checked(pageSize * totalPages));

            var page = rootPage;
            for (var level = 1; level < levels; level++)
            {
                var pageData = DirectoryPage(tree, page, pageSize, totalPages);
                Require(pageData, 0, 6);
                page = ReadInt16(pageData, 4);
            }

            var streams = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<int>();
            while (page != -1)
            {
                if (page < 0 || page >= totalPages || !visited.Add(page))
                    throw new InvalidDataException("WinHelp directory leaf chain is invalid.");
                var pageData = DirectoryPage(tree, page, pageSize, totalPages);
                Require(pageData, 0, 8);
                var count = ReadInt16(pageData, 2);
                var nextPage = ReadInt16(pageData, 6);
                if (count < 0 || count > totalEntries)
                    throw new InvalidDataException("WinHelp directory entry count is invalid.");
                var offset = 8;
                for (var entry = 0; entry < count; entry++)
                {
                    var terminator = pageData[offset..].IndexOf((byte)0);
                    if (terminator < 0 || terminator > 255)
                        throw new InvalidDataException("WinHelp directory name is invalid.");
                    var name = Encoding.ASCII.GetString(pageData.Slice(offset, terminator));
                    offset += terminator + 1;
                    Require(pageData, offset, 4);
                    if (!streams.TryAdd(name, ReadInt32(pageData, offset)))
                        throw new InvalidDataException("WinHelp directory contains a duplicate stream.");
                    offset += 4;
                }
                page = nextPage;
            }
            if (streams.Count != totalEntries)
                throw new InvalidDataException("WinHelp directory entry count does not match its header.");
            return streams;
        }

        private static ReadOnlySpan<byte> DirectoryPage(
            ReadOnlySpan<byte> tree,
            int index,
            int pageSize,
            int totalPages)
        {
            if (index < 0 || index >= totalPages)
                throw new InvalidDataException("WinHelp directory page is out of bounds.");
            return tree.Slice(BTreeHeaderSize + index * pageSize, pageSize);
        }
    }
}
