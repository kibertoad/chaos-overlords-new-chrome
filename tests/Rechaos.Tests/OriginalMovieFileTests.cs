using System.Buffers.Binary;
using Rechaos.Core.Assets;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Reads and decodes both shipped movies with the rebuild's Smacker reader and decoders and checks
/// the values against FMT-VIDEO-001.
/// </summary>
public sealed class OriginalMovieFileTests
{
    /// <summary>FMT-VIDEO-001: the values that differ between the two movies.</summary>
    private static readonly Dictionary<string, (int Frames, uint AudioBuffer, int Channels)> Movies = new()
    {
        ["MVINTRO"] = (1150, 48_512, 2),
        ["MVLOGOS"] = (200, 24_260, 1)
    };

    [Fact]
    public void MoviesAreTheSmackerFilesOfFmtVideo001()
    {
        var files = OriginalFormatFiles.Require("FMT-VIDEO-001");
        Assert.Equal(Movies.Keys.Order(), files.Select(file => file.Name).Order());
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var expected = Movies[file.Name];
            var bytes = file.ReadAllBytes();
            using var stream = new MemoryStream(bytes, writable: false);
            // The reader refuses a file whose header, tables, trees and frames do not add up to
            // its length, so a successful read covers every byte.
            var metadata = SmackerVideoReader.Read(stream);
            Equal("width", 480, metadata.Width);
            Equal("height", 256, metadata.Height);
            Equal("frame_count", expected.Frames, metadata.FrameCount);
            Equal("frame time in ticks", TimeSpan.FromMilliseconds(100).Ticks, metadata.FrameDuration.Ticks);
            Equal("frame_rate", -10_000, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x10, 4)));
            Equal("flags", 0, metadata.Flags);
            Equal("stored frames", expected.Frames, metadata.Frames.Count);

            // Track 0 is the only audio track; tracks 1 to 6 hold 0 in both tables.
            var track = Assert.Single(metadata.AudioTracks);
            Equal("audio track", 0, track.Index);
            Equal("audio_buffer_sizes[0]", expected.AudioBuffer, track.MaximumDecodedBytes);
            Equal("audio_sample_rate", 22_050, track.SampleRate);
            Equal("audio bits", 8, track.BitsPerSample);
            Equal("audio channels", expected.Channels, track.Channels);
            Equal("audio_compressed", 1, track.IsPacked ? 1 : 0);
            var rate = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x48, 4));
            Equal("audio_reserved_bits", 0, (rate >> 24) & 0xF);
            Equal("audio_present", 1, (rate >> 30) & 1);
            for (var index = 1; index < 7; index++)
            {
                Equal($"audio_buffer_sizes[{index}]", 0, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x18 + index * 4, 4)));
                Equal($"audio_rates[{index}]", 0, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x48 + index * 4, 4)));
            }
            Equal("reserved_64", 0, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x64, 4)));

            for (var index = 0; index < metadata.Frames.Count; index++)
            {
                // Bits 0 and 1 of every frame size are clear.
                Equal($"low bits of frame_sizes[{index}]", 0,
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x68 + index * 4, 4)) & 3);
                // 3 on the first frame, 0 on the last ten, 2 on the rest.
                var type = index == 0 ? 3 : index >= metadata.Frames.Count - 10 ? 0 : 2;
                Equal($"frame_types[{index}]", type, metadata.Frames[index].TypeFlags);
            }

            // Every frame decodes with the rebuild's demuxer and decoders, as the extractor runs them.
            var video = SmackerVideoDecoder.Create(stream, metadata);
            var palette = SmackerPaletteDecoder.CreateEmpty();
            for (var index = 0; index < metadata.Frames.Count; index++)
            {
                var packet = SmackerFrameDemuxer.Read(stream, metadata, index);
                Equal($"palette chunk in frame {index}", index == 0 ? 1 : 0, packet.PaletteChunk.IsEmpty ? 0 : 1);
                if (!packet.PaletteChunk.IsEmpty)
                    palette = SmackerPaletteDecoder.Apply(packet.PaletteChunk.Span, palette);
                foreach (var chunk in packet.AudioChunks)
                {
                    var samples = SmackerAudioDecoder.Decode(chunk, track);
                    Equal($"audio bytes in frame {index}", chunk.DecodedBytes ?? -1, samples.Length);
                }
                var frame = video.Decode(packet);
                Equal($"decoded width of frame {index}", 480, frame.Width);
                Equal($"decoded height of frame {index}", 256, frame.Height);
                Equal($"decoded pixels of frame {index}", 480 * 256, frame.ColorIndices.Length);
            }
        });
    }

    private static void Equal(string label, long expected, long actual)
    {
        if (expected != actual) throw new InvalidDataException($"{label} is {actual}, expected {expected}");
    }
}
