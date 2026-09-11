namespace Rechaos.Game;

public sealed partial class ChaosGame
{
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
            _message = "ACTION CANCELLED";
            return;
        }
        if (_draggedSetupPlayerSlot is not null)
        {
            CancelSetupPlayerDrag();
            _message = "PLAYER MOVE CANCELLED";
            return;
        }
        if (_draggedHireDefinitionId is not null)
        {
            CancelHireDrag();
            _message = "HIRE CANCELLED";
            return;
        }
        if (_draggedGangId is not null)
        {
            CancelGangDrag();
            _message = "MOVE CANCELLED";
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
            case ClientScreen.Finance:
            case ClientScreen.Ranking:
            case ClientScreen.CombatSummary:
                _screens.Show(_managementReturnScreen);
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
