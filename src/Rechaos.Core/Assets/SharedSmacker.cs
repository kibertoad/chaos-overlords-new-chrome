using Shared = RefurbishedDinosaurs.Media.Smacker;

namespace Rechaos.Core.Assets;

internal static class SharedSmacker
{
    internal static Shared.SmackerMovie Movie(SmackerVideoMetadata metadata) => new(
        2, metadata.Width, metadata.Height, metadata.FrameDuration, metadata.Flags,
        checked((int)metadata.TreeOffset), checked((int)metadata.TreeBytes),
        new Shared.SmackerTreeSizes(checked((int)metadata.TreeAllocationSizes.MonochromeMap),
            checked((int)metadata.TreeAllocationSizes.MonochromeColor),
            checked((int)metadata.TreeAllocationSizes.FullBlock),
            checked((int)metadata.TreeAllocationSizes.BlockType)),
        metadata.AudioTracks.Select(Track).ToArray(),
        metadata.Frames.Select(frame => new Shared.SmackerFrame(frame.Index,
            checked((int)frame.Offset), frame.PayloadBytes, frame.TypeFlags, frame.IsKeyFrame)).ToArray());

    internal static Shared.SmackerAudioTrack Track(SmackerAudioTrack track) => new(
        track.Index, track.SampleRate, checked((int)track.MaximumDecodedBytes),
        track.IsPacked, track.BitsPerSample == 16, track.Channels == 2);
}
