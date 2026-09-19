using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

/// <summary>
/// Which open absence vote, if any, is put to the player at this client.
/// </summary>
public static class TakeoverVotePolicy
{
    /// <summary>
    /// The absent seat this client is asked about, of the ones with an open vote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Never this client's own seat. A player who lets one timed turn pass without sending anything
    /// is marked absent until their client reports that turn, and the server asks the whole match
    /// about them — the seat it is about included. Put to them, that was a modal naming the player
    /// reading it, offering two buttons the server refuses from a seat that is not active, and
    /// swallowing every click behind it at the moment they were trying to get back to planning.
    /// </para>
    /// <para>
    /// Of the rest, the oldest absence first, and the player id to break a tie: the question the
    /// match has been held up by longest is the one worth answering, and every client has to be
    /// looking at the same one for the tally on it to mean anything.
    /// </para>
    /// </remarks>
    public static string? SeatToVoteOn(
        IEnumerable<(string PlayerId, int Turn)> openVotes,
        string selfPlayerId)
    {
        ArgumentNullException.ThrowIfNull(openVotes);
        return openVotes
            .Where(vote => !string.Equals(vote.PlayerId, selfPlayerId, StringComparison.Ordinal))
            .OrderBy(vote => vote.Turn)
            .ThenBy(vote => vote.PlayerId, StringComparer.Ordinal)
            .Select(vote => vote.PlayerId)
            .FirstOrDefault();
    }
}

/// <summary>
/// The absence vote: what the present players are asked about a seat that missed a deadline.
/// </summary>
/// <remarks>
/// The question is only ever put about somebody else. A player whose own seat is being voted on is
/// told so on the turn status line instead — see <see cref="MultiplayerUiState.CurrentTakeoverVote"/>
/// for why a modal naming the player reading it was worse than useless.
/// </remarks>
public sealed partial class ChaosGame
{
    private static readonly Rectangle TakeoverVoteWait = new(164, 300, 140, 32);
    private static readonly Rectangle TakeoverVoteComputer = new(336, 300, 140, 32);

    /// <summary>
    /// Whether an absence vote is in front of the player, and so owns their input.
    /// </summary>
    /// <remarks>
    /// The modal covers the city and answers for every click behind it, so the keyboard goes the
    /// same way: without this the player would keep giving orders to a city they cannot see. The
    /// escape menu is deliberately still reachable — a player who wants to leave a match that is
    /// waiting on somebody else should not have to wait for the vote to close first.
    /// <para>
    /// It owns the keyboard on whatever screen the player is on, which is only safe because a
    /// finished match has no open vote to be asked about: see
    /// <see cref="MultiplayerUiState.ConcludeMatch"/>. Were one left standing, this would swallow
    /// the endgame's own keys as well as its clicks, and the player would have no way forward from
    /// the endgame at all.
    /// </para>
    /// </remarks>
    private bool TakeoverVoteBlocksInput =>
        _session is not null && _online.CurrentTakeoverVote is not null;

    private bool HandleTakeoverVoteClick(Point point)
    {
        if (_online.CurrentTakeoverVote is not { } vote || _session is null) return false;
        TakeoverChoice? choice = point switch
        {
            _ when TakeoverVoteWait.Contains(point) => TakeoverChoice.Wait,
            _ when TakeoverVoteComputer.Contains(point) => TakeoverChoice.Computer,
            _ => null,
        };
        if (choice is { } selected)
        {
            Forget(
                _session.VoteOnTakeoverAsync(vote.PlayerId, selected),
                "multiplayer.takeover-vote.failed");
            _message = selected == TakeoverChoice.Wait
                ? "VOTED TO WAIT FOR THE PLAYER"
                : "VOTED TO USE COMPUTER CONTROL";
        }
        return true;
    }

    private void DrawTakeoverVote(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_gameMenuOpen || _online.CurrentTakeoverVote is not { } vote || _session is null) return;
        var panel = new Rectangle(120, 154, 400, 198);
        batch.Draw(pixel, panel, new Color(6, 12, 12, 248));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "PLAYER ABSENT", 174, Color.Gold, 2);
        DrawCentered(font, batch, vote.DisplayName.ToUpperInvariant(), 212, Color.White, 1);
        DrawCentered(font, batch, $"MISSED TURN {vote.Turn}", 232, new Color(150, 165, 165), 1);
        var eligible = _online.Match?.Players.Count(player => player.Status == WirePlayerStatus.Active) ?? 0;
        var approvals = vote.Votes.Count(entry => entry.Value == TakeoverChoice.Computer);
        DrawCentered(font, batch, $"AI APPROVALS {approvals}/{eligible}  UNANIMOUS REQUIRED", 256,
            new Color(150, 165, 165), 1);
        DrawButton(batch, pixel, font, TakeoverVoteWait, "WAIT", true);
        DrawButton(batch, pixel, font, TakeoverVoteComputer, "USE AI", true);
    }
}
