using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>
/// DEV-UI-026: F10 plays the journal F6 saved, read-only, over the city screen. The live match is
/// set aside, not replaced: nothing advances it while the viewer is open, and closing the viewer
/// puts it back as it was.
/// </summary>
public sealed partial class ChaosGame
{
    private ReplayViewer? _replayViewer;
    private MatchState? _matchBeforeReplay;
    private int _cursorBeforeReplay;
    private int _gangBeforeReplay;
    private string _messageBeforeReplay = string.Empty;

    /// <summary>
    /// The seat the replay's city is drawn for: the replay's active player, or while no one is
    /// active (upkeep, execution, hiring) the last one that was, so the board does not jump to
    /// seat 0 and back on every phase.
    /// </summary>
    private PlayerId? _replaySeat;

    private void OpenReplayPlayback()
    {
        if (_state is null || _session is not null || _definitions is null) return;
        MatchReplayPlaybackLoadResult opened;
        // Opening replays and checks the whole journal, which takes a moment in a long match.
        using (_pointer.Busy())
        {
            try
            {
                opened = MatchReplayStore.OpenPlaybackRecoveringBackup(_replayPath, _definitions);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException
                                              or UnauthorizedAccessException)
            {
                var failure = ReplayFailure.Of(exception);
                _diagnostics?.Write("replay.open.failed", new Dictionary<string, string?>
                {
                    ["kind"] = failure.Kind.ToString(),
                    ["error"] = exception.ToString()
                });
                _message = ReplayViewer.DescribeFailure(failure);
                return;
            }
        }
        if (opened.PrimaryFailure is { } primaryFailure)
            _diagnostics?.Write("replay.open.backup", new Dictionary<string, string?>
            {
                ["primaryFailure"] = primaryFailure.Kind.ToString()
            });
        // The viewer takes every pointer release, so a drag or press begun on the live board
        // would otherwise complete against it after exit.
        CancelCurrentInteraction();
        _matchBeforeReplay = _state;
        _cursorBeforeReplay = _cursor;
        _gangBeforeReplay = _selectedGangIndex;
        _messageBeforeReplay = _message;
        _replayViewer = new ReplayViewer(opened.Playback, opened.PrimaryFailure);
        _replaySeat = null;
        _planningTimer.Pause(_inputTime);
        ShowReplayFrame();
    }

