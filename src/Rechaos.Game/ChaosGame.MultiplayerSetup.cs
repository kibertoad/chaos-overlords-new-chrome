using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenOnlineSetup()
    {
        if (!_online.IsHost || _online.Match is null) return;
        var settings = MultiplayerGameSettings.FromWire(_online.Match.Settings.GameSettings);
        _selectedScenario = settings.Scenario;
        _selectedDuration = settings.Duration;
        _selectedAiMentality = settings.AiMentality;
        _defaultAiPolicy = settings.AiPolicy;
        _online.AllowLateJoin = settings.AllowLateJoin;
        _online.PublicListing = _online.Match.Settings.Visibility == MatchVisibility.Public;
        _selectedPlanningTimeLimit = OnlinePlanningLimit(_online.Match.Settings.TurnTimerSeconds);
        _configuringOnlineLobby = true;
        _screens.Show(ClientScreen.Setup);
    }

    private void SaveOnlineSetup()
    {
        if (!_configuringOnlineLobby || _lobby is null || _online.Match is null) return;
        var game = new MultiplayerGameSettings(
            _selectedScenario, _selectedDuration, _selectedAiMentality, _playerPortraits,
            _defaultAiPolicy, _online.AllowLateJoin);
        _lobby.UpdateSettings(new MatchSettings(
            _online.Match.Settings.Name,
            _online.Match.Settings.MaxPlayers,
            SelectedOnlineTurnTimerSeconds,
            _online.PublicListing ? MatchVisibility.Public : MatchVisibility.Private,
            game.ToWire()));
        _configuringOnlineLobby = false;
        _online.Status = "SAVING SESSION SETTINGS";
        _screens.Show(ClientScreen.Lobby);
    }

    private void CloseOnlineSetup()
    {
        _configuringOnlineLobby = false;
        _screens.Show(ClientScreen.Lobby);
    }

    private int SelectedOnlineTurnTimerSeconds =>
        checked((int)(PlanningTimerPolicy.Duration(_selectedPlanningTimeLimit)?.TotalSeconds ?? 0));

    private static PlanningTimeLimit OnlinePlanningLimit(int seconds) => seconds switch
    {
        0 => PlanningTimeLimit.None,
        <= 30 => PlanningTimeLimit.ThirtySeconds,
        <= 120 => PlanningTimeLimit.TwoMinutes,
        _ => PlanningTimeLimit.FiveMinutes,
    };
}
