using Rechaos.Core.GameModel;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// A wire op read into the core's types: everything it names is known to be constructible, so
/// applying it can only be refused by the rules, never by a malformed field.
/// </summary>
/// <remarks>
/// Readers decode a whole document before applying any of it. A refusal then comes before the
/// state is touched, rather than after half a turn has been applied and journalled.
/// </remarks>
public abstract record DecodedOrderOp
{
    private protected DecodedOrderOp()
    {
    }

    /// <summary>Queue <see cref="Command"/>, already attributed to the seat it is applied under.</summary>
    public sealed record Submit(GameCommand Command) : DecodedOrderOp;

    /// <summary>Cancel whatever <see cref="Gang"/> has queued.</summary>
    public sealed record Cancel(GangId Gang) : DecodedOrderOp;

    /// <summary>Hire <see cref="GangDefinitionId"/> into <see cref="SectorId"/>, a sector on the board.</summary>
    public sealed record QueueHire(short GangDefinitionId, int SectorId) : DecodedOrderOp;

    /// <summary>Turn down the offer of <see cref="GangDefinitionId"/>.</summary>
    public sealed record SnubHireOffer(short GangDefinitionId) : DecodedOrderOp;

    /// <summary>
    /// Send a Comlink message, sealed in one of <see cref="Letters"/> for each recipient
    /// (RULE-COMLINK-003).
    /// </summary>
    public sealed record SendComlinkMessage(IReadOnlyList<SealedComlinkLetter> Letters) : DecodedOrderOp;

    /// <summary>Mark the inbox message numbered <see cref="Sequence"/> read (RULE-COMLINK-005).</summary>
    public sealed record MarkComlinkRead(long Sequence) : DecodedOrderOp;

    /// <summary>Dismiss the oldest notification. It carries nothing, so every op shares one.</summary>
    public sealed record DismissNotification : DecodedOrderOp
    {
        public static DismissNotification Instance { get; } = new();

        private DismissNotification()
        {
        }
    }
}
