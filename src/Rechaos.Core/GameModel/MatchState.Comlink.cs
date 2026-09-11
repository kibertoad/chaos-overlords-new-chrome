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

    public bool MarkComlinkRead(PlayerId player)
    {
        var inbox = GetComlinkInbox(player);
        var changed = inbox.HasUnread;
        inbox.MarkAllRead();
        return changed;
    }

    private ComlinkSendResult ValidateComlinkMessage(
        PlayerId sender,
        IReadOnlyList<PlayerId> recipients,
        string message)
    {
        var senderSetup = Setup.Players.SingleOrDefault(player => player.Id == sender);
        if (senderSetup is null)
            return Rejected(ComlinkValidationCode.SenderNotFound, "Sender is not in this match.");
        if (senderSetup.Controller != PlayerController.Human)
            return Rejected(ComlinkValidationCode.SenderNotHuman, "Only human players can use Comlink.");
        if (Coordinator.ActivePlayer != sender)
            return Rejected(ComlinkValidationCode.SenderNotActive, "Sender is not the active player.");
        if (Coordinator.Phase != TurnPhase.Command)
            return Rejected(ComlinkValidationCode.WrongPhase, "Comlink sending requires the command phase.");
        if (recipients.Count == 0)
            return Rejected(ComlinkValidationCode.NoRecipients, "Select at least one recipient.");
        if (string.IsNullOrWhiteSpace(message))
            return Rejected(ComlinkValidationCode.EmptyMessage, "Enter a message.");
        if (message.Length > MatchLimits.ComlinkMessageCharacters)
            return Rejected(ComlinkValidationCode.MessageTooLong,
                $"Message cannot exceed {MatchLimits.ComlinkMessageCharacters} characters.");
        if (recipients.Distinct().Count() != recipients.Count)
            return Rejected(ComlinkValidationCode.DuplicateRecipient, "Recipients must be unique.");
        if (recipients.Contains(sender))
            return Rejected(ComlinkValidationCode.SenderIsRecipient, "You cannot send Comlink to yourself.");
        foreach (var recipient in recipients)
        {
            var setup = Setup.Players.SingleOrDefault(player => player.Id == recipient);
            if (setup is null)
                return Rejected(ComlinkValidationCode.RecipientNotFound, "Recipient is not in this match.");
            if (setup.Controller != PlayerController.Human)
                return Rejected(ComlinkValidationCode.RecipientNotHuman,
                    "Comlink recipients must be human players.");
        }
        return new ComlinkSendResult(true, ComlinkValidationCode.Accepted,
            recipients.OrderBy(player => player.Value).ToArray(), "Message sent.");

        ComlinkSendResult Rejected(ComlinkValidationCode code, string reason) =>
            new(false, code, [], reason);
    }

    private ComlinkInbox GetComlinkInbox(PlayerId player) =>
        _comlinkInboxes.TryGetValue(player, out var inbox)
            ? inbox
            : throw new ArgumentOutOfRangeException(nameof(player));
}
