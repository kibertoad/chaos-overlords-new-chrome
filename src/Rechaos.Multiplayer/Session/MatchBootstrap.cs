using Rechaos.Core.GameModel;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The match as this client generated it from the seed and roster, before a single fact from the
/// server was applied to it.
/// </summary>
/// <remarks>
/// Written once, when the session starts, and never again. A session that is reconstructing a
/// match that has moved on hands the interface its reconstructed state through
/// <see cref="MultiplayerNotice.Resumed"/> instead, so nothing here ever changes under the game
/// thread while it reads it.
/// </remarks>
/// <param name="State">The first Command phase, a copy for the interface to plan turn 1 on.</param>
/// <param name="Deadline">
/// When the open turn seals regardless of readiness, as the match view said at bootstrap.
/// <para>
/// Taken from the view rather than waited for on the stream. The event that opened turn 1 was
/// published before this client started reading, so resuming from the view's sequence number
/// skips it by design — and without this the first turn of every match would run with no
/// countdown on screen.
/// </para>
/// </param>
public sealed record MatchBootstrap(MatchState State, DateTimeOffset? Deadline);
