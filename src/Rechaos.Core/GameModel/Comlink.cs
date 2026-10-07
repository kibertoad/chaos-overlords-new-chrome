namespace Rechaos.Core.GameModel;

/// <summary>One message in a player's inbox.</summary>
/// <remarks>
/// A hot-seat message carries its <see cref="Text"/>. An online message carries only the
/// <see cref="Envelope"/> sealed for its recipient, with an empty <see cref="Text"/>: every client
/// stores and hashes the envelope, and only the recipient's client can open it (see
/// docs/MULTIPLAYER.md, "Comlink privacy"). Exactly one of the two is set.
/// </remarks>
public sealed record ComlinkMessage(
    long Sequence,
    int Turn,
    PlayerId Sender,
    string Text,
    string? Envelope = null)
{
    /// <summary>Whether the text is sealed for the recipient rather than carried in the clear.</summary>
    /// <remarks>
    /// Left out of saves and snapshots: it follows from <see cref="Envelope"/>, and a stored copy
    /// would be read back and ignored.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSealed => Envelope is not null;
}

/// <summary>One recipient's copy of a sealed Comlink message.</summary>
/// <param name="Recipient">The seat the envelope is sealed for.</param>
/// <param name="Envelope">What only that seat's client can open; see <see cref="ComlinkEnvelope"/>.</param>
public sealed record SealedComlinkLetter(PlayerId Recipient, string Envelope);

/// <summary>
/// The shape of a sealed Comlink message, as the core holds it: opaque text of one fixed length.
/// </summary>
/// <remarks>
/// The core never opens an envelope and does not know how one is made; the online client seals and
/// opens them. What the core checks is that it is base64 of <see cref="Bytes"/> bytes, because the
/// envelope is stored in the inbox, hashed and saved, and every client has to agree on what it
/// accepted. Every envelope has the same length whatever the text, so the length says nothing about
/// the message.
/// </remarks>
public static class ComlinkEnvelope
{
    /// <summary>The bytes of an envelope: an ephemeral P-256 point, the padded text, the tag.</summary>
    public const int Bytes = 64 + MatchLimits.ComlinkMessageCharacters + 16;

    /// <summary>The base64 characters of an envelope. <see cref="Bytes"/> divides by three, so none is padding.</summary>
    public const int Characters = Bytes / 3 * 4;

    /// <summary>Whether <paramref name="envelope"/> is standard base64 of exactly <see cref="Bytes"/> bytes.</summary>
    public static bool IsWellFormed(string? envelope) =>
        envelope is { Length: Characters }
        && envelope.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
            or >= '0' and <= '9' or '+' or '/');
}

public enum ComlinkValidationCode : byte
{
    Accepted,
    SenderNotFound,
    SenderNotHuman,
    SenderNotActive,
    WrongPhase,
    NoRecipients,
    RecipientNotFound,
    RecipientNotHuman,
    SenderIsRecipient,
    DuplicateRecipient,
    EmptyMessage,
    MessageTooLong,
    RecipientNotActive,
    /// <summary>A sealed message whose envelope for some recipient is not one (see <see cref="ComlinkEnvelope"/>).</summary>
    MalformedEnvelope,
    /// <summary>
    /// The online client holds no key to seal the message to some recipient. The core never answers
    /// this; it is the client's refusal, in the same vocabulary as the core's.
    /// </summary>
    RecipientUnreachable
}

public static class ComlinkValidationMessages
{
    private static readonly IReadOnlyDictionary<ComlinkValidationCode, string> Messages =
        new Dictionary<ComlinkValidationCode, string>
        {
            [ComlinkValidationCode.Accepted] = "Message sent.",
            [ComlinkValidationCode.SenderNotFound] = "Sender is not in this match.",
            [ComlinkValidationCode.SenderNotHuman] = "Comlink is for human players.",
            [ComlinkValidationCode.SenderNotActive] = "Sender is not the active player.",
            [ComlinkValidationCode.WrongPhase] = "Comlink requires Command phase.",
            [ComlinkValidationCode.NoRecipients] = "Select at least one recipient.",
            [ComlinkValidationCode.RecipientNotFound] = "Recipient is not in this match.",
            [ComlinkValidationCode.RecipientNotHuman] = "Recipient must be human.",
            [ComlinkValidationCode.SenderIsRecipient] = "Cannot send Comlink to yourself.",
            [ComlinkValidationCode.DuplicateRecipient] = "Recipients must be unique.",
            [ComlinkValidationCode.EmptyMessage] = "Enter a message.",
            [ComlinkValidationCode.MessageTooLong] = "Message exceeds 160 characters.",
            [ComlinkValidationCode.RecipientNotActive] = "Recipient was eliminated.",
            [ComlinkValidationCode.MalformedEnvelope] = "Message is not sealed.",
            [ComlinkValidationCode.RecipientUnreachable] = "Recipient has no Comlink key."
        };

    public static string For(ComlinkValidationCode code) => Messages[code];
}

public sealed record ComlinkSendResult(
    bool Accepted,
    ComlinkValidationCode Code,
    IReadOnlyList<PlayerId> Recipients,
    string Message);

public sealed class ComlinkInbox
{
    private readonly Queue<ComlinkMessage> _messages = [];
    private readonly HashSet<long> _readSequences = [];
    private long _legacyReadThroughSequence = -1;

    public int Count => _messages.Count;
    public IReadOnlyList<ComlinkMessage> Messages => _messages.ToArray();
    public IReadOnlyList<long> ReadSequences => _readSequences.Order().ToArray();
    public long NextSequence { get; private set; }
    internal long LegacyReadThroughSequence => _legacyReadThroughSequence;
    public bool HasUnread => _messages.Any(message => !_readSequences.Contains(message.Sequence));

