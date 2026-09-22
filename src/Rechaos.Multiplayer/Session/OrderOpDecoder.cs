using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The one conversion from a wire op to what the core takes, refusing by field name.
/// </summary>
/// <remarks>
/// <para>
/// A sealed set and a saved draft carry the same ops and are read into the same core, so they
/// share one decoder. Before this was shared, each reader cast for itself, and a value that did
/// not fit — an action past the enum, a gang definition past a <c>short</c> — surfaced as an
/// <see cref="OverflowException"/> or <see cref="InvalidCastException"/> named after the CLR
/// rather than after the field, which told a player nothing.
/// </para>
/// <para>
/// Every refusal is a <see cref="MultiplayerProtocolException"/>: the payload cannot be made sense
/// of by this build, and retrying will not change that.
/// </para>
/// </remarks>
public static class OrderOpDecoder
{
    /// <summary>Every op of a document, read before any of it is applied.</summary>
    /// <param name="ops">The document's ops, in order.</param>
    /// <param name="player">The seat the ops are applied under, whatever they say.</param>
    /// <param name="document">What the ops are part of, for the refusal: "the sealed set" or "the saved draft".</param>
    public static DecodedOrderOp[] Decode(IReadOnlyList<OrderOp> ops, PlayerId player, string document)
    {
        ArgumentNullException.ThrowIfNull(ops);
        var decoded = new DecodedOrderOp[ops.Count];
        for (var index = 0; index < decoded.Length; index++) decoded[index] = Decode(ops[index], player, document);
        return decoded;
    }

    /// <summary>One op, attributed to <paramref name="player"/> whatever the op says.</summary>
    /// <inheritdoc cref="Decode(IReadOnlyList{OrderOp}, PlayerId, string)" path="/param[@name='document']"/>
    public static DecodedOrderOp Decode(OrderOp op, PlayerId player, string document)
    {
        ArgumentNullException.ThrowIfNull(op);
        return op switch
        {
            SubmitCommandOp submit => new DecodedOrderOp.Submit(Command(submit, player, document)),
            CancelCommandOp cancel => new DecodedOrderOp.Cancel(Gang(cancel.Gang, document, "gang")),
            QueueHireOp hire => new DecodedOrderOp.QueueHire(
                GangDefinitionId(hire.GangDefinitionId, document), HireSector(hire.SectorId, document)),
            SnubHireOfferOp snub => new DecodedOrderOp.SnubHireOffer(GangDefinitionId(snub.GangDefinitionId, document)),
            DismissNotificationOp => DecodedOrderOp.DismissNotification.Instance,
            _ => throw new MultiplayerProtocolException(
                $"{document} carries an op this client cannot apply: {op.Op}"),
        };
    }

    private static GameCommand Command(SubmitCommandOp submit, PlayerId player, string document) => new(
        player,
        Gang(submit.Gang, document, "gang"),
        Action(submit.Action, document),
        Target(submit.Target, document, "target"),
        submit.Repeat,
        submit.SecondaryTarget is { } secondary ? Target(secondary, document, "secondaryTarget") : null,
        submit.TertiaryTarget is { } tertiary ? Target(tertiary, document, "tertiaryTarget") : null,
        submit.QuaternaryTarget is { } quaternary ? Target(quaternary, document, "quaternaryTarget") : null);

    /// <summary>A gang definition id that fits the core's <c>short</c>.</summary>
    private static short GangDefinitionId(int value, string document)
    {
        if (value is < short.MinValue or > short.MaxValue)
        {
            throw new MultiplayerProtocolException(
                $"{document} names gang definition {value}, which is outside the range this build reads");
        }
        return (short)value;
    }

    private static GangId Gang(int value, string document, string field) =>
        GangId.IsValid(value) ? new GangId(value) : throw NotAnIdentifier(document, field, value, "gang");

    /// <summary>
    /// A hire sector on the board. Whether the player may hire there is the rules' call; a sector the
    /// board does not have is not, because it is the same id a command target refuses by name.
    /// </summary>
    private static int HireSector(int value, string document) =>
        MatchLimits.IsSectorId(value) ? value : throw NotAnIdentifier(document, "hire sector", value, "sector");

    private static GangAction Action(int value, string document)
    {
        if (value is < byte.MinValue or > byte.MaxValue || !Enum.IsDefined((GangAction)value))
        {
            throw new MultiplayerProtocolException(
                $"{document} carries action {value}, which is not a gang action this build knows");
        }
        return (GangAction)value;
    }

    private static Core.GameModel.CommandTarget Target(
        Generated.CommandTarget target,
        string document,
        string field) => target switch
    {
        NoneTarget => Core.GameModel.CommandTarget.None,
        GangTarget gang => Bounded(CommandTargetKind.Gang, gang.Id, document, field),
        SectorTarget sector => Bounded(CommandTargetKind.Sector, sector.Id, document, field),
        SiteTarget site => Bounded(CommandTargetKind.Site, site.Id, document, field),
        ItemTarget item => Bounded(CommandTargetKind.Item, item.Id, document, field),
        _ => throw new MultiplayerProtocolException(
            $"{document} carries a {field} kind this client cannot apply: {target?.Kind ?? "null"}"),
    };

    /// <summary>A target whose id the core itself accepts, so the bound lives in one place.</summary>
    private static Core.GameModel.CommandTarget Bounded(
        CommandTargetKind kind,
        int id,
        string document,
        string field)
    {
        if (Core.GameModel.CommandTarget.TryCreate(kind, id, out var target)) return target;
        var name = kind.ToString().ToLowerInvariant();
        throw NotAnIdentifier(document, $"{field} {name}", id, name);
    }

    private static MultiplayerProtocolException NotAnIdentifier(
        string document,
        string field,
        int value,
        string kind) =>
        new($"{document} names {field} {value}, which is not a valid {kind} identifier");
}
