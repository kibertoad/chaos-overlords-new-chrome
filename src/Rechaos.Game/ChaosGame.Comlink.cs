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

    /// <summary>
    /// Whether Comlink can be opened at all, which an online match currently cannot.
    /// </summary>
    /// <remarks>
    /// Both halves of Comlink write hashed state — a message lands in an inbox, and opening the
    /// view clears the read mark — so neither can happen on one client alone. Refusing at the door
    /// says so once, where a player can see it, rather than letting them write a message the turn
    /// will not carry.
    /// </remarks>
    private bool ComlinkAvailable()
    {
        if (_actions?.IsOnline != true) return true;
        RejectInput("COMLINK UNAVAILABLE ONLINE");
        return false;
    }

    private void OpenComlinkView(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        if (!ComlinkAvailable()) return;
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
    /// Selects the opening page using original View handler <c>0x0045d61a</c>'s
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

    private void OpenComlinkSend(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is null) return;
        if (!ComlinkAvailable()) return;
        _managementReturnScreen = returnScreen;
        Array.Fill(_comlinkRecipients, false);
        _comlinkEditor.Clear();
        _comlinkCaretCadence.Reset(_inputTime);
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
        // Original Send handler 0x0045eab1 submits on Execute. Enter moves its
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
            {
                _comlinkEditor.TryAppend(character);
                _comlinkStatus = string.Empty;
            }
        }
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
                _comlinkStatus = string.Empty;
            }
        }
        else if (ComlinkSendLayout.Cancel.Contains(point))
            BeginComlinkSendButton(ComlinkSendButton.Cancel);
        else if (ComlinkSendLayout.Ok.Contains(point))
            BeginComlinkSendButton(ComlinkSendButton.Send);
    }

    private void BeginComlinkSendButton(ComlinkSendButton button)
    {
        // Native Send handler 0x0045eab1 rejects the face immediately when no
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
        if (_state?.Coordinator.ActivePlayer is not { } sender) return false;
        var player = _state.Players.FirstOrDefault(value => value.Id.Value == playerIndex);
        return player is not null && player.Id != sender
            && player.Setup.Controller == PlayerController.Human;
    }

    private void MoveComlinkCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
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
        if (_state?.Coordinator.ActivePlayer is not { } sender || _actions is null) return;
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
        batch.Draw(pixel, ComlinkViewLayout.Page, Color.Black);
        batch.Draw(pixel, ComlinkViewLayout.Date, Color.Black);
        batch.Draw(pixel, ComlinkViewLayout.SenderPortrait, Color.Black);
        batch.Draw(pixel, ComlinkViewLayout.SenderName, Color.Black);
        batch.Draw(pixel, ComlinkViewLayout.Message, Color.Black);
        if (messages.Count == 0)
        {
            font.Draw(batch, "NO INCOMING MESSAGES", ComlinkViewLayout.Message.Location.ToVector2(), Color.Lime, 1);
            return;
        }

        _comlinkCursor = Math.Clamp(_comlinkCursor, 0, messages.Count - 1);
        var message = messages[_comlinkCursor];
        var sender = state.FindPlayer(message.Sender);
        font.Draw(batch, $"{_comlinkCursor + 1:00} OF {messages.Count:00}",
            ComlinkViewLayout.Page.Location.ToVector2(), Color.Lime, 1);
        font.Draw(batch, MatchDate(message.Turn), ComlinkViewLayout.Date.Location.ToVector2(), Color.Lime, 1);
        font.Draw(batch, sender?.Setup.Name ?? $"PLAYER {message.Sender.Value + 1}",
            ComlinkViewLayout.SenderName.Location.ToVector2(), Color.White, 1);
        if (sender is not null && _uiSprites is not null)
            batch.Draw(_uiSprites, ComlinkViewLayout.SenderPortrait,
                OriginalSpriteLayout.OverlordPortrait(sender.Setup.PortraitId), Color.White);
        DrawComlinkLines(batch, font, ComlinkTextEditor.DisplayLines(message.Text),
            ComlinkViewLayout.Message.Location, Color.Lime);
        if (_hoverPoint is { } hover && ComlinkViewLayout.Page.Contains(hover))
            DrawHoverTooltip(batch, pixel, font, hover, ComlinkInboxTooltip);
    }

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
            var cell = ComlinkSendLayout.Recipient(slot);
            var player = state.Players.FirstOrDefault(value => value.Id.Value == slot);
            var eligible = EligibleComlinkRecipient(slot);
            batch.Draw(pixel, cell, _comlinkRecipients[slot] ? Color.Lime : Color.Black);
            if (player is null) continue;
            var foreground = _comlinkRecipients[slot] ? Color.Black
                : eligible ? Color.Lime : Color.DarkGray;
            var accent = eligible ? PlayerColors[slot] : Color.DarkGray;
            batch.Draw(pixel, ComlinkSendLayout.RecipientAccent(slot), accent);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, ComlinkSendLayout.RecipientPortrait(slot),
                    OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId),
                    eligible ? Color.White : Color.DarkGray);
            var name = player.Setup.Name.Length <= 10 ? player.Setup.Name : player.Setup.Name[..10];
            font.Draw(batch, name,
                ComlinkSendLayout.RecipientNameOrigin(slot).ToVector2(), foreground, 1);
        }
        batch.Draw(pixel, ComlinkSendLayout.Message, Color.Black);
        DrawComlinkLines(batch, font, _comlinkEditor.DisplayLines(),
            ComlinkSendLayout.TextOrigin, Color.Lime, ComlinkSendLayout.TextRowStride);
        DrawPressedComlinkSendButton(batch);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites,
                ComlinkSendLayout.CaretDestination(_comlinkEditor.Column, _comlinkEditor.Row),
                ComlinkSendLayout.CaretSource(_comlinkEditor.CharacterAtCursor,
                    _comlinkCaretCadence.UsesInverseGlyph), Color.White);
        if (_comlinkStatus.Length > 0)
            font.Draw(batch, _comlinkStatus.Length <= 40 ? _comlinkStatus : _comlinkStatus[..40],
                new Vector2(SharedPanelLayout.X(92), SharedPanelLayout.Y(181)), Color.OrangeRed, 1);
    }

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
