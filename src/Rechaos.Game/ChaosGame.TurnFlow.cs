using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle HandoffReady = HandoffLayout.Ready;
    private Texture2D? _handoffPanel;
    private readonly PlanningSelectionMemory _planningSelections = new();

    private void AdvanceTurn()
    {
        // FND-OBJECTIVE-004, FND-STATE-010: Done ends a final view, with no idle-gang warning.
        if (_finalViewPlayer is not null)
        {
            ShowNextFinalView();
            return;
        }
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
        // Including a gang still held under the pointer, should any path end the turn from under
        // a drag: the next player must not inherit it, nor a hire offer held from the dock of the
        // player whose turn this was. The planning clock waits for the drag (RULE-TIMER-002).
        ForgetGangDrag();
        ForgetHireDrag();
        StopPlanningTimer();
        if (_state.Outcome is not null)
        {
            _message = string.Empty;
            return;
        }
        var previousTurn = _state.Coordinator.Turn;
        if (_state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            // Starting outside Command (a load mid-phase) can still roll into the next turn, which
            // earns the same autosave and cue as the ordinary advance below.
            GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
            _message = string.Empty;
            CompleteTurnAdvance(previousTurn);
            return;
        }

        // FND-SAVE-003: the selection the player leaves is kept for its next planning.
        _planningSelections.Store(playerId, _cursor);
        PlanningAdvance advance;
        // The original shows the hourglass while it resolves a turn (RULE-UI-007).
        using (_pointer.Busy())
            advance = GameplayTurnFlow.FinishPlanningTurn(_actions.HotSeatRecorder, playerId);
        QueueHotSeatEliminations(advance);
        _diagnostics?.Write("planning.finished", new Dictionary<string, string?>
        {
            ["player"] = playerId.Value.ToString(),
            ["turnBefore"] = previousTurn.ToString(),
            ["turnAfter"] = _state.Coordinator.Turn.ToString(),
            ["phase"] = _state.Coordinator.Phase.ToString()
        });
        _message = string.Empty;
        CompleteTurnAdvance(previousTurn);

        if (_state.Outcome is not null)
            ShowMatchEnd();
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

    /// <summary>Autosaves the turn that has just begun, if one has.</summary>
    private void CompleteTurnAdvance(int previousTurn)
    {
        if (_state is null || _state.Coordinator.Turn == previousTurn) return;
        // RULE-AUDIO-006: a local game has no turn-start sound.
        WriteAutoSave();
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
        ForgetGangDrag();
        ForgetHireDrag();
        if (_state.Outcome is not null)
        {
            _message = string.Empty;
            return;
        }
        var previousActivePlayer = _state.Coordinator.ActivePlayer;
        var previousTurn = _state.Coordinator.Turn;
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
            WriteAutoSave();
        }
        if (_state.Outcome is not null)
            ShowMatchEnd();
        else if (transition.ActivePlayer is not null && transition.ActivePlayer != previousActivePlayer)
        {
            _selectedGangIndex = 0;
            _screens.Show(ClientScreen.Handoff);
        }
    }

    // The computers plan in a local match on its own screens, while no menu, elimination card or
    // elimination hand-off card waits for the human.
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(_state), nameof(_actions))]
    private bool ComputerTurnsCanRun() =>
        _session is null && !_gameMenuOpen && _state is not null && _actions is not null
        && _screens.Current is not (ClientScreen.Title or ClientScreen.Options or ClientScreen.Help
            or ClientScreen.Setup or ClientScreen.Online or ClientScreen.Lobby
            or ClientScreen.Endgame or ClientScreen.Elimination)
        && _eliminationHandoffPlayer is null;

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
        if (!ComputerTurnsCanRun()) return;
        var acted = false;
        var startingTurn = _state.Coordinator.Turn;
        // A computer's planning and the resolution it ends in run below in this one update, under
        // the hourglass the original shows while it resolves a turn (RULE-UI-007).
        using var busy = PresentationPointer.Idle(_state) == PointerShape.Hourglass ? _pointer.Busy() : null;
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
                // RULE-TURN-001, RULE-HIRE-002: the vacant offers are refilled on entry, before the
                // planner (RULE-AI-001) makes any draw of its own.
                PrepareCurrentHireOffers();
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
        CompleteTurnAdvance(startingTurn);
        if (_state.Outcome is not null)
        {
            ShowMatchEnd();
        }
        else if (!ShowPendingHotSeatElimination()
                 && _state.Coordinator.ActivePlayer is { } nextPlayer
                 && _state.FindPlayer(nextPlayer)!.Setup.Controller == PlayerController.Human)
        {
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
        // SCR-SETUP-002, FND-SETUP-016: the slot's colour bar, its name over a black backing and
        // its portrait doubled to 64 by 64. The name's cells are copied from the plain strip, as
        // the Send panel's cards copy theirs (FND-UI-019). The backing holds the ten characters a
        // local name can have; a longer online name is cut there.
        batch.Draw(pixel, HandoffLayout.ColourBar, SetupPlayerCardArtLayout.Colours[playerId.Value]);
        batch.Draw(pixel, HandoffLayout.NameBacking, Color.Black);
        var name = player.Setup.Name;
        font.Copy(batch, name[..Math.Min(name.Length, LocalSetupPolicy.MaximumPlayerNameCharacters)],
            HandoffLayout.Name, OriginalFontLayout.PlainStrip);
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, HandoffLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
            if (_handoffReadyHeld && _hoverPoint is { } hover && HandoffLayout.Ready.Contains(hover))
                batch.Draw(_uiSprites, HandoffLayout.Ready, HandoffLayout.ReadyPressedSource, Color.White);
        }
        if (_session is null) return;
        // Online the card is the break between turns rather than a privacy gate, and the server's
        // clock keeps running behind it. It says how the last turn sealed in full, which the city's
        // 32-character message line cannot, and where the new one stands.
        DrawCentered(font, batch, _message, HandoffLayout.Panel.Bottom + 14, Color.Gold, 1);
        font.Draw(batch, OnlineTurnStatus(), new Vector2(18, 439), new Color(180, 190, 190), 1);
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

        // FND-SAVE-003: the player's planning starts on the sector it left selected.
        _cursor = _planningSelections.For(playerId, _cursor);
        if (HotSeatHandoffPresentation.RequiresPrivateHandoff(_state))
        {
            _screens.Show(ClientScreen.Handoff);
            return;
        }

        FinishHandoff();
    }
}
