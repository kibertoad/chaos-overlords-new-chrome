using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

/// <summary>
/// Which open removal vote, if any, is put to the player at this client, and how one is counted.
/// </summary>
/// <remarks>
/// The server removes a seat once every other active player's latest choice is <c>remove</c>
/// (docs/DECISIONS.md, "Let the other players remove a seat by unanimous vote").
/// </remarks>
public static class RemovalVotePolicy
{
    /// <summary>No seat kept this turn.</summary>
    public static readonly IReadOnlySet<string> NoSeatsKept = new HashSet<string>();

    /// <summary>
    /// The seat this client is asked about: one with an open vote that is not its own and that it
    /// has not answered yet, the lowest player id first so every client asks in the same order.
    /// </summary>
    /// <remarks>
    /// A vote the player has answered is not asked again. The vote stays open while anybody holds
    /// <c>remove</c>, so a player who chose to keep the seat would otherwise sit behind a modal for
    /// as long as somebody else disagreed with them. A seat the player chose to keep earlier in the
    /// same turn is not asked about either, even under a vote that closed and opened again since.
    /// The vote does not stop the clock, so without this one player could withdraw and propose
    /// again and again, and put a modal that owns the input in front of everybody else each time,
    /// while their planning time ran out. The open vote still shows in the players panel.
    /// </remarks>
    public static string? SeatToVoteOn(
        IEnumerable<(string PlayerId, IReadOnlyDictionary<string, RemovalChoice> Votes)> openVotes,
        string selfPlayerId,
        IReadOnlySet<string>? keptThisTurn = null)
    {
        ArgumentNullException.ThrowIfNull(openVotes);
        return openVotes
            .Where(vote => !string.Equals(vote.PlayerId, selfPlayerId, StringComparison.Ordinal)
                && !vote.Votes.ContainsKey(selfPlayerId)
                && keptThisTurn?.Contains(vote.PlayerId) != true)
            .OrderBy(vote => vote.PlayerId, StringComparer.Ordinal)
            .Select(vote => vote.PlayerId)
            .FirstOrDefault();
    }

    /// <summary>
    /// Approvals and the number of players whose approval the removal needs: every active player
    /// but the seat itself, as the server counts them.
    /// </summary>
    public static (int Approvals, int Required) Tally(
        IEnumerable<PlayerView> roster,
        string targetPlayerId,
        IReadOnlyDictionary<string, RemovalChoice> votes)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(votes);
        var voters = roster
            .Where(player => player.Status == WirePlayerStatus.Active
                && !string.Equals(player.Id, targetPlayerId, StringComparison.Ordinal))
            .ToList();
        var approvals = voters.Count(voter =>
            votes.TryGetValue(voter.Id, out var choice) && choice == RemovalChoice.Remove);
        return (approvals, voters.Count);
    }

    /// <summary>
    /// Whether a seat can be put to a removal vote: a human seat that has not been removed already.
    /// A seat handed to the computer still can be, since its owner could otherwise come back to it.
    /// </summary>
    public static bool IsRemovable(PlayerView player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Status is WirePlayerStatus.Active or WirePlayerStatus.TakeoverPending
            or WirePlayerStatus.Left or WirePlayerStatus.Computer;
    }
}

/// <summary>
/// Removing a player by vote: the roster the game menu opens to start one, and the modal that puts
/// an open one to everybody else.
/// </summary>
/// <remarks>
/// Only the host can kick, and the game never offered that either, so a host who never readied an
/// untimed turn held the match for as long as they liked, and nobody could remove a player who
/// desynced every turn. The vote is the same for every player, the host included.
/// </remarks>
public sealed partial class ChaosGame
{
    private static readonly Rectangle RemovalVoteKeep = new(164, 300, 140, 32);
    private static readonly Rectangle RemovalVoteRemove = new(336, 300, 140, 32);
    private static readonly Rectangle PlayersPanel = new(100, 68, 440, 330);
    private static readonly Rectangle PlayersPanelBack = new(266, 352, 108, 34);

    /// <summary>The most other seats a roster can hold: six players, less this one.</summary>
    private const int PlayersPanelRows = 5;

    private bool _playersPanelOpen;
    private int _playersPanelCursor;

    private static Rectangle PlayersPanelRow(int index) => new(116, 112 + index * 46, 408, 40);

