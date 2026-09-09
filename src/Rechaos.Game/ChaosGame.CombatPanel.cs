using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawCombatPanel(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_combatAnimationPlayer.Active is not { } clip) return;
        if (_combatBackground is not null)
            batch.Draw(_combatBackground, CombatPanelLayout.Panel, Color.White);
        else
            batch.Draw(pixel, CombatPanelLayout.Panel, new Color(0, 0, 0, 245));

        var gameEvent = state.Events.FirstOrDefault(value => value.Sequence == clip.EventSequence);
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
                gameEvent?.Resolution?.ItemId);
        if (right is not null)
            DrawCombatant(batch, pixel, font, state, right, rightSide: true,
                gameEvent?.Resolution?.RetaliationItemId);

        DrawCombatFrames(batch, pixel, clip);
    }

    private void DrawCombatSector(SpriteBatch batch, PixelFont font, MatchState state, int sectorId)
    {
        var sector = state.Sectors[sectorId];
        var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
        if (layer is not null)
            batch.Draw(layer, CombatPanelLayout.Sector, CityMapLayout.Source(sectorId), Color.White);
        font.Draw(batch, SectorCode(sectorId), new Vector2(148, 203), Color.Lime, 1);
    }

    private void DrawCombatant(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        MatchGangState gang,
        bool rightSide,
        short? eventWeapon)
    {
        var player = state.FindPlayer(gang.Owner)!;
        batch.Draw(pixel, CombatPanelLayout.HeaderColor(rightSide), PlayerColors[gang.Owner.Value]);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, CombatPanelLayout.HeaderPortrait(rightSide),
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        var header = rightSide ? CombatPanelLayout.RightHeader : CombatPanelLayout.LeftHeader;
        var name = player.Setup.Name.ToUpperInvariant();
        if (name.Length > 10) name = name[..10];
        font.Draw(batch, name, new Vector2(header.X + 52, header.Y + 4),
            PlayerColors[gang.Owner.Value], 1);

        var gangCell = rightSide ? CombatPanelLayout.RightGang : CombatPanelLayout.LeftGang;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, new Rectangle(gangCell.X + 1, gangCell.Y + 1, 64, 64),
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawCombatForce(batch, pixel, CombatPanelLayout.ForceBar(rightSide), gang.Force);

        var weaponCell = rightSide ? CombatPanelLayout.RightWeapon : CombatPanelLayout.LeftWeapon;
        DrawCombatItem(batch, eventWeapon ?? gang.WeaponItemId, weaponCell.Center.X, weaponCell.Y + 27);
        var equipmentCell = rightSide ? CombatPanelLayout.RightEquipment : CombatPanelLayout.LeftEquipment;
        DrawCombatItem(batch, gang.ArmorItemId, equipmentCell.Center.X, equipmentCell.Y + 13);
        DrawCombatItem(batch, gang.MiscellaneousItemId, equipmentCell.Center.X, equipmentCell.Y + 40);
    }

    private void DrawPoliceCombatant(SpriteBatch batch, Texture2D pixel, PixelFont font, bool rightSide)
    {
        var header = rightSide ? CombatPanelLayout.RightHeader : CombatPanelLayout.LeftHeader;
        font.Draw(batch, "POLICE", new Vector2(header.X + 8, header.Y + 4), Color.LightBlue, 1);
        var gangCell = rightSide ? CombatPanelLayout.RightGang : CombatPanelLayout.LeftGang;
        if (_policeSprites is not null)
            batch.Draw(_policeSprites, new Rectangle(gangCell.X + 8, gangCell.Y + 8, 48, 64),
                OriginalSpriteLayout.PolicePatrolCar, Color.White);
        DrawCombatForce(batch, pixel, CombatPanelLayout.ForceBar(rightSide), ManualRules.MaximumForce);
    }

    private void DrawCombatItem(SpriteBatch batch, short? itemId, int centerX, int centerY)
    {
        if (_itemPortraits is null || itemId is not { } resolved) return;
        batch.Draw(_itemPortraits, new Rectangle(centerX - 15, centerY - 15, 30, 30),
            OriginalSpriteLayout.ItemPortrait(resolved), Color.White);
    }

    private static void DrawCombatForce(SpriteBatch batch, Texture2D pixel, Rectangle bar, int force)
    {
        batch.Draw(pixel, bar, Color.DarkRed);
        var width = Math.Clamp(bar.Width * force / ManualRules.MaximumForce, 0, bar.Width);
        if (width > 0)
            batch.Draw(pixel, new Rectangle(bar.X, bar.Y, width, bar.Height), Color.Lime);
    }

    private void DrawCombatFrames(SpriteBatch batch, Texture2D pixel, CombatAnimationClip clip)
    {
        var frame = CombatAnimationRouting.FrameSource(_combatAnimationPlayer.Frame);
        var attackDestination = clip.Reversed ? CombatPanelLayout.RightAction : CombatPanelLayout.LeftAction;
        var hitDestination = clip.Reversed ? CombatPanelLayout.LeftAction : CombatPanelLayout.RightAction;
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
