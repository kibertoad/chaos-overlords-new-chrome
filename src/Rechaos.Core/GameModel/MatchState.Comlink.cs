namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    public ComlinkInbox ComlinkFor(PlayerId player) => GetComlinkInbox(player);

    public ComlinkSendResult SendComlinkMessage(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string message)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        var validation = ValidateComlinkMessage(sender, recipients, message);
        if (!validation.Accepted) return validation;
        foreach (var recipient in validation.Recipients)
            GetComlinkInbox(recipient).Receive(Coordinator.Turn, sender, message);
        return validation;
    }

    /// <summary>
    /// RULE-COMLINK-003 for an online message: stores each letter's envelope in its recipient's
    /// inbox, in recipient order, as <see cref="SendComlinkMessage"/> stores the text.
    /// </summary>
    /// <remarks>
    /// The text is sealed for each recipient, so no other client can read it and the core never
    /// does. What every client can judge, it judges the same way as for a message in the clear:
    /// the sender, the phase, and each recipient (RULE-COMLINK-002), in the same order, with the
    /// envelopes' shape checked where the text would be. Whether the text is blank or too long
    /// (RULE-COMLINK-003, RULE-COMLINK-006) is the sender's client's check before it seals, and the
    /// recipient's client's after it opens.
    /// </remarks>
    public ComlinkSendResult SendSealedComlinkMessage(
        PlayerId sender,
        IReadOnlyList<SealedComlinkLetter> letters)
    {
        ArgumentNullException.ThrowIfNull(letters);
        // A journal or an order read from a damaged file can hold a null letter; the load paths
        // report an ArgumentException as a damaged file.
        if (letters.Any(letter => letter is null))
            throw new ArgumentException("A sealed Comlink message cannot have a null letter.", nameof(letters));
        var recipients = letters.Select(letter => letter.Recipient).ToArray();
        var validation = ValidateComlinkSend(
            sender,
            recipients,
            () => letters.All(letter => ComlinkEnvelope.IsWellFormed(letter.Envelope))
                ? null
                : ComlinkValidationCode.MalformedEnvelope);
        if (!validation.Accepted) return validation;
        foreach (var letter in letters.OrderBy(letter => letter.Recipient.Value))
            GetComlinkInbox(letter.Recipient).ReceiveSealed(Coordinator.Turn, sender, letter.Envelope);
        return validation;
    }

    /// <summary>
    /// Judges a message in the clear as <see cref="SendComlinkMessage"/> would, without storing it.
    /// </summary>
    /// <remarks>
    /// What an online client asks before it seals a message: the same refusal a hot-seat send would
    /// meet, or for a blank draft the same accepted send with nobody to store it for
    /// (RULE-COMLINK-003).
    /// </remarks>
    public ComlinkSendResult CheckComlinkMessage(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string message)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        return ValidateComlinkMessage(sender, recipients, message);
    }

    public bool MarkComlinkRead(PlayerId player, long sequence) =>
        GetComlinkInbox(player).MarkRead(sequence);

    /// <summary>
    /// RULE-COMLINK-002: a player can take a message from <paramref name="sender"/> when it is
    /// another human player still in the match, the original's <c>player_active</c> and
    /// <c>players_human</c> both set (FND-COMLINK-007). A computer player and the sender never can
    /// (EXP-COMLINK-001), and neither can an eliminated player.
    /// </summary>
    public bool IsComlinkRecipient(PlayerId sender, PlayerId recipient)
    {
        if (recipient == sender) return false;
        var player = FindPlayer(recipient);
        return player is { Status: PlayerStatus.Active, Setup.Controller: PlayerController.Human };
    }

    /// <summary>
    /// RULE-COMLINK-002: the Send panel opens only when some other player can take a message;
    /// otherwise the original refuses it with the rejected-input sound (EXP-COMLINK-002).
    /// </summary>
    public bool HasComlinkRecipient(PlayerId sender) =>
        Players.Any(player => IsComlinkRecipient(sender, player.Id));

    /// <summary>
    /// RULE-COMLINK-004, FMT-STATE-005: the original keeps no messages in its save and empties
    /// every inbox when a match is entered, so a loaded match starts with none (FND-COMLINK-006,
    /// FND-SEARCH-005). A local load calls this. An online match never does: its clients rebuild
    /// the state from the server on every resume, inboxes included, and every client must hold the
    /// same inboxes because they are hashed.
    /// </summary>
    /// <returns>Whether any inbox held a message.</returns>
    public bool EmptyComlinkInboxes()
    {
        var changed = false;
        foreach (var inbox in _comlinkInboxes.Values) changed |= inbox.Clear();
        return changed;
    }

    private ComlinkSendResult ValidateComlinkMessage(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string message)
    {
        var validation = ValidateComlinkSend(sender, recipients, () =>
            // A draft of spaces only is the RULE-COMLINK-003 blank; other whitespace alone is refused.
            message is null || !IsBlankComlinkDraft(message) && string.IsNullOrWhiteSpace(message)
                ? ComlinkValidationCode.EmptyMessage
                : message.Length > MatchLimits.ComlinkMessageCharacters
                    ? ComlinkValidationCode.MessageTooLong
                    : null);
        // RULE-COMLINK-003: once a recipient is chosen, a blank draft is stored for no one and the
        // send still counts as made, with nothing to report.
        if (validation.Accepted && IsBlankComlinkDraft(message))
            return new ComlinkSendResult(true, ComlinkValidationCode.Accepted, [], string.Empty);
        return validation;
    }

    /// <summary>
    /// The checks every Comlink send takes, in the order the codes are recorded in: the sender, the
    /// phase, that there is a recipient, then <paramref name="content"/> (the text, or the
    /// envelopes of a sealed message), then each recipient.
    /// </summary>
    private ComlinkSendResult ValidateComlinkSend(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        Func<ComlinkValidationCode?> content)
    {
        var senderSetup = Setup.Players.SingleOrDefault(player => player.Id == sender);
        if (senderSetup is null)
            return Rejected(ComlinkValidationCode.SenderNotFound);
        if (senderSetup.Controller != PlayerController.Human)
            return Rejected(ComlinkValidationCode.SenderNotHuman);
        if (Coordinator.ActivePlayer != sender)
            return Rejected(ComlinkValidationCode.SenderNotActive);
        if (Coordinator.Phase != TurnPhase.Command)
            return Rejected(ComlinkValidationCode.WrongPhase);
        if (recipients.Count == 0)
            return Rejected(ComlinkValidationCode.NoRecipients);
        if (content() is { } refused)
            return Rejected(refused);
        if (recipients.Distinct().Count() != recipients.Count)
            return Rejected(ComlinkValidationCode.DuplicateRecipient);
        if (recipients.Contains(sender))
            return Rejected(ComlinkValidationCode.SenderIsRecipient);
        foreach (var recipient in recipients)
        {
            var setup = Setup.Players.SingleOrDefault(player => player.Id == recipient);
            if (setup is null)
                return Rejected(ComlinkValidationCode.RecipientNotFound);
            if (setup.Controller != PlayerController.Human)
                return Rejected(ComlinkValidationCode.RecipientNotHuman);
            // RULE-COMLINK-002: an eliminated player's card cannot be selected.
            if (!IsComlinkRecipient(sender, recipient))
                return Rejected(ComlinkValidationCode.RecipientNotActive);
        }
        return new ComlinkSendResult(true, ComlinkValidationCode.Accepted,
            recipients.OrderBy(player => player.Value).ToArray(),
            ComlinkValidationMessages.For(ComlinkValidationCode.Accepted));

        static ComlinkSendResult Rejected(ComlinkValidationCode code) =>
            new(false, code, [], ComlinkValidationMessages.For(code));
    }

    /// <summary>
    /// RULE-COMLINK-003: the original tests all 160 characters of the draft for spaces. The text
    /// the rebuild stores drops trailing spaces (RULE-COMLINK-006), so a blank draft is empty or
    /// spaces only.
    /// </summary>
    public static bool IsBlankComlinkDraft(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.All(character => character == ' ');
    }

    /// <summary>
    /// Whether a restored message can name <paramref name="sender"/>: a human seat, or one a human
    /// held until the computer took it over online (RULE-AI-027 sets raider mode only then). A seat
    /// the computer has played from the start never sent anything.
    /// </summary>
    /// <remarks>
    /// The check goes only as far as the state does: a save does not record which seats began as
    /// computer players, so a save that sets raider mode on such a seat passes it. Play never
    /// produces one, because only a takeover enters raider mode.
    /// </remarks>
    private bool CouldHaveSentComlink(PlayerId sender) =>
        FindPlayer(sender) is { } player
        && (player.Setup.Controller == PlayerController.Human || AiPlanning.RaiderMode(sender));

    private ComlinkInbox GetComlinkInbox(PlayerId player) =>
        _comlinkInboxes.TryGetValue(player, out var inbox)
            ? inbox
            : throw new ArgumentOutOfRangeException(nameof(player));
}
