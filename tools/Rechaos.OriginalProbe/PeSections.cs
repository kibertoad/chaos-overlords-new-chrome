namespace Rechaos.OriginalProbe;

/// <summary>The sections of a 32-bit PE file, read from its headers.</summary>
internal sealed record PeSection(string Name, uint VirtualAddress, uint VirtualSize, uint Characteristics)
{
    private const uint Writable = 0x80000000;

    public bool IsWritable => (Characteristics & Writable) != 0;

    public static IReadOnlyList<PeSection> Read(string path, out uint imageBase)
    {
        var bytes = File.ReadAllBytes(path);
        var header = BitConverter.ToInt32(bytes, 0x3C);
        var sectionCount = BitConverter.ToUInt16(bytes, header + 6);
        var optionalSize = BitConverter.ToUInt16(bytes, header + 20);
        imageBase = BitConverter.ToUInt32(bytes, header + 24 + 28);
        var sections = new List<PeSection>();
        for (var i = 0; i < sectionCount; i++)
        {
            var at = header + 24 + optionalSize + i * 40;
            var name = System.Text.Encoding.ASCII.GetString(bytes, at, 8).TrimEnd('\0');
            sections.Add(new PeSection(
                name,
                imageBase + BitConverter.ToUInt32(bytes, at + 12),
                BitConverter.ToUInt32(bytes, at + 8),
                BitConverter.ToUInt32(bytes, at + 36)));
        }

        return sections;
    }
}
