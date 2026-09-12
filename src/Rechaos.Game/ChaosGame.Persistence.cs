using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private SaveSlotSummary? SaveGameToSlot(int slot, string name)
    {
        if (_state is null) return null;
        try
        {
            var summary = SaveSlotCatalog.Save(
                _saveDirectory, slot, name, _state, _session is not null);
            _message = "GAME SAVED";
            return summary;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _message = "SAVE FAILED";
            return null;
        }
    }

    private bool LoadGameFromSlot(int slot)
    {
        if (_definitions is null) return false;
        try
        {
            var loaded = SaveSlotCatalog.Load(_saveDirectory, slot, _definitions);
            if (_session is not null) EndOnlineMatch("LOADED SAVED GAME");
            _state = loaded;
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
            _siteSearchSelections.Reset();
            _managementReturnScreen = ClientScreen.City;
            _screens.Show(_state.Outcome is null ? ClientScreen.GameInfo : ClientScreen.Endgame);
            if (_state.Outcome is null) StartPlanningTimer(_inputTime);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "LOAD FAILED";
            return false;
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
            _siteSearchSelections.Reset();
            StartPlanningTimer(_inputTime);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }
}
