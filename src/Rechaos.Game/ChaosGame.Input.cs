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
            ClientScreen.Lobby => _online.IsHost && _online.SessionName.IsFocused,
            ClientScreen.ComlinkSend => true,
            _ => false
        };
    }

    private void CancelCurrentInteraction()
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
        if (_pressedSetupButton is not null)
        {
            _pressedSetupButton = null;
            _message = string.Empty;
            return;
        }
        if (_pressedCityConsoleControl is not null)
        {
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
        if (_pressedHireRejectSlot is not null)
        {
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
            CancelHireDrag();
            _message = string.Empty;
            return;
        }
        if (_draggedGangId is not null)
        {
            CancelGangDrag();
            _message = string.Empty;
            return;
        }

        switch (_screens.Current)
        {
            case ClientScreen.Options:
                if (_editingKeyBindings && _capturingKeyBinding) _capturingKeyBinding = false;
                else if (_editingKeyBindings) CloseKeyBindings();
                else CloseOptions();
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
                BackFromCommands();
                break;
            case ClientScreen.Hire:
                _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Sector:
                _screens.Show(ClientScreen.City);
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
            case ClientScreen.CombatSummary:
                CloseCombatResults();
                break;
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
