using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private Texture2D? LoadTexture(
        string fileName,
        bool transparentWhite = false)
    {
        var path = Path.Combine(_assetRoot, "images", fileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);
        if (!transparentWhite) return texture;
        var colors = new Color[texture.Width * texture.Height];
        texture.GetData(colors);
        for (var index = 0; index < colors.Length; index++)
            // Native mode-1 copies key the maximum RGB555 white. Depending on
            // the BMP decoder, a maximum five-bit channel expands to 248 or 255.
            if (colors[index].R >= 248 && colors[index].G >= 248 && colors[index].B >= 248)
                colors[index] = Color.Transparent;
        texture.SetData(colors);
        return texture;
    }

    private void LoadCombatAnimationTextures()
    {
        for (short animation = 0; animation <= 27; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.AttackFile(animation, false));
        for (short animation = 0; animation <= 28; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.AttackFile(animation, true));
        for (short animation = 0; animation <= 19; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.HitFile(animation, false));
        for (short animation = 0; animation <= 20; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.HitFile(animation, true));
    }

    private void LoadCombatAnimationTexture(string fileName)
    {
        var texture = LoadTexture(fileName);
        if (texture is not null) _combatAnimationTextures[fileName] = texture;
    }

    /// <summary>Set once the audio backend has refused to open a device.</summary>
    /// <remarks>
    /// Every other audio path in the game treats sound as optional: <see cref="TryPlaySound"/>,
    /// the soundtrack loader and the movie audio all swallow backend failures. Loading the effects
    /// did not, so a machine with no output device enabled, a remote desktop session with no audio
    /// redirection, or a Linux box with no sound server running could not start the game at all, and
    /// was told to re-import its assets.
    /// </remarks>
    private bool _audioUnavailable;

    private SoundEffect? LoadSound(string fileName)
    {
        if (_audioUnavailable) return null;
        var path = Path.Combine(_assetRoot, "audio", fileName);
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return SoundEffect.FromStream(stream);
        }
        catch (NoAudioHardwareException)
        {
            _audioUnavailable = true;
            _diagnostics?.Write("audio.unavailable", new Dictionary<string, string?>
            {
                ["firstFile"] = fileName
            });
            return null;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException or ArgumentException)
        {
            // One unreadable effect file is not a reason to stop loading the rest.
            return null;
        }
    }

    private void PlayCombatSound(short soundIndex)
    {
        if (_combatSounds.TryGetValue(soundIndex, out var sound)) TryPlaySound(sound);
    }

    /// <summary>The player whose view is drawn: the active one, or the first seat between turns.</summary>
    private static PlayerId ViewingPlayer(MatchState state) =>
        state.Coordinator.ActivePlayer ?? new PlayerId(0);

    private void PlayGeneralSound(int slot)
    {
        if (_generalSounds.TryGetValue(slot, out var sound)) TryPlaySound(sound);
    }

    private void ReportInputResult(bool accepted, string rejectionMessage)
    {
        _message = accepted ? string.Empty : CityStatusMessage.Error(rejectionMessage);
        PlayGeneralSound(AudioRouting.InputResultSound(accepted));
    }

    /// <summary>Answers an action in whichever voice the control that asked speaks.</summary>
    /// <remarks>
    /// A pointer button has its own result sounds, so an answer it triggered has to use those rather
    /// than the keyboard's, which would land on top of the push the button has already played.
    /// </remarks>
    private void ReportButtonResult(bool accepted, string rejectionMessage, bool pointerButton)
    {
        if (!pointerButton)
        {
            ReportInputResult(accepted, rejectionMessage);
            return;
        }
        _message = accepted ? string.Empty : CityStatusMessage.Error(rejectionMessage);
        if (AudioRouting.PointerPushResultSound(accepted) is { } sound) PlayGeneralSound(sound);
    }

    private void RejectInput(string message) => ReportInputResult(false, message);

    private void AcceptInput() => PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);

    private void AcceptAndInvoke(Action action)
    {
        AcceptInput();
        action();
    }

    private void AcceptAndShow(ClientScreen screen)
    {
        AcceptInput();
        _screens.Show(screen);
    }

    private void TryPlaySound(SoundEffect sound)
    {
        if (_soundEffectVolumeLevel == 0) return;
        SoundEffectInstance? next = null;
        try
        {
            next = sound.CreateInstance();
            next.Volume = AudioRouting.EffectVolumeForLevel(_soundEffectVolumeLevel);
            // Native effects go through PlaySoundA without SND_NOSTOP. Its next sound
            // interrupts the preceding one, while MCI music is a separate path.
            StopEffectVoice();
            next.Play();
            _activeEffectVoice = next;
        }
        catch
        {
            try { next?.Dispose(); } catch { }
            // Optional presentation audio must never interrupt gameplay.
        }
    }

    private void StopEffectVoice()
    {
        var voice = _activeEffectVoice;
        _activeEffectVoice = null;
        if (voice is null) return;
        try { voice.Stop(); } catch { }
        try { voice.Dispose(); } catch { }
    }

    private void CaptureNewCombatAnimations()
    {
        if (_state is null) return;
        if (_screens.Current is not (ClientScreen.City or ClientScreen.CombatSummary)) return;
        var viewer = ViewingPlayer(_state);
        var lastSeen = _combatPresentationProgress.LastSeen(viewer);
        // The list is append-only in sequence order and this runs twice per Update, so walk back
        // from the end to the first unseen event instead of filtering and re-sorting all of it.
        var events = _state.Events;
        var first = events.Count;
        while (first > 0 && events[first - 1].Sequence > lastSeen) first--;
        for (var index = first; index < events.Count; index++)
        {
            var gameEvent = events[index];
            if (_detailedCombat && _combatAnimationTextures.Count > 0
                && CombatResultProjection.IsFromLastCompletedTurn(
                    gameEvent.Turn, _state.Coordinator.Turn)
                && IsVisibleCombatEvent(_state, viewer, gameEvent))
                foreach (var clip in CombatClipsOrNone(_state, gameEvent))
                    _combatAnimationPlayer.Enqueue(clip);
            _combatPresentationProgress.MarkSeen(viewer, gameEvent.Sequence);
        }
    }

    private void ValidateAssetPack()
    {
        var manifestPath = Path.Combine(_assetRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Original assets are not installed. Run Rechaos.Extractor with --source pointing at a legal Chaos Overlords installation.", manifestPath);
        var manifest = JsonSerializer.Deserialize<AssetManifest>(File.ReadAllText(manifestPath));
        if (manifest?.FormatVersion != AssetManifest.CurrentFormatVersion)
            throw new InvalidDataException("The asset pack is incompatible. Run the current extractor again.");
    }
}
