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
        bool transparentBlack = false,
        bool transparentWhite = false)
    {
        var path = Path.Combine(_assetRoot, "images", fileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);
        if (!transparentBlack && !transparentWhite) return texture;
        var colors = new Color[texture.Width * texture.Height];
        texture.GetData(colors);
        for (var index = 0; index < colors.Length; index++)
            if ((transparentBlack && colors[index].R == 0 && colors[index].G == 0 && colors[index].B == 0)
                || (transparentWhite && colors[index].R >= 248 && colors[index].G >= 248 && colors[index].B >= 248))
                colors[index] = Color.Transparent;
        texture.SetData(colors);
        return texture;
    }

    private void LoadCombatAnimationTextures()
    {
        for (short animation = 0; animation <= 27; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.AttackFile(animation, false), transparentBlack: true);
        for (short animation = 0; animation <= 28; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.AttackFile(animation, true), transparentBlack: true);
        for (short animation = 0; animation <= 19; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.HitFile(animation, false), transparentBlack: false);
        for (short animation = 0; animation <= 20; animation++)
            LoadCombatAnimationTexture(CombatAnimationRouting.HitFile(animation, true), transparentBlack: false);
    }

    private void LoadCombatAnimationTexture(string fileName, bool transparentBlack)
    {
        var texture = LoadTexture(fileName, transparentBlack: transparentBlack);
        if (texture is not null) _combatAnimationTextures[fileName] = texture;
    }

    private SoundEffect? LoadSound(string fileName)
    {
        var path = Path.Combine(_assetRoot, "audio", fileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return SoundEffect.FromStream(stream);
    }

    private void PlayNewCombatSounds()
    {
        if (_state is null) return;
        foreach (var gameEvent in _state.Events.Where(value => value.Sequence > _lastAudibleEventSequence)
                     .OrderBy(value => value.Sequence))
        {
            if (AudioRouting.WeaponSound(_state, gameEvent) is { } soundIndex
                && _weaponSounds.TryGetValue(soundIndex, out var sound))
                sound.Play();
            _lastAudibleEventSequence = gameEvent.Sequence;
        }
    }

    private void CaptureNewCombatAnimations()
    {
        if (_state is null) return;
        var viewer = _state.Coordinator.ActivePlayer ?? new PlayerId(0);
        foreach (var gameEvent in _state.Events
                     .Where(value => value.Sequence > _lastAnimatedEventSequence)
                     .OrderBy(value => value.Sequence))
        {
            if (_combatAnimationTextures.Count > 0 && IsVisibleCombatEvent(_state, viewer, gameEvent))
                foreach (var clip in CombatAnimationRouting.ForEvent(_state, gameEvent))
                    _combatAnimationPlayer.Enqueue(clip);
            _lastAnimatedEventSequence = gameEvent.Sequence;
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
