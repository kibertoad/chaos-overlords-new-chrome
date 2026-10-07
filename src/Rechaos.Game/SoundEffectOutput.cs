using Microsoft.Xna.Framework.Audio;

namespace Rechaos.Game;

/// <summary>A sound effect the game asks for: a general slot or a combat sound.</summary>
public readonly record struct SoundEffectCue(SoundEffectBank Bank, int Index)
{
    public static SoundEffectCue General(int slot) => new(SoundEffectBank.General, slot);

    public static SoundEffectCue Combat(short index) => new(SoundEffectBank.Combat, index);
}

public enum SoundEffectBank
{
    /// <summary>The general slots of <see cref="GeneralSoundSlot"/> (RULE-AUDIO-004).</summary>
    General,

    /// <summary>The combat sounds of <see cref="AudioRouting.SoundFile"/>.</summary>
    Combat
}

/// <summary>
/// The one voice the effects are played on (RULE-AUDIO-005). The game decides whether a cue plays
/// and at what volume; the output plays it in place of the effect before it.
/// </summary>
internal interface ISoundEffectOutput
{
    /// <summary>
    /// Plays <paramref name="cue"/> at <paramref name="volume"/>, stopping the effect before it. A cue
    /// with no sound loaded plays nothing and leaves the effect before it playing (RULE-AUDIO-004).
    /// </summary>
    void Play(SoundEffectCue cue, float volume);

    /// <summary>Sets the volume of the effect playing, if any (RULE-AUDIO-003).</summary>
    void SetVolume(float volume);

    /// <summary>Stops the effect playing, if any.</summary>
    void Stop();
}

/// <summary>
/// The effects loaded from the asset pack and played through MonoGame on one voice.
/// </summary>
internal sealed class NativeSoundEffects : ISoundEffectOutput, IDisposable
{
    private readonly Dictionary<SoundEffectCue, SoundEffect> _sounds = [];
    private readonly RuntimeDiagnostics? _diagnostics;
    private SoundEffectInstance? _voice;

    public NativeSoundEffects(RuntimeDiagnostics? diagnostics = null) => _diagnostics = diagnostics;

    /// <summary>Set once the audio backend has refused to open a device.</summary>
    /// <remarks>
    /// Every other audio path in the game treats sound as optional: <see cref="Play(SoundEffect, float)"/>,
    /// the soundtrack loader and the movie audio all swallow backend failures. Loading the effects
    /// did not, so a machine with no output device enabled, a remote desktop session with no audio
    /// redirection, or a Linux box with no sound server running could not start the game at all, and
    /// was told to re-import its assets.
    /// </remarks>
    public bool AudioUnavailable { get; private set; }

    public int Count(SoundEffectBank bank) => _sounds.Keys.Count(cue => cue.Bank == bank);

    /// <summary>The voice of the effect last played, while it has not been stopped.</summary>
    internal SoundEffectInstance? Voice => _voice;

    /// <summary>Loads every combat sound and general slot of the asset pack under <paramref name="assetRoot"/>.</summary>
    public void LoadAll(string assetRoot)
    {
        for (short index = 0; index <= 18; index++)
            if (Load(assetRoot, AudioRouting.SoundFile(index)) is { } sound)
                Add(SoundEffectCue.Combat(index), sound);
        foreach (var slot in AudioRouting.GeneralSoundSlots)
            if (Load(assetRoot, AudioRouting.GeneralSoundFile(slot)) is { } sound)
                Add(SoundEffectCue.General(slot), sound);
    }

    /// <summary>Puts <paramref name="sound"/> in the place of <paramref name="cue"/>; it is disposed with the rest.</summary>
    internal void Add(SoundEffectCue cue, SoundEffect sound) => _sounds.Add(cue, sound);

    /// <summary>Reads one effect file, or null when it is missing or cannot be read.</summary>
    public SoundEffect? Load(string assetRoot, string fileName)
    {
        if (AudioUnavailable) return null;
        var path = Path.Combine(assetRoot, "audio", fileName);
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return SoundEffect.FromStream(stream);
        }
        catch (NoAudioHardwareException)
        {
            AudioUnavailable = true;
            _diagnostics?.Write("audio.unavailable", new Dictionary<string, string?>
            {
                ["firstFile"] = fileName
            });
            return null;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException or ArgumentException or OutOfMemoryException)
        {
            // RULE-AUDIO-004, FND-AUDIO-006: unreadable files or failed memory
            // allocation leave the slot empty and do not stop loading the rest.
            return null;
        }
    }

    public void Play(SoundEffectCue cue, float volume)
    {
        if (_sounds.TryGetValue(cue, out var sound)) Play(sound, volume);
    }

    /// <summary>Plays <paramref name="sound"/> on the voice, in place of the effect before it.</summary>
    internal void Play(SoundEffect sound, float volume)
    {
        SoundEffectInstance? next = null;
        try
        {
            next = sound.CreateInstance();
            next.Volume = volume;
            // Native effects go through PlaySoundA without SND_NOSTOP. Its next sound
            // interrupts the preceding one, while MCI music is a separate path.
            Stop();
            next.Play();
            _voice = next;
        }
        catch
        {
            try { next?.Dispose(); } catch { }
            // Optional presentation audio must never interrupt gameplay.
        }
    }

    public void SetVolume(float volume)
    {
        if (_voice is not null) _voice.Volume = volume;
    }

    public void Stop()
    {
        var voice = _voice;
        _voice = null;
        if (voice is null) return;
        try { voice.Stop(); } catch { }
        try { voice.Dispose(); } catch { }
    }

    public void Dispose()
    {
        Stop();
        foreach (var sound in _sounds.Values) sound.Dispose();
        _sounds.Clear();
    }
}
