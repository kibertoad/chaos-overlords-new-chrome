using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class NativeEffectVoiceTests
{
    [Fact]
    public void NewEffectDisposesPreviousVoiceWithoutChangingMusicTransport()
    {
        // RULE-AUDIO-005, FND-AUDIO-006: PlaySoundA omits SND_NOSTOP;
        // music uses a separate MCI path.
        var game = CreateAudioOnlyGame(6);
        using var sound = new SoundEffect(new byte[16000], 8000, AudioChannels.Mono);
        using var song = Song.FromUri("effect-test-music", new Uri(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.ogg")));
        // The streaming lock prevents natural music completion during the effect
        // assertions; an accidental owner-thread music stop can still run and fail them.
        lock (StreamingMutex(song))
        {
            MediaPlayer.Play(song);
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            var musicState = MediaPlayer.State;
            var musicVolume = MediaPlayer.Volume;
            try
            {
                Play(game, sound);
                var first = Voice(game);
                // Loop the existing voice before testing interruption: completion cannot
                // race this assertion on a loaded host. No short fixture timing is assumed.
                first.IsLooped = true;
                first.Play();
                Assert.Equal(SoundState.Playing, first.State);
                Play(game, sound);
                var second = Voice(game);
                Assert.NotSame(first, second);
                Assert.True(first.IsDisposed);
                Assert.False(second.IsDisposed);
                Assert.Equal(AudioRouting.EffectVolumeForLevel(6), second.Volume);
                Assert.Equal(musicState, MediaPlayer.State);
                Assert.Equal(musicVolume, MediaPlayer.Volume);
            }
            finally
            {
                Invoke(game, "StopEffectVoice");
                MediaPlayer.Stop();
            }
        }
    }

    [Fact]
    public void EffectsGateKeepsExistingVoiceButDirectTurnCueReplacesItAtZeroVolume()
    {
        // RULE-AUDIO-005, BUG-AUDIO-001, RULE-AUDIO-006: only the direct
        // turn-start call bypasses the effects gate; RULE-AUDIO-003 sets volume zero.
        var game = CreateAudioOnlyGame(6);
        using var sound = new SoundEffect(new byte[16000], 8000, AudioChannels.Mono);
        try
        {
            Play(game, sound);
            var first = Voice(game);
            first.IsLooped = true;
            first.Play();
            SetField(game, "_soundEffectVolumeLevel", 0);
            Play(game, sound);
            Assert.Same(first, Voice(game));
            Assert.False(first.IsDisposed);
            Assert.Equal(SoundState.Playing, first.State);
            Play(game, sound, ignoresEffectsEnabled: true);
            var direct = Voice(game);
            Assert.NotSame(first, direct);
            Assert.True(first.IsDisposed);
            Assert.Equal(0f, direct.Volume);
        }
        finally { Invoke(game, "StopEffectVoice"); }
    }

    [Fact]
    public void UnloadedOrInvalidGeneralSlotLeavesThePlayingVoiceAlone()
    {
        // RULE-AUDIO-004, RULE-AUDIO-005, FND-AUDIO-006: an empty slot
        // never reaches PlaySoundA, so it cannot interrupt an existing effect.
        var game = CreateAudioOnlyGame(6);
        SetField(game, "_generalSounds", new Dictionary<int, SoundEffect>());
        using var sound = new SoundEffect(new byte[16000], 8000, AudioChannels.Mono);
        try
        {
            Play(game, sound);
            var first = Voice(game);
            first.IsLooped = true;
            first.Play();
            foreach (var slot in new[] { -1, 5, GeneralSoundSlot.IncomingMessageAlert, 48 })
            {
                Invoke(game, "PlayGeneralSound", slot, false);
                Assert.Same(first, Voice(game));
                Assert.False(first.IsDisposed);
                Assert.Equal(SoundState.Playing, first.State);
            }
        }
        finally { Invoke(game, "StopEffectVoice"); }
    }

    private static object StreamingMutex(Song song)
    {
        var stream = typeof(Song).GetField("stream", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(song) ?? throw new InvalidOperationException("Expected the NVorbis Song backend.");
        return stream.GetType().GetField("prepareMutex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(stream) ?? throw new InvalidOperationException("Expected the Ogg preparation lock.");
    }

    private static ChaosGame CreateAudioOnlyGame(int level)
    {
        // Exercise the actual private audio methods without constructing a window,
        // graphics device, preference store or network session. These methods use
        // only the fields initialized here, plus the native audio framework.
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        SetField(game, "_soundEffectVolumeLevel", level);
        return game;
    }

    private static void Play(ChaosGame game, SoundEffect sound, bool ignoresEffectsEnabled = false) =>
        Invoke(game, "TryPlaySound", sound, ignoresEffectsEnabled);

    private static SoundEffectInstance Voice(ChaosGame game) =>
        (SoundEffectInstance?)Field("_activeEffectVoice").GetValue(game)
        ?? throw new InvalidOperationException("The actual effect player did not retain a voice.");

    private static void SetField(ChaosGame game, string name, object value) => Field(name).SetValue(game, value);
    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
    private static void Invoke(ChaosGame game, string name, params object[] arguments) =>
        (typeof(ChaosGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(name)).Invoke(game, arguments);
}
