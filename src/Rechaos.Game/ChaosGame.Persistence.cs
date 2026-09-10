using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void SaveQuickGame()
    {
        if (_state is null) return;
        try
        {
            NativeSaveStore.SaveAtomic(_quickSavePath, _state);
            _message = "GAME SAVED";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _message = "SAVE FAILED";
        }
    }

    private void LoadQuickGame()
    {
        if (_definitions is null) return;
        try
        {
            var result = NativeSaveStore.LoadRecoveringBackup(_quickSavePath, _definitions);
            _state = result.State;
            _actions = new MatchActions(new MatchReplayRecorder(_state));
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.Replay);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _selectedGangIndex = 0;
            _message = result.RecoveredFromBackup ? "BACKUP GAME LOADED" : "GAME LOADED";
            _lastAudibleEventSequence = _state.Events.LastOrDefault()?.Sequence ?? -1;
            _lastAnimatedEventSequence = _state.Events.LastOrDefault()?.Sequence ?? -1;
            _combatAnimationPlayer.Clear();
            _screens.Show(_state.Outcome is null ? ClientScreen.City : ClientScreen.Endgame);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "LOAD FAILED";
        }
    }

    private void SaveReplay()
    {
        if (_actions is null) return;
        try
        {
            MatchReplayStore.SaveAtomic(_replayPath, _actions.Replay);
            _message = "REPLAY SAVED";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _message = "REPLAY SAVE FAILED";
        }
    }

    private void LoadReplay()
    {
        if (_state is null) return;
        try
        {
            _state = MatchReplayStore.LoadAndReplay(_replayPath, _state.Definitions);
            _actions = new MatchActions(new MatchReplayRecorder(_state));
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.Replay);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _message = "REPLAY VERIFIED";
            _lastAudibleEventSequence = _state.Events.LastOrDefault()?.Sequence ?? -1;
            _lastAnimatedEventSequence = _state.Events.LastOrDefault()?.Sequence ?? -1;
            _combatAnimationPlayer.Clear();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }
}
