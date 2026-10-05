using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>Whether a visible text editor currently owns keyboard input.</summary>
    private bool TextInputHasFocus()
    {
        if (_gameMenuOpen)
            return (_bugReportOpen && _bugReportFocus == BugReportFocus.Message)
                   || (_saveBrowserMode != SaveBrowserMode.None && _editingSaveName);

        return _screens.Current switch
        {
            ClientScreen.Setup => _editingPlayerName is not null,
            ClientScreen.Online => _online.Stage == MultiplayerStage.Connect
                                   && OnlineFields.Any(field => field.IsFocused),
            ClientScreen.Lobby => (_online.IsHost && _online.SessionName.IsFocused) || EditingLobbyName,
            ClientScreen.ComlinkSend => true,
            _ => false
        };
    }

    /// <summary>
    /// Lets go of what the player is in the middle of, or backs out of the screen. A right press
    /// passes its <paramref name="rightPress"/> point, for the screens whose own entry gives the
    /// right button a meaning of its own.
    /// </summary>
    private void CancelCurrentInteraction(Point? rightPress = null)
    {
        if (_idleGangWarningOpen)
        {
            CancelIdleGangWarning();
            return;
        }
        if (_editingPlayerName is not null)
        {
            FinishSetupNameEdit(cancel: true);
            return;
        }
        if (_screens.Current == ClientScreen.Lobby && _online.DisplayName.IsFocused)
        {
            FinishLobbyNameEdit(cancel: true);
            return;
        }
        if (_pressedSetupButton is not null)
        {
            _pressedSetupButton = null;
            _message = string.Empty;
            return;
        }
        if (_pressedSetupPanelControl is not null)
        {
            _pressedSetupPanelControl = null;
            _message = string.Empty;
            return;
        }
        if (_pressedCityConsoleControl is not null)
        {
            KeepLeftHoldUntilRelease();
            CancelCityConsolePress();
            _message = string.Empty;
            return;
        }
        if (_pressedComlinkSendButton is not null)
        {
            CancelComlinkSendButton();
            _message = string.Empty;
            return;
        }
        if (_pressedAttackFace is not null)
        {
            CancelAttackFace();
            _message = string.Empty;
            return;
        }
        if (_pressedEventsButton is not null)
        {
            CancelEventsButton();
            _message = string.Empty;
            return;
        }
        if (_pressedCommandPanelButton is not null)
        {
            CancelCommandPanelButton();
            _message = string.Empty;
            return;
        }
        if (_pressedHireRejectSlot is not null)
        {
            KeepLeftHoldUntilRelease();
            CancelHireReject();
            _message = string.Empty;
            return;
        }
        if (_draggedSetupPlayerSlot is not null)
        {
            CancelSetupPlayerDrag();
            _message = string.Empty;
            return;
        }
        if (_draggedHireDefinitionId is not null)
        {
            KeepLeftHoldUntilRelease();
            CancelHireDrag();
            _message = string.Empty;
            return;
        }
        if (_draggedGangId is not null)
        {
            KeepLeftHoldUntilRelease();
            CancelGangDrag();
            _message = string.Empty;
            return;
        }

        switch (_screens.Current)
        {
            case ClientScreen.Options:
                CloseOptions();
                break;
            case ClientScreen.Help:
                CloseHelp();
                break;
            case ClientScreen.Setup:
                _screens.Show(ClientScreen.Title);
                break;
            case ClientScreen.Events:
                CloseEvents();
                break;
            case ClientScreen.ComlinkView:
            case ClientScreen.ComlinkSend:
                CloseComlink();
                break;
            case ClientScreen.Commands:
                // SCR-ATTACK-001 lists no right-button input, so the Attack picker stays open.
                if (!IsAttackPickerOpen()) BackFromCommands();
                break;
            case ClientScreen.Hire:
                _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Sector:
                // SCR-UI-004: the right button presses the back control and the cards as the left
                // does, and nothing else.
                if (rightPress is { } point) HandleSectorRightPress(point);
                else _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.SectorGangs:
                CloseSectorGangs();
                break;
            case ClientScreen.Gang:
                CloseGangDetails();
                break;
            case ClientScreen.Site:
                CloseSiteDetails();
                break;
            case ClientScreen.ItemInformation:
                CloseItemDetails();
                break;
            case ClientScreen.GameInfo:
                CloseGameInformation();
                break;
            case ClientScreen.Finance:
            case ClientScreen.Ranking:
                _screens.Show(_managementReturnScreen);
                break;
            // Combat Results takes no right-button input (SCR-COMBAT-001).
            case ClientScreen.Items:
                _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Give:
                CloseGiveEquipment();
                break;
            case ClientScreen.GiveTarget:
                _screens.Show(ClientScreen.Give);
                break;
            case ClientScreen.Sell:
                CloseSellEquipment();
                break;
            case ClientScreen.Search:
                CancelSiteSearch();
                break;
        }
    }
}
