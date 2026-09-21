using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawCombatPanel(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_combatAnimationPlayer.Active is not { } clip) return;
        DrawPanelArtwork(batch, pixel, _combatBackground, CombatPanelLayout.Panel);

        var gameEvent = EventBySequence(state.Events, clip.EventSequence);
        var leftId = gameEvent?.Gang;
        var rightId = gameEvent?.Target.Kind == CommandTargetKind.Gang
            ? new GangId(gameEvent.Target.Id)
            : clip.Defender;
        var left = leftId is { } resolvedLeft ? state.FindGang(resolvedLeft) : null;
        var right = state.FindGang(rightId);
        var sectorId = left?.SectorId ?? right?.SectorId ?? 0;
        DrawCombatSector(batch, font, state, sectorId);

        if (clip.Police)
            DrawPoliceCombatant(batch, pixel, font, rightSide: false);
        else if (left is not null)
            DrawCombatant(batch, pixel, font, state, left, rightSide: false,
                gameEvent?.Resolution?.ItemId, clip, gameEvent);
        if (right is not null)
            DrawCombatant(batch, pixel, font, state, right, rightSide: true,
                gameEvent?.Resolution?.RetaliationItemId, clip, gameEvent);

        DrawCombatFrames(batch, pixel, clip);
    }

    private void DrawCombatSector(SpriteBatch batch, PixelFont font, MatchState state, int sectorId)
    {
        var sector = state.Sectors[sectorId];
        var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
        if (layer is not null)
            batch.Draw(layer, CombatPanelLayout.Sector, CityMapLayout.Source(sectorId), Color.White);
        font.Draw(batch, SectorCode(sectorId),
            CombatPanelLayout.SectorCodeText.ToVector2(), Color.Lime, 1);
    }

    private void DrawCombatant(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        MatchGangState gang,
        bool rightSide,
        short? eventWeapon,
        CombatAnimationClip clip,
        GameEvent? gameEvent)
    {
        var player = state.FindPlayer(gang.Owner)!;
        batch.Draw(pixel, CombatPanelLayout.HeaderColor(rightSide), PlayerColors[gang.Owner.Value]);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, CombatPanelLayout.HeaderPortrait(rightSide),
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        var name = player.Setup.Name.ToUpperInvariant();
        if (name.Length > 10) name = name[..10];
        font.Draw(batch, name, CombatPanelLayout.HeaderName(rightSide).ToVector2(),
            PlayerColors[gang.Owner.Value], 1);

        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, CombatPanelLayout.GangPortrait(rightSide),
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        var damage = gang.Id == clip.Defender ? CombatDamage(gameEvent, clip) : 0;
        DrawDetailedCombatForce(batch, pixel, rightSide, gang.Force, damage);

        DrawCombatItem(batch, state, eventWeapon ?? gang.WeaponItemId, CombatPanelLayout.EquipmentItem(rightSide, 0));
        DrawCombatItem(batch, state, gang.ArmorItemId, CombatPanelLayout.EquipmentItem(rightSide, 1));
        DrawCombatItem(batch, state, gang.MiscellaneousItemId, CombatPanelLayout.EquipmentItem(rightSide, 2));
    }

    private void DrawPoliceCombatant(SpriteBatch batch, Texture2D pixel, PixelFont font, bool rightSide)
    {
        font.Draw(batch, "POLICE", CombatPanelLayout.PoliceName(rightSide).ToVector2(), Color.LightBlue, 1);
        if (_policeSprites is not null)
            batch.Draw(_policeSprites, CombatPanelLayout.PolicePortrait(rightSide),
                OriginalSpriteLayout.PolicePatrolCar, Color.White);
        DrawDetailedCombatForce(batch, pixel, rightSide, ManualRules.MaximumForce, 0);
    }

    private void DrawCombatItem(SpriteBatch batch, MatchState state, short? itemId, Rectangle aperture)
    {
        if (itemId is not { } resolved || resolved < 0 || resolved >= state.Definitions.Items.Count) return;
        var item = state.Definitions.Items[resolved];
        if (resolved < _itemRotationTextures.Length && _itemRotationTextures[resolved] is { } rotation)
        {
            batch.Draw(rotation, aperture,
                ItemRotationPresentation.Frame(item.CombatPortraitFrame), Color.White);
            return;
        }

        if (_itemPortraits is not null)
            batch.Draw(_itemPortraits, new Rectangle(aperture.Center.X - 15, aperture.Center.Y - 15, 30, 30),
                OriginalSpriteLayout.ItemPortrait(resolved), Color.White);
    }

    private void DrawCombatForce(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle bar,
        int force,
        int damage = 0)
    {
        DrawBeveledForce(batch, pixel, bar, 0, bar.Width, red: true);
        var width = Math.Clamp(bar.Width * force / ManualRules.MaximumForce, 0, bar.Width);
        if (width > 0)
            DrawBeveledForce(batch, pixel, bar, 0, width, red: false);

        var previousForce = Math.Clamp(force + damage, 0, ManualRules.MaximumForce);
        var previousWidth = Math.Clamp(
            bar.Width * previousForce / ManualRules.MaximumForce, width, bar.Width);
        if (previousWidth <= width) return;
        if (_combatAnimationPlayer.ShowsPreDamageForce)
            DrawBeveledForce(batch, pixel, bar, width, previousWidth - width, red: false);
        else if (_combatAnimationPlayer.ShowsDamageFlash)
            batch.Draw(pixel, new Rectangle(bar.X + width, bar.Y, previousWidth - width, bar.Height), Color.White);
    }

    private void DrawDetailedCombatForce(
        SpriteBatch batch,
        Texture2D pixel,
        bool rightSide,
        int force,
        int damage)
    {
        DrawCombatForce(batch, pixel, CombatPanelLayout.ForceBar(rightSide), force, damage);
    }

    private static void DrawBeveledForce(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle bar,
        int offset,
        int width,
        bool red)
    {
        if (width <= 0) return;
        var x = bar.X + offset;
        for (var row = 0; row < bar.Height; row++)
        {
            var color = (red, row) switch
            {
                (true, 0) => new Color(255, 148, 148),
                (true, 1) => new Color(247, 0, 0),
                (true, _) => new Color(148, 0, 0),
                (false, 0) => new Color(148, 255, 148),
                (false, 1) => new Color(0, 247, 0),
                _ => new Color(0, 140, 0)
            };
            batch.Draw(pixel, new Rectangle(x, bar.Y + row, width, 1), color);
        }
    }

    private static int CombatDamage(GameEvent? gameEvent, CombatAnimationClip clip)
    {
        if (gameEvent?.Kind == GameEventKind.PoliceAttackResolved)
            return gameEvent.PoliceAttack?.Damage ?? 0;
        if (gameEvent?.Resolution is not { } resolution) return 0;
        return clip.Reversed ? resolution.RetaliationDamage : resolution.Damage;
    }

    private void DrawCombatFrames(SpriteBatch batch, Texture2D pixel, CombatAnimationClip clip)
    {
        var frame = CombatAnimationRouting.FrameSource(_combatAnimationPlayer.Frame);
        var attackDestination = CombatPanelLayout.Animation(right: clip.Reversed);
        var hitDestination = CombatPanelLayout.Animation(right: !clip.Reversed);
        batch.Draw(pixel, attackDestination, Color.Black);
        batch.Draw(pixel, hitDestination, Color.Black);
        if (clip.HitAnimation is { } hit && _combatAnimationTextures.TryGetValue(
                CombatAnimationRouting.HitFile(hit, clip.Reversed), out var hitTexture))
            batch.Draw(hitTexture, hitDestination, frame, Color.White);
        if (_combatAnimationTextures.TryGetValue(
                CombatAnimationRouting.AttackFile(clip.AttackAnimation, clip.Reversed), out var attackTexture))
            batch.Draw(attackTexture, attackDestination, frame, Color.White);
    }
}
