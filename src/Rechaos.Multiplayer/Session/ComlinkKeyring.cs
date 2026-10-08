using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using PlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// This client's Comlink keys in one online match: its own key pair, and the public key each other
/// seat has published.
/// </summary>
/// <remarks>
/// <para>
/// A message is sealed for each recipient before it goes into the order document, so the sealed
/// set every client receives, and every inbox every client hashes, carries envelopes only. Only the
/// recipient's client can open its envelope, and it does so here when the interface shows the
/// message; nothing it opens is written back into the match.
/// </para>
/// <para>
/// The directory is filled from the match view's roster and from <c>match.comlinkKeyPublished</c>,
/// and read on the game thread when the player sends, so every access takes the lock. A seat is
/// found by its slot: the row a human holds it under now, since RULE-COMLINK-002 lets only a human
/// take a message.
/// </para>
/// </remarks>
public sealed class ComlinkKeyring
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Seat> _seats = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Envelope, int Turn, PlayerId Sender), string?> _opened = new();

    /// <summary>A keyring for the seat at <paramref name="self"/> in <paramref name="matchId"/>.</summary>
    public ComlinkKeyring(string matchId, PlayerId self, ComlinkKeyPair own)
    {
        ArgumentException.ThrowIfNullOrEmpty(matchId);
        ArgumentNullException.ThrowIfNull(own);
        MatchId = matchId;
        Self = self;
        Own = own;
    }

    /// <summary>The match every envelope here is bound to.</summary>
    public string MatchId { get; }

    /// <summary>The seat this client plays, whose inbox is the only one it can read.</summary>
    public PlayerId Self { get; }

    /// <summary>This client's own key pair.</summary>
    public ComlinkKeyPair Own { get; }

    /// <summary>Takes every seat's published key from a roster the server answered with.</summary>
    public void Learn(IEnumerable<PlayerView> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        lock (_gate)
        {
            foreach (var player in players)
            {
                _seats.TryGetValue(player.Id, out var known);
                // A key already held was checked when it was learned; only a new one is imported.
                var key = string.Equals(player.ComlinkKey, known?.Key, StringComparison.Ordinal)
                    || ComlinkKeyPair.IsPublicKey(player.ComlinkKey)
                    ? player.ComlinkKey
                    : known?.Key;
                _seats[player.Id] = new Seat(player.Slot, player.Status, key);
            }
        }
    }

    /// <summary>Takes a key a player published since the roster was read.</summary>
    public void Learn(string playerId, string publicKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        if (!ComlinkKeyPair.IsPublicKey(publicKey)) return;
        lock (_gate)
        {
            _seats[playerId] = _seats.TryGetValue(playerId, out var known)
                ? known with { Key = publicKey }
                : new Seat(Slot: -1, PlayerStatus.Active, publicKey);
        }
    }

    /// <summary>The key messages to <paramref name="seat"/> are sealed to, or null when it has none.</summary>
    public string? KeyFor(PlayerId seat)
    {
        lock (_gate)
        {
            string? departed = null;
            foreach (var candidate in _seats.Values)
            {
                if (candidate.Slot != seat.Value) continue;
                // The row a human holds the seat under now decides, even before it has published a
                // key: sealing to a row that left the seat would hand the message to its former
                // holder and leave the current one a message it cannot open.
                if (candidate.Status is PlayerStatus.Active or PlayerStatus.TakeoverPending)
                    return candidate.Key;
                departed ??= candidate.Key;
            }
            return departed;
        }
    }

    /// <summary>
    /// One letter per recipient, each sealed to that seat's key, or null when some recipient has
    /// published none.
    /// </summary>
    public IReadOnlyList<SealedComlinkLetter>? Seal(
        int turn,
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string text)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        var letters = new SealedComlinkLetter[recipients.Count];
        for (var index = 0; index < letters.Length; index++)
        {
            var recipient = recipients[index];
            if (KeyFor(recipient) is not { } key) return null;
            letters[index] = new SealedComlinkLetter(
                recipient,
                ComlinkSeal.Seal(text, key, new ComlinkSealContext(MatchId, turn, sender, recipient)));
        }
        return letters;
    }

    /// <summary>
    /// The text of a message in this client's own inbox, or null when it cannot be read here.
    /// </summary>
    /// <remarks>
    /// A message in the clear is its own text. A sealed one is opened with this client's key and
    /// remembered, opened or not, since the interface asks again every frame it draws the message.
    /// Null means the
    /// envelope was not sealed to this key: a message sent to this seat before its current player
    /// held it, or one a modified client sealed wrongly.
    /// </remarks>
    public string? Read(ComlinkMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Envelope is not { } envelope) return message.Text;
        // Keyed by where the envelope was opened as well, so an envelope copied into another
        // message is judged against that message's own turn and sender.
        var key = (envelope, message.Turn, message.Sender);
        lock (_gate)
        {
            if (_opened.TryGetValue(key, out var known)) return known;
        }
        var text = ComlinkSeal.Open(
            envelope, Own, new ComlinkSealContext(MatchId, message.Turn, message.Sender, Self));
        lock (_gate) _opened[key] = text;
        return text;
    }

    private sealed record Seat(int Slot, PlayerStatus Status, string? Key);
}
