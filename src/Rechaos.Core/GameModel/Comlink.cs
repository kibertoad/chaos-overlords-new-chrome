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

    public int Count => _messages.Count;
    public IReadOnlyList<ComlinkMessage> Messages => _messages.ToArray();
    public long NextSequence { get; private set; }
    public long ReadThroughSequence { get; private set; } = -1;
    public bool HasUnread => _messages.Any(message => message.Sequence > ReadThroughSequence);

    internal static ComlinkInbox Restore(
        IReadOnlyList<ComlinkMessage> messages,
        long nextSequence,
        long readThroughSequence)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count > MatchLimits.ComlinkMessagesPerPlayer
            || nextSequence < 0
            || readThroughSequence < -1
            || readThroughSequence >= nextSequence
            || messages.Any(message => message.Sequence < 0
                || message.Sequence >= nextSequence
                || message.Turn < 1
                || message.Sender.Value is < 0 or >= MatchLimits.PlayerCount
                || string.IsNullOrWhiteSpace(message.Text)
                || message.Text.Length > MatchLimits.ComlinkMessageCharacters)
            || messages.Select(message => message.Sequence).Distinct().Count() != messages.Count
            || !messages.Select(message => message.Sequence).SequenceEqual(
                messages.Select(message => message.Sequence).Order()))
            throw new ArgumentException("Restored Comlink inbox is invalid.", nameof(messages));

        var inbox = new ComlinkInbox
        {
            NextSequence = nextSequence,
            ReadThroughSequence = readThroughSequence
        };
        foreach (var message in messages) inbox._messages.Enqueue(message);
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
        if (_messages.Count == MatchLimits.ComlinkMessagesPerPlayer) _messages.Dequeue();
        _messages.Enqueue(message);
        return message;
    }

    internal void MarkAllRead()
    {
        if (_messages.TryPeek(out _)) ReadThroughSequence = _messages.Last().Sequence;
    }
}
