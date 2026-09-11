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
            _message = string.Empty;
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
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _selectedGangIndex = 0;
            _message = string.Empty;
            _combatPresentationProgress.ResetTo(
                _state.Players.Select(player => player.Id),
                _state.Events.LastOrDefault()?.Sequence ?? -1);
            _combatAnimationPlayer.Clear();
            _managementReturnScreen = ClientScreen.City;
            _screens.Show(_state.Outcome is null ? ClientScreen.GameInfo : ClientScreen.Endgame);
            if (_state.Outcome is null) StartPlanningTimer(_inputTime);
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
            MatchReplayStore.SaveAtomic(_replayPath, _actions.HotSeatRecorder);
            _message = string.Empty;
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
            var result = MatchReplayStore.LoadAndReplayRecoveringBackup(
                _replayPath, _state.Definitions);
            _state = result.State;
            _actions = new MatchActions(new MatchReplayRecorder(_state));
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _message = string.Empty;
            _combatPresentationProgress.ResetTo(
                _state.Players.Select(player => player.Id),
                _state.Events.LastOrDefault()?.Sequence ?? -1);
            _combatAnimationPlayer.Clear();
            StartPlanningTimer(_inputTime);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }
}