    private static Rectangle PlayersPanelButton(int index) =>
        new(400, 117 + index * 46, 116, 30);

    /// <summary>
    /// Whether a removal vote is in front of the player, and so owns their input. An absence vote
    /// goes first: it pauses the clock, and the two never share the screen.
    /// </summary>
    private bool RemovalVoteBlocksInput =>
        _session is not null && ShownRemovalVote is not null;

    /// <summary>The removal vote drawn as a modal, when no absence vote is drawn instead.</summary>
    private RemovalVotePrompt? ShownRemovalVote =>
        _online.CurrentTakeoverVote is null && SelfIsActive ? _online.CurrentRemovalVote : null;

    private bool SelfIsActive =>
        _online.Match?.Players.Any(player => player.Id == _online.SelfPlayerId
            && player.Status == WirePlayerStatus.Active) == true;

    private bool HandleRemovalVoteClick(Point point)
    {
        if (ShownRemovalVote is not { } vote || _session is null) return false;
        RemovalChoice? choice = point switch
        {
            _ when RemovalVoteKeep.Contains(point) => RemovalChoice.Keep,
            _ when RemovalVoteRemove.Contains(point) => RemovalChoice.Remove,
            _ => null,
        };
        if (choice is { } selected) VoteOnRemoval(vote.PlayerId, vote.DisplayName, selected);
        return true;
    }

    private void VoteOnRemoval(string playerId, string displayName, RemovalChoice choice)
    {
        if (_session is null) return;
        Forget(_session.VoteOnRemovalAsync(playerId, choice), "multiplayer.removal-vote.faulted");
        _message = choice == RemovalChoice.Remove
            ? $"VOTED TO REMOVE {displayName.ToUpperInvariant()}"
            : $"VOTED TO KEEP {displayName.ToUpperInvariant()}";
    }

