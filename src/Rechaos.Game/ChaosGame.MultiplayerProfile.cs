using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

/// <summary>
/// The player's own name and face in a lobby that has not started.
/// </summary>
/// <remarks>
/// <para>
/// Both are the player's to change until the host presses start: the roster is only what every
/// client generates its city from once the match is running, so nothing has been built from it yet.
/// After that the server refuses the change, and the controls here go quiet.
/// </para>
/// <para>
/// The screens draw the player's own row from <see cref="MultiplayerUiState.DisplayName"/> and
/// <see cref="MultiplayerUiState.Portrait"/> rather than from the roster the server last sent, so a
/// click is answered in the frame it was made. What reaches the server is coalesced: a change made
/// while a call is in flight waits for it, and the latest name and face then go in one request.
/// </para>
/// </remarks>
public sealed partial class ChaosGame
{
    /// <summary>The name as it stood when an edit began, which a cancelled edit puts back.</summary>
    private string _lobbyNameBeforeEdit = string.Empty;

    /// <summary>Whether this client is seated in a lobby whose roster may still change.</summary>
    private bool CanEditLobbyProfile() =>
        _session is null
        && _lobby?.Handle is not null
        && _online.Match is { Status: MatchStatus.Lobby };

    /// <summary>This client's own roster entry, as the server last described it.</summary>
    private PlayerView? OwnLobbyPlayer()
    {
        var own = _lobby?.OwnPlayerId;
        return string.IsNullOrEmpty(own)
            ? null
            : _online.Match?.Players.FirstOrDefault(player => player.Id == own);
    }

    /// <summary>
    /// The row the player's own entry is drawn on, which both lobby layouts number the same way.
    /// </summary>
    private int? OwnLobbyRosterRow()
    {
        if (_online.Match is not { } match || _lobby is null) return null;
        var row = 0;
        foreach (var player in match.Players.Where(Seated).Take(MatchLimits.PlayerCount))
        {
            if (player.Id == _lobby.OwnPlayerId) return row;
            row++;
        }
        return null;
    }

    /// <summary>The name and face a roster row shows: the player's own edits for their own row.</summary>
    private (string Name, short Portrait) LobbyRosterEntry(PlayerView player) =>
        player.Id == _lobby?.OwnPlayerId && CanEditLobbyProfile()
            ? (_online.DisplayName.Value, _online.Portrait)
            : (player.DisplayName, OnlinePortrait(player));

    /// <summary>Whether the player's own name is being typed into on the lobby screen.</summary>
    private bool EditingLobbyName => _online.DisplayName.IsFocused && CanEditLobbyProfile();

    /// <summary>
    /// Takes the server's word for this player's name and face.
    /// </summary>
    /// <remarks>
    /// On being seated, because the server trims and normalizes what it was sent and a resumed seat
    /// was never typed on this screen at all; and on a refused change, because the roster is then
    /// the only account of what the other players are looking at.
    /// </remarks>
    private void AdoptOwnProfile(PlayerView player)
    {
        _online.DisplayName.Set(player.DisplayName);
        _online.DisplayName.IsFocused = false;
        _online.Portrait = OnlinePortrait(player);
        _online.ProfilePending = false;
    }

    /// <summary>Handles a click on the player's own roster row; false when it landed elsewhere.</summary>
    private bool HandleLobbyProfileClick(Point point)
    {
        if (!CanEditLobbyProfile() || OwnLobbyRosterRow() is not { } row) return false;
        if (LobbyRosterPortrait(row).Contains(point))
        {
            CommitLobbySessionName();
            FinishLobbyNameEdit(cancel: false);
            CycleLobbyPortrait(1);
            return true;
        }
        if (!LobbyRosterName(row).Contains(point)) return false;
        BeginLobbyNameEdit();
        return true;
    }

    private void CycleLobbyPortrait(int delta)
    {
        if (!CanEditLobbyProfile()) return;
        _online.Portrait = checked((short)Mod(
            _online.Portrait + delta, PlayerPortraitLayout.SelectableCount));
        _online.ProfilePending = true;
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
    }

    private void BeginLobbyNameEdit()
    {
        if (!CanEditLobbyProfile() || _online.DisplayName.IsFocused) return;
        // One caret on the screen at a time: the session name the host was typing is sent first.
        CommitLobbySessionName();
        _lobbyNameBeforeEdit = _online.DisplayName.Value;
        _online.DisplayName.IsFocused = true;
    }

    /// <summary>
    /// Ends an edit of the player's own name, keeping it unless it was cancelled or is unusable.
    /// </summary>
    private void FinishLobbyNameEdit(bool cancel)
    {
        if (!_online.DisplayName.IsFocused) return;
        _online.DisplayName.IsFocused = false;
        if (cancel || !CanEditLobbyProfile() || !RequireUsableName())
        {
            _online.DisplayName.Set(_lobbyNameBeforeEdit);
            return;
        }
        var name = _online.DisplayName.Value.Trim();
        _online.DisplayName.Set(name);
        if (!string.Equals(name, _lobbyNameBeforeEdit, StringComparison.Ordinal))
            _online.ProfilePending = true;
    }

    /// <summary>
    /// Sends the latest name and face once nothing else is in flight.
    /// </summary>
    /// <remarks>
    /// The lobby session drops a call made while another is running, so asking only when it is idle
    /// is what keeps a change from being lost to the once-a-second poll. A name still being typed
    /// is not sent half-written; the face waits for it.
    /// </remarks>
    private void SendPendingLobbyProfile()
    {
        if (!_online.ProfilePending || _online.DisplayName.IsFocused) return;
        if (!CanEditLobbyProfile())
        {
            _online.ProfilePending = false;
            return;
        }
        if (_lobby is not { IsBusy: false } lobby) return;
        _online.ProfilePending = false;
        lobby.UpdateProfile(new UpdatePlayerProfileRequest(
            _online.DisplayName.Value.Trim(), _online.Portrait));
    }

    /// <summary>
    /// A refused change: the roster the others see is put back on screen, with the reason.
    /// </summary>
    private void RejectLobbyProfile(LobbyNotice.Failed failed)
    {
        if (OwnLobbyPlayer() is { } own) AdoptOwnProfile(own);
        _online.Status = failed.Reason;
    }

    /// <summary>
    /// Keeps the unfinished-sessions list naming the player as the roster now does.
    /// </summary>
    private void RememberOwnLobbyName(MatchView match)
    {
        if (_activeMultiplayerRecovery is not { Completed: false } recovery) return;
        var own = match.Players.FirstOrDefault(player => player.Id == recovery.PlayerId);
        if (own is null || string.Equals(own.DisplayName, recovery.DisplayName, StringComparison.Ordinal))
            return;
        UpdateOnlineRecovery(recovery with { DisplayName = own.DisplayName });
    }
}
