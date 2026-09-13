using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;

namespace Rechaos.Game;

/// <summary>Where the bug report panel's controls sit in the 640x460 interface.</summary>
public static class BugReportLayout
{
    public static Rectangle Panel => new(56, 40, 528, 380);
    public static Rectangle Message => new(76, 92, 488, 200);
    public static Rectangle ShareStateBox => new(76, 304, 13, 13);
    public static Rectangle ShareStateRow => new(76, 300, 488, 22);
    public static Rectangle Send => new(330, 368, 108, 34);
    public static Rectangle Cancel => new(450, 368, 108, 34);

    /// <summary>Characters and lines the message box holds, at the original font's 6x7 cell.</summary>
    public const int MessageColumns = 79;
    public const int MessageRows = 21;
}

/// <summary>Which control the keyboard is driving.</summary>
internal enum BugReportFocus
{
    Message,
    ShareState,
    Send,
    Cancel
}

public sealed partial class ChaosGame
{
    /// <summary>
    /// One client for the process, as <see cref="HttpClient"/> is meant to be used.
    /// </summary>
    /// <remarks>
    /// A field rather than a per-report instance: a new <see cref="HttpClient"/> per send leaks a
    /// socket for the length of its TIME_WAIT, and a player who files three reports in a session
    /// should not be paying for connection setup three times either. Built by the submitter, which
    /// is the only thing that knows what deadline the send needs — a default client carries one of
    /// its own that would quietly win.
    /// </remarks>
    private static readonly HttpClient BugReportHttp = BugReportSubmitter.CreateHttpClient();

    private bool _bugReportOpen;
    private BugReportFocus _bugReportFocus = BugReportFocus.Message;
    private readonly BugReportTextEditor _bugReportText = new();
    private bool _bugReportShareState = true;
    private string _bugReportStatus = string.Empty;

    /// <summary>The send in flight, or null. Composing and compressing are off the game loop.</summary>
    /// <remarks>
    /// Anonymizing a journal replays the whole match and compressing it at the highest Brotli
    /// quality takes seconds on a long one. Doing either on the update thread would freeze the game
    /// for as long as it took, which reads as the crash the player is trying to report.
    /// </remarks>
    private Task<string>? _bugReportSend;

    private void OpenBugReport()
    {
        _bugReportOpen = true;
        _bugReportFocus = BugReportFocus.Message;
        _message = string.Empty;
    }

    private void CloseBugReport()
    {
        // Only the panel closes; a send already in flight is left to finish, and its result is still
        // waiting in the status line when the player comes back. Clearing it here would mean closing
        // the panel one second early threw away the only answer they were going to get.
        _bugReportOpen = false;
        _gameMenuCursor = GameMenuLayout.ReportBugIndex;
    }

    /// <summary>Picks up a finished send, wherever the player has navigated to since.</summary>
    /// <remarks>
    /// The outcome goes to the panel's own line and, when the panel is not up, to the interface's
    /// status line as well. A player who closed the panel while the upload ran is the ordinary case,
    /// and they are the one person who most needs to be told it did not arrive.
    /// </remarks>
    private void PumpBugReportSend()
    {
        if (_bugReportSend is not { IsCompleted: true } send) return;
        _bugReportSend = null;
        // The task reports every expected failure as its own result, so a faulted one is something
        // nobody predicted; say so plainly rather than letting it disappear.
        _bugReportStatus = send.IsCompletedSuccessfully ? send.Result : "COULD NOT SEND THE REPORT";
        var sent = send.IsCompletedSuccessfully
            && send.Result.StartsWith("REPORT SENT", StringComparison.Ordinal);
        if (sent) _bugReportText.Clear();
        if (!_bugReportOpen) _message = _bugReportStatus;
    }

