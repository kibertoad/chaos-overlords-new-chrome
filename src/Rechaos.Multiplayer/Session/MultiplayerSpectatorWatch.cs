using System.Collections.Concurrent;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>Something a spectator's watch wants the interface to know, drained from the game loop.</summary>
/// <remarks>The same shape as <see cref="LobbyNotice"/>, for the same reason.</remarks>
public abstract record SpectatorNotice
{
    private SpectatorNotice()
    {
    }

    /// <summary>
    /// The server let this client watch: the membership and the token that proves it, which is
    /// what a reconnect needs.
    /// </summary>
    public sealed record Joined(SpectatorMembership Membership) : SpectatorNotice;

    /// <summary>What the spectator can now see.</summary>
    /// <param name="View">The match as the server last described it to this spectator.</param>
    /// <param name="State">
    /// A copy of the shown state, or null when the shown state did not move since the last notice
    /// (the interface keeps the one it has) or there is none yet.
    /// </param>
    /// <param name="HasState">Whether the session has a state to show at all.</param>
    /// <param name="ShownTurn">The last turn the shown state has resolved, or null without one.</param>
    /// <param name="IsComplete">Whether nothing more will be released.</param>
    public sealed record Progressed(
        SpectatorMatchView View,
        MatchState? State,
        bool HasState,
        int? ShownTurn,
        bool IsComplete) : SpectatorNotice;

    /// <summary>
    /// Whether the server is answering. A spectator polls, so an outage is only ever shown: the
    /// next poll is the retry.
    /// </summary>
    public sealed record ConnectionChanged(bool IsConnected, string? Detail) : SpectatorNotice;

    /// <summary>
    /// The watch is over and will not resume: a refusal the server will keep repeating, or a match
    /// this build cannot rebuild.
    /// </summary>
    /// <param name="Reason">Text a player can act on.</param>
    /// <param name="Error">What went wrong, for diagnostics.</param>
    /// <param name="MembershipGone">
    /// Whether the token is dead (removed by the host, left elsewhere, or the match deleted), so a
    /// reconnect record for it is worth nothing.
    /// </param>
    public sealed record Ended(string Reason, Exception? Error, bool MembershipGone) : SpectatorNotice;
}

/// <summary>
/// Watches one online match as a spectator, off the game loop's thread.
/// </summary>
/// <remarks>
/// <para>
/// A spectator has no event stream: the server holds everything a spectator reads behind the
/// match's delay, and a released turn arrives no sooner than a turn the players seal, so polling
/// at a few seconds costs nothing a viewer could notice. Each poll asks
/// <see cref="MultiplayerSpectatorSession"/> for what has been released since the last one and
/// answers with a <see cref="SpectatorNotice.Progressed"/> when anything changed.
/// </para>
/// <para>
/// Nothing here writes to the match. The only call with a side effect is
/// <see cref="LeaveAsync"/>, which ends this spectator's own token.
/// </para>
/// </remarks>
public sealed class MultiplayerSpectatorWatch : IAsyncDisposable
{
    /// <summary>How long between two polls of the released state.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(3);

    private readonly MultiplayerClient _anonymous;
    private readonly OriginalData _definitions;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentQueue<SpectatorNotice> _notices = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private SpectatorHandle? _handle;
    private Task _running = Task.CompletedTask;
    private Task _leaving = Task.CompletedTask;
    private Task? _disposal;
    private bool _started;
    private bool _leaveRequested;

    /// <param name="http">Shared by every call; the game owns it.</param>
    /// <param name="options">Where the server is, and how patiently to wait for it.</param>
    /// <param name="definitions">The original data the match was generated from.</param>
    /// <param name="pollInterval">How long between polls; <see cref="DefaultPollInterval"/> when null.</param>
    public MultiplayerSpectatorWatch(
        HttpClient http,
        MultiplayerClientOptions options,
        OriginalData definitions,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(definitions);
        _anonymous = new MultiplayerClient(http, options);
        _definitions = definitions;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        BaseAddress = options.RootAddress;
    }

