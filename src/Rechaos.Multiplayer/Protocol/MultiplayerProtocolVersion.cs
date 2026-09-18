namespace Rechaos.Multiplayer.Protocol;

/// <summary>The wire protocol spoken by this game build.</summary>
/// <remarks>
/// Keep this equal to <c>MULTIPLAYER_PROTOCOL_VERSION</c> in the TypeScript contracts. Increment
/// both whenever a change can affect communication between the game and coordination server.
/// </remarks>
public static class MultiplayerProtocolVersion
{
    public const int Current = 2;
}
