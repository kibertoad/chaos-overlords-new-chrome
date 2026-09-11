namespace Rechaos.Core.GameModel;

public sealed record ComlinkMessage(
    long Sequence,
    int Turn,
    PlayerId Sender,
    string Text);

public sealed class ComlinkInbox
{
    private readonly Queue<ComlinkMessage> _messages = [];

    public int Count => _messages.Count;
    public IReadOnlyList<ComlinkMessage> Messages => _messages.ToArray();
    public long NextSequence { get; private set; }
    public long ReadThroughSequence { get; private set; } = -1;
    public bool HasUnread => _messages.Any(message => message.Sequence > ReadThroughSequence);

    public ComlinkMessage Receive(int turn, PlayerId sender, string text)
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

    public void MarkAllRead()
    {
        if (_messages.TryPeek(out _)) ReadThroughSequence = _messages.Last().Sequence;
    }
}