    private void UpdateReplayPlayback(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        if (_replayViewer is null) return;
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)
            || PointerButtonEdges.Pressed(mouse.RightButton, _previousMouse.RightButton))
        {
            CloseReplayPlayback();
            return;
        }

        if (Pressed(keyboard, Keys.Space)) RunReplayCommand(ReplayViewerCommand.TogglePlay);
        if (Pressed(keyboard, Keys.Left)) RunReplayCommand(ReplayViewerCommand.PreviousStep);
        if (Pressed(keyboard, Keys.Right)) RunReplayCommand(ReplayViewerCommand.NextStep);
        if (Pressed(keyboard, Keys.PageUp)) RunReplayCommand(ReplayViewerCommand.PreviousTurn);
        if (Pressed(keyboard, Keys.PageDown)) RunReplayCommand(ReplayViewerCommand.NextTurn);
        if (Pressed(keyboard, Keys.Home)) RunReplayCommand(ReplayViewerCommand.Start);
        if (Pressed(keyboard, Keys.End)) RunReplayCommand(ReplayViewerCommand.End);
        if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.OemPlus) || Pressed(keyboard, Keys.Add))
            RunReplayCommand(ReplayViewerCommand.Faster);
        if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.OemMinus) || Pressed(keyboard, Keys.Subtract))
            RunReplayCommand(ReplayViewerCommand.Slower);
        // The city's own keys for the selected sector, so any frame's sector values can be read.
        if (Pressed(keyboard, Keys.W)) MoveCursor(0, -1);
        if (Pressed(keyboard, Keys.S)) MoveCursor(0, 1);
        if (Pressed(keyboard, Keys.A)) MoveCursor(-1, 0);
        if (Pressed(keyboard, Keys.D)) MoveCursor(1, 0);

        if (PointerButtonEdges.Pressed(mouse.LeftButton, _previousMouse.LeftButton)
            && VirtualInput.TryMap(GraphicsDevice.Viewport, mouse.Position, out var point))
            HandleReplayClick(point);

        // A command above may have closed the viewer on a failed step.
        if (_replayViewer is not { } viewer) return;
        TryMoveReplay(() => viewer.Advance(gameTime.ElapsedGameTime));
    }

    private void HandleReplayClick(Point point)
    {
        if (_replayViewer is not { } viewer) return;
        if (ReplayControlLayout.CommandAt(point) is { } command)
        {
            RunReplayCommand(command);
            return;
        }
        if (ReplayControlLayout.TimelinePositionAt(point, viewer.Playback.StepCount) is { } position)
        {
            TryMoveReplay(() => viewer.SeekTo(position));
            return;
        }
        if (!ReplayControlLayout.Panel.Contains(point) && CityMapLayout.TrySectorAt(point, out var sector))
            _cursor = sector;
    }

    private void RunReplayCommand(ReplayViewerCommand command)
    {
        if (_replayViewer is not { } viewer) return;
        if (command == ReplayViewerCommand.Exit)
        {
            CloseReplayPlayback();
            return;
        }
        TryMoveReplay(() => viewer.Execute(command));
    }

    /// <summary>
    /// Runs a move of the viewer and shows the frame it lands on. The journal was verified when it
    /// was opened and each step is checked again as it is applied, so a failure here means this
    /// build stopped reproducing a step it reproduced a moment ago; the viewer closes with the
    /// reason instead of letting the exception end the process.
    /// </summary>
    private void TryMoveReplay(Func<bool> move)
    {
        if (_replayViewer is null) return;
        bool moved;
        try
        {
            moved = move();
        }
        catch (InvalidDataException exception)
        {
            _diagnostics?.Write("replay.step.failed", new Dictionary<string, string?>
            {
                ["error"] = exception.ToString()
            });
            CloseReplayPlayback();
            _message = ReplayViewer.DescribeFailure(ReplayFailure.Of(exception));
            return;
        }
        if (moved) ShowReplayFrame();
    }

    private void ShowReplayFrame()
    {
        if (_replayViewer is null) return;
        _state = _replayViewer.Playback.State;
        if (_state.Coordinator.ActivePlayer is { } active) _replaySeat = active;
        _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
        _selectedGangIndex = 0;
        _message = string.Empty;
    }

    /// <summary>Puts the live match back. Does nothing when no viewer is open.</summary>
    private void CloseReplayPlayback()
    {
        // A second close would restore the null the first one left and drop the live match.
        if (_replayViewer is null) return;
        _replayViewer = null;
        _replaySeat = null;
        _state = _matchBeforeReplay;
        _matchBeforeReplay = null;
        _cursor = _cursorBeforeReplay;
        _selectedGangIndex = _gangBeforeReplay;
        _message = _messageBeforeReplay;
        _planningTimer.Resume(_inputTime);
    }

    private void DrawReplayControls(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_replayViewer is not { } viewer || _state is not { } state) return;
        var playback = viewer.Playback;
        var panel = ReplayControlLayout.Panel;
        batch.Draw(pixel, panel, new Color(5, 12, 14));
        DrawBorder(batch, pixel, panel, Color.DarkCyan, 1);

        var columns = ReplayControlLayout.TextColumns;
        var line = panel.Top + 6;
        void Text(string text, Color color)
        {
            font.Draw(batch, text.Length <= columns ? text : text[..columns],
                new Vector2(ReplayControlLayout.TextLeft, line), color, 1);
            line += 12;
        }

        Text("REPLAY", Color.Gold);
        Text(string.Create(CultureInfo.InvariantCulture, $"TURN {playback.TurnAt(playback.Position)}"), Color.Lime);
        Text(string.Create(CultureInfo.InvariantCulture,
            $"STEP {playback.Position} OF {playback.StepCount}"), Color.Lime);
        Text(ReplayViewer.DescribePhase(state), Color.Lime);
        Text(ReplayViewer.DescribeStep(state, playback.CurrentStep, columns), Color.White);

        var timeline = ReplayControlLayout.Timeline;
        batch.Draw(pixel, timeline, new Color(24, 37, 39));
        batch.Draw(pixel, timeline with
        {
            Width = ReplayControlLayout.TimelineFill(playback.Position, playback.StepCount)
        }, Color.DarkCyan);
        DrawBorder(batch, pixel, timeline, Color.Gray, 1);

        foreach (var (bounds, command) in ReplayControlLayout.Buttons)
        {
            batch.Draw(pixel, bounds, new Color(24, 37, 39));
            DrawBorder(batch, pixel, bounds, Color.Gray, 1);
            var label = ReplayControlLayout.Label(command, viewer.Playing);
            var width = label.Length * OriginalFontLayout.CellWidth;
            font.Draw(batch, label,
                new Vector2(bounds.Center.X - width / 2, bounds.Center.Y - OriginalFontLayout.GlyphHeight / 2),
                Color.Gold, 1);
        }

        line = ReplayControlLayout.Buttons[^1].Bounds.Bottom + 10;
        Text(viewer.DescribeTransport(), Color.White);
        Text(viewer.DescribeStatus(), viewer.AtEnd || viewer.PrimaryFailure is not null ? Color.Gold : Color.Lime);
        if (viewer.PrimaryFailure is { } primaryFailure)
            Text(ReplayViewer.DescribePrimaryFailure(primaryFailure), Color.OrangeRed);

        line = panel.Bottom - 4 * 12;
        foreach (var help in ReplayControlLayout.KeyHelp) Text(help, Color.Gray);
    }
}
