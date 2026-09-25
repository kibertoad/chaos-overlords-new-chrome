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
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            _diagnostics?.Write("save.failed", new Dictionary<string, string?>
            {
                ["slot"] = slot.ToString(),
                ["error"] = exception.ToString()
            });
            _message = "SAVE FAILED";
            return null;
        }
    }

    private bool LoadGameFromSlot(int slot) => AdoptLoadedMatch(
        () => SaveSlotCatalog.Load(_saveDirectory, slot, _definitions!),
        loaded => SaveSlotCatalog.LoadJournal(_saveDirectory, slot, loaded),
        _saveSlots[slot]);

    /// <summary>
    /// Loads the rolling autosave.
    /// </summary>
    /// <remarks>
    /// The autosave writes no journal (it is written from inside the turn flow, where capturing one
    /// would double the cost of every turn boundary), so the loaded match starts a fresh recorder.
    /// That loses the session history a slot save keeps, which is the price of the autosave being
    /// there at all after a crash.
    /// </remarks>
    private bool LoadGameFromAutoSave()
    {
        FlushAutoSaves();
        // Loading rewrites the autosave when the primary is damaged, and leaves a damaged primary
        // in place when that repair fails, so the file is no longer one this process wrote and
        // read back. The next autosave has to prove the primary is worth keeping before it may
        // become the backup generation.
        _autoSave.ForgetVerifiedPrimary();
        return AdoptLoadedMatch(
            () => NativeSaveStore.LoadRecoveringBackup(_autoSavePath, _definitions!).State,
            _ => null,
            _saveSlots[SaveSlotCatalog.AutoSaveRow]);
    }

    private bool AdoptLoadedMatch(
        Func<MatchState> load,
        Func<MatchState, MatchReplayRecorder?> journal,
        SaveSlotSummary? summary)
    {
        if (_session is not null) return false;
        if (_definitions is null) return false;
        try
        {
            var loaded = load();
            // The save is the match; the companion journal, when the slot has one that belongs to
            // it, is only how it got there — the history from the first turn, which is what lets a
            // bug report filed after a load reproduce the whole session rather than the tail of it.
            // Either way the state that is played on is the one that was saved.
            _state = loaded;
            _actions = new MatchActions(journal(loaded) ?? new MatchReplayRecorder(loaded));
            ResetHotSeatEliminationPresentation(acknowledgeExistingEliminations: true);
            if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            if (!_debugPhaseStepping) PrepareCurrentHireOffers();
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _selectedGangIndex = 0;
            _message = summary?.RecoveredFromBackup == true
                ? summary.PrimaryRepaired ? "BACKUP RECOVERED" : "BACKUP LOADED  REPAIR FAILED"
                : string.Empty;
            ResetMatchPresentation(_state);
            _resumedMatchTurn = _state.Coordinator.Turn;
            _resumedGameInfoShown.Clear();
            _continuePlanningEntryAfterGameInfo = false;
            _deferComlinkAlertUntilPlanningVisible = false;
            _managementReturnScreen = ClientScreen.City;
            if (_state.Outcome is not null) _screens.Show(ClientScreen.Endgame);
            else PresentHotSeatPlanningEntry();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "LOAD FAILED";
            return false;
        }
    }

    /// <summary>
    /// Writes the live match to <c>crash-recovery.rchsave</c> on the way out of a main-loop crash.
    /// </summary>
    /// <returns>The path written, or null when there was nothing to write or it failed.</returns>
    /// <remarks>
    /// Everything here is best effort and nothing may throw: this runs while the process is already
    /// ending because something else threw, and a second exception would replace the one the player
    /// needs to see. A save that fails its own round trip is exactly the class of defect that gets
    /// here, so the catch is deliberately wide.
    /// </remarks>
    public string? TryWriteCrashRecoverySave()
    {
        try
        {
            if (_state is null || _session is not null) return null;
            var path = Path.Combine(_saveDirectory, "crash-recovery.rchsave");
            NativeSaveStore.SaveAtomic(path, _state);
            return path;
        }
        catch (Exception exception)
        {
            _diagnostics?.Write("crash.recovery.failed", new Dictionary<string, string?>
            {
                ["error"] = exception.ToString()
            });
            return null;
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
        // InvalidOperationException is the desynced recorder, which MatchReplayRecorder.Capture
        // raises before it writes anything; SaveSlotCatalog.WriteJournal already treats it as a
        // companion failure rather than a reason to end the process, and F6 must do the same.
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException)
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
            ResetMatchPresentation(_state);
            StartPlanningTimer(_inputTime);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }
}
