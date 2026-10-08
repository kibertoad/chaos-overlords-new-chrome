using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.OnlineSmoke;

/// <summary>
/// Watches a short online match as a spectator, through the same watch the game's spectator view
/// drains, and fails loudly if the spectator ever sees more than the delay releases.
/// </summary>
/// <remarks>
/// <para>
/// The kernel tests prove what the server releases, and the session tests prove what the client
/// does with a scripted server. This proves the two together, on the server CI starts: that a
/// spectator joined by join code and password follows the players' own state hashes turn by turn,
/// that the server holds back every turn the delay has not released, that the spectator's token
/// opens no player route and a player's token no spectator route, that a watch resumed with the
/// saved token picks up where the stopped one was, and that the host can end a spectator's watch.
/// </para>
/// <para>
/// The match is a second one, apart from the match <see cref="Program"/> plays, so neither stage
/// depends on what the other did to its players. The first match is used once, by its join code,
/// as the match that does not allow spectators.
/// </para>
/// </remarks>
internal static class SpectatorSmoke
{
    /// <summary>The shortest delay a host may choose (the contracts' `spectatorMinDelayTurns`).</summary>
    private const int Delay = 2;

    private const string Password = "SMOKE-WATCH";

    /// <summary>
    /// Turns played: enough for the delay to release the turns a seat changed hands before.
    /// </summary>
    private const int Turns = ReturnAfterTurn + 1 + Delay;

    /// <summary>The turn after which the first watch stops, keeping its token.</summary>
    private const int StopAfterTurn = 2;

    /// <summary>
    /// The turn after which the guest leaves and the host hands the seat to the computer, so the
    /// next turn is played with it under computer control.
    /// </summary>
    private const int LeaveAfterTurn = 3;

    /// <summary>The turn after which a watch resumes with the kept token.</summary>
    private const int ResumeAfterTurn = 4;

    /// <summary>The turn after which the guest takes the seat back from the computer.</summary>
    private const int ReturnAfterTurn = 4;

