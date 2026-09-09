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
                font.Draw(batch, values[row].ToString(), HireComparisonLayout.StatPosition(slot, row),
                    HireComparisonLayout.IsBestValue(row, values[row],
                        valuesBySlot.Where(candidate => candidate is not null).Select(candidate => candidate![row]))
                        ? Color.Lime : Color.Red, 1);
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
        if (_state is null || _replay is null
            || _state.Coordinator.Phase is not (TurnPhase.Command or TurnPhase.Hire)
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            _message = "HIRING REQUIRES A PLANNING TURN";
            return;
        }
        var player = _state.FindPlayer(playerId)!;
        PrepareCurrentHireOffers();
        if (player.HirePool.Count == 0)
        {
            _message = "NO HIRE OFFER AVAILABLE";
            return;
        }
        _hireCursor = 0;
        _managementReturnScreen = returnScreen;
        _screens.Show(ClientScreen.Hire);
    }

    private void PrepareCurrentHireOffers()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        var player = _state.FindPlayer(playerId)!;
        if (player.HirePool.Count == 0 && player.PendingHires.Count == 0
            && !player.HasSnubbedHireOfferThisTurn)
            _replay.PrepareHireOffers(playerId);
    }

    private IReadOnlyList<HireDockEntry?> CurrentHireDock(MatchPlayerState player) =>
        HireDockLayout.Project(player.HirePool, player.PendingHires.FirstOrDefault(), _pendingHireSlot);

    private void BeginHireDrag(int slot, Point point)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null
            || _state.Coordinator.Phase != TurnPhase.Command)
        {
            _message = "HIRING REQUIRES A PLANNING TURN";
            return;
        }
        PrepareCurrentHireOffers();
        var entry = CurrentHireDock(_state.FindPlayer(playerId)!)[slot];
        if (entry is null)
        {
            _message = "NO HIRE OFFER IN THIS SLOT";
            return;
        }
        if (entry.Hired)
        {
            _message = "GANG ALREADY HIRED THIS TURN";
            return;
        }
        _draggedHireSlot = slot;
        _draggedHireDefinitionId = entry.GangDefinitionId;
        _hirePressPoint = point;
        _hireDragStarted = false;
        _dragPoint = point;
        _message = "DRAG TO HIRE; DOUBLE-CLICK FOR DETAILS";
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
        else
            _message = "DOUBLE-CLICK FOR DETAILS OR DRAG TO HIRE";
    }

    private void CompleteHireDrag(Point point)
    {
        var definitionId = _draggedHireDefinitionId;
        var slot = _draggedHireSlot;
        _draggedHireDefinitionId = null;
        _draggedHireSlot = null;
        if (definitionId is null || slot is null || _state?.Coordinator.ActivePlayer is not { } playerId
            || _replay is null)
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
            _message = "HIRE CANCELLED";
            return;
        }
        var result = _replay.QueueHire(playerId, definitionId.Value, sectorId);
        _message = result.Accepted
            ? $"HIRED FOR SECTOR {sectorId + 1}"
            : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted) _pendingHireSlot = slot;
    }

    private void CancelHireDrag()
    {
        _draggedHireDefinitionId = null;
        _draggedHireSlot = null;
        _hireDragStarted = false;
        _message = "HIRE CANCELLED";
    }

    private void MoveHireCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var count = _state.FindPlayer(playerId)!.HirePool.Count;
        if (count > 0) _hireCursor = Mod(_hireCursor + delta, count);
    }

    private void HandleHireClick(Point point)
    {
        if (HireComparisonLayout.Ok.Contains(point)) _screens.Show(_managementReturnScreen);
    }

    private void QueueSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        var player = _state.FindPlayer(playerId)!;
        if (player.HirePool.Count == 0) return;
        _hireCursor = Math.Clamp(_hireCursor, 0, player.HirePool.Count - 1);
        var offer = player.HirePool[_hireCursor];
        var result = _replay.QueueHire(playerId, offer, _cursor);
        _message = result.Accepted ? "HIRE QUEUED" : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted)
        {
            _pendingHireSlot = _hireCursor;
            _screens.Show(ClientScreen.City);
        }
    }

    private void SnubSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        var player = _state.FindPlayer(playerId)!;
        if (player.HirePool.Count == 0) return;
        _hireCursor = Math.Clamp(_hireCursor, 0, player.HirePool.Count - 1);
        var offer = player.HirePool[_hireCursor];
        var result = _replay.SnubHireOffer(playerId, offer);
        _message = result.Accepted ? "OFFER SNUBBED" : result.Validation.Message.ToUpperInvariant();
        _hireCursor = Math.Clamp(_hireCursor, 0, Math.Max(0, player.HirePool.Count - 1));
    }

    private void SnubHireDockOffer(int slot)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        PrepareCurrentHireOffers();
        var entry = CurrentHireDock(_state.FindPlayer(playerId)!)[slot];
        if (entry is null || entry.Hired)
        {
            _message = entry is null ? "NO HIRE OFFER IN THIS SLOT" : "GANG ALREADY HIRED THIS TURN";
            return;
        }
        var result = _replay.SnubHireOffer(playerId, entry.GangDefinitionId);
        _message = result.Accepted ? "OFFER REJECTED" : result.Validation.Message.ToUpperInvariant();
    }
}
