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
public sealed record MultiplayerSessionOptions(
    MatchHandle Match,
    OriginalData Definitions,
    MatchView View,
    string OwnPlayerId,
    int ResumeAfterSeq);