    /// <summary>Fast enough that every released turn is seen on its own; the game polls every 3 s.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    public static async Task RunAsync(
        HttpClient http,
        Uri baseAddress,
        OriginalData definitions,
        string unwatchableJoinCode)
    {
        var options = new MultiplayerClientOptions(baseAddress);
        var anonymous = new MultiplayerClient(http, options);

        // A match that does not allow spectators refuses one at the door.
        await RequireRefused(
            () => anonymous.SpectateAsync(
                new SpectateRequest(unwatchableJoinCode, "MALLORY", Password: null), CancellationToken.None),
            HttpStatusCode.Forbidden, "spectating_disabled", "watching a match that does not allow it");

        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
        var host = await anonymous.CreateMatchAsync(
            new CreateMatchRequest(
                new MatchSettings(
                    "WATCHED CITY", 2, 0, MatchVisibility.Public, settings.ToWire(), Delay),
                "ADA",
                HostPortraitId: 0,
                Password,
                MultiplayerProtocolVersion.Current,
                MultiplayerSessionVersion.Current),
            CancellationToken.None);
        var matchId = host.Match.Id;
        Console.WriteLine($"spectators: hosted {matchId} with code {host.JoinCode}, {Delay} turns behind");

        // The browser offers WATCH from this field of the listing.
        var listing = (await anonymous.ListLobbiesAsync(CancellationToken.None))
            .Matches.FirstOrDefault(entry => entry.Id == matchId);
        Program.Require(listing is not null, "the watchable lobby did not come back from browsing");
        Program.Require(listing!.Settings.SpectatorDelayTurns == Delay,
            $"the listing says the delay is {listing.Settings.SpectatorDelayTurns}, not {Delay}");

        await RequireRefused(
            () => anonymous.SpectateAsync(
                new SpectateRequest(host.JoinCode, "MALLORY", "NOT-THE-PASSWORD"), CancellationToken.None),
            HttpStatusCode.Unauthorized, "wrong_password", "watching with the wrong password");

        // Joined in the lobby, the way the game's WATCH A GAME form joins.
        var watch = new MultiplayerSpectatorWatch(http, options, definitions, PollInterval);
        var follower = new Follower("first watch");
        watch.Join(new SpectateRequest(host.JoinCode, "EVE", Password));
        var membership = await follower.JoinedAsync(watch);
        Program.Require(membership.Match.Id == matchId, "the spectator was let into another match");
        Program.Require(membership.Match.DelayTurns == Delay,
            $"the spectator was told the delay is {membership.Match.DelayTurns}");
        var waiting = await follower.WaitAsync(
            watch, "the lobby", progress => progress.View.Status == MatchStatus.Lobby);
        Program.Require(!waiting.HasState, "the spectator had a city before the match started");
        Program.Require(waiting.View.Seed is null, "the spectator was told the seed in the lobby");
        Console.WriteLine($"spectators: EVE ({membership.Spectator.Id}) is waiting in the lobby");

        var guest = await anonymous.JoinAsync(
            new JoinMatchRequest(host.JoinCode, "GRACE", PortraitId: 1, Password),
            CancellationToken.None);
        var hostMatch = anonymous.WithToken(host.Token).Match(matchId);
        var guestMatch = anonymous.WithToken(guest.Token).Match(matchId);

        var listed = await hostMatch.SpectatorsAsync(CancellationToken.None);
        Program.Require(
            listed.Spectators.Any(spectator => spectator.Id == membership.Spectator.Id),
            "the host's list of spectators did not name EVE");
        await RequireRefused(
            () => guestMatch.RemoveSpectatorAsync(membership.Spectator.Id, CancellationToken.None),
            HttpStatusCode.Forbidden, "host_only", "a guest removing a spectator");

        var spectatorClient = anonymous.WithToken(membership.Token);
        await RequireSpectatorCannotAct(spectatorClient.Match(matchId), "in the lobby");
        await RequireRefused(
            () => anonymous.WithToken(host.Token).Spectator(matchId).GetAsync(CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", "a player's token at the spectator door");

        await hostMatch.StartAsync(CancellationToken.None);
        var started = (await hostMatch.GetAsync(CancellationToken.None)).Match;
        var smokeBudget = TimeSpan.FromMinutes(2);
        await using var hostSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            hostMatch, definitions, started, host.Player.Id, started.LastEventSeq,
            StreamOutageBudget: smokeBudget));
        // Replaced when the guest takes the seat back, as the game starts a new session then.
        var guestSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            guestMatch, definitions, started, guest.Player.Id, started.LastEventSeq,
            StreamOutageBudget: smokeBudget));
        await using var guestScope = new Deferred(() => guestSession.DisposeAsync());
        var hostNotices = new List<MultiplayerNotice>();

        var hostState = hostSession.Bootstrap.State;
        var guestState = guestSession.Bootstrap.State;
        // By turn, the fingerprint of the city every player's client reached on that turn. A
        // spectator shows a turn the same way: from the log up to that turn's seal, without a seat
        // that changed hands after it, which the server releases with the next turn.
        var fingerprints = new List<string> { MatchStateHasher.ComputeFingerprint(hostState) };

        // The bound only ever rises as the players submit, so a notice drained late is still
        // checked against a bound that held when it was published.
        follower.Bound = 0;
        await follower.HeldBackAsync(watch, currentTurn: 1);
        Console.WriteLine("spectators: EVE has no city while the players plan turn 1");

        await RequireSpectatorCannotAct(spectatorClient.Match(matchId), "while the match runs");

        string? frankId = null;
        for (var turn = 1; turn <= Turns; turn++)
        {
            // The guest's seat is the computer's for the turns between leaving and returning.
            var guestPlays = turn <= LeaveAfterTurn || turn > ReturnAfterTurn;
            var hostTurn = SpeculativeTurn.For(hostState, definitions, hostSession.Slot);
            Program.Hide(hostTurn);

            // From the moment the turn is submitted the server may seal it and release the one the
            // delay lets through, but never more.
            follower.Bound = Math.Max(0, turn - Delay);
            await hostSession.SubmitOrdersAsync(turn, hostTurn.Build(), true, CancellationToken.None);
            if (guestPlays)
            {
                var guestTurn = SpeculativeTurn.For(guestState, definitions, guestSession.Slot);
                Program.Hide(guestTurn);
                await guestSession.SubmitOrdersAsync(turn, guestTurn.Build(), true, CancellationToken.None);
            }
            if (turn == 1)
            {
                // A spectator token cannot submit for any seat.
                await RequireRefused(
                    () => spectatorClient.Match(matchId).SubmitOrdersAsync(
                        turn, new SubmitOrdersRequest(hostTurn.Build(), true), CancellationToken.None),
                    HttpStatusCode.Unauthorized, "invalid_token", "a spectator submitting orders");
            }

            var hostResolved = await Program.Resolved(hostSession, turn, hostNotices);
            var guestResolved = await Program.Resolved(guestSession, turn);
            Program.Require(hostResolved.StateHash == guestResolved.StateHash,
                $"turn {turn} resolved differently for the players of the watched match");
            hostState = hostResolved.State;
            guestState = guestResolved.State;
            fingerprints.Add(MatchStateHasher.ComputeFingerprint(hostState));

            var released = Math.Max(0, turn - Delay);
            var startReleased = StartReleased(currentTurn: turn + 1);
            if (watch is null)
            {
                Console.WriteLine($"spectators: turn {turn} sealed with nobody watching");
            }
            else if (!startReleased)
            {
                await follower.HeldBackAsync(watch, currentTurn: turn + 1);
                Console.WriteLine($"spectators: turn {turn} sealed, EVE still has no city");
            }
            else
            {
                await follower.ShowsAsync(watch, released, currentTurn: turn + 1, fingerprints);
                Console.WriteLine(
                    $"spectators: turn {turn} sealed, EVE shows turn {released} on {fingerprints[released][..12]}");
            }
            await RequireHeldBack(spectatorClient.Spectator(matchId), released, turn + 1);

            if (turn == 1)
            {
                // A second spectator arrives mid-match and leaves on their own; the players hear both.
                var frank = await anonymous.SpectateAsync(
                    new SpectateRequest(host.JoinCode, "FRANK", Password), CancellationToken.None);
                frankId = frank.Spectator.Id;
                Program.Require(frank.Match.ReleasedTurn == released,
                    $"a spectator joining after turn {turn} was told turn {frank.Match.ReleasedTurn} is released");
                await anonymous.WithToken(frank.Token).Spectator(matchId).LeaveAsync(CancellationToken.None);
                await RequireRefused(
                    () => anonymous.WithToken(frank.Token).Spectator(matchId).GetAsync(CancellationToken.None),
                    HttpStatusCode.Unauthorized, "invalid_token", "a spectator's token after leaving");
            }

            if (turn == LeaveAfterTurn)
            {
                // A seat changing hands is in the log and nowhere in a sealed set, so the spectator
                // reaches the players' hashes from here on only by replaying it on the right turn.
                await guestMatch.LeaveAsync(CancellationToken.None);
                await Program.WaitForPlayer(guestMatch, guest.Player.Id, WirePlayerStatus.Left);
                await hostMatch.VoteOnTakeoverAsync(
                    guest.Player.Id,
                    new TakeoverVoteRequest(TakeoverVoteRequestDecision.Computer),
                    CancellationToken.None);
                await Program.WaitForPlayer(hostMatch, guest.Player.Id, WirePlayerStatus.Computer);
                Console.WriteLine($"spectators: GRACE left after turn {turn} and the computer took the seat");
            }
            if (turn == ReturnAfterTurn)
            {
                await using var lobby = new MultiplayerLobbySession(http, options);
                lobby.Resume(matchId, guest.Player.Id, guest.Token, host.JoinCode);
                var seated = await Program.Seated(lobby);
                Program.Require(seated.Membership.Player.Status == WirePlayerStatus.Active,
                    "the returning guest's seat was not made active");
                // The city the host plans the next turn from has the seat back. A spectator only
                // sees the return with that turn, so this is not the fingerprint of the turn just played.
                var plannedFrom = HandedOver(hostState, definitions, guestSession.Slot, toComputer: false);

                // The session that saw the seat go to the computer never reports a state hash
                // for it again, so the guest plays on from a new session restored from the
                // server's history, the way the game resumes a saved seat.
                await guestSession.DisposeAsync();
                var rejoined = (await guestMatch.GetAsync(CancellationToken.None)).Match;
                guestSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
                    guestMatch, definitions, rejoined, guest.Player.Id, rejoined.LastEventSeq,
                    JoinedInProgress: true, StreamOutageBudget: smokeBudget));
                guestState = (await Restored(guestSession)).State;
                Program.Require(MatchStateHasher.ComputeFingerprint(guestState) == plannedFrom,
                    $"the returning guest restored another city than the one the host plans turn {turn + 1} on");
                Console.WriteLine($"spectators: GRACE took the seat back after turn {turn}");
            }

            if (turn == StopAfterTurn)
            {
                // The game keeps this token in multiplayer-spectating.json and stops the watch
                // without ending it when the player quits or the connection drops.
                await watch!.StopAsync();
                watch = null;
                Console.WriteLine("spectators: EVE's watch stopped, keeping the token");
            }
            else if (turn == ResumeAfterTurn)
            {
                watch = new MultiplayerSpectatorWatch(http, options, definitions, PollInterval);
                follower = new Follower("resumed watch") { Bound = follower.Bound };
                watch.Resume(matchId, membership.Token);
                await follower.ShowsAsync(watch, released, currentTurn: turn + 1, fingerprints);
                Console.WriteLine(
                    $"spectators: EVE resumed with the saved token and shows turn {released}");
            }
        }

        Program.Require(frankId is not null, "the second spectator never joined");
        await WaitForNotice<MultiplayerNotice.SpectatorArrived>(
            hostSession, hostNotices, notice => notice.Spectator.Id == frankId, "FRANK arriving");
        await WaitForNotice<MultiplayerNotice.SpectatorDeparted>(
            hostSession, hostNotices, notice => notice.SpectatorId == frankId && !notice.Removed,
            "FRANK leaving");

        // The host ends EVE's watch; the watch says so and drops the token.
        await hostMatch.RemoveSpectatorAsync(membership.Spectator.Id, CancellationToken.None);
        var ended = await follower.EndedAsync(watch!);
        Program.Require(ended.MembershipGone,
            $"the removed spectator's watch ended without forgetting the token: {ended.Reason}");
        await watch!.DisposeAsync();
        await WaitForNotice<MultiplayerNotice.SpectatorDeparted>(
            hostSession, hostNotices,
            notice => notice.SpectatorId == membership.Spectator.Id && notice.Removed, "EVE being removed");
        await RequireRefused(
            () => spectatorClient.Spectator(matchId).GetAsync(CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", "a removed spectator's token");
        // A game that kept the token and reconnects after the host removed it is told so, and
        // forgets the token rather than offering the reconnect again.
        await using (var stale = new MultiplayerSpectatorWatch(http, options, definitions, PollInterval))
        {
            stale.Resume(matchId, membership.Token);
            var refused = await new Follower("stale watch") { Bound = follower.Bound }.EndedAsync(stale);
            Program.Require(refused.MembershipGone,
                $"resuming with a removed token ended without forgetting it: {refused.Reason}");
        }

        var remaining = await hostMatch.SpectatorsAsync(CancellationToken.None);
        Program.Require(remaining.Spectators.Count == 0,
            $"{remaining.Spectators.Count} spectators are still listed after both stopped");
        Console.WriteLine(
            $"OK: a spectator followed {Turns - Delay} released turns {Delay} behind through two seat "
            + "handovers, reconnected with its token and was removed");
    }

    /// <summary>
    /// Whether a running match on <paramref name="currentTurn"/> has released its starting city:
    /// once the players are the delay past it, when `currentTurn - 1 - delay` reaches 0.
    /// </summary>
    private static bool StartReleased(int currentTurn) => currentTurn - 1 - Delay >= 0;

    /// <summary>The fingerprint of a copy of <paramref name="state"/> with one seat handed over.</summary>
    private static string HandedOver(MatchState state, OriginalData definitions, int slot, bool toComputer)
    {
        var copy = MatchStateClone.Of(state, definitions);
        var seat = new PlayerId(slot);
        var changed = toComputer ? copy.TransferPlayerToComputer(seat) : copy.TransferPlayerToHuman(seat);
        Program.Require(changed, $"seat {slot} did not change hands in the players' city");
        return MatchStateHasher.ComputeFingerprint(copy);
    }

    /// <summary>
    /// Every player route a spectator token could reach for, refused at the door: the two doors
    /// are kept apart, so the token reads as no token at all there.
    /// </summary>
    private static async Task RequireSpectatorCannotAct(MatchHandle asSpectator, string when)
    {
        await RequireRefused(() => asSpectator.GetAsync(CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", $"a spectator reading the match {when}");
        await RequireRefused(() => asSpectator.StartAsync(CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", $"a spectator starting the match {when}");
        await RequireRefused(
            () => asSpectator.PostChatAsync(new PostChatMessageRequest("HELLO"), CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", $"a spectator chatting {when}");
        await RequireRefused(() => asSpectator.SpectatorsAsync(CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", $"a spectator listing spectators {when}");
        await RequireRefused(() => asSpectator.EventsAsync(0, 10, CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", $"a spectator reading the players' log {when}");
        await RequireRefused(() => asSpectator.LeaveAsync(CancellationToken.None),
            HttpStatusCode.Unauthorized, "invalid_token", $"a spectator leaving a seat {when}");
    }

    /// <summary>
    /// What the spectator door itself answers, asked directly rather than through the session, so
    /// a server that leaked a newer turn is caught even when the session would have ignored it.
    /// </summary>
    private static async Task RequireHeldBack(SpectatorHandle door, int released, int currentTurn)
    {
        var view = await door.GetAsync(CancellationToken.None);
        Program.Require(view.ReleasedTurn == released && view.CurrentTurn == currentTurn,
            $"the server released turn {view.ReleasedTurn} on turn {view.CurrentTurn}, "
            + $"expected {released} on {currentTurn}");
        Program.Require((view.Seed is null) == (released < 1),
            $"the seed was {(view.Seed is null ? "withheld" : "given")} with turn {released} released");
        // The roster as the match started: who left or was handed to the computer since is told
        // through the released log, on the turn it reaches.
        Program.Require(view.Players.All(player => player.Status == WirePlayerStatus.Active),
            "the spectator view's roster carried a seat's live status");

        if (StartReleased(currentTurn))
        {
            var snapshot = await door.LatestSnapshotAsync(CancellationToken.None);
            Program.Require(snapshot.Turn <= released,
                $"the server offered the snapshot for turn {snapshot.Turn} with turn {released} released");
        }
        else
        {
            // The starting city is the board the players plan turn 1 on, so it waits for the delay.
            await RequireRefused(() => door.LatestSnapshotAsync(CancellationToken.None),
                HttpStatusCode.NotFound, "no_snapshot", $"the starting city on turn {currentTurn}");
        }

        await RequireRefused(() => door.SealedOrdersAsync(released + 1, CancellationToken.None),
            HttpStatusCode.Conflict, "turn_not_released", $"turn {released + 1}'s sealed set");
        if (released >= 1)
        {
            var orders = await door.SealedOrdersAsync(released, CancellationToken.None);
            Program.Require(orders.Turn == released, $"turn {released}'s sealed set came back as {orders.Turn}");
        }

        // The log ends at the seal of the released turn (the start while none is, and nothing
        // while the start is held back): a seat that changed hands after it, even before the next
        // turn opened, belongs to an unreleased turn.
        var cursor = 0;
        MatchEvent? last = null;
        while (true)
        {
            var page = await door.EventsAsync(cursor, 200, CancellationToken.None);
            foreach (var @event in page.Events)
            {
                last = @event;
                switch (@event)
                {
                    case TurnSealedEvent sealedTurn:
                        Program.Require(sealedTurn.Payload.Turn <= released,
                            $"the spectator log announced turn {sealedTurn.Payload.Turn}'s seal with {released} released");
                        break;
                    case TurnOpenedEvent opened:
                        Program.Require(opened.Payload.Turn <= released,
                            $"the spectator log opened turn {opened.Payload.Turn} with {released} released");
                        break;
                    case MatchStartedEvent or MatchPlayerTakenOverEvent or MatchPlayerReturnedEvent
                        or MatchLatePlayerJoinedEvent:
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"the spectator log carried a {@event.GetType().Name}, which is the players' own");
                }
            }
            if (page.Cursor <= cursor) break;
            cursor = page.Cursor;
        }
        var endsAtTheCut = !StartReleased(currentTurn)
            ? last is null
            : released == 0
            ? last is MatchStartedEvent
            : last is TurnSealedEvent { Payload.Turn: var lastSealed } && lastSealed == released;
        Program.Require(endsAtTheCut,
            $"the spectator log ended on {last?.GetType().Name ?? "nothing"} with turn {released} released");
    }

    private static async Task RequireRefused(
        Func<Task> call,
        HttpStatusCode status,
        string reason,
        string what)
    {
        try
        {
            await call();
        }
        catch (MultiplayerApiException refusal)
        {
            Program.Require(refusal.Status == status && refusal.Reason == reason,
                $"{what} was refused with {(int)refusal.Status} {refusal.Reason}, expected {(int)status} {reason}");
            return;
        }
        throw new InvalidOperationException($"{what} was allowed");
    }

    /// <summary>Waits for a session that joined in progress to rebuild the match.</summary>
    private static async Task<MultiplayerNotice.Resumed> Restored(MultiplayerMatchSession session)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            while (session.TryDequeueNotice(out var notice))
            {
                switch (notice)
                {
                    case MultiplayerNotice.Resumed resumed:
                        return resumed;
                    case MultiplayerNotice.Failed failed:
                        throw new InvalidOperationException(
                            $"the returning guest's session failed: {failed.Reason} ({failed.Error})");
                }
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("the returning guest's session never restored the match");
    }

    /// <summary>Disposes what the callback returns when the scope ends, read at that moment.</summary>
    private sealed class Deferred(Func<ValueTask> dispose) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => dispose();
    }

    private static async Task WaitForNotice<T>(
        MultiplayerMatchSession session,
        List<MultiplayerNotice> seen,
        Func<T, bool> match,
        string what)
        where T : MultiplayerNotice
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            while (session.TryDequeueNotice(out var notice)) seen.Add(notice);
            if (seen.OfType<T>().Any(match)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"the host's session never heard of {what}");
    }

    /// <summary>
    /// Drains one watch the way the game's spectator view does, holding every notice to the newest
    /// turn the server may have released.
    /// </summary>
    private sealed class Follower(string name)
    {
        private MatchState? _state;

        /// <summary>The newest turn the server may have released by now.</summary>
        public int Bound { get; set; }

        private SpectatorNotice.Progressed? Latest { get; set; }

        public async Task<SpectatorMembership> JoinedAsync(MultiplayerSpectatorWatch watch)
        {
            SpectatorMembership? joined = null;
            await PumpUntil(watch, "joining", notice =>
            {
                if (notice is SpectatorNotice.Joined membership) joined = membership.Membership;
                return joined is not null;
            });
            return joined!;
        }

        /// <summary>
        /// Waits until the watch shows <paramref name="turn"/> on a view of
        /// <paramref name="currentTurn"/>, and checks the city is the one the players had then.
        /// </summary>
        public async Task ShowsAsync(
            MultiplayerSpectatorWatch watch,
            int turn,
            int currentTurn,
            IReadOnlyList<string> fingerprints)
        {
            var progress = await WaitAsync(watch, $"turn {turn} on turn {currentTurn}", progress =>
                progress.HasState && progress.ShownTurn == turn && progress.View.CurrentTurn == currentTurn);
            Program.Require(progress.View.ReleasedTurn == turn,
                $"{name} shows turn {turn} with turn {progress.View.ReleasedTurn} released");
            Program.Require(!progress.IsComplete, $"{name} called a running match complete");
            Program.Require(_state is not null, $"{name} has a turn to show and no city");
            var shown = MatchStateHasher.ComputeFingerprint(_state!);
            Program.Require(shown == fingerprints[turn],
                $"{name} shows turn {turn} as {shown[..12]}, the players had {fingerprints[turn][..12]}");
            Program.Require(_state!.Coordinator.Turn == turn + 1,
                $"{name}'s city is planning turn {_state.Coordinator.Turn} after showing turn {turn}");
        }

        /// <summary>
        /// Waits until the watch has seen the running match on <paramref name="currentTurn"/>, and
        /// checks it still has no city because the start is not released yet.
        /// </summary>
        public async Task HeldBackAsync(MultiplayerSpectatorWatch watch, int currentTurn)
        {
            var progress = await WaitAsync(watch, $"the match on turn {currentTurn}", progress =>
                progress.View.Status == MatchStatus.Running && progress.View.CurrentTurn == currentTurn);
            Program.Require(!progress.HasState,
                $"{name} shows turn {progress.ShownTurn} on turn {currentTurn}, before the start is released");
            Program.Require(progress.View.ReleasedTurn == 0,
                $"{name} was told turn {progress.View.ReleasedTurn} is released on turn {currentTurn}");
        }

        public async Task<SpectatorNotice.Ended> EndedAsync(MultiplayerSpectatorWatch watch)
        {
            SpectatorNotice.Ended? ended = null;
            await PumpUntil(watch, "ending", notice =>
            {
                if (notice is SpectatorNotice.Ended end) ended = end;
                return ended is not null;
            });
            return ended!;
        }

        public async Task<SpectatorNotice.Progressed> WaitAsync(
            MultiplayerSpectatorWatch watch,
            string what,
            Func<SpectatorNotice.Progressed, bool> done)
        {
            if (Latest is { } latest && done(latest)) return latest;
            SpectatorNotice.Progressed? reached = null;
            await PumpUntil(watch, what, notice =>
            {
                if (notice is SpectatorNotice.Progressed progress && done(progress)) reached = progress;
                return reached is not null;
            });
            return reached!;
        }

        private async Task PumpUntil(
            MultiplayerSpectatorWatch watch,
            string what,
            Func<SpectatorNotice, bool> done)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                while (watch.TryDequeueNotice(out var notice))
                {
                    Check(notice, what);
                    if (done(notice)) return;
                }
                await Task.Delay(50);
            }
            throw new TimeoutException($"{name} never reached {what}");
        }

        private void Check(SpectatorNotice notice, string what)
        {
            switch (notice)
            {
                case SpectatorNotice.Progressed progress:
                    Program.Require(progress.View.ReleasedTurn <= Bound,
                        $"{name} was told turn {progress.View.ReleasedTurn} is released, at most {Bound} can be");
                    Program.Require(progress.ShownTurn is null || progress.ShownTurn <= Bound,
                        $"{name} showed turn {progress.ShownTurn}, at most {Bound} is released");
                    Program.Require(progress.HasState == progress.ShownTurn is not null,
                        $"{name} reported a state without a turn, or a turn without a state");
                    if (progress.State is { } state)
                    {
                        Program.Require(state.Coordinator.Turn == progress.ShownTurn + 1,
                            $"{name}'s city plans turn {state.Coordinator.Turn} while showing {progress.ShownTurn}");
                        _state = state;
                    }
                    Program.Require(!progress.HasState || _state is not null,
                        $"{name} said it has a state and never handed one over");
                    Latest = progress;
                    return;
                case SpectatorNotice.ConnectionChanged { IsConnected: false } outage:
                    Console.WriteLine($"spectators: {name} lost the server while waiting for {what}: {outage.Detail}");
                    return;
                case SpectatorNotice.Ended ended when what != "ending":
                    throw new InvalidOperationException(
                        $"{name} ended while waiting for {what}: {ended.Reason} ({ended.Error})");
            }
        }
    }
}
