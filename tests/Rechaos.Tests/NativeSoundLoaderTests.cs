using Microsoft.Xna.Framework.Audio;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class NativeSoundLoaderTests
{
    [Fact]
    public void EveryOriginalEffectLoadsWithItsRecordedSampleDuration()
    {
        // FMT-AUDIO-001, FND-AUDIO-004: all 28 files contain unsigned 8-bit,
        // mono PCM at 22,050 Hz, including six odd data sizes with a RIFF pad.
        var files = OriginalFormatFiles.Require("FMT-AUDIO-001");
        Assert.Equal(28, files.Count);
        using var assets = new TemporaryAudioAssets();
        using var effects = new NativeSoundEffects();
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var bytes = file.ReadAllBytes();
            var samples = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x28));
            var name = file.Name + ".wav";
            File.WriteAllBytes(Path.Combine(assets.AudioDirectory, name), bytes);
            using var effect = effects.Load(assets.Root, name);
            Assert.NotNull(effect);
            AssertDuration(samples, effect.Duration);
        });
    }

    [Fact]
    public void MissingSlotDoesNotPreventLoadingAnOddLengthPcmEffect()
    {
        // RULE-AUDIO-004, FND-AUDIO-006: a missing file leaves an empty slot,
        // not a failed sound system. FMT-AUDIO-001: the odd-length RIFF size
        // omits the pad byte; native decoding must still accept the samples.
        using var assets = new TemporaryAudioAssets();
        using var effects = new NativeSoundEffects();
        Assert.Null(effects.Load(assets.Root, "missing.wav"));
        const int samples = 22_051;
        var path = Path.Combine(assets.AudioDirectory, "synthetic.wav");
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8); writer.Write(36 + samples); writer.Write("WAVE"u8);
            writer.Write("fmt "u8); writer.Write(16); writer.Write((ushort)1);
            writer.Write((ushort)1); writer.Write(22_050); writer.Write(22_050);
            writer.Write((ushort)1); writer.Write((ushort)8);
            writer.Write("data"u8); writer.Write(samples);
            writer.Write(Enumerable.Repeat((byte)128, samples).ToArray());
            writer.Write((byte)0);
        }
        using var effect = effects.Load(assets.Root, "synthetic.wav");
        Assert.NotNull(effect);
        AssertDuration(samples, effect.Duration);
    }

    private static void AssertDuration(uint samples, TimeSpan actual) =>
        Assert.InRange(Math.Abs((actual - TimeSpan.FromSeconds(samples / 22_050d)).Ticks), 0, 1);

    private sealed class TemporaryAudioAssets : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"rechaos-native-wave-{Guid.NewGuid():N}");
        public string AudioDirectory => Path.Combine(Root, "audio");
        public TemporaryAudioAssets() => Directory.CreateDirectory(AudioDirectory);
        public void Dispose()
        {
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(Root);
            if (!target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Audio test cleanup escaped the temporary directory.");
            Directory.Delete(target, recursive: true);
        }
    }
}
