using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
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
        RejectInput("COMLINK IS NOT CARRIED BY AN ONLINE TURN YET");
        return false;
    }

    private void OpenComlinkView(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        if (!ComlinkAvailable()) return;
        _managementReturnScreen = returnScreen;
        var inbox = _state.ComlinkFor(playerId);
        _comlinkCursor = Math.Max(0, inbox.Count - 1);
        if (inbox.HasUnread) _actions?.MarkComlinkRead(playerId);
        _screens.Show(ClientScreen.ComlinkView);
    }

    private void OpenComlinkSend(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is null) return;
        if (!ComlinkAvailable()) return;
        _managementReturnScreen = returnScreen;
        Array.Fill(_comlinkRecipients, false);
        _comlinkEditor.Clear();
        _comlinkStatus = string.Empty;
        _screens.Show(ClientScreen.ComlinkSend);
    }

    private void UpdateComlinkView(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveComlinkCursor(-1);
        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveComlinkCursor(1);
        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Back)) CloseComlink();
    }

    private void UpdateComlinkSend(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Escape))
        {
            CloseComlink();
            return;
        }
        if (Pressed(keyboard, Keys.Enter))
        {
            SendComlink();
            return;
        }
        if (Pressed(keyboard, Keys.Back))
        {
            _comlinkEditor.Backspace();
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
        else if (ComlinkViewLayout.Ok.Contains(point)) CloseComlink();
    }

    private void HandleComlinkSendClick(Point point)
    {
        var recipient = Enumerable.Range(0, MatchLimits.PlayerCount)
            .FirstOrDefault(slot => ComlinkSendLayout.Recipient(slot).Contains(point), -1);
        if (recipient >= 0)
        {
            if (EligibleComlinkRecipient(recipient))
            {
                _comlinkRecipients[recipient] = !_comlinkRecipients[recipient];
                _comlinkStatus = string.Empty;
            }
        }
        else if (ComlinkSendLayout.Cancel.Contains(point)) CloseComlink();
        else if (ComlinkSendLayout.Ok.Contains(point)) SendComlink();
    }

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
        if (count > 0) _comlinkCursor = Mod(_comlinkCursor + delta, count);
    }

    private void SendComlink()
    {
        if (_state?.Coordinator.ActivePlayer is not { } sender || _actions is null) return;
        var recipients = Enumerable.Range(0, MatchLimits.PlayerCount)
            .Where(index => _comlinkRecipients[index])
            .Select(index => new PlayerId(index))
            .ToArray();
        var result = _actions.SendComlinkMessage(sender, recipients, _comlinkEditor.Text);
        ReportInputResult(result.Accepted, result.Message);
        if (!result.Accepted)
        {
            _comlinkStatus = _message;
            return;
        }
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
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
        DrawComlinkPanel(batch, pixel, _comlinkViewBackground, ComlinkViewLayout.Panel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
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
    }

    private void DrawComlinkSend(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        DrawComlinkPanel(batch, pixel, _comlinkSendBackground, ComlinkSendLayout.Panel);
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            var cell = ComlinkSendLayout.Recipient(slot);
            batch.Draw(pixel, cell, Color.Black);
            var player = state.Players.FirstOrDefault(value => value.Id.Value == slot);
            if (player is null || !EligibleComlinkRecipient(slot)) continue;
            batch.Draw(pixel, new Rectangle(cell.X, cell.Y, 7, cell.Height), PlayerColors[slot]);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, ComlinkSendLayout.RecipientPortrait(slot),
                    OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
            font.Draw(batch, _comlinkRecipients[slot] ? "1" : "0",
                new Vector2(cell.Right - 8, cell.Y + 12), Color.Lime, 1);
        }
        batch.Draw(pixel, ComlinkSendLayout.Message, Color.Black);
        DrawComlinkLines(batch, font, _comlinkEditor.DisplayLines(),
            ComlinkSendLayout.Message.Location, Color.Lime);
        if (_comlinkStatus.Length > 0)
            font.Draw(batch, _comlinkStatus.Length <= 40 ? _comlinkStatus : _comlinkStatus[..40],
                new Vector2(196, 306), Color.OrangeRed, 1);
    }

    private static void DrawComlinkPanel(
        SpriteBatch batch, Texture2D pixel, Texture2D? artwork, Rectangle panel)
    {
        if (artwork is not null) batch.Draw(artwork, panel, Color.White);
        else batch.Draw(pixel, panel, new Color(0, 0, 0, 245));
    }

    private static void DrawComlinkLines(
        SpriteBatch batch, PixelFont font, IReadOnlyList<string> lines, Point origin, Color color)
    {
        for (var row = 0; row < lines.Count; row++)
            font.Draw(batch, lines[row], new Vector2(origin.X, origin.Y + row * 9), color, 1);
    }

}
