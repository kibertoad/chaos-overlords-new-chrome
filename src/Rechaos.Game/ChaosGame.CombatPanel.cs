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
        // A reversed clip is an attack on the viewer's gang, which keeps the left side while its
        // attacker, a gang or the police, strikes from the right.
        var attacker = clip.Attacker is { } attackerId ? state.FindCombatant(gameEvent, attackerId) : null;
        var defender = state.FindCombatant(gameEvent, clip.Defender);
        // Police fought where the crackdown is, which a gang that moved on afterwards has left.
        var sectorId = gameEvent?.PoliceAttack?.SectorId
            ?? attacker?.SectorId ?? defender?.SectorId ?? 0;
        DrawCombatSector(batch, font, state, sectorId);

        var attackerOnRight = clip.Reversed;
        if (clip.Police)
            DrawPoliceCombatant(batch, pixel, rightSide: attackerOnRight);
        else if (attacker is not null && clip.Forces.AttackerAfter is { } attackerForce)
            DrawCombatant(batch, pixel, font, state, attacker, rightSide: attackerOnRight,
                gameEvent?.Resolution?.ItemId,
                clip.AttackerStart ?? clip.Forces.AttackerBefore ?? attackerForce,
                attackerForce, clip.Forces.AttackerDamage);
        if (defender is not null)
            DrawCombatant(batch, pixel, font, state, defender, rightSide: !attackerOnRight,
                gameEvent?.Resolution?.RetaliationItemId,
                clip.DefenderStart ?? clip.Forces.DefenderBefore,
                clip.Forces.DefenderAfter, clip.Forces.DefenderDamage);

        DrawCombatFrames(batch, pixel, clip);
        DrawPressedCombatExit(batch);
    }

    /// <summary>
    /// The pressed Exit face while a press that started on it is held over it; the release there
    /// ends the whole presentation (SCR-COMBAT-002, FND-COMBAT-010).
    /// </summary>
    private void DrawPressedCombatExit(SpriteBatch batch)
    {
        if (!_combatExit.ShowsPressed || _uiSprites is null) return;
        batch.Draw(_uiSprites, CombatPanelLayout.Exit, CombatPanelLayout.ExitPressedSource, Color.White);
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
        int start,
        int force,
        int damage)
    {
        // SCR-COMBAT-002, FND-COMBAT-014: the side clears its name field (left) or its whole
        // header (right) in black, fills the colour strip with the owner's colour, copies the
        // Overlord portrait and writes the owner's name with fn_00413FD5.
        var player = state.FindPlayer(gang.Owner)!;
        batch.Draw(pixel, CombatPanelLayout.HeaderClear(rightSide), Color.Black);
        batch.Draw(pixel, CombatPanelLayout.HeaderColor(rightSide), PlayerColors[gang.Owner.Value]);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, CombatPanelLayout.HeaderPortrait(rightSide),
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        var name = player.Setup.Name.ToUpperInvariant();
        if (name.Length > 10) name = name[..10];
        font.Draw(batch, name, CombatPanelLayout.HeaderName(rightSide).ToVector2(), Color.Lime, 1);

        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, CombatPanelLayout.GangPortrait(rightSide),
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawDetailedCombatForce(batch, pixel, rightSide, start, force, damage);

        DrawCombatItem(batch, state, eventWeapon ?? gang.WeaponItemId, CombatPanelLayout.EquipmentItem(rightSide, 0));
        DrawCombatItem(batch, state, gang.ArmorItemId, CombatPanelLayout.EquipmentItem(rightSide, 1));
        DrawCombatItem(batch, state, gang.MiscellaneousItemId, CombatPanelLayout.EquipmentItem(rightSide, 2));
    }

    /// <summary>
    /// SCR-COMBAT-002, FND-COMBAT-014: the police opponent, always on the right. The side clears
    /// its header in black and copies the header, the portrait and the three item pictures from
    /// <c>PX00300</c>.
    /// </summary>
    private void DrawPoliceCombatant(SpriteBatch batch, Texture2D pixel, bool rightSide)
    {
        batch.Draw(pixel, CombatPanelLayout.HeaderClear(rightSide), Color.Black);
        batch.Draw(pixel, CombatPanelLayout.GangPortrait(rightSide), Color.Black);
        if (_policeSprites is not null)
        {
            batch.Draw(_policeSprites, CombatPanelLayout.PoliceHeader,
                CombatPanelLayout.PoliceHeaderSource, Color.White);
            batch.Draw(_policeSprites, CombatPanelLayout.GangPortrait(rightSide),
                CombatPanelLayout.PolicePortraitSource, Color.White);
            for (var slot = 0; slot < 3; slot++)
                batch.Draw(_policeSprites, CombatPanelLayout.EquipmentItem(rightSide, slot),
                    CombatPanelLayout.PoliceItemSource(slot), Color.White);
        }
        // The police are drawn with Force 10 on both tracks (RULE-COMBAT-004).
        DrawDetailedCombatForce(batch, pixel, rightSide,
            ManualRules.MaximumForce, ManualRules.MaximumForce, 0);
    }

    private void DrawCombatItem(SpriteBatch batch, MatchState state, short? itemId, Rectangle aperture)
    {
        // FND-COMBAT-014: an empty slot is filled with black.
        if (itemId is not { } resolved || resolved < 0 || resolved >= state.Definitions.Items.Count)
        {
            if (_pixel is not null) batch.Draw(_pixel, aperture, Color.Black);
            return;
        }
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

    /// <summary>
    /// The lower track of SCR-COMBAT-002, <c>force_shown</c>: the Force before the clip until its
    /// hits land, the part lost in white on ticks 13 and 15, and the new Force after
    /// (FND-COMBAT-010).
    /// </summary>
    private void DrawShownCombatForce(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle bar,
        int force,
        int damage)
    {
        var width = CombatPanelLayout.TrackFill(force);
        var previousWidth = Math.Max(width,
            CombatPanelLayout.TrackFill(Math.Min(force + damage, ManualRules.MaximumForce)));
        if (previousWidth > width && _combatAnimationPlayer.ShowsPreDamageForce)
        {
            DrawForceTrack(batch, pixel, bar, previousWidth);
            return;
        }
        DrawForceTrack(batch, pixel, bar, width);
        if (previousWidth > width && _combatAnimationPlayer.ShowsDamageFlash)
            batch.Draw(pixel, new Rectangle(bar.X + width, bar.Y, previousWidth - width, bar.Height), Color.White);
    }

    /// <summary>
    /// The two tracks of one gang in SCR-COMBAT-002: <c>force_start</c> above, which does not
    /// move, and <c>force_shown</c> below (FND-COMBAT-009, FND-COMBAT-010).
    /// </summary>
    private void DrawDetailedCombatForce(
        SpriteBatch batch,
        Texture2D pixel,
        bool rightSide,
        int start,
        int force,
        int damage)
    {
        DrawForceTrack(batch, pixel, CombatPanelLayout.ForceBar(rightSide, 0),
            CombatPanelLayout.TrackFill(start));
        DrawShownCombatForce(batch, pixel, CombatPanelLayout.ForceBar(rightSide, 1), force, damage);
    }

    /// <summary>
    /// One Force track as the original copies it from <c>PX00129</c>: the red track across the
    /// whole width, then <paramref name="fill"/> pixels of the green strip from its left edge
    /// (FND-COMBAT-009). Without the sheet the rows are drawn in the art's three intensities.
    /// </summary>
    private void DrawForceTrack(SpriteBatch batch, Texture2D pixel, Rectangle bar, int fill)
    {
        fill = Math.Clamp(fill, 0, bar.Width);
        if (_uiSprites is not null)
        {
            var red = CombatPanelLayout.RedTrackSource;
            batch.Draw(_uiSprites, bar, red with { Width = bar.Width }, Color.White);
            if (fill > 0)
                batch.Draw(_uiSprites, bar with { Width = fill },
                    CombatPanelLayout.GreenTrackSource(fill), Color.White);
            return;
        }
        DrawBeveledForce(batch, pixel, bar, 0, bar.Width, red: true);
        DrawBeveledForce(batch, pixel, bar, 0, fill, red: false);
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
        if (!_combatAnimationPlayer.ShowsDimmedFrames) return;
        // FND-COMBAT-014: from tick 12 the last frames are darkened with black through bitmap
        // 143, anchored at the corner of the two stacked 64-by-64 frames.
        _combatFrameDimPattern ??= CreateCombatFrameDimPattern(GraphicsDevice);
        batch.Draw(_combatFrameDimPattern, attackDestination, Color.White);
        batch.Draw(_combatFrameDimPattern, hitDestination, Color.White);
    }

    private Texture2D? _combatFrameDimPattern;

    private static Texture2D CreateCombatFrameDimPattern(GraphicsDevice graphicsDevice)
    {
        var size = CombatAnimationRouting.FrameSize;
        var texture = new Texture2D(graphicsDevice, size, size);
        texture.SetData(OriginalPatternMask.ShadedRectangle(
            OriginalPatternMask.ForGrey(0x7fff), size, size, Color.Black, Color.Black));
        return texture;
    }
}
