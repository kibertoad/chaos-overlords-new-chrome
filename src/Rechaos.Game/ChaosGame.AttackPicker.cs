using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private enum AttackFace
    {
        Cancel,
        Confirm
    }

    private AttackPickerSelection _attackSelection = AttackPickerSelection.None;
    private AttackFace? _pressedAttackFace;

    private bool IsAttackPickerOpen() => _screens.Current == ClientScreen.Commands
        && _choosingCommandTarget && IsAttackCommandPicker();

    /// <summary>SCR-ATTACK-001, FND-ATTACK-003: the selection the picker opens with.</summary>
    private void OpenAttackPicker()
    {
        _pressedAttackFace = null;
        _attackTargetClicks.Cancel();
        _attackSelection = _state is not null && _commandTargetOptions.Count > 0
            && _state.FindGang(_commandTargetOptions[0].Gang) is { } actor
            ? AttackPicker.Initial(_state, _commandTargetOptions, actor)
            : AttackPickerSelection.None;
        _commandTargetCursor = _attackSelection.Target ?? 0;
    }

    private void HandleAttackCommandClick(Point point)
    {
        if (_state is null) return;
        // SCR-ATTACK-001, FND-ATTACK-003: a press outside the panel is refused and leaves it open.
        if (!AttackCommandLayout.Panel.Contains(point))
        {
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
            return;
        }
        // SCR-ATTACK-001, FND-ATTACK-003: both faces act when the button is released on them.
        if (AttackCommandLayout.Cancel.Contains(point))
        {
            _pressedAttackFace = AttackFace.Cancel;
            AcceptInput();
            return;
        }
        if (AttackCommandLayout.Ok.Contains(point))
        {
            if (!_attackSelection.CanConfirm)
            {
                RejectInput("CHOOSE A TARGET");
                return;
            }
            _pressedAttackFace = AttackFace.Confirm;
            AcceptInput();
            return;
        }

        var actor = _state.FindGang(_commandTargetOptions[0].Gang);
        if (actor is null) return;
        var opponents = AttackPicker.Opponents(actor.Owner);
        for (var slot = 0; slot < opponents.Count; slot++)
        {
            if (!AttackCommandLayout.Opponent(slot).Contains(point)) continue;
            // SCR-ATTACK-001, FND-ATTACK-001: a disabled cell does not react; an enabled one
            // chooses its player and clears the chosen target.
            if (AttackPicker.IsOpponentEnabled(_state, _commandTargetOptions, opponents[slot]))
                _attackSelection = new AttackPickerSelection(opponents[slot], null);
            _attackTargetClicks.Cancel();
            return;
        }

        var cells = AttackPicker.TargetCells(_state, _commandTargetOptions, _attackSelection.Opponent);
        var cell = AttackCommandLayout.TargetCellAt(point);
        if (cell >= 0 && cell < cells.Count)
        {
            _attackSelection = _attackSelection with { Target = cells[cell] };
            _commandTargetCursor = cells[cell];
        }
        HandleAttackDoubleClick(point, actor, cells);
    }

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-004: a double-click on the acting gang's portrait or on a
    /// listed target's portrait opens that gang's information panel, and one on an item icon opens
    /// Item Information. The picker's selection is kept for the return. It plays no sound.
    /// </summary>
    private void HandleAttackDoubleClick(Point point, MatchGangState actor, IReadOnlyList<int> cells)
    {
        if (_state is null) return;
        var region = -1;
        MatchGangState? gang = null;
        short? itemId = null;
        if (AttackCommandLayout.ActorPortrait.Contains(point))
        {
            region = 0;
            gang = actor;
        }
        for (var slot = 0; region < 0 && slot < AttackCommandLayout.EquippedItemCount; slot++)
        {
            if (!AttackCommandLayout.ActorItem(slot).Contains(point)
                || EquippedItem(actor, slot) is not { } item) continue;
            region = 1 + slot;
            itemId = item;
        }
        for (var cell = 0; region < 0 && cell < cells.Count; cell++)
        {
            if (_state.FindGang(new GangId(_commandTargetOptions[cells[cell]].Target.Id)) is not { } target)
                continue;
            // Regions are keyed by the target gang so a double-click has to land on one gang.
            var baseRegion = 4 + target.Id.Value * 4;
            if (AttackCommandLayout.TargetPortrait(cell).Contains(point))
            {
                region = baseRegion;
                gang = target;
                break;
            }
            for (var slot = 0; slot < AttackCommandLayout.EquippedItemCount; slot++)
            {
                if (!AttackCommandLayout.TargetItem(cell, slot).Contains(point)
                    || EquippedItem(target, slot) is not { } item) continue;
                region = baseRegion + 1 + slot;
                itemId = item;
                break;
            }
        }

        if (region < 0)
        {
            _attackTargetClicks.Cancel();
            return;
        }
        if (!_attackTargetClicks.Register(region, _inputTime)) return;
        if (gang is not null) OpenGangDetails(gang, ClientScreen.Commands);
        else if (itemId is { } resolved) OpenItemDetails(resolved);
    }

    private void CompleteAttackFace(Point point)
    {
        var face = _pressedAttackFace;
        CancelAttackFace();
        if (face is null || !IsAttackPickerOpen()) return;
        if (face == AttackFace.Cancel && AttackCommandLayout.Cancel.Contains(point))
            BackFromCommands();
        else if (face == AttackFace.Confirm && AttackCommandLayout.Ok.Contains(point))
            ConfirmAttack(pointerButton: true);
    }

    private void CancelAttackFace() => _pressedAttackFace = null;

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: Confirm orders the attack on the chosen target when an
    /// opponent and a target are chosen, and is refused otherwise.
    /// </summary>
    private void ConfirmAttack(bool pointerButton = false)
    {
        if (!_attackSelection.CanConfirm || _attackSelection.Target is not { } target
            || target >= _commandTargetOptions.Count)
        {
            RejectInput("CHOOSE A TARGET");
            return;
        }
        SubmitCommand(_commandTargetOptions[target], pointerButton);
    }

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: the handler tests Enter, 0x2B and Escape and no other key,
    /// so the rebuild's arrow and Backspace keys do nothing here.
    /// </summary>
    private void UpdateAttackPicker(KeyboardState keyboard)
    {
        if (AttackCommandLayout.ConfirmKeys.Any(key => Pressed(keyboard, key))) ConfirmAttack();
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-003: Escape presses Cancel and closes without an order.</summary>
    private void CancelAttackPickerByKey()
    {
        CancelAttackFace();
        AcceptInput();
        BackFromCommands();
    }

    private void DrawAttackCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _targetAcquisitionBackground, AttackCommandLayout.Panel, 248);

        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        DrawAttackGang(batch, actor, AttackCommandLayout.ActorPortrait, AttackCommandLayout.ActorItem);

        var opponents = AttackPicker.Opponents(actor.Owner);
        for (var slot = 0; slot < opponents.Count; slot++)
        {
            if (_uiSprites is null || state.FindPlayer(opponents[slot]) is not { } opponent) continue;
            var enabled = AttackPicker.IsOpponentEnabled(state, _commandTargetOptions, opponent.Id);
            batch.Draw(_uiSprites, AttackCommandLayout.Opponent(slot),
                AttackCommandLayout.OpponentSource(opponent.Setup.PortraitId, enabled), Color.White);
        }

        // SCR-ATTACK-001: the target cards' art is not recorded; the rebuild draws each target's
        // portrait and items at the rectangles FND-ATTACK-004 tests, as for the acting gang.
        var cells = AttackPicker.TargetCells(state, _commandTargetOptions, _attackSelection.Opponent);
        for (var cell = 0; cell < cells.Count; cell++)
        {
            var candidate = state.FindGang(new GangId(_commandTargetOptions[cells[cell]].Target.Id))!;
            DrawAttackGang(batch, candidate, AttackCommandLayout.TargetPortrait(cell),
                itemSlot => AttackCommandLayout.TargetItem(cell, itemSlot));
        }

        if (_uiKeyedSprites is not null)
        {
            var opponentSlot = _attackSelection.Opponent is { } chosen
                ? opponents.ToList().IndexOf(chosen)
                : -1;
            if (opponentSlot >= 0)
                batch.Draw(_uiKeyedSprites, AttackCommandLayout.OpponentFrame(opponentSlot),
                    AttackCommandLayout.OpponentFrameSource, Color.White);
            var targetCell = _attackSelection.Target is { } target ? cells.ToList().IndexOf(target) : -1;
            if (targetCell >= 0)
                batch.Draw(_uiKeyedSprites, AttackCommandLayout.TargetMarker(targetCell),
                    AttackCommandLayout.TargetMarkerSource, Color.White);
        }

        if (_uiSprites is null) return;
        // FND-ATTACK-004, FND-UI-019: the Confirm face shows whether the order can be confirmed.
        batch.Draw(_uiSprites, AttackCommandLayout.Ok, _attackSelection.CanConfirm
            ? AttackCommandLayout.OkEnabledSource
            : AttackCommandLayout.OkDisabledSource, Color.White);
        if (_hoverPoint is not { } hover) return;
        if (_pressedAttackFace == AttackFace.Cancel && AttackCommandLayout.Cancel.Contains(hover))
            batch.Draw(_uiSprites, AttackCommandLayout.Cancel,
                AttackCommandLayout.CancelPressedSource, Color.White);
        else if (_pressedAttackFace == AttackFace.Confirm && AttackCommandLayout.Ok.Contains(hover))
            batch.Draw(_uiSprites, AttackCommandLayout.Ok,
                AttackCommandLayout.OkPressedSource, Color.White);
    }

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: a gang's 64-by-64 portrait and the 20-by-20 icon of each
    /// item it holds.
    /// </summary>
    private void DrawAttackGang(
        SpriteBatch batch,
        MatchGangState gang,
        Rectangle portrait,
        Func<int, Rectangle> itemDestination)
    {
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        if (_itemPortraits is null) return;
        for (var slot = 0; slot < AttackCommandLayout.EquippedItemCount; slot++)
            if (EquippedItem(gang, slot) is { } itemId)
                batch.Draw(_itemPortraits, itemDestination(slot),
                    OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
    }
}
