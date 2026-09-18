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
}
