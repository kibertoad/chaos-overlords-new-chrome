namespace Rechaos.Multiplayer.Protocol;

/// <summary>The wire protocol spoken by this game build.</summary>
/// <remarks>
/// <para>
/// Keep this equal to <c>MULTIPLAYER_PROTOCOL_VERSION</c> in the TypeScript contracts. Increment
/// both whenever a change can affect communication between the game and coordination server.
/// </para>
/// <para>
/// It answers one question only: whether this build and the server it dialed can talk to each
/// other. Whether a match already in progress can be picked up and played on is
/// <see cref="MultiplayerSessionVersion"/>, which moves on its own schedule — a match created by a
/// build that spoke an older protocol resumes here as long as its session version is the one this
/// build plays.
/// </para>
/// </remarks>
public static class MultiplayerProtocolVersion
{
    public const int Current = 9;
}
