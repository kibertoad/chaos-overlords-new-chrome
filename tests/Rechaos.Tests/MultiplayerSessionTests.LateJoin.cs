using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// A late joiner taking a seat the vote handed to the computer, which leaves the seat's slot with
/// more than one roster row. See the 2026-10-06 late-join decision.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    [Fact]
    public async Task ALateJoinerTakesASeatTheVoteHandedToTheComputer()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        server.Events.Write(Frame(8, "match.playerTakenOver", """{"playerId":"p2"}"""));
        await WaitFor<MultiplayerNotice.TakeoverVoteClosed>(session, seen);
        Assert.Equal(PlayerController.Computer, session.ControllerOfSlot(1));
        server.Events.Write(Frame(9, "match.latePlayerJoined", """{"playerId":"late-2","slot":1}"""));
        await WaitFor<MultiplayerNotice.MatchUpdated>(session, seen);

        Assert.Equal(PlayerController.Human, session.ControllerOfSlot(1));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    [Fact]
    public async Task ALateJoinIntoASeatAHumanStillPlaysEndsTheSession()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        server.Events.Write(Frame(8, "match.latePlayerJoined", """{"playerId":"late-2","slot":1}"""));
        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("human-owned seat", failed.Error?.Message ?? failed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCityIsGeneratedFromTheFirstHolderOfARetakenSeat()
    {
        IReadOnlyList<PlayerView> roster =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Computer, IsHost: false, ComlinkKey: null),
            new("late-2", 1, "DAVE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false, ComlinkKey: null),
        ];

        var setup = MatchBootstrapFactory.Setup(Seed, GameSettings, roster);

        Assert.Equal("GRACE", setup.Players[1].Name);
        Assert.Equal(PlayerController.Human, setup.Players[1].Controller);
    }

    [Fact]
    public async Task ARosterWithARetakenSeatResumes()
    {
        IReadOnlyList<PlayerView> roster =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Computer, IsHost: false, ComlinkKey: null),
            new("late-2", 1, "DAVE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false, ComlinkKey: null),
        ];
        var (session, _, http) = Running(ownPlayerId: "late-2", matchView: View() with { Players = roster });
        using var __ = http;
        await using var ___ = session;

        Assert.True(session.IsRestoring);
    }

    [Fact]
    public void ARosterWithTwoHumansInOneSeatIsRefused()
    {
        IReadOnlyList<PlayerView> roster =
        [
            Roster[0],
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Left, IsHost: false, ComlinkKey: null),
            new("late-2", 1, "DAVE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false, ComlinkKey: null),
        ];

        var refused = Assert.Throws<MultiplayerProtocolException>(() =>
            Running(matchView: View() with { Players = roster }));

        Assert.Contains("duplicate or missing seats", refused.Message, StringComparison.Ordinal);
    }
}
