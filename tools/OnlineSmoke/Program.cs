using System.Globalization;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using CoreTarget = Rechaos.Core.GameModel.CommandTarget;

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
        using var http = new HttpClient();
        var anonymous = new MultiplayerClient(http, new MultiplayerClientOptions(baseAddress));
        var definitions = BundledOriginalData.Load();

        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
        var host = await anonymous.CreateMatchAsync(
            new CreateMatchRequest(
                new MatchSettings("SMOKE CITY", 2, 0, MatchVisibility.Private, settings.ToWire()),
                "ADA",
                Password: null),
            CancellationToken.None);
        Console.WriteLine($"hosted {host.Match.Id} with code {host.JoinCode}");

        var guest = await anonymous.JoinAsync(
            new JoinMatchRequest(host.JoinCode, "GRACE", Password: null), CancellationToken.None);
        Console.WriteLine($"joined as {guest.Player.Id}");

        var hostMatch = anonymous.WithToken(host.Token).Match(host.Match.Id);
        var guestMatch = anonymous.WithToken(guest.Token).Match(host.Match.Id);
        await hostMatch.StartAsync(CancellationToken.None);

        var started = (await hostMatch.GetAsync(CancellationToken.None)).Match;
        Console.WriteLine($"started on seed {started.Seed}, {started.Players.Count} seated");

        await using var hostSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            hostMatch, definitions, started, host.Player.Id, started.LastEventSeq));
        await using var guestSession = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            guestMatch, definitions, started, guest.Player.Id, started.LastEventSeq));

        var hostState = hostSession.InitialState;
        var guestState = guestSession.InitialState;
        Require(
            MatchStateHasher.ComputeSha256(hostState) == MatchStateHasher.ComputeSha256(guestState),
            "the two clients bootstrapped different cities");
        Console.WriteLine($"bootstrapped {MatchStateHasher.ComputeSha256(hostState)[..12]}");

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

        var final = (await hostMatch.GetAsync(CancellationToken.None)).Match;
        Require(final.Status == MatchStatus.Running, $"the match ended as {final.Status}");
        Require(final.CurrentTurn == turns + 1, $"the server is on turn {final.CurrentTurn}");
        Console.WriteLine($"OK: {turns} turns in lockstep, server on turn {final.CurrentTurn}");
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
                        throw new InvalidOperationException($"desynced on turn {desynced.Turn}");
                    default:
                        break;
                }
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"turn {turn} never resolved");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
