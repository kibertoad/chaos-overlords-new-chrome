using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>Which of the connect screen's controls the choices already made on it leave in use.</summary>
public static class OnlineConnectPolicy
{
    /// <summary>
    /// Whether a lobby password is worth asking for.
    /// </summary>
    /// <remarks>
    /// A private session is reached by its join code alone, so the code is the gate and a second
    /// secret to pass around the same table gates nothing further. A joining player is always asked,
    /// because the code in their hand does not tell them whether the session behind it was opened
    /// with a password.
    /// </remarks>
    public static bool PasswordApplies(bool hosting, bool listedPublicly) =>
        !hosting || listedPublicly;

    /// <summary>Whether this client may alter or start the lobby it is looking at.</summary>
    public static bool CanConfigureLobby(
        bool isHost,
        MatchStatus status,
        bool joinedInProgress) =>
        isHost && status == MatchStatus.Lobby && !joinedInProgress;

    /// <summary>
    /// Whether a control that asks the server something may be pressed.
    /// </summary>
    /// <remarks>
    /// A lobby session runs one call at a time and drops a second rather than queueing it, so a
    /// control pressed over an in-flight call does nothing whatever. REFRESH over the browse it had
    /// itself just asked for was the harmless half of that; JOIN over a REFRESH was not, because it
    /// announced JOINING LOBBY and then left the player reading it until the browse it had been
    /// dropped for answered and threw them back to the browser with their choice forgotten.
    /// Drawing and clicking both ask this, so a call that would be dropped is one the player can
    /// see is not being offered rather than one that swallows the press.
    /// </remarks>
    /// <param name="hasLobby">Whether there is a session to call through at all.</param>
    /// <param name="lobbyIsBusy">Whether that session already has a call in flight.</param>
    /// <param name="subjects">
    /// How many rows the control acts on, for the ones that act on a selected row. One for a
    /// control that needs no selection, which is what the default leaves it.
    /// </param>
    public static bool CanCallLobby(bool hasLobby, bool lobbyIsBusy, int subjects = 1) =>
        hasLobby && !lobbyIsBusy && subjects > 0;
}
