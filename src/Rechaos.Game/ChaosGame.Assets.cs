using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private Texture2D? LoadTexture(OriginalBitmap bitmap)
    {
        var path = ImagePath(bitmap.FileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);
        if (!bitmap.TransparentWhite) return texture;
        var colors = new Color[texture.Width * texture.Height];
        texture.GetData(colors);
        OriginalWhiteKey.Apply(colors);
        texture.SetData(colors);
        return texture;
    }

    private string ImagePath(string fileName) => Path.Combine(_assetRoot, "images", fileName);

    // The player's game decodes every bitmap of OriginalBitmap.All in LoadContent, so a bitmap that
    // cannot be read or decoded fails at start-up rather than in the middle of a match, and the
    // first frame of a screen does not stall on decoding. A reference frame draws one screen, which
    // uses a few of the bitmaps, and decoding all of them took about a fifth of its run: it decodes
    // each the first time something draws it. A missing file is remembered as missing.
    private readonly Dictionary<OriginalBitmap, Texture2D?> _textures = [];

    private bool DecodesTexturesOnFirstDraw => _referenceFrame is not null;

    private void DecodeAllTextures()
    {
        foreach (var bitmap in OriginalBitmap.All) Texture(bitmap);
    }

    private Texture2D? Texture(OriginalBitmap bitmap)
    {
        // Before LoadContent there is no device to decode into, and a shell that never has one
        // calls LoadGameData alone: every texture is missing to it, as when LoadContent filled them.
        if (_batch is null) return null;
        if (!_textures.TryGetValue(bitmap, out var texture))
            _textures[bitmap] = texture = LoadTexture(bitmap);
        return texture;
    }

    /// <summary>A row of the original's bitmaps read by index.</summary>
    private readonly struct TextureRow(ChaosGame game, IReadOnlyList<OriginalBitmap?> bitmaps)
    {
        public int Length => bitmaps.Count;

        public Texture2D? this[int index] => bitmaps[index] is { } bitmap ? game.Texture(bitmap) : null;
    }

    private TextureRow CityOwnershipLayers => new(this, OriginalBitmap.CityOwnershipLayers);
    private TextureRow LastTurnEventArtwork => new(this, OriginalBitmap.LastTurnEventArtwork);
    private TextureRow ItemRotationTextures => new(this, OriginalBitmap.ItemRotations);

    /// <summary>
    /// The combat animation strips found when the content loaded. Whether any were found decides
    /// whether Detailed Combat plays.
    /// </summary>
    private readonly struct CombatAnimationTextures(ChaosGame game)
    {
        public int Count => game._combatAnimationFiles.Count;

        public bool TryGetValue(string fileName, [NotNullWhen(true)] out Texture2D? texture)
        {
            texture = OriginalBitmap.CombatStrips.TryGetValue(fileName, out var bitmap)
                && game._combatAnimationFiles.Contains(bitmap)
                    ? game.Texture(bitmap)
                    : null;
            return texture is not null;
        }
    }

    private readonly HashSet<OriginalBitmap> _combatAnimationFiles = [];

    private CombatAnimationTextures CombatAnimations => new(this);

    private void FindCombatAnimationFiles()
    {
        foreach (var strip in OriginalBitmap.CombatStrips.Values)
            if (File.Exists(ImagePath(strip.FileName))) _combatAnimationFiles.Add(strip);
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
        if (_detailedCombat && CombatAnimations.Count > 0)
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
