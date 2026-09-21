using System.Globalization;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using CoreTarget = Rechaos.Core.GameModel.CommandTarget;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.OnlineSmoke;

/// <summary>
/// Plays a short online match against a running coordination server, with two clients in one
/// process, and fails loudly if they ever disagree.
/// </summary>
/// <remarks>
/// <para>
/// The unit tests prove the two halves separately: that this client reproduces the server's digests,
/// and that two independent clients applying the same sealed set reach the same hash. Only this
/// proves them together — that the documents this client sends are the documents the server seals,
/// that the events it sends back are ones this client can read, and that the hashes it is told to
/// agree on are the ones this client computes.
/// </para>
/// <para>
/// It is a tool and not a test because it needs a server: the .NET suite has no Node in it.
/// </para>
/// </remarks>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var baseAddress = new Uri(args.Length > 0 ? args[0] : "http://localhost:8787");
        var turns = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 3;
        using var http = MultiplayerClientOptions.CreateHttpClient();
        var anonymous = new MultiplayerClient(http, new MultiplayerClientOptions(baseAddress));
        var definitions = BundledOriginalData.Load();

        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
        var host = await anonymous.CreateMatchAsync(
            new CreateMatchRequest(
                // Public, so the browse below has something to find. Discovery is the one part of
                // the lobby flow the two-client match cannot exercise from a join code.
                new MatchSettings("SMOKE CITY", 2, 0, MatchVisibility.Public, settings.ToWire()),
                "ADA",
                HostPortraitId: 0,
                Password: null,
                MultiplayerProtocolVersion.Current,
                MultiplayerSessionVersion.Current),
            CancellationToken.None);
        Console.WriteLine($"hosted {host.Match.Id} with code {host.JoinCode}");

        var listing = (await anonymous.ListLobbiesAsync(CancellationToken.None))
            .Matches.FirstOrDefault(entry => entry.Id == host.Match.Id);
        Require(listing is not null, "the public lobby did not come back from browsing");
        var browsed = MultiplayerGameSettings.FromWire(listing!.Settings.GameSettings);
        Require(
            browsed.Scenario == settings.Scenario && browsed.AiMentality == settings.AiMentality,
            "browsing returned game settings this client read back as something else");
        Console.WriteLine(
            $"browsed {listing.Name} ({listing.PlayerCount}/{listing.MaxPlayers}, "
            + $"{listing.Status})");

        var guest = await anonymous.JoinAsync(
            new JoinMatchRequest(host.JoinCode, "GRACE", PortraitId: 1, Password: null),
            CancellationToken.None);
        Console.WriteLine($"joined as {guest.Player.Id}");

        var hostMatch = anonymous.WithToken(host.Token).Match(host.Match.Id);
        var guestMatch = anonymous.WithToken(guest.Token).Match(host.Match.Id);
        await hostMatch.StartAsync(CancellationToken.None);

        var started = (await hostMatch.GetAsync(CancellationToken.None)).Match;
        Console.WriteLine($"started on seed {started.Seed}, {started.Players.Count} seated");

        // A bounded outage budget, unlike the game's. Nobody is watching this run, so a server
        // that stops answering has to end it rather than keep it waiting; see
        // `MultiplayerSessionOptions.StreamOutageBudget`.
        var smokeBudget = TimeSpan.FromMinutes(2);
        await using var hostSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            hostMatch, definitions, started, host.Player.Id, started.LastEventSeq,
            StreamOutageBudget: smokeBudget));
        await using var guestSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            guestMatch, definitions, started, guest.Player.Id, started.LastEventSeq,
            StreamOutageBudget: smokeBudget));

        var hostState = hostSession.Bootstrap.State;
        var guestState = guestSession.Bootstrap.State;
        Require(
            MatchStateHasher.ComputeFingerprint(hostState) == MatchStateHasher.ComputeFingerprint(guestState),
            "the two clients bootstrapped different cities");
        Console.WriteLine($"bootstrapped {MatchStateHasher.ComputeFingerprint(hostState)[..12]}");

        for (var turn = 1; turn <= turns; turn++)
        {
            var hostTurn = SpeculativeTurn.For(hostState, definitions, hostSession.Slot);
            var guestTurn = SpeculativeTurn.For(guestState, definitions, guestSession.Slot);
            Hide(hostTurn);
            Hide(guestTurn);

            await hostSession.SubmitOrdersAsync(turn, hostTurn.Build(), true, CancellationToken.None);
            await guestSession.SubmitOrdersAsync(turn, guestTurn.Build(), true, CancellationToken.None);

            var hostResolved = await Resolved(hostSession, turn);
            var guestResolved = await Resolved(guestSession, turn);
            Require(
                hostResolved.StateHash == guestResolved.StateHash,
                $"turn {turn} resolved differently: {hostResolved.StateHash} vs {guestResolved.StateHash}");
            hostState = hostResolved.State;
            guestState = guestResolved.State;
            Console.WriteLine($"turn {turn} confirmed on {hostResolved.StateHash[..12]}");
        }

        // Exercise recovery through the same production sessions the game UI owns. The guest is
        // unanimously replaced, everybody leaves, and the old guest's durable membership then
        // reclaims both the AI seat and the vacant host role.
        await guestMatch.LeaveAsync(CancellationToken.None);
        await WaitForPlayer(guestMatch, guest.Player.Id, WirePlayerStatus.Left);
        await hostMatch.VoteOnTakeoverAsync(
            guest.Player.Id,
            new TakeoverVoteRequest(TakeoverVoteRequestDecision.Computer),
            CancellationToken.None);
        await WaitForPlayer(hostMatch, guest.Player.Id, WirePlayerStatus.Computer);
        await hostMatch.LeaveAsync(CancellationToken.None);
        await WaitForPlayer(hostMatch, host.Player.Id, WirePlayerStatus.Left);

        await using var recovery = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(baseAddress));
        recovery.Resume(host.Match.Id, guest.Player.Id, guest.Token, host.JoinCode);
        var reseated = await Seated(recovery);
        Require(reseated.Membership.Player.Status == WirePlayerStatus.Active,
            "the returning player's seat was not made active");
        Require(reseated.Membership.Player.IsHost,
            "the first player returning to an empty match did not become host");
        Console.WriteLine("rejoined the AI-held seat and inherited host control");

        var recoveryTurn = turns + 1;
        var recoveredPlan = SpeculativeTurn.For(guestState, definitions, guestSession.Slot);
        Hide(recoveredPlan);
        await guestSession.SubmitOrdersAsync(
            recoveryTurn, recoveredPlan.Build(), true, CancellationToken.None);
        var recoveredGuest = await Resolved(guestSession, recoveryTurn);
        Require(recoveredGuest.State.Players[guestSession.Slot].Setup.Controller == PlayerController.Human,
            "the rejoined seat stayed under computer control in the actual game state");

        var final = (await hostMatch.GetAsync(CancellationToken.None)).Match;
        Require(final.Status == MatchStatus.Running, $"the match ended as {final.Status}");
        Require(final.CurrentTurn == turns + 2, $"the server is on turn {final.CurrentTurn}");
        Require(final.HostPlayerId == guest.Player.Id, "the recovered host was not persisted");
        Console.WriteLine($"OK: {turns + 1} turns in lockstep, including crash recovery");
        return 0;
    }

    /// <summary>One legal order per turn, so the documents are not all empty.</summary>
    private static void Hide(SpeculativeTurn turn)
    {
        var player = turn.State.FindPlayer(turn.Player)!;
        var gang = player.Gangs.FirstOrDefault(candidate => candidate.IsActive);
        if (gang is null) return;
        turn.Submit(new GameCommand(turn.Player, gang.Id, GangAction.Hide, CoreTarget.None));
    }

    /// <summary>Waits for the session to report the turn resolved, or gives up saying why.</summary>
    private static async Task<MultiplayerNotice.TurnResolved> Resolved(
        MultiplayerMatchSession session,
        int turn)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            while (session.TryDequeueNotice(out var notice))
            {
                switch (notice)
                {
                    case MultiplayerNotice.TurnResolved resolved when resolved.Turn == turn:
                        return resolved;
                    case MultiplayerNotice.Failed failed:
                        throw new InvalidOperationException(
                            $"the session failed: {failed.Reason} ({failed.Error})");
                    case MultiplayerNotice.Desynced desynced:
                        throw new InvalidOperationException(
                            $"desynced on turn {desynced.Turn}: {desynced.Details}");
                    default:
                        break;
                }
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"turn {turn} never resolved");
    }

    private static async Task WaitForPlayer(
        MatchHandle match,
        string playerId,
        WirePlayerStatus status)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var detail = await match.GetAsync(CancellationToken.None);
            if (detail.Match.Players.First(player => player.Id == playerId).Status == status) return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"player {playerId} never became {status}");
    }

    private static async Task<LobbyNotice.Seated> Seated(MultiplayerLobbySession lobby)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            while (lobby.TryDequeueNotice(out var notice))
            {
                if (notice is LobbyNotice.Seated seated) return seated;
                if (notice is LobbyNotice.Failed failed)
                    throw new InvalidOperationException($"rejoin failed: {failed.Reason}");
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("the returning membership was never seated");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
