using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Votes to remove a seat from the running match: casting one, and keeping the open ones as the
/// event log describes them.
/// </summary>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>
    /// Open votes to remove a seat, by the seat, each with every voter's latest choice. Rebuilt from
    /// the event log on a restore exactly as the takeover votes are, since the match view does not
    /// carry them.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, RemovalChoice>> _removalVotes =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Proposes or approves removing another player from the match, or withdraws that vote.
    /// </summary>
    /// <remarks>
    /// Fire and forget from the interface, like <see cref="VoteOnTakeoverAsync"/>, and for the same
    /// reason a vote that did not land comes back on the notice queue as
    /// <see cref="MultiplayerNotice.RemovalVoteFailed"/>.
    /// </remarks>
    public async Task VoteOnRemovalAsync(
        string playerId,
        RemovalChoice choice,
        CancellationToken cancellationToken = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _stoppingToken);
        try
        {
            await CallAsync(
                token => _match.VoteOnRemovalAsync(
                    playerId,
                    new RemovalVoteRequest(choice == RemovalChoice.Remove
                        ? RemovalVoteRequestDecision.Remove
                        : RemovalVoteRequestDecision.Keep),
                    token),
                lane: null,
                lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or RetryExhaustedException
            or MultiplayerTimeoutException or HttpRequestException or IOException)
        {
            _notices.Enqueue(new MultiplayerNotice.RemovalVoteFailed(
                playerId, choice, Describe(exception)));
        }
    }

    private string RecordRemovalVote(MatchRemovalVoteCastEvent cast)
    {
        if (!_removalVotes.TryGetValue(cast.Payload.PlayerId, out var votes))
        {
            votes = new Dictionary<string, RemovalChoice>(StringComparer.Ordinal);
            _removalVotes[cast.Payload.PlayerId] = votes;
        }
        votes[cast.Payload.VoterPlayerId] = cast.Payload.Decision
            == MatchRemovalVoteCastEventPayloadDecision.Remove
                ? RemovalChoice.Remove
                : RemovalChoice.Keep;
        return cast.Payload.PlayerId;
    }

    private void PublishRemovalVote(string playerId)
    {
        if (!_removalVotes.TryGetValue(playerId, out var votes)) return;
        _notices.Enqueue(new MultiplayerNotice.RemovalVoteChanged(
            playerId, new Dictionary<string, RemovalChoice>(votes, StringComparer.Ordinal)));
    }
}
