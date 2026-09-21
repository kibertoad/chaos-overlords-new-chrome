using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private readonly HoverDwellTracker _commandTooltipDwell = new();

    private void OpenCommands(bool repeat = false, ClientScreen returnScreen = ClientScreen.City)
    {
        if (!CanOpenCommands(out var playerId)) return;
        var gang = SelectedGang(_state!.FindPlayer(playerId)!);
        if (gang is null)
        {
            RejectInput("NO ACTIVE GANG");
            return;
        }
        _commandOptions = CommandOptionCatalog.LegalCommands(_state, playerId, gang.Id)
            .Where(command => !repeat || CommandRules.CanRepeat(command.Action))
            .ToArray();
        ShowCommandOverlay(repeat, returnScreen, bulk: false);
    }

    /// <summary>
    /// Opens the command overlay on the whole ctrl-picked selection, listing only the orders a
    /// selection may be given at once.
    /// </summary>
    private void OpenBulkCommands(bool repeat)
    {
        if (!CanOpenCommands(out var playerId)) return;
        _commandOptions = BulkGangCommands.Options(
            _state!, playerId, _gangSelection.Gangs, repeat);
        ShowCommandOverlay(repeat, ClientScreen.Sector, bulk: true);
    }

    private bool CanOpenCommands(out PlayerId playerId)
    {
        if (_state is not null && _state.Coordinator.Phase == TurnPhase.Command
            && _state.Coordinator.ActivePlayer is { } active)
        {
            playerId = active;
            return true;
        }
        RejectInput("COMMANDS REQUIRE COMMAND PHASE");
        playerId = default;
        return false;
    }

    private void ShowCommandOverlay(bool repeat, ClientScreen returnScreen, bool bulk)
    {
        _commandCursor = 0;
        _commandTargetOptions = [];
        _commandTargetCursor = 0;
        _choosingCommandTarget = false;
        _commandRepeats = repeat;
        _bulkCommand = bulk;
        _commandReturnScreen = returnScreen;
        _screens.Show(ClientScreen.Commands);
    }

    /// <summary>The orders the overlay lists, which a bulk order shortens to its allowlist.</summary>
    private IReadOnlyList<GangAction> CommandOverlayActions => _bulkCommand
        ? BulkGangCommands.ActionsFor(_commandRepeats)
        : CommandOverlayLayout.ActionsFor(_commandRepeats);

    /// <summary>
    /// Feeds the hovered action row to the dwell tracker so the command overlay can explain
    /// an entry once the pointer has rested on it, without reacting to a passing cursor.
    /// </summary>
    private void UpdateCommandTooltipDwell()
    {
        var hoveredRow = HoveredCommandActionRow();
        _commandTooltipDwell.Update(hoveredRow, _inputTime);
        if (hoveredRow is { } row) _commandCursor = row;
    }

    private int? HoveredCommandActionRow() =>
        _screens.Current == ClientScreen.Commands && !_choosingCommandTarget && !_gameMenuOpen
        && _hoverPoint is { } hover
            ? CommandActionTooltips.RowAt(hover, CommandOverlayActions)
            : null;

    private void MoveCommandCursor(int delta)
    {
        if (_choosingCommandTarget)
        {
            if (IsEquipmentCommandPicker() && _state is not null)
            {
                var indices = EquipmentCommandIndices(_state);
                if (indices.Count > 0)
                {
                    var position = indices.IndexOf(_commandTargetCursor);
                    _commandTargetCursor = indices[Mod(position + delta, indices.Count)];
                }
            }
            else if (_commandTargetOptions.Count > 0)
                _commandTargetCursor = Mod(_commandTargetCursor + delta, _commandTargetOptions.Count);
        }
        else
        {
            _commandCursor = Mod(_commandCursor + delta, CommandOverlayActions.Count);
        }
    }

    private void HandleCommandsClick(Point point)
    {
        if (_choosingCommandTarget)
        {
            if (IsMovementCommandPicker())
            {
                HandleMovementCommandClick(point);
                return;
            }
            if (IsAttackCommandPicker())
            {
                HandleAttackCommandClick(point);
                return;
            }
            if (IsInfluenceCommandPicker())
            {
                if (InfluenceCommandLayout.Cancel.Contains(point))
                {
                    AcceptInput();
                    BackFromCommands();
                    return;
                }
                if (InfluenceCommandLayout.Ok.Contains(point))
                {
                    ActivateCommandSelection();
                    return;
                }
                for (var slot = 0; slot < MatchLimits.SitesPerSector; slot++)
                {
                    if (!InfluenceCommandLayout.SiteHit(slot).Contains(point)) continue;
                    var actor = _state is null || _commandTargetOptions.Count == 0
                        ? null
                        : _state.FindGang(_commandTargetOptions[0].Gang);
                    if (actor is null) return;
                    var targetId = actor.SectorId * MatchLimits.SitesPerSector + slot;
                    var openDetails = _influenceSiteClicks.Register(targetId, _inputTime);
                    for (var candidateIndex = 0; candidateIndex < _commandTargetOptions.Count; candidateIndex++)
                    {
                        if (_commandTargetOptions[candidateIndex].Target.Id != targetId) continue;
                        _commandTargetCursor = candidateIndex;
                        if (openDetails) OpenSiteDetails(actor.SectorId, slot, ClientScreen.Commands);
                        return;
                    }
                    return;
                }
                if (!InfluenceCommandLayout.Panel.Contains(point)) BackFromCommands();
                return;
            }
            if (IsEquipmentCommandPicker())
            {
                if (EquipmentCommandLayout.Cancel.Contains(point))
                {
                    AcceptInput();
                    BackFromCommands();
                    return;
                }
                if (EquipmentCommandLayout.Ok.Contains(point))
                {
                    ActivateCommandSelection();
                    return;
                }
                for (var category = 0; category < EquipmentCommandLayout.CategoryCount; category++)
                {
                    if (!EquipmentCommandLayout.CategoryHit(category).Contains(point)) continue;
                    SelectEquipmentCategory(category);
                    return;
                }
                if (_state is null) return;
                var indices = EquipmentCommandIndices(_state);
                var position = indices.IndexOf(_commandTargetCursor);
                var itemFirst = EquipmentCommandLayout.FirstVisibleItem(indices.Count, position);
                var itemVisible = Math.Min(
                    EquipmentCommandLayout.VisibleItemCount, indices.Count - itemFirst);
                var itemRow = _commandTargetOptions[0].Action == GangAction.Research
                    ? EquipmentCommandLayout.ResearchItemRowAt(point)
                    : EquipmentCommandLayout.ItemRowAt(point);
                if (itemRow >= itemVisible) itemRow = -1;
                if (itemRow >= 0)
                {
                    _commandTargetCursor = indices[itemFirst + itemRow];
                    var itemId = _commandTargetOptions[_commandTargetCursor].Target.Id;
                    if (_equipmentItemClicks.Register(itemId, _inputTime)) OpenItemDetails((short)itemId);
                }
                else if (!EquipmentCommandLayout.Panel.Contains(point)) BackFromCommands();
                return;
            }
            var first = Math.Max(0, _commandTargetCursor - 6);
            var visible = Math.Min(13, _commandTargetOptions.Count - first);
            var index = HitTest.IndexAt(Math.Max(0, visible), CommandOverlayLayout.TargetRow, point);
            if (index >= 0)
            {
                _commandTargetCursor = first + index;
                ActivateCommandSelection();
            }
            else if (!CommandOverlayLayout.TargetPanel.Contains(point)) BackFromCommands();
            return;
        }
        var actions = CommandOverlayActions;
        var actionIndex = HitTest.IndexAt(actions.Count, CommandOverlayLayout.ActionRow, point);
        if (actionIndex >= 0)
        {
            _commandCursor = actionIndex;
            ActivateCommandSelection();
        }
        else if (!CommandOverlayLayout.Panel.Contains(point)) BackFromCommands();
    }

    private void ActivateCommandSelection()
    {
        if (_actions is null) return;
        if (_choosingCommandTarget)
        {
            if (IsEquipmentCommandPicker() && (_state is null
                || !EquipmentCommandLayout.CanConfirm(
                    _commandTargetCursor, EquipmentCommandIndices(_state))))
            {
                RejectInput("NO ITEMS IN THIS CATEGORY");
            }
            else if (_commandTargetOptions.Count > 0)
                SubmitCommand(_commandTargetOptions[_commandTargetCursor]);
            return;
        }

        var action = CommandOverlayActions[_commandCursor];
        if (action == GangAction.None)
        {
            CancelSelectedCommand();
            return;
        }
        var options = _commandOptions.Where(command => command.Action == action).ToArray();
        if (options.Length == 0)
        {
            RejectInput($"{action.ToString().ToUpperInvariant()} IS NOT AVAILABLE");
            return;
        }
        if (CommandOverlayLayout.OpensTargetPicker(action))
        {
            if (action == GangAction.Give)
            {
                OpenGiveEquipment(_commandReturnScreen, _commandRepeats);
                return;
            }
            if (action == GangAction.Sell)
            {
                OpenSellEquipment(_commandReturnScreen, _commandRepeats);
                return;
            }
            _commandTargetOptions = action == GangAction.Attack && _state is not null
                ? AttackTargetRoster.Order(_state, options)
                : options;
            _commandTargetCursor = 0;
            _equipmentCategory = 0;
            _choosingCommandTarget = true;
            if (action is GangAction.Equip or GangAction.Research)
                SelectEquipmentCategory(0);
            return;
        }
        SubmitCommand(options[0]);
    }

    private void SubmitCommand(GameCommand selection)
    {
        if (_actions is null) return;
        if (_bulkCommand)
        {
            SubmitBulkCommand(selection.Action, selection.Target);
            return;
        }
        var command = selection with { Repeat = _commandRepeats };
        var result = _actions.Submit(command);
        ReportInputResult(result.Accepted, result.Validation.Message);
        if (result.Accepted) _screens.Show(_commandReturnScreen);
    }

    private void SubmitBulkCommand(GangAction action, CommandTarget target)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var intent = new BulkCommandIntent(action, target, _commandRepeats);
        if (ApplyBulkCommand(playerId, intent, BulkGangCommands.Rejection(action)))
            _screens.Show(_commandReturnScreen);
    }

    private void CancelSelectedCommand()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _actions is null) return;
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null) return;
        var result = _actions.Cancel(playerId, gang.Id);
        ReportInputResult(result.Accepted, result.Validation.Message);
        if (result.Accepted) _screens.Show(_commandReturnScreen);
    }

    private void BackFromCommands()
    {
        if (_choosingCommandTarget)
        {
            _choosingCommandTarget = false;
            _commandTargetOptions = [];
            return;
        }
        _screens.Show(_commandReturnScreen);
    }

    private void DrawCommands(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawMapBackdrop(batch, pixel, font, state, _commandReturnScreen);
        var playerId = ViewingPlayer(state);
        var gang = SelectedGang(state.FindPlayer(playerId)!);
        if (_choosingCommandTarget)
        {
            DrawCommandTargets(batch, pixel, font, state);
            return;
        }

        var panel = CommandOverlayLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 18, 18, 246));
        DrawBorder(batch, pixel, panel, new Color(0, 190, 65), 2);
        var heading = _commandRepeats ? "RECURRING ACTION" : "ONE-OFF ACTION";
        if (_bulkCommand) heading += $" X{_gangSelection.Count}";
        font.Draw(batch, heading, new Vector2(panel.X + 8, panel.Y + 5), Color.Gold, 1);
        var actions = CommandOverlayActions;
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index];
            var row = CommandOverlayLayout.ActionRow(index);
            var available = action == GangAction.None
                ? gang?.QueuedCommand is not null
                : _commandOptions.Any(command => command.Action == action);
            if (index == _commandCursor)
                batch.Draw(pixel, row, new Color(65, 35, 25));
            if (action is GangAction.None or GangAction.Terminate)
                batch.Draw(pixel, new Rectangle(row.X, row.Y - 2, row.Width, 1), new Color(65, 95, 80));
            var label = action.ToString().ToUpperInvariant()
                + (CommandOverlayLayout.OpensTargetPicker(action) ? "..." : "");
            font.Draw(batch, label, new Vector2(row.X + 5, row.Y + 6),
                available ? Color.White : new Color(90, 105, 100), 1);
        }

        if (_commandTooltipDwell.SettledRegion is { } settled && settled < actions.Count
            && _hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, CommandActionTooltips.Lines(actions[settled]));
    }

    private void DrawCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (IsEquipmentCommandPicker())
        {
            DrawEquipmentCommandTargets(batch, pixel, font, state);
            return;
        }
        if (IsMovementCommandPicker())
        {
            DrawMovementCommandTargets(batch, pixel, state);
            return;
        }
        if (IsInfluenceCommandPicker())
        {
            DrawInfluenceCommandTargets(batch, pixel, state);
            return;
        }
        if (IsAttackCommandPicker())
        {
            DrawAttackCommandTargets(batch, pixel, font, state);
            return;
        }
        var panel = CommandOverlayLayout.TargetPanel;
        batch.Draw(pixel, panel, new Color(12, 18, 18, 248));
        DrawBorder(batch, pixel, panel, new Color(0, 190, 65), 2);
        var action = _commandTargetOptions.Count == 0
            ? GangAction.None
            : _commandTargetOptions[_commandTargetCursor].Action;
        font.Draw(batch, action.ToString().ToUpperInvariant(),
            new Vector2(panel.X + 8, panel.Y + 8), Color.Gold, 1);
        var first = Math.Max(0, _commandTargetCursor - 6);
        foreach (var entry in _commandTargetOptions.Skip(first).Take(13)
                     .Select((command, row) => (command, row)))
        {
            var index = first + entry.row;
            var rectangle = CommandOverlayLayout.TargetRow(entry.row);
            if (index == _commandTargetCursor)
                batch.Draw(pixel, rectangle, new Color(65, 35, 25));
            var label = FormatCommandTargets(state, entry.command);
            if (label.Length > 30) label = label[..30];
            font.Draw(batch, label, new Vector2(rectangle.X + 4, rectangle.Y + 5), Color.White, 1);
        }
    }

    private bool IsEquipmentCommandPicker() => _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action is GangAction.Equip or GangAction.Research;

    private bool IsInfluenceCommandPicker() => _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action == GangAction.Influence;

    private bool IsAttackCommandPicker() => _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action == GangAction.Attack;

    private bool IsMovementCommandPicker() => _choosingCommandTarget
        && _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action == GangAction.Move;

    private void DrawInfluenceCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _influenceBackground, InfluenceCommandLayout.Panel, 248);

        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        var actorDefinition = state.Definitions.Gangs.Single(gang => gang.Id == actor.DefinitionId);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, InfluenceCommandLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(actorDefinition.Id), Color.White);

        var sector = state.Sectors[actor.SectorId];
        for (var slot = 0; slot < MatchLimits.SitesPerSector; slot++)
        {
            var site = sector.Sites.Single(value => value.Slot == slot);
            var destination = InfluenceCommandLayout.Site(slot);
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, destination,
                    OriginalSpriteLayout.SitePortrait(site.DefinitionId), Color.White);
            var targetId = actor.SectorId * MatchLimits.SitesPerSector + slot;
            if (_commandTargetOptions[_commandTargetCursor].Target.Id == targetId)
                DrawBorder(batch, pixel, destination, Color.White, 2);
        }
    }

    private void DrawEquipmentCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        var action = _commandTargetOptions[0].Action;
        var background = action == GangAction.Equip
            ? _equipmentPurchaseBackground
            : _equipmentResearchBackground;
        if (background is not null)
            batch.Draw(background, EquipmentCommandLayout.Panel, Color.White);
        else
            batch.Draw(pixel, EquipmentCommandLayout.Panel, new Color(0, 0, 0, 248));
        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentCommandLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(actor.DefinitionId), Color.White);

        var indices = EquipmentCommandIndices(state);
        var position = indices.IndexOf(_commandTargetCursor);
        var first = EquipmentCommandLayout.FirstVisibleItem(indices.Count, position);
        foreach (var entry in indices.Skip(first).Take(EquipmentCommandLayout.VisibleItemCount)
                     .Select((index, row) => (index, row)))
        {
            var command = _commandTargetOptions[entry.index];
            var item = state.Definitions.Items[command.Target.Id];
            var rectangle = EquipmentCommandLayout.ItemRow(entry.row);
            if (entry.index == _commandTargetCursor)
                batch.Draw(pixel, rectangle, new Color(55, 65, 25));
            font.Draw(batch, item.Name, new Vector2(rectangle.X + 2, rectangle.Y + 1), Color.Lime, 1);
            var value = action == GangAction.Equip
                ? SpecialSiteRules.EquipmentCost(state, actor, item).ToString()
                : EquipmentCommandLayout.ResearchProgress(item.ResearchDifficulty,
                    state.FindPlayer(actor.Owner)!.RemainingResearch(state.Definitions, item.Id));
            font.Draw(batch, value, new Vector2(rectangle.Right - value.Length * 6 - 2, rectangle.Y + 1),
                Color.Lime, 1);
        }
        DrawBorder(batch, pixel, EquipmentCommandLayout.Category(_equipmentCategory), Color.White, 2);
        DrawButton(batch, pixel, font, EquipmentCommandLayout.Ok, "OK",
            EquipmentCommandLayout.CanConfirm(_commandTargetCursor, indices));
    }

    private List<int> EquipmentCommandIndices(MatchState state) => Enumerable.Range(0, _commandTargetOptions.Count)
        .Where(index => EquipmentCommandLayout.CategoryForItemType(
            state.Definitions.Items[_commandTargetOptions[index].Target.Id].Type) == _equipmentCategory)
        .ToList();

    private void SelectEquipmentCategory(int category)
    {
        if (category is < 0 or >= EquipmentCommandLayout.CategoryCount)
            throw new ArgumentOutOfRangeException(nameof(category));
        _equipmentCategory = category;
        if (_state is null) return;
        var indices = EquipmentCommandIndices(_state);
        if (indices.Count > 0) _commandTargetCursor = indices[0];
    }

    private static string FormatCommandTargets(MatchState state, GameCommand command)
    {
        var text = command.Target.Kind == CommandTargetKind.None
            ? command.Action.ToString().ToUpperInvariant()
            : FormatTarget(state, command.Target);
        if (command.SecondaryTarget is { } secondary)
            text += " / " + FormatTarget(state, secondary);
        return text;
    }

    private static string FormatTarget(MatchState state, CommandTarget target) => target.Kind switch
    {
        CommandTargetKind.Gang => "GANG " + target.Id,
        CommandTargetKind.Sector => "SECTOR " + (target.Id + 1),
        CommandTargetKind.Site => state.Definitions.Sites.Single(definition => definition.Id ==
            state.FindSite(target.Id)!.DefinitionId).Name,
        CommandTargetKind.Item => state.Definitions.Items[target.Id].Name,
        _ => ""
    };
}
