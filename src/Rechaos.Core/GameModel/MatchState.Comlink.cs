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
    /// RULE-COMLINK-004, FMT-STATE-005: the original keeps no messages in its save and empties
    /// every inbox when a match is entered, so a loaded match starts with none (FND-COMLINK-006,
    /// FND-SEARCH-005). A local load calls this. An online match never does: its clients rebuild
    /// the state from the server on every resume, and Comlink cannot be opened online, so its
    /// inboxes stay empty anyway.
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

    private ComlinkInbox GetComlinkInbox(PlayerId player) =>
        _comlinkInboxes.TryGetValue(player, out var inbox)
            ? inbox
            : throw new ArgumentOutOfRangeException(nameof(player));
}
