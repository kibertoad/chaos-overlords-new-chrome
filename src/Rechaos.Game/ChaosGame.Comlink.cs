using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private enum ComlinkSendButton
    {
        Send,
        Cancel
    }

    private void OpenComlinkView(ClientScreen returnScreen)
    {
        if (_state is null || PlanningViewer is not { } playerId) return;
        // Online, once the turn is ended no read mark can go into it: a message viewed now would
        // stay unread and come back as the first unread one next turn. Hot-seat play never shows
        // the panel to a player whose turn is over either. The final view stays readable.
        if (_session is not null && _actions is null && _finalViewPlayer is null)
        {
            RejectInput(OnlinePlanningClosed);
            return;
        }
        _managementReturnScreen = returnScreen;
        var inbox = _state.ComlinkFor(playerId);
        if (inbox.Count == 0)
        {
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
            return;
        }

        _comlinkCursor = InitialComlinkViewCursor(inbox, _comlinkCursor);
        MarkDisplayedComlinkRead(playerId, inbox);
        _screens.Show(ClientScreen.ComlinkView);
    }

    /// <summary>
    /// FND-COMLINK-002: selects the opening page using original View handler <c>0x0045D61A</c>'s
    /// ascending first-unread scan, retaining the existing page after all
    /// retained records have been acknowledged.
    /// </summary>
    internal static int InitialComlinkViewCursor(ComlinkInbox inbox, int currentCursor)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        if (inbox.Count == 0) return 0;
        for (var index = 0; index < inbox.Count; index++)
        {
            if (!inbox.IsRead(inbox.Messages[index].Sequence)) return index;
        }
        return Math.Clamp(currentCursor, 0, inbox.Count - 1);
    }

    // FND-COMLINK-007, FND-UI-019: once an eligible card is pressed the Send face is drawn
    // enabled while a recipient is selected and unavailable while none is; until then the panel's
    // own face shows.
    private CommandPanelFaceState _comlinkSendFace;

    private void OpenComlinkSend(ClientScreen returnScreen)
    {
        if (_state is null || PlanningViewer is not { } sender) return;
        // Online, a turn the player has ended takes the planning handle away, so a message typed
        // now would have no order document to go into and Send would do nothing.
        if (_session is not null && _actions is null)
        {
            RejectInput(OnlinePlanningClosed);
            return;
        }
        // RULE-COMLINK-002: with no other human still in the match the panel does not open.
        if (!_state.HasComlinkRecipient(sender))
        {
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
            return;
        }
        _managementReturnScreen = returnScreen;
        Array.Fill(_comlinkRecipients, false);
        _comlinkSendFace = CommandPanelFaceState.NotDrawn;
        _comlinkEditor.Clear();
        _comlinkCaretCadence.Reset(_eventPump.Time);
        _comlinkStatus = string.Empty;
        _screens.Show(ClientScreen.ComlinkSend);
    }

    private void UpdateComlinkCaret(TimeSpan now)
    {
        if (_screens.Current == ClientScreen.ComlinkSend)
            _comlinkCaretCadence.Advance(now);
    }

    private void UpdateComlinkView(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left)) MoveComlinkCursor(-1);
        if (Pressed(keyboard, Keys.Right)) MoveComlinkCursor(1);
        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Execute))
            AcceptAndInvoke(CloseComlink);
    }

    private void UpdateComlinkSend(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Escape))
        {
            AcceptInput();
            CloseComlink();
            return;
        }
        // Original Send handler 0x0045EAB1 (FND-COMLINK-010) submits on Execute. Enter moves its
        // four-row editor cursor, so it must never dispatch a message here.
        if (Pressed(keyboard, Keys.Execute))
        {
            // FND-COMLINK-007: with a recipient chosen, Execute presses the Send face for one tick
            // of the presentation clock before sending (fn_00418CCC, RULE-TIMER-004).
            if (_comlinkRecipients.Any(selected => selected))
                PressKeyFace(PressedKeyFace.Confirm, ComlinkSendLayout.Ok.Location,
                    () => SendComlink(pointerButton: true));
            else
                SendComlink();
            return;
        }
        if (Pressed(keyboard, Keys.Back))
        {
            _comlinkEditor.Backspace();
            _comlinkStatus = string.Empty;
            return;
        }
        if (Pressed(keyboard, Keys.Enter))
        {
            _comlinkEditor.MoveNextRow();
            _comlinkStatus = string.Empty;
            return;
        }
        if (Pressed(keyboard, Keys.Left))
        {
            _comlinkEditor.MoveLeft();
            _comlinkStatus = string.Empty;
            return;
        }
        if (Pressed(keyboard, Keys.Up))
        {
            _comlinkEditor.MoveUp();
            _comlinkStatus = string.Empty;
            return;
        }
        if (Pressed(keyboard, Keys.Right))
        {
            _comlinkEditor.MoveRight();
            _comlinkStatus = string.Empty;
            return;
        }
        if (Pressed(keyboard, Keys.Down))
        {
            _comlinkEditor.MoveDown();
            _comlinkStatus = string.Empty;
            return;
        }

        var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        foreach (var key in keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key)) continue;
            if (OriginalTextInput.TryCharacter(key, shift, out var character))
                TypeComlinkCharacter(character);
        }
    }

    // RULE-COMLINK-006: a character typed in the Send panel, from the keyboard or a reference
    // frame's typed text.
    private void TypeComlinkCharacter(char character)
    {
        _comlinkEditor.TryAppend(character);
        _comlinkStatus = string.Empty;
    }

    private void HandleComlinkViewClick(Point point)
    {
        if (ComlinkViewLayout.Previous.Contains(point)) MoveComlinkCursor(-1);
        else if (ComlinkViewLayout.Next.Contains(point)) MoveComlinkCursor(1);
        else if (ComlinkViewLayout.Ok.Contains(point))
            AcceptAndInvoke(CloseComlink);
    }

    private void HandleComlinkSendClick(Point point)
    {
        var recipient = HitTest.IndexAt(MatchLimits.PlayerCount, ComlinkSendLayout.RecipientHit, point);
        if (recipient >= 0)
        {
            if (EligibleComlinkRecipient(recipient))
            {
                _comlinkRecipients[recipient] = !_comlinkRecipients[recipient];
                _comlinkSendFace = _comlinkRecipients.Contains(true)
                    ? CommandPanelFaceState.Enabled
                    : CommandPanelFaceState.Disabled;
                _comlinkStatus = string.Empty;
            }
            // SCR-COMLINK-002, EXP-COMLINK-001: the card of a player who cannot take a message,
            // the sender's own included, is refused with the rejected-input sound.
            else PlayGeneralSound(GeneralSoundSlot.RejectedInput);
        }
        else if (ComlinkSendLayout.Cancel.Contains(point))
            BeginComlinkSendButton(ComlinkSendButton.Cancel);
        else if (ComlinkSendLayout.Ok.Contains(point))
            BeginComlinkSendButton(ComlinkSendButton.Send);
    }

    private void BeginComlinkSendButton(ComlinkSendButton button)
    {
        // Native Send handler 0x0045EAB1 (FND-COMLINK-003) rejects the face immediately when no
        // recipient is selected; it only enters shared held-button helper
        // 0x00418821 after that predicate passes.
        if (button == ComlinkSendButton.Send && !_comlinkRecipients.Any(selected => selected))
        {
            SendComlink();
            return;
        }

        _pressedComlinkSendButton = button;
        AcceptInput();
    }

    private void CompleteComlinkSendButton(Point point)
    {
        var button = _pressedComlinkSendButton;
        CancelComlinkSendButton();
        if (button is null || _screens.Current != ClientScreen.ComlinkSend) return;

        if (button == ComlinkSendButton.Cancel && ComlinkSendLayout.Cancel.Contains(point))
        {
            CloseComlink();
            return;
        }

        if (button == ComlinkSendButton.Send && ComlinkSendLayout.Ok.Contains(point))
            SendComlink(pointerButton: true);
    }

    private void CancelComlinkSendButton() => _pressedComlinkSendButton = null;

    private bool EligibleComlinkRecipient(int playerIndex)
    {
        if (_state is null || PlanningViewer is not { } sender) return false;
        return _state.IsComlinkRecipient(sender, new PlayerId(playerIndex));
    }

    private void MoveComlinkCursor(int delta)
    {
        if (_state is null || PlanningViewer is not { } playerId) return;
        var count = _state.ComlinkFor(playerId).Count;
        if (count > 0)
        {
            var next = BoundedPageNavigation.Move(_comlinkCursor, count, delta);
            PlayGeneralSound(AudioRouting.PageNavigationSound(next != _comlinkCursor));
            _comlinkCursor = next;
            MarkDisplayedComlinkRead(playerId, _state.ComlinkFor(playerId));
        }
    }

    private void MarkDisplayedComlinkRead(PlayerId playerId, ComlinkInbox inbox)
    {
        if (_comlinkCursor >= inbox.Count) return;
        var sequence = inbox.Messages[_comlinkCursor].Sequence;
        if (!inbox.IsRead(sequence)) _actions?.MarkComlinkRead(playerId, sequence);
    }

    private void SendComlink(bool pointerButton = false)
    {
        if (_state is null || PlanningViewer is not { } sender || _actions is null) return;
        var recipients = Enumerable.Range(0, MatchLimits.PlayerCount)
            .Where(index => _comlinkRecipients[index])
            .Select(index => new PlayerId(index))
            .ToArray();
        var result = _actions.SendComlinkMessage(sender, recipients, _comlinkEditor.Text);
        ReportButtonResult(result.Accepted, result.Message, pointerButton);
        if (!result.Accepted)
        {
            _comlinkStatus = _message;
            return;
        }
        CloseComlink();
    }

    private void CloseComlink() => _screens.Show(_managementReturnScreen);

    private void DrawComlinkView(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        DrawPanelArtwork(batch, pixel, _comlinkViewBackground, ComlinkViewLayout.Panel);
        var playerId = ViewingPlayer(state);
        var messages = state.ComlinkFor(playerId).Messages;
        if (messages.Count == 0)
        {
            font.Draw(batch, "NO INCOMING MESSAGES", ComlinkViewLayout.MessageOrigin.ToVector2(), Color.Lime, 1);
            return;
        }

        _comlinkCursor = Math.Clamp(_comlinkCursor, 0, messages.Count - 1);
        var message = messages[_comlinkCursor];
        var sender = state.FindPlayer(message.Sender);
        // SCR-COMLINK-001, FND-COMLINK-007: the number and count over the panel art's frame and
        // OF, the date over its point, the name's black backing, the sender's colour strip and
        // portrait stretched to 64 by 64, and the four rows of the message.
        DrawDigitCells(batch, pixel, font, $"{Math.Min(_comlinkCursor + 1, 99):00}", ComlinkViewLayout.PageNumber);
        DrawDigitCells(batch, pixel, font, $"{Math.Min(messages.Count, 99):00}", ComlinkViewLayout.PageCount);
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, ComlinkViewLayout.Previous,
                LastTurnEventsLayout.PreviousSource(firstPage: _comlinkCursor == 0), Color.White);
            batch.Draw(_uiSprites, ComlinkViewLayout.Next,
                LastTurnEventsLayout.NextSource(lastPage: _comlinkCursor == messages.Count - 1), Color.White);
        }
        var (year, week) = MatchCalendar.Of(Math.Max(0, message.Turn - 1));
        DrawDigitCells(batch, pixel, font, $"{year:0000}", ComlinkViewLayout.Year);
        DrawDigitCells(batch, pixel, font, $"{week:00}", ComlinkViewLayout.Week);
        batch.Draw(pixel, ComlinkViewLayout.SenderName, Color.Black);
        var name = sender?.Setup.Name ?? $"PLAYER {message.Sender.Value + 1}";
        font.Copy(batch, name.Length <= ComlinkViewLayout.SenderNameColumns ? name : name[..ComlinkViewLayout.SenderNameColumns],
            ComlinkViewLayout.SenderName.Location, OriginalFontLayout.PlainStrip);
        if (message.Sender.Value is >= 0 and < MatchLimits.PlayerCount)
            batch.Draw(pixel, ComlinkViewLayout.SenderColour, SetupPlayerCardArtLayout.Colours[message.Sender.Value]);
        if (sender is not null && _uiSprites is not null)
            batch.Draw(_uiSprites, ComlinkViewLayout.SenderPortrait,
                OriginalSpriteLayout.OverlordPortrait(sender.Setup.PortraitId), Color.White);
        DrawComlinkLines(batch, font, ComlinkTextEditor.DisplayLines(ComlinkText(message)),
            ComlinkViewLayout.MessageOrigin, Color.Lime, ComlinkSendLayout.TextRowStride);
        if (_hoverPoint is { } hover && ComlinkViewLayout.Page.Contains(hover))
            DrawHoverTooltip(batch, pixel, font, hover, ComlinkInboxTooltip);
    }

    /// <summary>
    /// What a message says, as this client can read it.
    /// </summary>
    /// <remarks>
    /// A hot-seat message carries its text. An online one is sealed for its recipient, and the
    /// session's keyring opens it with this seat's key. The keyring is the session's rather than
    /// the planning copy's, because the planning copy is taken away once the turn is ended and the
    /// messages stay readable while the turn seals. One that will not open (sealed to a key this
    /// seat no longer holds, or by a modified client) still takes its place in the inbox every
    /// client hashes, and says why it shows nothing.
    /// </remarks>
    private string ComlinkText(ComlinkMessage message) =>
        !message.IsSealed
            ? message.Text
            : _session?.Comlink.Read(message) ?? UnreadableComlinkMessage;

    internal const string UnreadableComlinkMessage = "THIS MESSAGE CANNOT BE READ ON THIS COMPUTER.";

    // RULE-COMLINK-007
    internal static readonly IReadOnlyList<string> ComlinkInboxTooltip =
    [
        "INBOX",
        "WHEN YOU END PLANNING, THE READ MESSAGES",
        "AT THE FRONT OF THE INBOX ARE REMOVED.",
        "THE FIRST UNREAD MESSAGE AND ALL AFTER IT STAY."
    ];

    private void DrawComlinkSend(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        DrawPanelArtwork(batch, pixel, _comlinkSendBackground, ComlinkSendLayout.Panel);
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            // FND-COMLINK-007: a one-pixel frame, green while the slot is selected and black
            // otherwise, around the slot's colour bar, portrait and name; a slot that cannot be
            // sent to has each colour component divided by 4, the portrait of the row at y 594
            // and the name from the dim strip.
            var cell = ComlinkSendLayout.Recipient(slot);
            var player = state.Players.FirstOrDefault(value => value.Id.Value == slot);
            var eligible = EligibleComlinkRecipient(slot);
            batch.Draw(pixel, cell, Color.Black);
            DrawBorder(batch, pixel, cell, _comlinkRecipients[slot] ? Color.Lime : Color.Black, 1);
            if (player is null) continue;
            var colour = SetupPlayerCardArtLayout.Colours[slot];
            batch.Draw(pixel, ComlinkSendLayout.RecipientAccent(slot),
                eligible ? colour : new Color(colour.R / 4, colour.G / 4, colour.B / 4));
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, ComlinkSendLayout.RecipientPortrait(slot),
                    ComlinkSendLayout.RecipientPortraitSource(player.Setup.PortraitId, eligible), Color.White);
            var name = player.Setup.Name.Length <= 10 ? player.Setup.Name : player.Setup.Name[..10];
            font.Copy(batch, name, ComlinkSendLayout.RecipientNameOrigin(slot),
                eligible ? OriginalFontLayout.PlainStrip : OriginalFontLayout.DimStrip);
        }
        batch.Draw(pixel, ComlinkSendLayout.Message, Color.Black);
        DrawComlinkLines(batch, font, _comlinkEditor.DisplayLines(),
            ComlinkSendLayout.TextOrigin, Color.Lime, ComlinkSendLayout.TextRowStride);
        if (_uiSprites is not null && CommandPanelFaces.Source(_comlinkSendFace) is { } face)
            batch.Draw(_uiSprites, ComlinkSendLayout.OkPressed, face, Color.White);
        DrawPressedComlinkSendButton(batch);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites,
                ComlinkSendLayout.CaretDestination(_comlinkEditor.Column, _comlinkEditor.Row),
                ComlinkSendLayout.CaretSource(_comlinkEditor.CharacterAtCursor, ComlinkCaretInverse), Color.White);
        if (_comlinkStatus.Length > 0)
            font.Draw(batch, _comlinkStatus.Length <= 40 ? _comlinkStatus : _comlinkStatus[..40],
                new Vector2(SharedPanelLayout.X(92), SharedPanelLayout.Y(181)), Color.OrangeRed, 1);
    }

    // FND-COMLINK-010: the reference frame draws the caret in the phase the original's capture
    // recorded, 0 to 2 timer events since a flip to plain and 3 to 5 since a flip to inverse.
    private bool ComlinkCaretInverse => _referenceFrame?.CaretPhase is { } phase
        ? phase >= ComlinkCaretCadence.EventsPerGlyphRow
        : _comlinkCaretCadence.UsesInverseGlyph;

    private void DrawPressedComlinkSendButton(SpriteBatch batch)
    {
        if (_uiSprites is null || _hoverPoint is not { } hover) return;
        switch (_pressedComlinkSendButton)
        {
            case ComlinkSendButton.Cancel when ComlinkSendLayout.Cancel.Contains(hover):
                batch.Draw(_uiSprites, ComlinkSendLayout.CancelPressed,
                    ComlinkSendLayout.CancelPressedSource, Color.White);
                break;
            case ComlinkSendButton.Send when ComlinkSendLayout.Ok.Contains(hover):
                batch.Draw(_uiSprites, ComlinkSendLayout.OkPressed,
                    ComlinkSendLayout.OkPressedSource, Color.White);
                break;
        }
    }

    private static void DrawComlinkLines(
        SpriteBatch batch, PixelFont font, IReadOnlyList<string> lines, Point origin, Color color,
        int rowStride = OriginalFontLayout.LineHeight)
    {
        for (var row = 0; row < lines.Count; row++)
            font.Draw(batch, lines[row], new Vector2(origin.X, origin.Y + row * rowStride), color, 1);
    }

}
