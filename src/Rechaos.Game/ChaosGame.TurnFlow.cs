using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void AdvanceTurn()
    {
        if (_debugPhaseStepping) AdvanceDebugPhase();
        else FinishPlanningTurn();
    }

    private void FinishPlanningTurn()
    {
        if (_state is null || _replay is null) return;
        if (_state.Outcome is not null)
        {
            _message = "MATCH COMPLETE";
            return;
        }
        if (_state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            GameplayTurnFlow.AdvanceToPlanning(_replay);
            _message = "PLANNING TURN READY";
            return;
        }

        var previousTurn = _state.Coordinator.Turn;
        GameplayTurnFlow.FinishPlanningTurn(_replay, playerId);
        _pendingHireSlot = null;
        PrepareCurrentHireOffers();
        if (_state.Coordinator.Turn != previousTurn)
        {
            try
            {
                NativeSaveStore.SaveAtomic(_autoSavePath, _state);
                _message = "TURN RESOLVED  AUTOSAVED";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _message = "TURN RESOLVED  AUTOSAVE FAILED";
            }
        }
        else
        {
            _message = "PLANNING COMPLETE";
        }

        if (_state.Outcome is not null)
            _screens.Show(ClientScreen.Endgame);
        else
        {
            _selectedGangIndex = 0;
            _screens.Show(ClientScreen.Handoff);
        }
    }

    private void AdvanceDebugPhase()
    {
        if (_state is null) return;
        if (_state.Outcome is not null)
        {
            _message = "MATCH COMPLETE";
            return;
        }
        var previousActivePlayer = _state.Coordinator.ActivePlayer;
        var completedTurn = _state.Coordinator.Phase == TurnPhase.PlayerElimination;
        var transition = _state.Coordinator.Phase switch
        {
            TurnPhase.Upkeep => _replay!.FinishUpkeep(),
            TurnPhase.Command => _replay!.FinishCommand(_state.Coordinator.ActivePlayer!.Value),
            TurnPhase.Execution => _replay!.FinishExecutionPhase(),
            TurnPhase.Hire => _replay!.FinishHire(_state.Coordinator.ActivePlayer!.Value),
            TurnPhase.PlayerElimination => _replay!.FinishPlayerElimination(),
            _ => throw new InvalidOperationException("Unknown turn phase.")
        };
        _message = transition.ExecutionPhase is { } execution
            ? execution.ToString().ToUpperInvariant()
            : transition.Phase.ToString().ToUpperInvariant();
        if (completedTurn)
        {
            try
            {
                NativeSaveStore.SaveAtomic(_autoSavePath, _state);
                _message += "  AUTOSAVED";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _message += "  AUTOSAVE FAILED";
            }
        }
        if (_state.Outcome is not null)
            _screens.Show(ClientScreen.Endgame);
        else if (transition.ActivePlayer is not null && transition.ActivePlayer != previousActivePlayer)
        {
            _selectedGangIndex = 0;
            _screens.Show(ClientScreen.Handoff);
        }
    }

    private void RunComputerTurns()
    {
        if (_state is null || _replay is null
            || _screens.Current is ClientScreen.Title or ClientScreen.Setup or ClientScreen.Endgame) return;
        var acted = false;
        while (_state.Coordinator.ActivePlayer is { } playerId)
        {
            var player = _state.FindPlayer(playerId)!;
            if (player.Setup.Controller != PlayerController.Computer) break;
            if (_state.Coordinator.Phase == TurnPhase.Command)
            {
                foreach (var command in AiTurnPlanner.Plan(_state, playerId))
                    _replay.Submit(command);
                PrepareCurrentHireOffers();
                if (AiTurnPlanner.ChooseHire(_state, playerId) is { } planningHire)
                    _replay.QueueHire(playerId, planningHire.GangDefinitionId, planningHire.SectorId);
                if (_debugPhaseStepping) _replay.FinishCommand(playerId);
                else GameplayTurnFlow.FinishPlanningTurn(_replay, playerId);
            }
            else if (_debugPhaseStepping && _state.Coordinator.Phase == TurnPhase.Hire)
            {
                if (AiTurnPlanner.ChooseHire(_state, playerId) is { } hire)
                    _replay.QueueHire(playerId, hire.GangDefinitionId, hire.SectorId);
                _replay.FinishHire(playerId);
            }
            else
            {
                break;
            }
            acted = true;
        }
        if (!acted) return;
        _selectedGangIndex = 0;
        _message = "COMPUTER TURN COMPLETE";
        if (_state.Outcome is not null)
        {
            _screens.Show(ClientScreen.Endgame);
        }
        else if (_state.Coordinator.ActivePlayer is { } nextPlayer
                 && _state.FindPlayer(nextPlayer)!.Setup.Controller == PlayerController.Human)
        {
            _cursor = _state.FindPlayer(nextPlayer)!.Gangs.FirstOrDefault(gang => gang.IsActive)?.SectorId ?? _cursor;
            _screens.Show(ClientScreen.Handoff);
        }
        else
        {
            _screens.Show(ClientScreen.City);
        }
    }
}
