using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private int? _pressedHireRejectSlot;
    private ClientScreen _pressedHireRejectScreen;

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
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, HireDockLayout.Portrait(slot),
                    OriginalSpriteLayout.GangPortrait(entry.GangDefinitionId), Color.White);
            DrawHireDockMark(batch, entry.Mark, HireDockLayout.Portrait(slot));
            if (!entry.Hired)
            {
                var definition = state.Definitions.Gang(entry.GangDefinitionId);
                var price = HireDockLayout.Price(slot);
                font.Draw(batch, HireDockLayout.PriceText(HireRules.InitialCost(definition)),
                    price.ToVector2(), Color.Lime, 1);
            }
        }
    }

    private void DrawHireDockMark(SpriteBatch batch, HireDockMark mark, Rectangle stamp)
    {
        switch (mark)
        {
            case HireDockMark.Hired when _uiKeyedSprites is not null:
                batch.Draw(_uiKeyedSprites, stamp, OriginalSpriteLayout.HiredStamp, Color.White);
                break;
            case HireDockMark.Snubbed when _uiKeyedSprites is not null:
                batch.Draw(_uiKeyedSprites, stamp, OriginalSpriteLayout.SnubbedStamp, Color.White);
                break;
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
            batch.Draw(_hireComparisonBackground, HireComparisonLayout.Panel,
                HireComparisonLayout.BackgroundSource, Color.White);
        else
            batch.Draw(pixel, HireComparisonLayout.Panel, new Color(0, 0, 0, 245));
        var playerId = ViewingPlayer(state);
        var player = state.FindPlayer(playerId)!;
        var entries = CurrentHireDock(player);
        for (var slot = 0; slot < HireDockLayout.SlotCount; slot++)
            for (var row = 0; row < 16; row++)
                batch.Draw(pixel, HireComparisonLayout.ValueCell(slot, row), Color.Black);
        for (var slot = 0; slot < entries.Count; slot++)
        {
            if (entries[slot] is not { } entry) continue;
            var definition = state.Definitions.Gang(entry.GangDefinitionId);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, HireComparisonLayout.Portrait(slot),
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            var values = HireComparisonValues(definition);
            for (var row = 0; row < values.Length; row++)
                DrawNativeTwoCellValue(font, batch, values[row],
                    HireComparisonLayout.ValueCell(slot, row).X, HireComparisonLayout.StatY(row),
                    row < 6 ? NativeTwoCellNumberPresentation.Kind.Baseline
                        : NativeTwoCellNumberPresentation.Kind.Modifier);
        }
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.HireAt(hover));
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

    /// <summary>
    /// What the dock in front of the player allows, which a submitted online turn narrows.
    /// </summary>
    /// <remarks>
    /// <see cref="MatchActions"/> is the lock on changing a match, and online it is taken away the
    /// moment the turn goes to the server. That leaves the offers themselves readable, which is what
    /// this distinguishes: the browsing paths ask for anything but
    /// <see cref="HireDockAccess.Closed"/>, and the hiring and snubbing paths ask for the handle.
    /// </remarks>
    private HireDockAccess HireAccess => HireDockPolicy.Access(
        _state is not null,
        _actions is not null,
        _session is not null && _online.PlanningIsSubmitted);

    private void OpenHire(ClientScreen returnScreen = ClientScreen.City)
    {
        if (_state is null || HireAccess == HireDockAccess.Closed
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
        HireDockLayout.Project(
            player.HireOfferSlots, player.PendingHires.FirstOrDefault(), player.SnubbedHireOfferSlot);

    private void BeginHireDrag(int slot, Point point)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || HireAccess == HireDockAccess.Closed
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
        _hireDragStarted = false;
        if (definitionId is null || slot is null
            || _state?.Coordinator.ActivePlayer is not { } playerId)
            return;
        if (_actions is null)
        {
            RejectInput(OnlinePlanningClosed);
            return;
        }
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
        if (HireDropPlacement.Rejection(_state, playerId, sectorId) is { } rejection)
        {
            ReportInputResult(false, rejection.Message);
            return;
        }
        var result = _actions.QueueHire(playerId, definitionId.Value, sectorId);
        ReportHireSubmission(result, _state.FindPlayer(playerId)!);
        if (!result.Accepted) return;
        // The Hire handler flashes the cell the portrait was dropped on: the city cell with
        // fn_0041ACE6 (FND-UI-017), or the cell of the nine-sector display with fn_0041A0D4 on the
        // sector view (FND-UI-018), pausing on the presentation clock (RULE-TIMER-004, FND-UI-037).
        if (_screens.Current == ClientScreen.Sector)
        {
            if (SectorDetailLayout.CellOf(_cursor, sectorId) is { } cell)
                StartFlash(TickedPresentationKind.SectorDisplayCellFlash, cell);
        }
        else
            StartFlash(TickedPresentationKind.CityCellFlash, CityMapLayout.Destination(sectorId));
    }

    private void CancelHireDrag()
    {
        _draggedHireDefinitionId = null;
        _draggedHireSlot = null;
        _hireDragStarted = false;
        _message = string.Empty;
    }

    private void BeginHireReject(int slot, ClientScreen screen)
    {
        _pressedHireRejectSlot = slot;
        _pressedHireRejectScreen = screen;
        PlayGeneralSound(AudioRouting.PointerPushSound());
    }

    private void CompleteHireReject(Point point)
    {
        var slot = _pressedHireRejectSlot;
        var screen = _pressedHireRejectScreen;
        CancelHireReject();
        if (slot is null || _screens.Current != screen || !HireDockLayout.Reject(slot.Value).Contains(point))
            return;
        SnubHireDockOffer(slot.Value, pointerButton: true);
    }

    private void CancelHireReject() => _pressedHireRejectSlot = null;

    private void MoveHireCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var offers = _state.FindPlayer(playerId)!.HireOfferSlots;
        _hireCursor = HireDockLayout.MoveCursor(offers, _hireCursor, delta);
    }

    private void HandleHireClick(Point point)
    {
        if (HireComparisonLayout.Ok.Contains(point))
            AcceptAndShow(_managementReturnScreen);
    }

    private void ReportHireSubmission(HireSubmissionResult result, MatchPlayerState player)
    {
        if (!result.Accepted || result.PendingHire is not { } pending)
        {
            ReportInputResult(false, result.Validation.Message);
            return;
        }

        var projection = FinanceProjection.Project(_state!, player, null);
        var projectedHireBalance = HireReservationWarning.ProjectedBalanceAtHire(
            player.Cash, projection);
        _message = HireReservationWarning.For(projectedHireBalance);
        PlayGeneralSound(AudioRouting.InputResultSound(true));
    }

    private void SnubSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        if (_actions is null)
        {
            RejectInput(OnlinePlanningClosed);
            return;
        }
        var player = _state.FindPlayer(playerId)!;
        _hireCursor = HireDockLayout.MoveCursor(player.HireOfferSlots, _hireCursor, 0);
        if (_hireCursor < 0) return;
        var offer = player.HireOfferSlots[_hireCursor].GangDefinitionId!.Value;
        var result = _actions.SnubHireOffer(playerId, offer);
        ReportInputResult(result.Accepted, result.Validation.Message);
        _hireCursor = HireDockLayout.MoveCursor(player.HireOfferSlots, _hireCursor, 0);
    }

    private void SnubHireDockOffer(int slot, bool pointerButton = false)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        if (_actions is null)
        {
            ReportButtonResult(false, OnlinePlanningClosed, pointerButton);
            return;
        }
        PrepareCurrentHireOffers();
        var entry = CurrentHireDock(_state.FindPlayer(playerId)!)[slot];
        if (entry is null)
        {
            RejectInput("NO HIRE OFFER IN THIS SLOT");
            return;
        }
        var result = _actions.SnubHireOffer(playerId, entry.GangDefinitionId);
        ReportButtonResult(result.Accepted, result.Validation.Message, pointerButton);
    }
}
