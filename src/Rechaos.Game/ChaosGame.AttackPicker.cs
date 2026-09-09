using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void HandleAttackCommandClick(Point point)
    {
        if (AttackCommandLayout.Cancel.Contains(point))
        {
            BackFromCommands();
            return;
        }
        if (AttackCommandLayout.Ok.Contains(point))
        {
            ActivateCommandSelection();
            return;
        }

        if (_state is null || _commandTargetOptions.Count == 0) return;
        var owners = AttackTargetOwners(_state);
        for (var slot = 0; slot < owners.Count; slot++)
        {
            if (!AttackCommandLayout.Opponent(slot).Contains(point)) continue;
            SelectFirstAttackTarget(owners[slot]);
            return;
        }

        var targets = AttackTargetIndicesForSelectedOwner(_state);
        for (var slot = 0; slot < Math.Min(targets.Count, AttackCommandLayout.VisibleTargets); slot++)
        {
            if (!AttackCommandLayout.TargetCard(slot).Contains(point)) continue;
            _commandTargetCursor = targets[slot];
            return;
        }
        if (!AttackCommandLayout.Panel.Contains(point)) BackFromCommands();
    }

    private void DrawAttackCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_targetAcquisitionBackground is not null)
            batch.Draw(_targetAcquisitionBackground, AttackCommandLayout.Panel, Color.White);
        else
            batch.Draw(pixel, AttackCommandLayout.Panel, new Color(0, 0, 0, 248));

        var selected = _commandTargetOptions[_commandTargetCursor];
        var actor = state.FindGang(selected.Gang)!;
        var target = state.FindGang(new GangId(selected.Target.Id))!;
        DrawAttackGang(batch, pixel, actor, AttackCommandLayout.ActorPortrait,
            AttackCommandLayout.ActorForceBar, AttackCommandLayout.ActorItem);

        var owners = AttackTargetOwners(state);
        for (var slot = 0; slot < owners.Count; slot++)
        {
            var owner = state.FindPlayer(owners[slot])!;
            var destination = AttackCommandLayout.Opponent(slot);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, destination,
                    OriginalSpriteLayout.OverlordPortrait(owner.Setup.PortraitId), Color.White);
            DrawBorder(batch, pixel, destination, PlayerColors[owner.Id.Value],
                owner.Id == target.Owner ? 2 : 1);
        }

        var targets = AttackTargetIndicesForSelectedOwner(state);
        foreach (var entry in targets.Take(AttackCommandLayout.VisibleTargets)
                     .Select((commandIndex, slot) => (commandIndex, slot)))
        {
            var candidate = state.FindGang(new GangId(
                _commandTargetOptions[entry.commandIndex].Target.Id))!;
            DrawAttackGang(batch, pixel, candidate,
                AttackCommandLayout.TargetPortrait(entry.slot),
                AttackCommandLayout.TargetForceBar(entry.slot),
                itemSlot => AttackCommandLayout.TargetItem(entry.slot, itemSlot));
            if (entry.commandIndex == _commandTargetCursor)
                DrawTargetReticle(batch, pixel, AttackCommandLayout.TargetPortrait(entry.slot));
        }

        DrawActiveAttackOk(batch, pixel, font);
    }

    private void DrawAttackGang(
        SpriteBatch batch,
        Texture2D pixel,
        MatchGangState gang,
        Rectangle portrait,
        Rectangle forceBar,
        Func<int, Rectangle> itemDestination)
    {
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawBorder(batch, pixel, portrait, PlayerColors[gang.Owner.Value], 1);

        var itemIds = new short?[] { gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId };
        for (var slot = 0; slot < itemIds.Length; slot++)
        {
            var destination = itemDestination(slot);
            batch.Draw(pixel, destination, Color.Black);
            DrawBorder(batch, pixel, destination, Color.LightGray, 1);
            if (_itemPortraits is not null && itemIds[slot] is { } itemId)
                batch.Draw(_itemPortraits, destination,
                    OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
        }

        batch.Draw(pixel, forceBar, Color.DarkRed);
        var forceWidth = forceBar.Width * gang.Force / ManualRules.MaximumForce;
        if (forceWidth > 0)
            batch.Draw(pixel, new Rectangle(forceBar.X, forceBar.Y, forceWidth, forceBar.Height),
                Color.Lime);
    }

    private void DrawTargetReticle(SpriteBatch batch, Texture2D pixel, Rectangle target)
    {
        var center = target.Center;
        var red = new Color(220, 30, 20);
        batch.Draw(pixel, new Rectangle(center.X - 13, center.Y, 27, 1), red);
        batch.Draw(pixel, new Rectangle(center.X, center.Y - 13, 1, 27), red);
        DrawBorder(batch, pixel, new Rectangle(center.X - 8, center.Y - 8, 17, 17), red, 1);
    }

    private IReadOnlyList<PlayerId> AttackTargetOwners(MatchState state) => _commandTargetOptions
        .Select(command => state.FindGang(new GangId(command.Target.Id))?.Owner)
        .OfType<PlayerId>()
        .Distinct()
        .OrderBy(owner => owner.Value)
        .Take(5)
        .ToArray();

    private void SelectFirstAttackTarget(PlayerId owner)
    {
        if (_state is null) return;
        for (var index = 0; index < _commandTargetOptions.Count; index++)
        {
            if (_state.FindGang(new GangId(_commandTargetOptions[index].Target.Id))?.Owner != owner) continue;
            _commandTargetCursor = index;
            return;
        }
    }

    private IReadOnlyList<int> AttackTargetIndicesForSelectedOwner(MatchState state)
    {
        if (_commandTargetOptions.Count == 0) return [];
        var current = state.FindGang(new GangId(_commandTargetOptions[_commandTargetCursor].Target.Id));
        return current is null
            ? []
            : Enumerable.Range(0, _commandTargetOptions.Count)
                .Where(index => state.FindGang(
                    new GangId(_commandTargetOptions[index].Target.Id))?.Owner == current.Owner)
                .ToArray();
    }

    private static void DrawActiveAttackOk(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var button = AttackCommandLayout.Ok;
        batch.Draw(pixel, button, new Color(5, 18, 8));
        DrawBorder(batch, pixel, button, Color.Lime, 2);
        font.Draw(batch, "OK", new Vector2(button.Center.X - 6, button.Y + 8), Color.Lime, 1);
    }
}
