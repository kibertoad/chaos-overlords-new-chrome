namespace Rechaos.Core.Assets;

public readonly record struct SmackerPaletteColor(byte Red, byte Green, byte Blue);

/// <summary>Applies one bounded Smacker palette update to a 256-color palette.</summary>
public static class SmackerPaletteDecoder
{
    public const int ColorCount = 256;

    public static SmackerPaletteColor[] CreateEmpty() => new SmackerPaletteColor[ColorCount];

    public static SmackerPaletteColor[] Apply(
        ReadOnlySpan<byte> chunk,
        IReadOnlyList<SmackerPaletteColor> previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (previous.Count != ColorCount)
            throw new ArgumentException($"A Smacker palette must contain exactly {ColorCount} colors.", nameof(previous));
        if (chunk.IsEmpty || chunk[0] == 0 || chunk[0] * 4 != chunk.Length)
            throw new InvalidDataException("Smacker palette chunk has an invalid declared length.");

        var palette = previous.ToArray();
        var sourceOffset = 1;
        var destinationOffset = 0;
        while (destinationOffset < ColorCount)
        {
            Require(chunk, sourceOffset, 1);
            var command = chunk[sourceOffset++];
            if ((command & 0x80) != 0)
            {
                var count = (command & 0x7f) + 1;
                RequirePaletteRange(destinationOffset, count);
                destinationOffset += count;
                continue;
            }

            if ((command & 0x40) != 0)
            {
                Require(chunk, sourceOffset, 1);
                var count = (command & 0x3f) + 1;
                var previousOffset = chunk[sourceOffset++];
                RequirePaletteRange(destinationOffset, count);
                RequirePaletteRange(previousOffset, count);
                for (var index = 0; index < count; index++)
                    palette[destinationOffset + index] = previous[previousOffset + index];
                destinationOffset += count;
                continue;
            }

            Require(chunk, sourceOffset, 2);
            var blue = ExpandComponent(command);
            var green = ExpandComponent(chunk[sourceOffset++]);
            var red = ExpandComponent(chunk[sourceOffset++]);
            palette[destinationOffset++] = new SmackerPaletteColor(red, green, blue);
        }

        // Palette chunks are four-byte aligned. No full command may remain after
        // all 256 entries have been produced, but up to three padding bytes may.
        if (chunk.Length - sourceOffset > 3)
            throw new InvalidDataException("Smacker palette chunk contains trailing command data.");

        return palette;
    }

    private static byte ExpandComponent(byte value)
    {
        if (value > 63)
            throw new InvalidDataException("Smacker palette component exceeds six bits.");
        return (byte)((value << 2) | (value >> 4));
    }

    private static void Require(ReadOnlySpan<byte> chunk, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > chunk.Length - count)
            throw new InvalidDataException("Smacker palette command exceeds its chunk.");
    }

    private static void RequirePaletteRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > ColorCount - count)
            throw new InvalidDataException("Smacker palette command exceeds 256 colors.");
    }
}
