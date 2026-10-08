using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>
/// The chat in the online lobby: where it is on either lobby screen, how a message is sent, and how
/// the conversation is drawn.
/// </summary>
public sealed partial class ChaosGame
{
    private Rectangle LobbyChatInput => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.ChatInput : OnlineLobbyLayout.ChatInput;

    /// <summary>
    /// Sends the chat line, when it holds anything, and clears it for the next message.
    /// </summary>
    /// <remarks>
    /// The message is drawn when the next poll reads it back from the log, the same way every other
    /// member sees it, so the panel shows only what the server accepted.
    /// </remarks>
    private void SendLobbyChat()
    {
        var text = _online.Chat.Value.Trim();
        if (text.Length == 0 || _lobby is null) return;
        _lobby.SendChat(text);
        _online.Chat.Set(string.Empty);
    }

    /// <summary>
    /// The lobby chat: the newest messages that fit the log, and the line one is typed on.
    /// </summary>
    /// <remarks>
    /// A message is labelled with its author's name as the roster shows it. One from a member who
    /// has since left keeps a neutral label, because the roster no longer knows them.
    /// </remarks>
    private void DrawLobbyChat(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchView match,
        Rectangle log,
        Rectangle input,
        Color fill)
    {
        batch.Draw(pixel, log, fill);
        DrawBorder(batch, pixel, log, OnlineOutline, 1);
        var rows = LobbyChatPresentation.Rows(
            _online.ChatLines,
            playerId => match.Players.FirstOrDefault(player => player.Id == playerId) is { } author
                ? LobbyRosterEntry(author).Name
                : "PLAYER",
            (log.Width - 10) / OriginalFontLayout.CellWidth,
            (log.Height - 6) / OriginalFontLayout.LineHeight);
        for (var index = 0; index < rows.Count; index++)
            font.Draw(batch, rows[index],
                new Vector2(log.X + 5, log.Y + 4 + index * OriginalFontLayout.LineHeight),
                Color.White, 1);
        DrawFieldBox(batch, pixel, font, input, _online.Chat, "CLICK HERE TO CHAT");
    }
}
