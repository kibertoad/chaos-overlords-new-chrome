using Rechaos.Core.Assets;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;

namespace Rechaos.Multiplayer.Session;

/// <summary>What a session needs to start driving a match that has already been seated.</summary>
/// <param name="Match">The token-bound handle for this player.</param>
/// <param name="Definitions">The bundled gameplay tables the city is generated from.</param>
/// <param name="View">The match as the server last described it; the seed and roster come from it.</param>
/// <param name="OwnPlayerId">This client's player id, for reading its own row out of the roster.</param>
/// <param name="ResumeAfterSeq">
/// The last event sequence this client has already handled. Delivery is at least once, so resuming
/// from it may repeat facts already applied; every handler here is idempotent for that reason.
/// </param>
/// <param name="JoinedInProgress">
/// Whether this client took its seat after the match had started.
/// <para>
/// A late joiner's own row is on the roster by the time it bootstraps, so the city it would
/// generate seats itself under its own name where every peer seated a computer player under a
/// derived one — and both the name and the controller feed the state hash, so the first turn it
/// plays desyncs. It therefore starts from the host's snapshot and the event log like any other
/// client reconciling a match that has moved on without it.
/// </para>
/// </param>
public sealed record MultiplayerSessionOptions(
    MatchHandle Match,
    OriginalData Definitions,
    MatchView View,
    string OwnPlayerId,
    int ResumeAfterSeq,
    bool JoinedInProgress = false);