    internal static ComlinkInbox Restore(
        IReadOnlyList<ComlinkMessage> messages,
        long nextSequence,
        IReadOnlyList<long> readSequences,
        long? legacyReadThroughSequence = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(readSequences);
        var messageSequences = messages.Select(message => message.Sequence).ToHashSet();
        if (messages.Count > MatchLimits.ComlinkMessagesPerPlayer
            || nextSequence < 0
            || legacyReadThroughSequence is < -1
            || legacyReadThroughSequence >= nextSequence
            || readSequences.Count != readSequences.Distinct().Count()
            || readSequences.Any(sequence => !messageSequences.Contains(sequence))
            || messages.Where((message, index) =>
                message.Sequence != nextSequence - messages.Count + index
                || message.Turn < 1
                || message.Sender.Value is < 0 or >= MatchLimits.PlayerCount
                || !IsStorable(message)).Any()
            // RULE-COMLINK-007 drops read messages from the front, so an inbox can hold fewer
            // than it has received; what is left is still the newest run of messages.
            || messages.Count > Math.Min(nextSequence, MatchLimits.ComlinkMessagesPerPlayer))
            throw new ArgumentException("Restored Comlink inbox is invalid.", nameof(messages));

        var inbox = new ComlinkInbox
        {
            NextSequence = nextSequence,
            _legacyReadThroughSequence = legacyReadThroughSequence
                ?? (readSequences.Count == 0 ? -1 : readSequences.Max())
        };
        foreach (var message in messages) inbox._messages.Enqueue(message);
        foreach (var sequence in readSequences) inbox._readSequences.Add(sequence);
        return inbox;
    }

    /// <summary>
    /// A message as <see cref="Receive"/> or <see cref="ReceiveSealed"/> would have stored it: text
    /// in the clear that is not blank and fits the Send panel, or an empty text beside a
    /// well-formed envelope.
    /// </summary>
    private static bool IsStorable(ComlinkMessage message) =>
        message.Text is not null
        && (message.Envelope is null
            ? !string.IsNullOrWhiteSpace(message.Text)
                && message.Text.Length <= MatchLimits.ComlinkMessageCharacters
            : message.Text.Length == 0 && ComlinkEnvelope.IsWellFormed(message.Envelope));

    /// <summary>
    /// RULE-COMLINK-001: stores a message, dropping the oldest when the inbox holds 16. The
    /// original also moves the recipient's View cursor back one place when it drops a message. The
    /// sender is always the active player and never a recipient, and every other player's cursor
    /// was set to 0 when its planning ended (RULE-COMLINK-007), so that move changes nothing and
    /// the View keeps its page on the client instead.
    /// </summary>
    internal ComlinkMessage Receive(int turn, PlayerId sender, string text)
    {
        if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
        if (sender.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(sender));
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > MatchLimits.ComlinkMessageCharacters)
            throw new ArgumentException(
                $"A Comlink message cannot exceed {MatchLimits.ComlinkMessageCharacters} characters.",
                nameof(text));

        return Store(new ComlinkMessage(NextSequence++, turn, sender, text));
    }

    /// <summary>
    /// RULE-COMLINK-001 for a sealed message: stored, numbered and dropped exactly as
    /// <see cref="Receive"/> stores one in the clear, with the envelope in place of the text.
    /// </summary>
    internal ComlinkMessage ReceiveSealed(int turn, PlayerId sender, string envelope)
    {
        if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
        if (sender.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(sender));
        if (!ComlinkEnvelope.IsWellFormed(envelope))
            throw new ArgumentException("A sealed Comlink message needs a well-formed envelope.", nameof(envelope));
        return Store(new ComlinkMessage(NextSequence++, turn, sender, string.Empty, envelope));
    }

    private ComlinkMessage Store(ComlinkMessage message)
    {
        if (_messages.Count == MatchLimits.ComlinkMessagesPerPlayer)
            _readSequences.Remove(_messages.Dequeue().Sequence);
        _messages.Enqueue(message);
        return message;
    }

    public bool IsRead(long sequence) => _readSequences.Contains(sequence);

    /// <summary>
    /// FMT-STATE-005: empties every record, as the original does when a match is entered. The
    /// sequence keeps counting, so a message received later never takes an old message's number.
    /// </summary>
    internal bool Clear()
    {
        if (_messages.Count == 0) return false;
        _messages.Clear();
        _readSequences.Clear();
        return true;
    }

    /// <summary>
    /// RULE-COMLINK-007: removes the read messages at the front of the inbox, up to the first
    /// unread one, when the player's planning ends. Returns how many were removed.
    /// </summary>
    internal int DropLeadingRead()
    {
        var dropped = 0;
        while (_messages.TryPeek(out var message) && _readSequences.Remove(message.Sequence))
        {
            _messages.Dequeue();
            dropped++;
        }
        return dropped;
    }

    internal bool MarkRead(long sequence)
    {
        if (!_messages.Any(message => message.Sequence == sequence)
            || !_readSequences.Add(sequence))
            return false;
        _legacyReadThroughSequence = Math.Max(_legacyReadThroughSequence, sequence);
        return true;
    }

    internal bool MarkAllRead()
    {
        var changed = false;
        foreach (var message in _messages)
            changed |= _readSequences.Add(message.Sequence);
        if (_messages.Count > 0)
            _legacyReadThroughSequence = Math.Max(
                _legacyReadThroughSequence, _messages.Last().Sequence);
        return changed;
    }
}
