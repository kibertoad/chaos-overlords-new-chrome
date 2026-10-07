using System.Diagnostics.CodeAnalysis;
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

    // Textures are decoded the first time something draws them. A screen draws a few of the
    // original's 200-odd bitmaps, and decoding all of them before the first frame took about a
    // fifth of the start-up. A missing file is remembered as missing.
    private readonly Dictionary<(string FileName, bool TransparentWhite), Texture2D?> _textures = [];

    private Texture2D? Texture(string fileName, bool transparentWhite = false)
    {
        // Before LoadContent there is no device to decode into, and a shell that never has one
        // calls LoadGameData alone: every texture is missing to it, as when LoadContent filled them.
        if (_batch is null) return null;
        if (!_textures.TryGetValue((fileName, transparentWhite), out var texture))
            _textures[(fileName, transparentWhite)] = texture = LoadTexture(fileName, transparentWhite);
        return texture;
    }

    /// <summary>A row of the original's bitmaps read by index, each decoded on first use.</summary>
    private readonly struct TextureRow(ChaosGame game, (string FileName, bool TransparentWhite)?[] files)
    {
        public int Length => files.Length;

        public Texture2D? this[int index] =>
            files[index] is { } file ? game.Texture(file.FileName, file.TransparentWhite) : null;
    }

    private static readonly (string, bool)?[] CityOwnershipLayerFiles =
        [.. Enumerable.Range(0, MatchLimits.PlayerCount + 1).Select(index => ((string, bool)?)($"PX1000{index}.bmp", false))];

    // Event art 0 is none; art 4 is drawn with its white keyed out.
    private static readonly (string, bool)?[] LastTurnEventArtworkFiles =
        [null, .. Enumerable.Range(1, 9).Select(art => ((string, bool)?)($"PX060{art:00}.bmp", art == 4))];

    private static readonly (string, bool)?[] ItemRotationFiles =
        [.. Enumerable.Range(0, 53).Select(itemId => ((string, bool)?)($"PX04{itemId:000}.bmp", false))];

    private TextureRow CityOwnershipLayers => new(this, CityOwnershipLayerFiles);
    private TextureRow LastTurnEventArtwork => new(this, LastTurnEventArtworkFiles);
    private TextureRow ItemRotationTextures => new(this, ItemRotationFiles);

    /// <summary>
    /// The combat animation strips found when the content loaded, each decoded the first time a
    /// clip draws it. Whether any were found decides whether Detailed Combat plays.
    /// </summary>
    private readonly struct CombatAnimationTextures(ChaosGame game)
    {
        public int Count => game._combatAnimationFiles.Count;

        public bool TryGetValue(string fileName, [NotNullWhen(true)] out Texture2D? texture)
        {
            texture = game._combatAnimationFiles.Contains(fileName) ? game.Texture(fileName) : null;
            return texture is not null;
        }
    }

    private readonly HashSet<string> _combatAnimationFiles = [];

    private CombatAnimationTextures CombatAnimations => new(this);

    private void FindCombatAnimationFiles()
    {
        for (short animation = 0; animation <= 27; animation++)
            FindCombatAnimationFile(CombatAnimationRouting.AttackFile(animation, false));
        for (short animation = 0; animation <= 28; animation++)
            FindCombatAnimationFile(CombatAnimationRouting.AttackFile(animation, true));
        for (short animation = 0; animation <= 19; animation++)
            FindCombatAnimationFile(CombatAnimationRouting.HitFile(animation, false));
        for (short animation = 0; animation <= 20; animation++)
            FindCombatAnimationFile(CombatAnimationRouting.HitFile(animation, true));
    }

    private void FindCombatAnimationFile(string fileName)
    {
        if (File.Exists(Path.Combine(_assetRoot, "images", fileName))) _combatAnimationFiles.Add(fileName);
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
