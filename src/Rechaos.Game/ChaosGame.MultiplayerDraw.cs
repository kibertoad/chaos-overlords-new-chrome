using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// What an online match draws once it is running: the opponents still drafting, the line that says
/// where the turn stands, and the two modals that take the screen when the server stops answering.
/// </summary>
/// <remarks>
/// The screens that get a player into a match are in <c>ChaosGame.OnlineScreens.cs</c>. They are
/// separated because nothing here is a form: this is drawn over the city the player is looking at,
/// and it has to keep out of the way of it.
/// </remarks>
public sealed partial class ChaosGame
{
    private static readonly Rectangle StopReconnectButton = new(222, 354, 196, 28);

    /// <summary>
    /// Marks every seat still drafting this turn, the player's own included, under its portrait on
    /// the city top bar.
    /// </summary>
    /// <remarks>
    /// Drawn on black because the eight rows under the portraits are background art, which lime
    /// text alone is not reliably legible over. Offline there is nobody to wait for, and once the
    /// match is paused by a desync or over altogether nobody is drafting anything, so the captions
    /// go with the turn they describe rather than lingering as a state that cannot change.
    /// </remarks>
    private void DrawOpponentPlanning(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_session is null) return;
        var ownTurnSent = _online.PlanningIsSubmitted;
        var turnIsOpen = _online.PlanningIsOpen || ownTurnSent;
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            if (!OpponentPlanningPresentation.IsDrafting(
                    slot, _session.Slot, turnIsOpen, ownTurnSent,
                    _online.AwaitedSlots, _online.ReadySlots))
                continue;
            var caption = PlayerPortraitLayout.CityCaption(
                slot, OpponentPlanningPresentation.WaitingCaption.Length);
            batch.Draw(pixel, caption, Color.Black);
            font.Draw(batch, OpponentPlanningPresentation.WaitingCaption,
                new Vector2(caption.X, caption.Y), OpponentPlanningPresentation.WaitingColor, 1);
        }
    }

    /// <summary>
    /// The countdown for the open turn, or an empty string when the match has no timer.
    /// </summary>
    /// <remarks>
    /// It is recomputed every frame from the deadline rather than counted down, so a paused match
    /// that resumes with a restarted clock corrects itself the moment the new deadline arrives.
    /// </remarks>
    private string OnlineCountdown()
    {
        if (_online.DeadlineAt is not { } deadline) return string.Empty;
        var remaining = deadline - OnlineServerNow();
        if (remaining <= TimeSpan.Zero) return "SEALING";
        // Built when the second changes, not every frame: the string is identical in between, and
        // this runs in the draw loop of every frame a timed online turn is on screen.
        var whole = (int)remaining.TotalSeconds;
        if (whole != _onlineCountdownSeconds)
        {
            _onlineCountdownSeconds = whole;
            _onlineCountdownText = $"{whole / 60:00}:{whole % 60:00}";
        }
        return _onlineCountdownText;
    }

    /// <summary>
    /// The time now on the SERVER's clock, which every online deadline is an instant on.
    /// </summary>
    /// <remarks>
    /// A machine thirty seconds fast on a thirty-second timer showed the turn expiring before the
    /// server sealed it, while one that was slow was sealed on with time still on the screen. The
    /// footer and the countdown bar both read this, so the two can never disagree.
    /// </remarks>
    private DateTimeOffset OnlineServerNow() =>
        DateTimeOffset.UtcNow + (_session?.ServerTimeOffset ?? TimeSpan.Zero);

    /// <summary>The whole second <see cref="_onlineCountdownText"/> was built for.</summary>
    private int _onlineCountdownSeconds = -1;
    private string _onlineCountdownText = string.Empty;

    /// <summary>
    /// A line for the city screen saying where the online turn stands.
    /// </summary>
    /// <remarks>
    /// A disconnection takes the line over, because it explains everything else on it: a countdown
    /// that is still running and a turn that is not resolving mean something quite different when the
    /// server has stopped answering, and the player is the one who can do something about it.
    ///
    /// An open vote on this player's own seat takes the line over from the stage it is planned in,
    /// and it is the only place they are told: the modal that asks everyone else about them is
    /// deliberately not shown to them (see <see cref="MultiplayerUiState.CurrentTakeoverVote"/>),
    /// and without this they would be looking at a stopped clock with nothing to explain it. It
    /// gives way in turn to a halt the whole match is under, on the same reasoning: a player owed
    /// both answers is owed the one that explains why nothing is resolving for anybody.
    /// </remarks>
    private string OnlineTurnStatus()
    {
        if (!_online.IsConnected)
            return $"RECONNECTING TO THE SERVER  ATTEMPT {_online.ReconnectAttempt}";
        if (_online.TurnSyncError.Length > 0)
            return $"TURN SYNC ERROR  {_online.TurnSyncError}";
        return _online.Stage switch
        {
            MultiplayerStage.Desynced => "MATCH PAUSED  REPAIRING A DESYNC",
            MultiplayerStage.Finished => "MATCH COMPLETE",
            _ when _online.OwnTakeoverVote is { } ownVote =>
                $"YOU MISSED TURN {ownVote.Turn}  THE OTHER PLAYERS ARE VOTING ON "
                    + "COMPUTER CONTROL OF YOUR SEAT",
            MultiplayerStage.WaitingForSeal =>
                _online.ReadySubmissionPending
                    ? "SENDING FINISHED TURN  AWAITING SERVER ACKNOWLEDGEMENT"
                    : _online.SeatedSeats > 0 && _online.ReadySeats >= _online.SeatedSeats
                        ? $"SERVER ACKNOWLEDGED  ALL PLAYERS READY {OnlineSeatTally()}"
                        : $"SERVER ACKNOWLEDGED  WAITING FOR OTHER PLAYERS "
                            + $"{OnlineSeatTally()} {OnlineCountdown()}",
            MultiplayerStage.Playing => $"TURN {_online.PlanningTurn}  {OnlineCountdown()}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// How many seats have finished planning, of the ones the turn seals on.
    /// </summary>
    /// <remarks>
    /// The point of showing it is that "waiting for the other players" does not say whether one
    /// opponent is deciding or four have closed the game. Empty until the server has said something
    /// about this turn's readiness, rather than claiming nobody is ready when nobody has reported.
    /// </remarks>
    private string OnlineSeatTally() =>
        _online.SeatedSeats > 0 ? $"{_online.ReadySeats}/{_online.SeatedSeats}" : string.Empty;

    /// <summary>Modal progress and diagnostics while the session reconnects in the background.</summary>
    private void DrawReconnectPopup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_screens.Current == ClientScreen.Online && _online.ConnectionError.Length > 0)
        {
            DrawOnlineErrorPopup(batch, pixel, font);
            return;
        }
        if (_session is null || _online.IsConnected) return;
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 190));
        var panel = new Rectangle(82, 82, 476, 316);
        batch.Draw(pixel, panel, new Color(12, 22, 20));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "CONNECTION LOST  RECONNECTING", 102, Color.Gold, 1);
        font.Draw(batch, "AUTOMATIC RETRIES CONTINUE FOR UP TO FIVE MINUTES.",
            new Vector2(104, 132), Color.White, 1);
        font.Draw(batch, "RECENT ATTEMPTS", new Vector2(104, 162), new Color(150, 165, 165), 1);
        IReadOnlyList<string> lines = _online.ReconnectLog.Count == 0
            ? ["WAITING FOR THE NEXT ATTEMPT"]
            : _online.ReconnectLog;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Length > 66) line = line[..63] + "...";
            font.Draw(batch, line, new Vector2(104, 184 + index * 22), Color.White, 1);
        }
        DrawButton(batch, pixel, font, StopReconnectButton, "STOP RETRYING", true);
    }

    /// <summary>A modal error that keeps the complete diagnostic available without overflowing.</summary>
    private void DrawOnlineErrorPopup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_screens.Current != ClientScreen.Online || _online.ConnectionError.Length == 0) return;
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 200));
        var panel = OnlineConnectLayout.ErrorPanel;
        batch.Draw(pixel, panel, new Color(12, 22, 20));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        var matchStopped = _online.ConnectionError.StartsWith(
            "ONLINE MATCH STOPPED", StringComparison.Ordinal);
        DrawCentered(font, batch, matchStopped ? "ONLINE MATCH ERROR" : "COULD NOT CONNECT",
            94, Color.Gold, 2);
        DrawCentered(font, batch,
            matchStopped ? "THE MATCH COULD NOT CONTINUE. DETAILS:" : "THE ONLINE REQUEST FAILED. DETAILS:",
            126, Color.White, 1);

        var lines = BugReportTextEditor.Wrap(_online.ConnectionError, 74);
        const int visibleRows = 9;
        for (var index = 0; index < Math.Min(visibleRows, lines.Count); index++)
        {
            var line = index == visibleRows - 1 && lines.Count > visibleRows
                ? lines[index][..Math.Min(lines[index].Length, 71)] + "..."
                : lines[index];
            DrawCentered(font, batch, line, 152 + index * 18, new Color(205, 215, 212), 1);
        }
        DrawCentered(font, batch, _online.ConnectionErrorCopyStatus, 320, Color.Lime, 1);
        DrawButton(batch, pixel, font, OnlineConnectLayout.CopyError, "COPY FULL ERROR", true);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DismissError, "CLOSE", false);
    }

    /// <summary>
    /// Draws the two online panels that take input away, over a frame that has already ended.
    /// </summary>
    /// <remarks>
    /// The screens that return early from <c>Draw</c> (the sliding panels: Hire, Ranking, the combat
    /// summary; and the filtered Last Turn Events) never reached the generic branch that draws these.
    /// The absence vote gates the keyboard and every click on every screen, so a vote that opened
    /// while the player was reading their combat summary — which is where the turn boundary puts
    /// them — left the game looking frozen with nothing on screen to explain it.
    /// </remarks>
    private void DrawBlockingOnlineOverlays(Viewport viewport)
    {
        if (_batch is null || _pixel is null || _font is null || _session is null) return;
        if (_online.IsConnected && _online.CurrentTakeoverVote is null) return;
        _batch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: VirtualInput.Transform(viewport));
        DrawTakeoverVote(_batch, _pixel, _font);
        DrawReconnectPopup(_batch, _pixel, _font);
        _batch.End();
    }
}
