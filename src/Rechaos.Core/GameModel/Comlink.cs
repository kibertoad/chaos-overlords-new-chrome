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
            || messages.Count != Math.Min(nextSequence, MatchLimits.ComlinkMessagesPerPlayer))
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

    internal static ComlinkInbox RestoreLegacy(
        IReadOnlyList<ComlinkMessage> messages,
        long nextSequence,
        long readThroughSequence)
    {
        if (readThroughSequence < -1 || readThroughSequence >= nextSequence)
            throw new ArgumentException("Restored Comlink read position is invalid.", nameof(readThroughSequence));
        return Restore(
            messages,
            nextSequence,
            messages.Where(message => message.Sequence <= readThroughSequence)
                .Select(message => message.Sequence).ToArray(),
            readThroughSequence);
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
