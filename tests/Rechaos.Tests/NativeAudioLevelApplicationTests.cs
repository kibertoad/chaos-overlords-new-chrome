using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class NativeAudioLevelApplicationTests
{
    [Theory]
    [InlineData(true, 5)]
    [InlineData(true, 0)]
    [InlineData(false, 6)]
    [InlineData(false, 0)]
    public void EitherOptionReappliesBothStoredLevelsIncludingUnchangedSelections(bool musicChoice, int level)
    {
        // RULE-AUDIO-003, FND-AUDIO-007: either menu command calls the common
        // effects-then-music helper. Zero effects volume does not stop its voice.
        // DEV-OPTIONS-001: the rebuild may persist the choice; isolate that write
        // in a temporary file and do not compare persistence with the original.
        var preferencePath = Path.Combine(Path.GetTempPath(), $"rechaos-audio-level-{Guid.NewGuid():N}.json");
        using var song = Song.FromUri("level-test", new Uri(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.ogg")));
        using var sound = new SoundEffect(new byte[16000], 8000, AudioChannels.Mono);
        using var voice = sound.CreateInstance();
        voice.IsLooped = true;
        voice.Volume = AudioRouting.EffectVolumeForLevel(1);
        voice.Play();
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Set(game, "_musicVolumeLevel", 5);
        Set(game, "_soundEffectVolumeLevel", 6);
        Set(game, "_activeEffectVoice", voice);
        Set(game, "_soundtrack", new Dictionary<string, Song> { ["track"] = song });
        Set(game, "_generalSounds", new Dictionary<int, SoundEffect>());
        Set(game, "_preferencesPath", preferencePath);
        var onlineField = Field("_online");
        onlineField.SetValue(game, Activator.CreateInstance(onlineField.FieldType, nonPublic: true));
        MediaPlayer.Stop();
        MediaPlayer.Volume = OriginalSoundtrackPolicy.VolumeForLevel(1);
        try
        {
            var handler = musicChoice ? "SetMusicVolumeLevel" : "SetSoundEffectVolumeLevel";
            (typeof(ChaosGame).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(handler)).Invoke(game, [level]);
            var musicLevel = musicChoice ? level : 5;
            var effectsLevel = musicChoice ? 6 : level;
            Assert.Equal(effectsLevel, Field("_soundEffectVolumeLevel").GetValue(game));
            Assert.Equal(musicLevel, Field("_musicVolumeLevel").GetValue(game));
            Assert.Same(voice, Field("_activeEffectVoice").GetValue(game));
            Assert.False(voice.IsDisposed);
            Assert.Equal(SoundState.Playing, voice.State);
            Assert.Equal(AudioRouting.EffectVolumeForLevel(effectsLevel), voice.Volume);
            Assert.Equal(musicLevel != 0, Field("_soundtrackEnabled").GetValue(game));
            // FND-AUDIO-007: muting an already stopped device does not write a
            // music volume; nonzero choices apply it without starting a program.
            Assert.Equal(OriginalSoundtrackPolicy.VolumeForLevel(musicLevel == 0 ? 1 : musicLevel),
                MediaPlayer.Volume);
            Assert.Equal(MediaState.Stopped, MediaPlayer.State);
        }
        finally
        {
            // Reset the native loop flag before returning this effect source to
            // the shared pool; Ogg streams do not reset a recycled source's flag.
            voice.IsLooped = false;
            // IsLooped's setter changes managed state only; apply the native
            // flag while this looping voice still owns its source.
            (typeof(SoundEffectInstance).GetMethod("PlatformSetIsLooped",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException("PlatformSetIsLooped")).Invoke(voice, [false]);
            voice.Stop();
            MediaPlayer.Stop();
            File.Delete(preferencePath);
            File.Delete(preferencePath + ".tmp");
        }
    }

    private static void Set(ChaosGame game, string name, object value) => Field(name).SetValue(game, value);
    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
}
