using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenOnlineSetup()
    {
        if (!_online.IsHost || _online.Match is null) return;
        _configuringOnlineLobby = true;
        _screens.Show(ClientScreen.Setup);
    }

    private void SaveOnlineSetup()
    {
        if (!_configuringOnlineLobby) return;
        _configuringOnlineLobby = false;
        PushLobbySettings();
        _screens.Show(ClientScreen.Lobby);
    }

    private void CloseOnlineSetup()
    {
        _configuringOnlineLobby = false;
        _screens.Show(ClientScreen.Lobby);
    }

    /// <summary>Names the session, falling back to the one the host would have been given.</summary>
    private string SessionNameOrDefault()
    {
        var name = _online.SessionName.Value.Trim();
        return name.Length > 0 ? name : $"{_online.DisplayName.Value.Trim()}'S CITY";
    }

    /// <summary>
    /// Sends the lobby's settings as the screens have them.
    /// </summary>
    /// <remarks>
    /// One writer for every control that changes a lobby, because the server takes the settings as a
    /// whole document: sending only what one button changed would blank everything else. That is
    /// also why <see cref="AdoptLobbySettings"/> reads the whole document back in when a lobby is
    /// joined, rather than leaving the screens showing whatever the last local game chose.
    /// </remarks>
    private void PushLobbySettings()
    {
        if (!_online.IsHost || _lobby is null || _online.Match is not { } match) return;
        if (match.Status != MatchStatus.Lobby) return;
        var game = new MultiplayerGameSettings(
            _selectedScenario, _selectedDuration, _selectedAiMentality, _playerPortraits,
            _defaultAiPolicy, _online.AllowLateJoin);
        _lobby.UpdateSettings(new MatchSettings(
            SessionNameOrDefault(),
            match.Settings.MaxPlayers,
            SelectedOnlineTurnTimerSeconds,
            _online.PublicListing ? MatchVisibility.Public : MatchVisibility.Private,
            game.ToWire()));
        _online.Status = "SAVING SESSION SETTINGS";
    }

    /// <summary>
    /// Takes a lobby's own settings into the screens that edit them.
    /// </summary>
    /// <remarks>
    /// Called for every lobby this client is seated in, its own included: the settings are sent back
    /// as a whole document, so a host who rejoined an interrupted lobby and changed one thing would
    /// otherwise overwrite the scenario with whatever this copy of the game last had on its setup
    /// screen. A blob this build cannot read is left alone rather than guessed at; the lobby has
    /// nothing to show for it, and starting the match reports it properly.
    /// </remarks>
    private void AdoptLobbySettings(MatchView match)
    {
        _online.SessionName.Set(match.Settings.Name);
        _online.PublicListing = match.Settings.Visibility == MatchVisibility.Public;
        _selectedPlanningTimeLimit = OnlinePlanningLimit(match.Settings.TurnTimerSeconds);
        MultiplayerGameSettings settings;
        try
        {
            settings = MultiplayerGameSettings.FromWire(match.Settings.GameSettings);
        }
        catch (MultiplayerProtocolException)
        {
            return;
        }
        _selectedScenario = settings.Scenario;
        _selectedDuration = settings.Duration;
        _selectedAiMentality = settings.AiMentality;
        _defaultAiPolicy = settings.AiPolicy;
        _online.AllowLateJoin = settings.AllowLateJoin;
        for (var slot = 0; slot < _playerPortraits.Length; slot++)
            _playerPortraits[slot] = settings.Portraits[slot];
    }

    /// <summary>Takes the typed session name off the screen and sends it.</summary>
    private void CommitLobbySessionName()
    {
        if (!_online.SessionName.IsFocused) return;
        _online.SessionName.IsFocused = false;
        if (string.Equals(_online.SessionName.Value.Trim(),
                _online.Match?.Settings.Name, StringComparison.Ordinal))
            return;
        PushLobbySettings();
    }

    private void ChangeLobbyListing(bool publicly)
    {
        if (_online.PublicListing == publicly) return;
        _online.PublicListing = publicly;
        PushLobbySettings();
    }

    private void ChangeLobbyLateJoin(bool allowed)
    {
        if (_online.AllowLateJoin == allowed) return;
        _online.AllowLateJoin = allowed;
        PushLobbySettings();
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