    private void UpdateBugReport(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Escape))
        {
            CloseBugReport();
            return;
        }
        if (Pressed(keyboard, Keys.Tab)) _bugReportFocus = NextBugReportFocus(keyboard);
        if (_bugReportFocus == BugReportFocus.ShareState
            && (Pressed(keyboard, Keys.Space) || Pressed(keyboard, Keys.Enter)))
        {
            _bugReportShareState = !_bugReportShareState;
            return;
        }
        // Enter in the message box is a paragraph break, and arrives through the window's text
        // input like every other character; only the buttons read it as an activation.
        if (!Pressed(keyboard, Keys.Enter)) return;
        if (_bugReportFocus == BugReportFocus.Send) SubmitBugReport();
        else if (_bugReportFocus == BugReportFocus.Cancel) CloseBugReport();
    }

    private BugReportFocus NextBugReportFocus(KeyboardState keyboard)
    {
        var order = Enum.GetValues<BugReportFocus>();
        var step = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
            ? -1
            : 1;
        return order[Mod(Array.IndexOf(order, _bugReportFocus) + step, order.Length)];
    }

    private void HandleBugReportClick(Point point)
    {
        if (BugReportLayout.Message.Contains(point)) _bugReportFocus = BugReportFocus.Message;
        else if (BugReportLayout.ShareStateRow.Contains(point))
        {
            _bugReportFocus = BugReportFocus.ShareState;
            _bugReportShareState = !_bugReportShareState;
        }
        else if (BugReportLayout.Send.Contains(point))
        {
            _bugReportFocus = BugReportFocus.Send;
            SubmitBugReport();
        }
        else if (BugReportLayout.Cancel.Contains(point))
        {
            CloseBugReport();
        }
    }

    /// <summary>
    /// Lifts the match here and does everything else in the background.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The split is about the journal, not about speed. Anonymizing it replays the match, and a
    /// replay reads the live recorder — so a background task holding one would be reading a match
    /// the player is still mutating, and would produce a journal of a game nobody played. What
    /// crosses the boundary is therefore what <see cref="BugReportComposer.Capture"/> takes: bytes
    /// and values, with nothing shared behind them.
    /// </para>
    /// <para>
    /// Capturing is a serialize, which is what saving a game already costs. Anonymizing, compressing
    /// at the highest Brotli quality and uploading are the parts measured in seconds and minutes,
    /// and all three happen off the loop — so a long match does not freeze the game at the moment
    /// somebody is trying to report that it froze.
    /// </para>
    /// </remarks>
    private void SubmitBugReport()
    {
        if (_bugReportSend is not null)
        {
            _bugReportStatus = "ALREADY SENDING";
            return;
        }
        if (_bugReportText.IsEmpty)
        {
            _bugReportStatus = "DESCRIBE WHAT WENT WRONG FIRST";
            _bugReportFocus = BugReportFocus.Message;
            return;
        }

        _diagnostics?.Write("bugreport.submitting", new Dictionary<string, string?>
        {
            ["turn"] = _state?.Coordinator.Turn.ToString(),
            ["phase"] = _state?.Coordinator.Phase.ToString()
        });
        var message = _bugReportText.Value;
        var shareState = _bugReportShareState;
        var captured = BugReportComposer.Capture(
            _actions?.Journal, BugReportComposer.MatchTypeOf(_state, _session is not null));
        _bugReportStatus = "SENDING...";
        _bugReportSend = Task.Run(async () =>
        {
            try
            {
                var composed = BugReportComposer.Compose(message, captured, shareState);
                var receipt = await new BugReportSubmitter(BugReportHttp)
                    .SubmitAsync(composed.Request, CancellationToken.None)
                    .ConfigureAwait(false);
                return SentMessage(composed, receipt.StateStored);
            }
            catch (BugReportException failure)
            {
                // Every outcome the player can act on is named; the rest reads as "it did not go".
                return failure.Failure switch
                {
                    BugReportFailure.Unreachable => "COULD NOT REACH THE REPORT SERVER",
                    BugReportFailure.NotAccepted => "THAT SERVER IS NOT TAKING REPORTS",
                    BugReportFailure.TooMany => "TOO MANY REPORTS SENT  TRY AGAIN SHORTLY",
                    BugReportFailure.Refused => "THE SERVER REFUSED THIS REPORT",
                    _ => "THE SERVER COULD NOT STORE THE REPORT"
                };
            }
        });
    }

    /// <summary>
    /// What the player is told once it landed.
    /// </summary>
    /// <remarks>
    /// Both halves are said: what this build decided to attach, and what the server decided to keep.
    /// A player who ticked the box is owed the truth about whether the journal actually travelled,
    /// because if it did not, the description they wrote is the entire report.
    /// </remarks>
    private static string SentMessage(
        ComposedBugReport composed,
        BugReportReceiptStateStored stored) => composed.StateOutcome switch
    {
        BugReportStateOutcome.NotAnonymizable =>
            "REPORT SENT  GAME STATE COULD NOT BE ANONYMIZED SO IT WAS NOT INCLUDED",
        BugReportStateOutcome.TooLarge =>
            "REPORT SENT  GAME STATE WAS TOO LARGE TO INCLUDE",
        BugReportStateOutcome.Attached when stored
            == BugReportReceiptStateStored.Omitted =>
            "REPORT SENT  THE SERVER COULD NOT STORE THE GAME STATE",
        BugReportStateOutcome.Attached =>
            $"REPORT SENT WITH GAME STATE ({composed.CompressedBytes / 1024} KB)",
        _ => "REPORT SENT"
    };

    private void DrawBugReport(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var panel = BugReportLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 20, 20, 252));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "REPORT BUG", 56, Color.Gold, 2);
        font.Draw(batch, "WHAT WENT WRONG?", new Vector2(76, 78), Color.LightGray, 1);

        var box = BugReportLayout.Message;
        batch.Draw(pixel, box, Color.Black);
        DrawBorder(
            batch, pixel, box,
            _bugReportFocus == BugReportFocus.Message ? Color.Gold : Color.Gray, 1);
        var lines = _bugReportText.DisplayLines(
            BugReportLayout.MessageColumns,
            BugReportLayout.MessageRows,
            _bugReportFocus == BugReportFocus.Message);
        for (var row = 0; row < lines.Count; row++)
            font.Draw(batch, lines[row], new Vector2(box.X + 5, box.Y + 5 + row * 9), Color.White, 1);

        DrawShareStateCheckbox(batch, pixel, font);
        if (!string.IsNullOrEmpty(_bugReportStatus))
            font.Draw(batch, _bugReportStatus, new Vector2(76, 346), Color.Gold, 1);

        DrawButton(
            batch, pixel, font, BugReportLayout.Send,
            _bugReportSend is null ? "SEND" : "SENDING",
            _bugReportFocus == BugReportFocus.Send);
        DrawButton(
            batch, pixel, font, BugReportLayout.Cancel, "CANCEL",
            _bugReportFocus == BugReportFocus.Cancel);
    }

    /// <summary>
    /// The checkbox, and the sentence that says what ticking it actually does.
    /// </summary>
    /// <remarks>
    /// The second line is not decoration. "Share anonymized game state" does not tell anybody what
    /// leaves their machine; "every turn of this match, with names removed" does, and a box that is
    /// on by default has to be one the player could have turned off knowingly.
    /// </remarks>
    private void DrawShareStateCheckbox(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var box = BugReportLayout.ShareStateBox;
        batch.Draw(pixel, box, Color.Black);
        DrawBorder(
            batch, pixel, box,
            _bugReportFocus == BugReportFocus.ShareState ? Color.Gold : Color.Gray, 1);
        if (_bugReportShareState)
            batch.Draw(pixel, new Rectangle(box.X + 3, box.Y + 3, box.Width - 6, box.Height - 6),
                Color.Gold);
        font.Draw(batch, "SHARE ANONYMIZED GAME STATE", new Vector2(96, 307), Color.White, 1);
        if (_actions is null)
        {
            font.Draw(
                batch, "NO MATCH IS RUNNING, SO THERE IS NO GAME STATE TO SHARE.",
                new Vector2(96, 324), Color.LightGray, 1);
            return;
        }
        font.Draw(
            batch, "SENDS EVERY TURN OF THIS MATCH SO THE BUG CAN BE REPLAYED HERE.",
            new Vector2(96, 324), Color.LightGray, 1);
        font.Draw(
            batch, "PLAYER NAMES AND COMLINK MESSAGES ARE REPLACED BEFORE IT IS SENT.",
            new Vector2(96, 334), Color.LightGray, 1);
    }
}
