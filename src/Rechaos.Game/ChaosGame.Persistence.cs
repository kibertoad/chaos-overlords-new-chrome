using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>This session's journal, when the match is one this client records in full.</summary>
    private MatchReplayRecorder? HotSeatJournal => _actions?.HotSeatJournal;

    private SaveSlotSummary? SaveGameToSlot(int slot, string name)
    {
        if (_session is not null) return null;
        if (_state is null) return null;
        try
        {
            var summary = SaveSlotCatalog.Save(
                _saveDirectory, slot, name, _state, _session is not null, HotSeatJournal);
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
        if (_session is not null) return false;
        if (_definitions is null) return false;
        try
        {
            var summary = _saveSlots[slot];
            var loaded = SaveSlotCatalog.Load(_saveDirectory, slot, _definitions);
            // The save is the match; the companion journal, when the slot has one that belongs to
            // it, is only how it got there — the history from the first turn, which is what lets a
            // bug report filed after a load reproduce the whole session rather than the tail of it.
            // Either way the state that is played on is the one that was saved.
            _state = loaded;
            _actions = new MatchActions(
                SaveSlotCatalog.LoadJournal(_saveDirectory, slot, loaded)
                ?? new MatchReplayRecorder(loaded));
            ResetHotSeatEliminationPresentation(acknowledgeExistingEliminations: true);
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _selectedGangIndex = 0;
            _message = summary?.RecoveredFromBackup == true
                ? summary.PrimaryRepaired ? "BACKUP RECOVERED" : "BACKUP LOADED  REPAIR FAILED"
                : string.Empty;
            _combatPresentationProgress.ResetTo(
                _state.Players.Select(player => player.Id),
                _state.Events.LastOrDefault()?.Sequence ?? -1);
            _combatAnimationPlayer.Clear();
            _siteSearchSelections.Reset();
            _lastTurnEventArchive.Clear();
            _showGameInfoAtPlanningEntry = false;
            _continuePlanningEntryAfterGameInfo = false;
            _deferComlinkAlertUntilPlanningVisible = false;
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
            ResetHotSeatEliminationPresentation(acknowledgeExistingEliminations: true);
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _message = result.RecoveredFromBackup
                ? result.PrimaryRepaired ? "REPLAY RECOVERED" : "REPLAY LOADED  REPAIR FAILED"
                : string.Empty;
            _combatPresentationProgress.ResetTo(
                _state.Players.Select(player => player.Id),
                _state.Events.LastOrDefault()?.Sequence ?? -1);
            _combatAnimationPlayer.Clear();
            _siteSearchSelections.Reset();
            _lastTurnEventArchive.Clear();
            StartPlanningTimer(_inputTime);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }
}
