using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle HandoffReady = HandoffLayout.Ready;
    private Texture2D? _handoffPanel;

    private void AdvanceTurn()
    {
        // Online, finishing planning sends the turn and waits: the match advances when the server
        // seals it and every client applies the same set, not when this one decides it is done.
        // The idle-gang warning still gets its say first — an unordered gang is as easy to miss
        // online as off, and harder to fix once the turn has sealed.
        if (_session is not null)
        {
            if (!_online.PlanningIsOpen) _message = OnlinePlanningClosed;
            else if (!TryOpenIdleGangWarning()) SubmitOnlineTurn();
        }
        else if (_debugPhaseStepping) AdvanceDebugPhase();
        else if (!TryOpenIdleGangWarning()) FinishPlanningTurn();
    }

    private void FinishPlanningTurn()
    {
        if (_state is null || _actions is null) return;
        StopPlanningTimer();
        if (_state.Outcome is not null)
        {
            _message = string.Empty;
            return;
        }
        if (_state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            _message = string.Empty;
            return;
        }

        var previousTurn = _state.Coordinator.Turn;
        GameplayTurnFlow.FinishPlanningTurn(_actions.HotSeatRecorder, playerId);
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
                _message = string.Empty;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _message = "AUTOSAVE FAILED";
            }
        }
        else
        {
            _message = string.Empty;
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
            _message = string.Empty;
            return;
        }
        var previousActivePlayer = _state.Coordinator.ActivePlayer;
        var completedTurn = _state.Coordinator.Phase == TurnPhase.PlayerElimination;
        var transition = _state.Coordinator.Phase switch
        {
            TurnPhase.Upkeep => _actions!.HotSeatRecorder.FinishUpkeep(),
            TurnPhase.Command => _actions!.HotSeatRecorder.FinishCommand(_state.Coordinator.ActivePlayer!.Value),
            TurnPhase.Execution => _actions!.HotSeatRecorder.FinishExecutionPhase(),
            TurnPhase.Hire => _actions!.HotSeatRecorder.FinishHire(_state.Coordinator.ActivePlayer!.Value),
            TurnPhase.PlayerElimination => _actions!.HotSeatRecorder.FinishPlayerElimination(),
            _ => throw new InvalidOperationException("Unknown turn phase.")
        };
        _message = string.Empty;
        if (completedTurn)
        {
            try
            {
                NativeSaveStore.SaveAtomic(_autoSavePath, _state);
                _message = string.Empty;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _message = "AUTOSAVE FAILED";
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

    /// <summary>
    /// Plays out the computer players of a hot-seat match.
    /// </summary>
    /// <remarks>
    /// It does nothing online. Every unseated slot is a computer player there too, but it is
    /// planned inside the sealed turn, by the same code on every client — planning one here would
    /// be this client alone deciding what a shared player did.
    /// </remarks>
    private void RunComputerTurns()
    {
        if (_session is not null) return;
        if (_state is null || _actions is null
            || _screens.Current is ClientScreen.Title or ClientScreen.Options or ClientScreen.Help
                or ClientScreen.Setup or ClientScreen.Online or ClientScreen.Lobby
                or ClientScreen.Endgame) return;
        var acted = false;
        while (_state.Coordinator.ActivePlayer is { } playerId)
        {
            var player = _state.FindPlayer(playerId)!;
            if (player.Setup.Controller != PlayerController.Computer) break;
            if (_state.Coordinator.Phase == TurnPhase.Command)
            {
                _actions.HotSeatRecorder.PrepareAiPlanning(playerId);
                var commands = AiTurnPlanner.Plan(_state, playerId);
                foreach (var command in commands)
                    _actions.HotSeatRecorder.Submit(command);
                _diagnostics?.Write("ai.planned", new Dictionary<string, string?>
                {
                    ["player"] = playerId.Value.ToString(),
                    ["turn"] = _state.Coordinator.Turn.ToString(),
                    ["commands"] = commands.Count.ToString()
                });
                PrepareCurrentHireOffers();
                var hiring = _actions.HotSeatRecorder.PrepareAiHiring(playerId);
                if (hiring.Choice is { } planningHire)
                    _actions.HotSeatRecorder.QueueHire(playerId, planningHire.GangDefinitionId, planningHire.SectorId);
                else if (hiring.RejectedGangDefinitionId is { } rejectedOffer)
                    _actions.HotSeatRecorder.SnubHireOffer(playerId, rejectedOffer);
                if (_debugPhaseStepping) _actions.HotSeatRecorder.FinishCommand(playerId);
                else GameplayTurnFlow.FinishPlanningTurn(_actions.HotSeatRecorder, playerId);
            }
            else if (_debugPhaseStepping && _state.Coordinator.Phase == TurnPhase.Hire)
            {
                if (AiTurnPlanner.ChooseHire(_state, playerId) is { } hire)
                    _actions.HotSeatRecorder.QueueHire(playerId, hire.GangDefinitionId, hire.SectorId);
                _actions.HotSeatRecorder.FinishHire(playerId);
            }
            else
            {
                break;
            }
            acted = true;
        }
        if (!acted) return;
        _selectedGangIndex = 0;
        _message = string.Empty;
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
        var panel = HandoffLayout.Panel;
        if (_handoffPanel is not null) batch.Draw(_handoffPanel, panel, Color.White);
        else batch.Draw(pixel, panel, new Color(24, 37, 39));
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, HandoffLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        DrawCentered(font, batch, player.Setup.Name, HandoffLayout.NameY,
            PlayerColors[playerId.Value], 1);
        DrawBorder(batch, pixel, HandoffReady, Color.Gold, 2);
    }
}
