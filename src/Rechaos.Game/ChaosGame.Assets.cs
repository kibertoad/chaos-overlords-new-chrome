using System.Text.Json;
using Microsoft.Xna.Framework;
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
        OriginalWhiteKey.Apply(colors);
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

    private void PlayCombatSound(short soundIndex) => PlaySound(SoundEffectCue.Combat(soundIndex));

    /// <summary>
    /// The player whose view is drawn: the one in its end-of-match final view, else the active one,
    /// or the first seat between turns.
    /// </summary>
    private PlayerId ViewingPlayer(MatchState state) =>
        PlanningViewer ?? new PlayerId(0);

    private void PlayGeneralSound(int slot, bool ignoresEffectsEnabled = false) =>
        PlaySound(SoundEffectCue.General(slot), ignoresEffectsEnabled);

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

    /// <remarks>
    /// <paramref name="ignoresEffectsEnabled"/> plays the sound at effects level 0 as the original's
    /// direct call does (BUG-AUDIO-001). The level still sets the volume (RULE-AUDIO-003), so at
    /// level 0 the sound is silent, but it still stops the effect before it.
    /// </remarks>
    private void PlaySound(SoundEffectCue cue, bool ignoresEffectsEnabled = false)
    {
        if (_soundEffectVolumeLevel == 0 && !ignoresEffectsEnabled) return;
        _soundEffects.Play(cue, AudioRouting.EffectVolumeForLevel(_soundEffectVolumeLevel));
    }

    private void StopEffectVoice() => _soundEffects.Stop();

    private void CaptureNewCombatAnimations()
    {
        if (_state is null) return;
        if (_screens.Current is not (ClientScreen.City or ClientScreen.CombatSummary)) return;
        // Left unseen while a computer holds the turn, so each human's fights play when they take it.
        if (CombatPresentationProgress.Viewer(_state, _finalViewPlayer) is not { } viewer) return;
        var lastSeen = _combatPresentationProgress.LastSeen(viewer);
        // The list is append-only in sequence order and this runs twice per Update, so walk back
        // from the end to the first unseen event instead of filtering and re-sorting all of it.
        var events = _state.Events;
        var first = events.Count;
        while (first > 0 && events[first - 1].Sequence > lastSeen) first--;
        if (first == events.Count) return;
        if (_detailedCombat && _combatAnimationTextures.Count > 0)
        {
            var presented = CombatResultProjection.AutomaticPresentationEvents(_state, viewer, events.Skip(first));
            foreach (var clip in CombatAnimationRouting.ForPresentation(_state, presented, viewer))
                _combatAnimationPlayer.Enqueue(clip);
        }
        _combatPresentationProgress.MarkSeen(viewer, events[^1].Sequence);
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