    private void DrawRemovalVote(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_gameMenuOpen || _session is null || ShownRemovalVote is not { } vote) return;
        var panel = new Rectangle(120, 154, 400, 198);
        batch.Draw(pixel, panel, new Color(6, 12, 12, 248));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "REMOVE PLAYER?", 174, Color.Gold, 2);
        DrawCentered(font, batch, vote.DisplayName.ToUpperInvariant(), 212, Color.White, 1);
        DrawCentered(font, batch, "A REMOVED PLAYER CANNOT RETURN", 232, new Color(150, 165, 165), 1);
        var (approvals, required) = RemovalVotePolicy.Tally(
            _online.Match?.Players ?? [], vote.PlayerId, vote.Votes);
        DrawCentered(font, batch, $"APPROVALS {approvals}/{required}  UNANIMOUS REQUIRED", 256,
            new Color(150, 165, 165), 1);
        DrawButton(batch, pixel, font, RemovalVoteKeep, "KEEP", true);
        DrawButton(batch, pixel, font, RemovalVoteRemove, "REMOVE", true);
    }

    /// <summary>The other seats of the match a removal vote can name, in seat order.</summary>
    private List<PlayerView> RemovablePlayers() =>
        (_online.Match?.Players ?? [])
            .Where(player => player.Id != _online.SelfPlayerId && RemovalVotePolicy.IsRemovable(player))
            .OrderBy(player => player.Slot)
            .Take(PlayersPanelRows)
            .ToList();

    private void OpenPlayersPanel()
    {
        if (_session is null) return;
        _playersPanelOpen = true;
        _playersPanelCursor = 0;
        _message = string.Empty;
    }

    private void ClosePlayersPanel()
    {
        _playersPanelOpen = false;
        _gameMenuCursor = Math.Max(0, Array.FindIndex(GameMenuButtons(),
            button => button.Action == GameMenuAction.Players));
        _message = string.Empty;
    }

    /// <summary>What the row's button does: approve a removal, or withdraw this player's approval.</summary>
    private RemovalChoice PlayersPanelChoice(PlayerView player) =>
        _online.RemovalVotes.TryGetValue(player.Id, out var vote)
            && vote.Votes.TryGetValue(_online.SelfPlayerId, out var own)
            && own == RemovalChoice.Remove
                ? RemovalChoice.Keep
                : RemovalChoice.Remove;

    private bool PlayersPanelVotingOpen =>
        SelfIsActive && _online.Stage is not MultiplayerStage.Finished;

    private void ActivatePlayersPanelRow(int index)
    {
        var players = RemovablePlayers();
        if (index >= players.Count)
        {
            ClosePlayersPanel();
            return;
        }
        if (!PlayersPanelVotingOpen)
        {
            _message = "ONLY A PLAYER IN THE MATCH CAN VOTE";
            return;
        }
        var player = players[index];
        VoteOnRemoval(player.Id, player.DisplayName, PlayersPanelChoice(player));
    }

    private void UpdatePlayersPanel(KeyboardState keyboard)
    {
        var last = RemovablePlayers().Count;
        if (Pressed(keyboard, Keys.Up)) _playersPanelCursor = Math.Max(0, _playersPanelCursor - 1);
        if (Pressed(keyboard, Keys.Down)) _playersPanelCursor = Math.Min(last, _playersPanelCursor + 1);
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)) ClosePlayersPanel();
        else if (Pressed(keyboard, Keys.Enter)) ActivatePlayersPanelRow(_playersPanelCursor);
    }

    private void HandlePlayersPanelClick(Point point)
    {
        if (PlayersPanelBack.Contains(point))
        {
            ClosePlayersPanel();
            return;
        }
        var count = RemovablePlayers().Count;
        for (var index = 0; index < count; index++)
        {
            if (!PlayersPanelButton(index).Contains(point)) continue;
            _playersPanelCursor = index;
            ActivatePlayersPanelRow(index);
            return;
        }
    }

    private void DrawPlayersPanel(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        batch.Draw(pixel, PlayersPanel, new Color(12, 20, 20, 250));
        DrawBorder(batch, pixel, PlayersPanel, Color.Gold, 2);
        DrawCentered(font, batch, "PLAYERS", 82, Color.Gold, 2);
        var players = RemovablePlayers();
        if (players.Count == 0)
            DrawCentered(font, batch, "NO OTHER PLAYERS", 130, Color.White, 1);
        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            var row = PlayersPanelRow(index);
            batch.Draw(pixel, row, index == _playersPanelCursor
                ? new Color(72, 54, 18, 235) : new Color(24, 37, 39, 235));
            DrawBorder(batch, pixel, row, index == _playersPanelCursor ? Color.Gold : Color.Gray, 1);
            font.Draw(batch, player.DisplayName.ToUpperInvariant(),
                new Vector2(row.X + 7, row.Y + 5), Color.White, 1);
            font.Draw(batch, PlayersPanelDetail(player),
                new Vector2(row.X + 7, row.Y + 22), Color.LightGray, 1);
            if (PlayersPanelVotingOpen)
            {
                DrawButton(batch, pixel, font, PlayersPanelButton(index),
                    PlayersPanelChoice(player) == RemovalChoice.Remove ? "REMOVE" : "WITHDRAW",
                    index == _playersPanelCursor);
            }
        }
        DrawButton(batch, pixel, font, PlayersPanelBack, "BACK", _playersPanelCursor >= players.Count);
        var footer = string.IsNullOrEmpty(_message)
            ? "REMOVING A PLAYER TAKES EVERY OTHER PLAYER"
            : _message;
        // Below the fifth row, which ends at y 336.
        DrawCentered(font, batch, footer, 340, Color.White, 1);
    }

    /// <summary>The second line of a roster row: the host mark, the seat's state and an open vote.</summary>
    private string PlayersPanelDetail(PlayerView player)
    {
        var parts = new List<string>(3);
        if (player.IsHost) parts.Add("HOST");
        parts.Add(player.Status switch
        {
            WirePlayerStatus.Active => "PLAYING",
            WirePlayerStatus.TakeoverPending => "MISSED A TURN",
            WirePlayerStatus.Left => "LEFT",
            WirePlayerStatus.Computer => "COMPUTER",
            _ => string.Empty,
        });
        if (_online.RemovalVotes.TryGetValue(player.Id, out var vote))
        {
            var (approvals, required) = RemovalVotePolicy.Tally(
                _online.Match?.Players ?? [], player.Id, vote.Votes);
            parts.Add($"REMOVAL {approvals}/{required}");
        }
        return string.Join("  ", parts.Where(part => part.Length > 0));
    }
}
