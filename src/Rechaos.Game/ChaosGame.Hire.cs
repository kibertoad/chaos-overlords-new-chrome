using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawHireDock(
        SpriteBatch batch,
        PixelFont font,
        MatchState state,
        MatchPlayerState player)
    {
        var entries = CurrentHireDock(player);
        for (var slot = 0; slot < entries.Count; slot++)
        {
            if (entries[slot] is not { } entry) continue;
            var portrait = HireDockLayout.Portrait(slot);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, portrait,
                    OriginalSpriteLayout.GangPortrait(entry.GangDefinitionId), Color.White);
            if (entry.Hired && _uiSprites is not null)
                batch.Draw(_uiSprites,
                    new Rectangle(portrait.X + 2, portrait.Y + 2, 60, 60),
                    OriginalSpriteLayout.HiredStamp, Color.White);
            if (!entry.Hired)
            {
                var definition = state.Definitions.Gangs.Single(gang => gang.Id == entry.GangDefinitionId);
                var price = HireDockLayout.Price(slot);
                font.Draw(batch, HireDockLayout.PriceText(HireRules.InitialCost(definition)),
                    price.ToVector2(), Color.Lime, 1);
            }
        }
    }

    private void DrawHire(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        DrawHirePanel(batch, pixel, font, state);
    }

    private void DrawHirePanel(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_hireComparisonBackground is not null)
            batch.Draw(_hireComparisonBackground, HireComparisonLayout.Panel, Color.White);
        else
            batch.Draw(pixel, HireComparisonLayout.Panel, new Color(0, 0, 0, 245));
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        var entries = CurrentHireDock(player);
        var valuesBySlot = entries.Select(entry => entry is null
            ? null
            : HireComparisonValues(state.Definitions.Gangs.Single(gang => gang.Id == entry.GangDefinitionId)))
            .ToArray();
        for (var slot = 0; slot < entries.Count; slot++)
        {
            if (entries[slot] is not { } entry) continue;
            var definition = state.Definitions.Gangs.Single(gang => gang.Id == entry.GangDefinitionId);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, HireComparisonLayout.Portrait(slot),
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            var values = valuesBySlot[slot]!;
            for (var row = 0; row < values.Length; row++)
                DrawPanelValue(font, batch, values[row].ToString(),
                    HireComparisonLayout.StatRight(slot), HireComparisonLayout.StatY(row),
                    HireComparisonLayout.IsBestValue(row, values[row],
                        valuesBySlot.Where(candidate => candidate is not null).Select(candidate => candidate![row]))
                        ? Color.Lime : Color.Red);
        }
    }

    private static short[] HireComparisonValues(GangDefinition definition) =>
    [
        definition.TechLevel, definition.Upkeep,
        definition.Stats.Combat, definition.Stats.Defense,
        definition.Stats.Stealth, definition.Stats.Detect,
        definition.Stats.Chaos, definition.Stats.Control,
        definition.Stats.Heal, definition.Stats.Influence,
        definition.Stats.Research, definition.Stats.Strength,
        definition.Stats.Blade, definition.Stats.Range,
        definition.Stats.Fighting, definition.Stats.MartialArts
    ];

    private void OpenHire(ClientScreen returnScreen = ClientScreen.City)
    {
        if (_state is null || _actions is null
            || _state.Coordinator.Phase is not (TurnPhase.Command or TurnPhase.Hire)
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            RejectInput("HIRING REQUIRES A PLANNING TURN");
            return;
        }
        var player = _state.FindPlayer(playerId)!;
        PrepareCurrentHireOffers();
        _hireCursor = HireDockLayout.MoveCursor(player.HireOfferSlots, -1, 1);
        if (_hireCursor < 0)
        {
            RejectInput("NO HIRE OFFER AVAILABLE");
            return;
        }
        _managementReturnScreen = returnScreen;
        _screens.Show(ClientScreen.Hire);
    }

    private void PrepareCurrentHireOffers()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _actions is null) return;
        var player = _state.FindPlayer(playerId)!;
        if (player.HireOfferSlots.Any(slot => !slot.GangDefinitionId.HasValue)
            && player.PendingHires.Count == 0
            && !player.HasSnubbedHireOfferThisTurn)
            _actions.PrepareHireOffers(playerId);
    }

    private IReadOnlyList<HireDockEntry?> CurrentHireDock(MatchPlayerState player) =>
        HireDockLayout.Project(player.HireOfferSlots, player.PendingHires.FirstOrDefault());

    private void BeginHireDrag(int slot, Point point)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _actions is null
            || _state.Coordinator.Phase != TurnPhase.Command)
        {
            RejectInput("HIRING REQUIRES A PLANNING TURN");
            return;
        }
        PrepareCurrentHireOffers();
        var entry = CurrentHireDock(_state.FindPlayer(playerId)!)[slot];
        if (entry is null)
        {
            RejectInput("NO HIRE OFFER IN THIS SLOT");
            return;
        }
        _draggedHireSlot = slot;
        _draggedHireDefinitionId = entry.GangDefinitionId;
        _hirePressPoint = point;
        _hireDragStarted = false;
        _dragPoint = point;
    }

    private void CompleteHireClick()
    {
        var definitionId = _draggedHireDefinitionId;
        var slot = _draggedHireSlot;
        _draggedHireDefinitionId = null;
        _draggedHireSlot = null;
        _hireDragStarted = false;
        if (definitionId is null || slot is null) return;
        if (_hirePortraitClicks.Register(slot.Value, _inputTime))
            OpenGangDefinitionDetails(definitionId.Value, _screens.Current);
    }

    private void CompleteHireDrag(Point point)
    {
        var definitionId = _draggedHireDefinitionId;
        var slot = _draggedHireSlot;
        _draggedHireDefinitionId = null;
        _draggedHireSlot = null;
        if (definitionId is null || slot is null || _state?.Coordinator.ActivePlayer is not { } playerId
            || _actions is null)
            return;
        _hireDragStarted = false;
        bool hasSector;
        int sectorId;
        if (_screens.Current == ClientScreen.Sector)
        {
            hasSector = SectorDetailLayout.TrySectorAt(point, _cursor, out sectorId);
            if (!hasSector && SectorDetailLayout.Workspace.Contains(point))
            {
                sectorId = _cursor;
                hasSector = true;
            }
        }
        else
        {
            hasSector = CityMapLayout.TrySectorAt(point, out sectorId);
        }
        if (!hasSector)
        {
            _message = string.Empty;
            return;
        }
        var result = _actions.QueueHire(playerId, definitionId.Value, sectorId);
        ReportInputResult(result.Accepted, result.Validation.Message);
    }

    private void CancelHireDrag()
    {
        _draggedHireDefinitionId = null;
        _draggedHireSlot = null;
        _hireDragStarted = false;
        _message = string.Empty;
    }

    private void MoveHireCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var offers = _state.FindPlayer(playerId)!.HireOfferSlots;
        _hireCursor = HireDockLayout.MoveCursor(offers, _hireCursor, delta);
    }

    private void HandleHireClick(Point point)
    {
        if (HireComparisonLayout.Ok.Contains(point))
        {
            AcceptInput();
            _screens.Show(_managementReturnScreen);
        }
    }

    private void QueueSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _actions is null) return;
        var player = _state.FindPlayer(playerId)!;
        _hireCursor = HireDockLayout.MoveCursor(player.HireOfferSlots, _hireCursor, 0);
        if (_hireCursor < 0) return;
        var offer = player.HireOfferSlots[_hireCursor].GangDefinitionId!.Value;
        var result = _actions.QueueHire(playerId, offer, _cursor);
        ReportInputResult(result.Accepted, result.Validation.Message);
        if (result.Accepted)
        {
            _screens.Show(ClientScreen.City);
        }
    }

    private void SnubSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _actions is null) return;
        var player = _state.FindPlayer(playerId)!;
        _hireCursor = HireDockLayout.MoveCursor(player.HireOfferSlots, _hireCursor, 0);
        if (_hireCursor < 0) return;
        var offer = player.HireOfferSlots[_hireCursor].GangDefinitionId!.Value;
        var result = _actions.SnubHireOffer(playerId, offer);
        ReportInputResult(result.Accepted, result.Validation.Message);
        _hireCursor = HireDockLayout.MoveCursor(player.HireOfferSlots, _hireCursor, 0);
    }

    private void SnubHireDockOffer(int slot)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _actions is null) return;
        PrepareCurrentHireOffers();
        var entry = CurrentHireDock(_state.FindPlayer(playerId)!)[slot];
        if (entry is null)
        {
            RejectInput("NO HIRE OFFER IN THIS SLOT");
            return;
        }
        var result = _actions.SnubHireOffer(playerId, entry.GangDefinitionId);
        ReportInputResult(result.Accepted, result.Validation.Message);
    }
}