    /// <summary>The server this watch dials, which a reconnect record names.</summary>
    public Uri BaseAddress { get; }

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out SpectatorNotice notice) => _notices.TryDequeue(out notice!);

    /// <summary>Asks to watch a match by its join code, then follows it.</summary>
    public void Join(SpectateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Begin(async cancellationToken =>
        {
            var membership = await _anonymous.SpectateAsync(request, cancellationToken)
                .ConfigureAwait(false);
            _notices.Enqueue(new SpectatorNotice.Joined(membership));
            return _anonymous.WithToken(membership.Token).Spectator(membership.Match.Id);
        });
    }

    /// <summary>Follows a match again with a spectator token kept from an earlier watch.</summary>
    public void Resume(string matchId, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Begin(_ => Task.FromResult(_anonymous.WithToken(token).Spectator(matchId)));
    }

    /// <summary>
    /// Stops watching and ends the token, and answers a task that completes when the server has
    /// been told.
    /// </summary>
    /// <remarks>
    /// Like a player's leave, best effort and outside the watch's own cancellation, so stopping
    /// right after it does not cut it off. The caller should not wait on it.
    /// </remarks>
    public Task LeaveAsync()
    {
        SpectatorHandle? handle;
        lock (_gate)
        {
            handle = _handle;
            _handle = null;
            // Read by the run under the same lock, so a join answered after this point leaves the
            // token it was given instead of keeping it on the server's list.
            _leaveRequested = true;
        }
        _stopping.Cancel();
        if (handle is null) return Task.CompletedTask;
        var leaving = handle.LeaveAsync(CancellationToken.None);
        _leaving = leaving;
        return leaving;
    }

    /// <summary>Stops polling, keeping the token; answers when the last call has finished.</summary>
    public Task StopAsync() => DisposeAsync().AsTask();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate) _disposal ??= DisposeCoreAsync();
        return new ValueTask(_disposal);
    }

    private async Task DisposeCoreAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _running.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping mid-poll is how a watch ends.
        }
        try
        {
            await _leaving.ConfigureAwait(false);
        }
        catch (Exception exception) when (IsServerOrNetworkFailure(exception))
        {
            // A leave that did not get through leaves a token the server retires with the match.
        }
        _stopping.Dispose();
    }

    private void Begin(Func<CancellationToken, Task<SpectatorHandle>> open)
    {
        lock (_gate)
        {
            if (_started || _disposal is not null) return;
            _started = true;
            _running = RunAsync(open, _stopping.Token);
        }
    }

    private async Task RunAsync(
        Func<CancellationToken, Task<SpectatorHandle>> open,
        CancellationToken cancellationToken)
    {
        // Off the caller's thread from the first byte: the caller is a game loop.
        await Task.Yield();
        try
        {
            var handle = await open(cancellationToken).ConfigureAwait(false);
            bool orphaned;
            lock (_gate)
            {
                orphaned = _leaveRequested;
                if (!orphaned && cancellationToken.IsCancellationRequested) return;
                if (!orphaned) _handle = handle;
            }
            if (orphaned)
            {
                // The spectator left while the server was admitting them: end the token it gave.
                try
                {
                    await handle.LeaveAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsServerOrNetworkFailure(exception))
                {
                    // Best effort, like any leave: the server retires the token with the match.
                }
                return;
            }
            var session = await StartWithRetriesAsync(handle, cancellationToken).ConfigureAwait(false);
            Publish(session, moved: true);
            var connected = true;
            while (!session.IsComplete)
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                bool moved;
                var before = session.View;
                try
                {
                    moved = await session.PollAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (TransientFailure.IsTransient(exception))
                {
                    if (connected)
                    {
                        connected = false;
                        _notices.Enqueue(new SpectatorNotice.ConnectionChanged(
                            false, MultiplayerFailureText.Describe(exception)));
                    }
                    continue;
                }
                if (!connected)
                {
                    connected = true;
                    _notices.Enqueue(new SpectatorNotice.ConnectionChanged(true, null));
                }
                if (moved || !SameProgress(before, session.View)) Publish(session, moved);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Left or stopped; there is nobody to tell.
        }
        catch (Exception exception)
        {
            // Every failure ends the view with its reason, including one the replay itself raises
            // (a sealed set that cannot be applied): a run that died without saying so would leave
            // the view showing its last city as if it were still following the match.
            _notices.Enqueue(new SpectatorNotice.Ended(
                MultiplayerFailureText.Describe(exception),
                exception,
                MultiplayerFailureText.IsWatchGone(exception)));
        }
    }

    /// <summary>
    /// Reads the match for the first time, waiting out an outage rather than ending on it.
    /// </summary>
    /// <remarks>
    /// A spectator reconnecting after a dropped network is the common case for a resumed watch,
    /// and the first read is the one most likely to meet the outage that caused it.
    /// </remarks>
    private async Task<MultiplayerSpectatorSession> StartWithRetriesAsync(
        SpectatorHandle handle,
        CancellationToken cancellationToken)
    {
        var connected = true;
        while (true)
        {
            try
            {
                var session = await MultiplayerSpectatorSession.StartAsync(
                    handle, _definitions, cancellationToken).ConfigureAwait(false);
                if (!connected) _notices.Enqueue(new SpectatorNotice.ConnectionChanged(true, null));
                return session;
            }
            catch (Exception exception) when (TransientFailure.IsTransient(exception))
            {
                if (connected)
                {
                    connected = false;
                    _notices.Enqueue(new SpectatorNotice.ConnectionChanged(
                        false, MultiplayerFailureText.Describe(exception)));
                }
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Whether two views would draw the same lines about where the match stands.</summary>
    private static bool SameProgress(SpectatorMatchView before, SpectatorMatchView after) =>
        before.Status == after.Status
        && before.CurrentTurn == after.CurrentTurn
        && before.ReleasedTurn == after.ReleasedTurn
        && before.DelayTurns == after.DelayTurns
        && before.Players.Count == after.Players.Count
        && before.Players.Zip(after.Players).All(pair => pair.First == pair.Second);

    private void Publish(MultiplayerSpectatorSession session, bool moved) =>
        _notices.Enqueue(new SpectatorNotice.Progressed(
            session.View,
            moved ? session.CloneState() : null,
            session.HasState,
            session.ShownTurn,
            session.IsComplete));

    private static bool IsServerOrNetworkFailure(Exception exception) =>
        exception is MultiplayerApiException or MultiplayerProtocolException
            or MultiplayerTimeoutException or HttpRequestException or IOException
            or RetryExhaustedException;
}
