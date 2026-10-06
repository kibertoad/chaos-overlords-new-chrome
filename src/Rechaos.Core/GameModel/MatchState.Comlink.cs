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
        // A draft of spaces only is the RULE-COMLINK-003 blank; other whitespace alone is refused.
        if (message is null || !IsBlankComlinkDraft(message) && string.IsNullOrWhiteSpace(message))
            return Rejected(ComlinkValidationCode.EmptyMessage);
        if (message.Length > MatchLimits.ComlinkMessageCharacters)
            return Rejected(ComlinkValidationCode.MessageTooLong);
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
        // RULE-COMLINK-003: once a recipient is chosen, a blank draft is stored for no one and the
        // send still counts as made, with nothing to report.
        if (IsBlankComlinkDraft(message))
            return new ComlinkSendResult(true, ComlinkValidationCode.Accepted, [], string.Empty);
        return new ComlinkSendResult(true, ComlinkValidationCode.Accepted,
            recipients.OrderBy(player => player.Value).ToArray(),
            ComlinkValidationMessages.For(ComlinkValidationCode.Accepted));

        ComlinkSendResult Rejected(ComlinkValidationCode code) =>
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
    private bool CouldHaveSentComlink(PlayerId sender) =>
        FindPlayer(sender) is { } player
        && (player.Setup.Controller == PlayerController.Human || AiPlanning.RaiderMode(sender));

    private ComlinkInbox GetComlinkInbox(PlayerId player) =>
        _comlinkInboxes.TryGetValue(player, out var inbox)
            ? inbox
            : throw new ArgumentOutOfRangeException(nameof(player));
}
