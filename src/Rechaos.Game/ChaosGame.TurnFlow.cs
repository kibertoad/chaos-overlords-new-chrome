using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle HandoffReady = new(266, 246, 108, 66);
    private Texture2D? _handoffPanel;

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
        _diagnostics?.Write("planning.finished", new Dictionary<string, string?>
        {
            ["player"] = playerId.Value.ToString(),
            ["turnBefore"] = previousTurn.ToString(),
            ["turnAfter"] = _state.Coordinator.Turn.ToString(),
            ["phase"] = _state.Coordinator.Phase.ToString()
        });
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
            || _screens.Current is ClientScreen.Title or ClientScreen.Options or ClientScreen.Help
                or ClientScreen.Setup or ClientScreen.Endgame) return;
        var acted = false;
        while (_state.Coordinator.ActivePlayer is { } playerId)
        {
            var player = _state.FindPlayer(playerId)!;
            if (player.Setup.Controller != PlayerController.Computer) break;
            if (_state.Coordinator.Phase == TurnPhase.Command)
            {
                _replay.PrepareAiPlanning(playerId);
                var commands = AiTurnPlanner.Plan(_state, playerId);
                foreach (var command in commands)
                    _replay.Submit(command);
                _diagnostics?.Write("ai.planned", new Dictionary<string, string?>
                {
                    ["player"] = playerId.Value.ToString(),
                    ["turn"] = _state.Coordinator.Turn.ToString(),
                    ["commands"] = commands.Count.ToString()
                });
                PrepareCurrentHireOffers();
                var hiring = _replay.PrepareAiHiring(playerId);
                if (hiring.Choice is { } planningHire)
                    _replay.QueueHire(playerId, planningHire.GangDefinitionId, planningHire.SectorId);
                else if (hiring.RejectedGangDefinitionId is { } rejectedOffer)
                    _replay.SnubHireOffer(playerId, rejectedOffer);
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

    private void DrawHandoff(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height), Color.Black);
        var panel = new Rectangle(266, 148, 108, 164);
        if (_handoffPanel is not null) batch.Draw(_handoffPanel, panel, Color.White);
        else batch.Draw(pixel, panel, new Color(24, 37, 39));
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        DrawCentered(font, batch, player.Setup.Name, 194, PlayerColors[playerId.Value], 1);
        DrawBorder(batch, pixel, HandoffReady, Color.Gold, 2);
    }
}
