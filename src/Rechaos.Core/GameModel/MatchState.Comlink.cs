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

    internal bool MarkAllComlinkReadLegacy(PlayerId player) =>
        GetComlinkInbox(player).MarkAllRead();

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
        if (string.IsNullOrWhiteSpace(message))
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
        return new ComlinkSendResult(true, ComlinkValidationCode.Accepted,
            recipients.OrderBy(player => player.Value).ToArray(),
            ComlinkValidationMessages.For(ComlinkValidationCode.Accepted));

        ComlinkSendResult Rejected(ComlinkValidationCode code) =>
            new(false, code, [], ComlinkValidationMessages.For(code));
    }

    private ComlinkInbox GetComlinkInbox(PlayerId player) =>
        _comlinkInboxes.TryGetValue(player, out var inbox)
            ? inbox
            : throw new ArgumentOutOfRangeException(nameof(player));
}
