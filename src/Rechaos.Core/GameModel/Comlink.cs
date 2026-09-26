namespace Rechaos.Core.GameModel;

public sealed record ComlinkMessage(
    long Sequence,
    int Turn,
    PlayerId Sender,
    string Text);

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
    MessageTooLong
}

internal static class ComlinkValidationMessages
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
            [ComlinkValidationCode.MessageTooLong] = "Message exceeds 160 characters."
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
                || string.IsNullOrWhiteSpace(message.Text)
                || message.Text.Length > MatchLimits.ComlinkMessageCharacters).Any()
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

        var message = new ComlinkMessage(NextSequence++, turn, sender, text);
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
