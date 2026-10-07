using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// What a test may read of a running game. Each member only reads; a test changes the game through
/// its shell's input, its clock and its services, as a player and the program would.
/// </summary>
public sealed partial class ChaosGame
{
    internal ClientScreen CurrentScreen => _screens.Current;

    /// <summary>The match on screen, or null on the title, setup and online screens before a start.</summary>
    internal MatchState? Match => _state;

    internal PlanningTimer PlanningClock => _planningTimer;

    internal bool IdleGangWarningOpen => _idleGangWarningOpen;

    internal PanelSlideTransition PanelSlide => _panelSlideTransition;

    /// <summary>The time the last update read its input at.</summary>
    internal TimeSpan InputTime => _inputTime;

    /// <summary>Whether the left button is held in a loop the planning loop does not run through.</summary>
    internal bool HoldsPlanningLoop => HoldsCityPointer();

    /// <summary>The gang the player holds under the pointer, pressed or dragged.</summary>
    internal GangId? HeldGang => _draggedGangId;

    /// <summary>The order the command overlay lists at its cursor, while it lists orders.</summary>
    internal GangAction? CommandAtCursor =>
        _screens.Current == ClientScreen.Commands && !_choosingCommandTarget
            ? CommandOverlayActions[_commandCursor]
            : null;

    /// <summary>Whether the Commands screen shows an order panel rather than the order list.</summary>
    internal bool OrderPanelOpen => _screens.Current == ClientScreen.Commands && _choosingCommandTarget;

    /// <summary>What the online screens say: the status line and the connection error, if any.</summary>
    internal string OnlineStatus => $"{_online.Stage}: {_online.Status} {_online.ConnectionError}".Trim();
}
