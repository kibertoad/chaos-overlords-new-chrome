using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

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
        // The orders are going in, so the picks that were waiting to give one are spent. The idle
        // gang warning has already had its say, and a turn it sends back keeps its selection.
        _gangSelection.Clear();
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
        var advance = GameplayTurnFlow.FinishPlanningTurn(_actions.HotSeatRecorder, playerId);
        QueueHotSeatEliminations(advance);
        _diagnostics?.Write("planning.finished", new Dictionary<string, string?>
        {
            ["player"] = playerId.Value.ToString(),
            ["turnBefore"] = previousTurn.ToString(),
            ["turnAfter"] = _state.Coordinator.Turn.ToString(),
            ["phase"] = _state.Coordinator.Phase.ToString()
        });
        _message = string.Empty;
        if (_state.Coordinator.Turn != previousTurn) WriteAutoSave();

        if (_state.Outcome is not null)
            _screens.Show(ClientScreen.Endgame);
        else if (ShowPendingHotSeatElimination())
        {
            _selectedGangIndex = 0;
        }
        else
        {
            PrepareCurrentHireOffers();
            _selectedGangIndex = 0;
            PresentHotSeatPlanningEntry();
        }
    }

    /// <summary>
    /// Writes the rolling autosave for the turn that has just begun.
    /// </summary>
    /// <remarks>
    /// This used to fire only inside the human's own <c>FinishPlanningTurn</c>, when the turn number
    /// changed there. With the human in slot 0 and computers after, the turn rolls over inside
    /// <see cref="RunComputerTurns"/> instead, so the default single-player game wrote an autosave
    /// on no turn at all. Both paths call this now.
    /// </remarks>
    private void WriteAutoSave()
    {
        if (_state is null) return;
        _autoSave.Capture(_state);
    }

    /// <summary>
    /// Drains the rolling autosave worker so the game thread may read or replace the file.
    /// </summary>
    /// <remarks>
    /// The autosave file has one writer, on the worker, and readers on the game thread: the save
    /// browser lists it and the player can load it. Blocking here keeps those apart, and what the
    /// game thread then sees is the turn that has just been played rather than the one before it.
    /// </remarks>
    private void FlushAutoSaves() => _autoSave.Flush();

    private void ReportAutoSaveFailure(int turn, Exception exception)
    {
        _diagnostics?.Write("autosave.failed", new Dictionary<string, string?>
        {
            ["turn"] = turn.ToString(),
            ["error"] = exception.ToString()
        });
        _message = "AUTOSAVE FAILED";
    }

    private void AdvanceDebugPhase()
    {
        if (_state is null) return;
        _gangSelection.Clear();
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
        if (completedTurn) WriteAutoSave();
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
        if (_session is not null || _gameMenuOpen) return;
        if (_state is null || _actions is null
            || _screens.Current is ClientScreen.Title or ClientScreen.Options or ClientScreen.Help
                or ClientScreen.Setup or ClientScreen.Online or ClientScreen.Lobby
                or ClientScreen.Endgame or ClientScreen.Elimination
            || _eliminationHandoffPlayer is not null) return;
        var acted = false;
        var startingTurn = _state.Coordinator.Turn;
        while (_state.Coordinator.ActivePlayer is { } playerId)
        {
            var player = _state.FindPlayer(playerId)!;
            if (player.Setup.Controller != PlayerController.Computer) break;
            // One turn per frame at most. With two or more humans all eliminated and two or more
            // computers still alive the match does not end, and this loop used to run every
            // remaining turn — up to about 190 of them — inside a single Update, with the window
            // unresponsive and the game menu unreachable for the whole of it.
            if (acted && _state.Coordinator.Turn != startingTurn) break;
            if (_state.Coordinator.Phase == TurnPhase.Command)
            {
                _actions.HotSeatRecorder.PrepareAiPlanning(playerId);
                var commands = AiPolicyPlanner.Plan(_state, playerId);
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
                else
                {
                    var advance = GameplayTurnFlow.FinishPlanningTurn(_actions.HotSeatRecorder, playerId);
                    QueueHotSeatEliminations(advance);
                    if (_pendingHotSeatEliminations.Count > 0)
                    {
                        acted = true;
                        break;
                    }
                }
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
        if (_state.Coordinator.Turn != startingTurn) WriteAutoSave();
        if (_state.Outcome is not null)
        {
            _screens.Show(ClientScreen.Endgame);
        }
        else if (!ShowPendingHotSeatElimination()
                 && _state.Coordinator.ActivePlayer is { } nextPlayer
                 && _state.FindPlayer(nextPlayer)!.Setup.Controller == PlayerController.Human)
        {
            _cursor = _state.FindPlayer(nextPlayer)!.Gangs.FirstOrDefault(gang => gang.IsActive)?.SectorId ?? _cursor;
            PresentHotSeatPlanningEntry();
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
        var playerId = _eliminationHandoffPlayer ?? ViewingPlayer(state);
        var player = state.FindPlayer(playerId)!;
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, HandoffLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        DrawCentered(font, batch, player.Setup.Name, HandoffLayout.NameY,
            PlayerColors[playerId.Value], 1);
    }

    /// <summary>
    /// Preserves the original privacy gate: the next-player card appears only when at least two
    /// active local players remain. A one-person game enters planning directly, even when it has
    /// computer opponents.
    /// </summary>
    private void PresentHotSeatPlanningEntry()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || _state.FindPlayer(playerId)?.Setup.Controller != PlayerController.Human)
        {
            _screens.Show(ClientScreen.City);
            return;
        }

        if (HotSeatHandoffPresentation.RequiresPrivateHandoff(_state))
        {
            _screens.Show(ClientScreen.Handoff);
            return;
        }

        FinishHandoff();
    }
}
