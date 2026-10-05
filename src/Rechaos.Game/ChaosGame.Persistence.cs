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
            // FND-SAVE-003: the save keeps every player's selected sector, the planning player's
            // as it stands.
            if (_state.Coordinator.ActivePlayer is { } active) _planningSelections.Store(active, _cursor);
            var summary = SaveSlotCatalog.Save(
                _saveDirectory, slot, name, _state, _session is not null, HotSeatJournal,
                _planningSelections.Snapshot());
            // RULE-UI-015: a written save marks the match saved; the autosave does not.
            _actions?.MarkSaved();
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

    /// <summary>The save named on the command line, opened at start (StartupSave).</summary>
    private readonly string? _startupSavePath;

    /// <summary>
    /// RULE-UI-013, FND-PLATFORM-009: opens the save named on the command line and goes straight
    /// to its match. A file that cannot be loaded leaves the title with the load's message.
    /// </summary>
    private void OpenStartupSave()
    {
        if (_startupSavePath is not { } path || _referenceFrame is not null) return;
        var loaded = AdoptLoadedMatch(
            () => NativeSaveStore.LoadRecoveringBackup(path, _definitions!).State,
            match => MatchJournalStore.TryResumeOnto(MatchJournalStore.PathFor(path), match),
            null,
            () => SaveSlotCatalog.ReadSelectedSectors(path));
        if (loaded) CloseSaveBrowserAfterLoad();
    }

    private bool LoadGameFromSlot(int slot) => AdoptLoadedMatch(
        () => SaveSlotCatalog.Load(_saveDirectory, slot, _definitions!),
        loaded => SaveSlotCatalog.LoadJournal(_saveDirectory, slot, loaded),
        _saveSlots[slot],
        () => SaveSlotCatalog.ReadSelectedSectors(SaveSlotCatalog.SavePath(_saveDirectory, slot)));

    /// <summary>
    /// Loads the browser's automatic row: the rolling autosave, or the crash-recovery save when
    /// that is the newer (<see cref="SaveSlotCatalog.ReadAutomatic"/>).
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
        var path = _automaticRowPath ?? _autoSavePath;
        return AdoptLoadedMatch(
            () => _autoSave.Load(
                () => NativeSaveStore.LoadRecoveringBackup(path, _definitions!).State),
            _ => null,
            _saveSlots[SaveSlotCatalog.AutoSaveRow],
            () => SaveSlotCatalog.ReadSelectedSectors(path));
    }

    private bool AdoptLoadedMatch(
        Func<MatchState> load,
        Func<MatchState, MatchReplayRecorder?> journal,
        SaveSlotSummary? summary,
        Func<IReadOnlyList<int>?>? selectedSectors = null)
    {
        if (_session is not null) return false;
        if (_definitions is null) return false;
        // The original shows the hourglass while it loads a game (RULE-UI-007).
        using var busy = _pointer.Busy();
        try
        {
            var loaded = load();
            // The save is the match; the companion journal, when the slot has one that belongs to
            // it, is only how it got there — the history from the first turn, which is what lets a
            // bug report filed after a load reproduce the whole session rather than the tail of it.
            // Either way the state that is played on is the one that was saved.
            AdoptMatch(
                loaded,
                journal(loaded) ?? new MatchReplayRecorder(loaded),
                summary?.RecoveredFromBackup == true
                    ? summary.PrimaryRepaired ? "BACKUP RECOVERED" : "BACKUP LOADED  REPAIR FAILED"
                    : string.Empty,
                enteredFromSave: true,
                selectedSectors?.Invoke());
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "LOAD FAILED";
            return false;
        }
    }

    /// <summary>
    /// Puts a match read from disk on screen, the one way every load does it.
    /// </summary>
    /// <remarks>
    /// Shared by the save browser and F10's replay load. The replay load had its own copy that
    /// predated the endgame routing, the hot-seat planning entry and the per-match resets below, so
    /// a replay of a finished match opened the city instead of the endgame, and the gang selection,
    /// management return screen and planning-entry flags of the previous match leaked into it.
    /// </remarks>
    /// <param name="enteredFromSave">
    /// Whether the match is entered the way the original enters a loaded game. RULE-RNG-001: it
    /// draws on from the run's sequence, so reloading a save does not replay its luck.
    /// RULE-COMLINK-004, FMT-STATE-005: every inbox is emptied, so it starts with no messages.
    /// RULE-AI-003, FND-AI-045: every player's sector weights and combat-advantage hostility are
    /// refreshed, as the original's load does. The journal records all three moves, and replays them. A replay load keeps the sequence and the
    /// inboxes the journal reached.
    /// </param>
    private void AdoptMatch(
        MatchState loaded, MatchReplayRecorder recorder, string message,
        bool enteredFromSave = false, IReadOnlyList<int>? selectedSectors = null)
    {
        // RULE-AUDIO-001, FND-AUDIO-001: re-entering the outer game starts its
        // program from the first track even when another match was already playing.
        _restartSoundtrackProgram = true;
        ReplaceMatch(loaded, new MatchActions(recorder));
        if (enteredFromSave)
        {
            _actions.HotSeatRecorder.ContinueRandomStream(_runRandomState);
            _actions.HotSeatRecorder.EmptyComlinkInboxes();
            _actions.HotSeatRecorder.RefreshAiSectorRecords();
        }
        ResetHotSeatEliminationPresentation(acknowledgeExistingEliminations: true);
        if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
        if (!_debugPhaseStepping) PrepareCurrentHireOffers();
        // RULE-UI-015, FND-UI-058: a loaded match starts saved.
        _actions.MarkSaved();
        _selectedGangIndex = 0;
        _message = message;
        ResetMatchPresentation(_state);
        // FND-SAVE-003: a loaded match restores each player's selected sector from the save; one
        // the save does not keep starts on the sector of its roster slot 0, as a new game does.
        _planningSelections.Restore(_state, selectedSectors);
        _cursor = _planningSelections.For(
            _state.Coordinator.ActivePlayer ?? _state.Players[0].Id, Math.Clamp(_cursor, 0, _state.Sectors.Count - 1));
        _resumedMatchTurn = _state.Coordinator.Turn;
        _resumedGameInfoShown.Clear();
        _continuePlanningEntryAfterGameInfo = false;
        _deferComlinkAlertUntilPlanningVisible = false;
        _managementReturnScreen = ClientScreen.City;
        if (_state.Outcome is not null) ShowMatchEnd(justEnded: false);
        else PresentHotSeatPlanningEntry();
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
            var path = SaveSlotCatalog.CrashRecoveryPath(_saveDirectory);
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
        if (_state is null || _session is not null) return;
        try
        {
            var result = MatchReplayStore.LoadAndReplayRecoveringBackup(
                _replayPath, _state.Definitions);
            AdoptMatch(
                result.State,
                new MatchReplayRecorder(result.State),
                result.RecoveredFromBackup
                    ? result.PrimaryRepaired ? "REPLAY RECOVERED" : "REPLAY LOADED  REPAIR FAILED"
                    : string.Empty);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }
}
