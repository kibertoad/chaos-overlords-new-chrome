namespace Rechaos.Multiplayer.Protocol;

/// <summary>The shape of a stored online session that this game build can play.</summary>
/// <remarks>
/// <para>
/// Keep this equal to <c>MULTIPLAYER_SESSION_VERSION</c> in the TypeScript contracts. Increment
/// both only when a session written by an older build can no longer be resumed correctly by this
/// one: a change to the deterministic rules, to the order document, to the settings a city is
/// generated from, or to how a state is hashed. A change that only moves bytes over the wire
/// belongs to <see cref="MultiplayerProtocolVersion"/> and leaves this alone, so a match in
/// progress survives both sides being updated underneath it.
/// </para>
/// <para>
/// A match carries the version it was created under for the whole of its life. It is never
/// migrated: the server holds the session as opaque history and the clients are the only things
/// that can read it, so an old session stays playable by old builds and is refused by new ones
/// rather than being reinterpreted by them.
/// </para>
/// </remarks>
public static class MultiplayerSessionVersion
{
    public const int Current = 12;

    /// <summary>
    /// What a record that predates the field is read as.
    /// </summary>
    /// <remarks>
    /// The first version is the only one that can have been stored without saying so, because the
    /// field and the number were introduced together.
    /// </remarks>
    public const int Initial = 1;

    /// <summary>Whether a session stored under <paramref name="sessionVersion"/> can be played here.</summary>
    public static bool CanResume(int sessionVersion) => sessionVersion == Current;
}
