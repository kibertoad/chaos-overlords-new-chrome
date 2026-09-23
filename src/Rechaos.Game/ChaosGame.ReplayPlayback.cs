using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly double[] ReplaySpeeds = [0.25, 0.5, 1, 2, 4, 8];
    private static readonly string[] ReplayControlLabels =
        ["START", "PREV", "PLAY", "NEXT", "END", "SPEED", "EXIT"];
    private const int ReplayControlLeft = 12;
    private const int ReplayControlWidth = 90;
    private MatchReplayPlayback? _replayPlayback;
    private MatchState? _matchBeforeReplay;
    private int _cursorBeforeReplay;
    private int _gangBeforeReplay;
    private string _messageBeforeReplay = string.Empty;
    private bool _replayPlaying;
    private int _replaySpeed = 2;
    private double _replayElapsed;
    private string _replayStatus = string.Empty;

    private void OpenReplayPlayback()
    {
        if (_state is null || _session is not null) return;
        try
        {
            var opened = MatchReplayStore.OpenPlaybackRecoveringBackup(
                _replayPath, _state.Definitions);
            // Playback swallows pointer releases, so a drag or press begun on the live board
            // would otherwise complete against it after exit.
            CancelCurrentInteraction();
            _matchBeforeReplay = _state;
            _cursorBeforeReplay = _cursor;
            _gangBeforeReplay = _selectedGangIndex;
            _messageBeforeReplay = _message;
            _replayPlayback = opened.Playback;
            _replayPlaying = false;
            _replayElapsed = 0;
            _replayStatus = opened.RecoveredFromBackup
                ? opened.PrimaryRepaired ? "BACKUP RECOVERED" : "BACKUP LOADED  REPAIR FAILED"
                : "VERIFIED REPLAY";
            _planningTimer.Pause(_inputTime);
            ShowReplayFrame();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            _message = IncompatibleSave.IsIncompatible(exception)
                ? "REPLAY INCOMPATIBLE"
                : exception is FileNotFoundException ? "REPLAY NOT FOUND"
                : exception is InvalidDataException ? ReplayVerificationMessage(exception)
                : "REPLAY LOAD FAILED";
        }
    }

    private static string ReplayVerificationMessage(Exception exception) =>
        exception.Message.Contains("diverg", StringComparison.OrdinalIgnoreCase)
            ? "REPLAY DIVERGED"
            : "REPLAY VERIFICATION FAILED";

    private void UpdateReplayPlayback(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        if (_replayPlayback is null) return;
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)
            || PointerButtonEdges.Pressed(mouse.RightButton, _previousMouse.RightButton))
        {
            CloseReplayPlayback();
            return;
        }

        if (Pressed(keyboard, Keys.Space)) _replayPlaying = !_replayPlaying;
        if (Pressed(keyboard, Keys.Up)) _replaySpeed = Math.Min(_replaySpeed + 1, ReplaySpeeds.Length - 1);
        if (Pressed(keyboard, Keys.Down)) _replaySpeed = Math.Max(_replaySpeed - 1, 0);
        if (Pressed(keyboard, Keys.Left)) SeekReplay(_replayPlayback.Position - 1);
        if (Pressed(keyboard, Keys.Right)) SeekReplay(_replayPlayback.Position + 1);
        if (Pressed(keyboard, Keys.Home)) SeekReplay(0);
        if (Pressed(keyboard, Keys.End)) SeekReplay(_replayPlayback.StepCount);

        if (PointerButtonEdges.Pressed(mouse.LeftButton, _previousMouse.LeftButton)
            && VirtualInput.TryMap(GraphicsDevice.Viewport, mouse.Position, out var point)
            && point.Y >= 408 && point.Y < 432)
        {
            switch (Math.Clamp(point.X / ReplayControlWidth, 0, ReplayControlLabels.Length - 1))
            {
                case 0: SeekReplay(0); break;
                case 1: SeekReplay(_replayPlayback.Position - 1); break;
                case 2: _replayPlaying = !_replayPlaying; break;
                case 3: SeekReplay(_replayPlayback.Position + 1); break;
                case 4: SeekReplay(_replayPlayback.StepCount); break;
                case 5: _replaySpeed = (_replaySpeed + 1) % ReplaySpeeds.Length; break;
                default: CloseReplayPlayback(); break;
            }
        }

        if (!_replayPlaying || _replayPlayback is null) return;
        _replayElapsed += gameTime.ElapsedGameTime.TotalSeconds;
        var interval = 0.35 / ReplaySpeeds[_replaySpeed];
        var stepsThisFrame = 0;
        while (_replayElapsed >= interval && _replayPlaying && stepsThisFrame++ < 16)
        {
            _replayElapsed -= interval;
            if (!TryMoveReplay(playback => playback.MoveNext()))
            {
                _replayPlaying = false;
                break;
            }
        }
    }

    private void SeekReplay(int position)
    {
        if (_replayPlayback is null) return;
        _replayPlaying = false;
        _replayElapsed = 0;
        var target = Math.Clamp(position, 0, _replayPlayback.StepCount);
        TryMoveReplay(playback =>
        {
            playback.Seek(target);
            return true;
        });
    }

    /// <summary>
    /// Moves the cursor and shows the frame it lands on. The journal was verified when opened, so a
    /// failure here is a defect rather than bad data; it ends playback instead of the process, which
    /// would otherwise write the replayed frame as the crash-recovery save.
    /// </summary>
    private bool TryMoveReplay(Func<MatchReplayPlayback, bool> move)
    {
        if (_replayPlayback is null) return false;
        try
        {
            if (!move(_replayPlayback)) return false;
        }
        catch (InvalidDataException exception)
        {
            CloseReplayPlayback();
            _message = ReplayVerificationMessage(exception);
            return false;
        }
        ShowReplayFrame();
        return true;
    }

    private void ShowReplayFrame()
    {
        if (_replayPlayback is null) return;
        _state = _replayPlayback.State;
        _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
        _selectedGangIndex = 0;
        _message = string.Empty;
    }

    private void CloseReplayPlayback()
    {
        _replayPlayback = null;
        _replayPlaying = false;
        _state = _matchBeforeReplay;
        _matchBeforeReplay = null;
        _cursor = _cursorBeforeReplay;
        _selectedGangIndex = _gangBeforeReplay;
        _message = _messageBeforeReplay;
        _planningTimer.Resume(_inputTime);
    }

    private void DrawReplayControls(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_replayPlayback is null) return;
        batch.Draw(pixel, new Rectangle(0, 390, 640, 70), new Color(5, 12, 14));
        var position = _replayPlayback.Position;
        var step = _replayPlayback.CurrentStep?.Kind.ToString().ToUpperInvariant() ?? "OPENING STATE";
        font.Draw(batch, $"REPLAY {position}/{_replayPlayback.StepCount}  {step}",
            new Vector2(ReplayControlLeft, 394), Color.Lime, 1);
        // Each label sits at the left of the band UpdateReplayPlayback hit-tests for it.
        for (var index = 0; index < ReplayControlLabels.Length; index++)
            font.Draw(batch, ReplayControlLabels[index],
                new Vector2(ReplayControlLeft + index * ReplayControlWidth, 413), Color.Gold, 1);
        var status = position == _replayPlayback.StepCount && position > 0
            ? "END OF REPLAY"
            : _replayStatus;
        font.Draw(batch,
            $"{(_replayPlaying ? "PLAYING" : "PAUSED")}  {ReplaySpeeds[_replaySpeed]:0.##}X  {status}",
            new Vector2(ReplayControlLeft, 437), Color.White, 1);
    }
}
