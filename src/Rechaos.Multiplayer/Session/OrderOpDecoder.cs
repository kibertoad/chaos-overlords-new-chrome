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
    /// <summary>A queued command, attributed to <paramref name="player"/> whatever the op says.</summary>
    /// <param name="submit">The op as it came off the wire.</param>
    /// <param name="player">The seat the op is applied under.</param>
    /// <param name="document">What the op is part of, for the refusal: "the sealed set" or "the saved draft".</param>
    public static GameCommand Command(SubmitCommandOp submit, PlayerId player, string document)
    {
        ArgumentNullException.ThrowIfNull(submit);
        return new GameCommand(
            player,
            Gang(submit.Gang, document, "gang"),
            Action(submit.Action, document),
            Target(submit.Target, document, "target"),
            submit.Repeat,
            submit.SecondaryTarget is { } secondary ? Target(secondary, document, "secondaryTarget") : null,
            submit.TertiaryTarget is { } tertiary ? Target(tertiary, document, "tertiaryTarget") : null,
            submit.QuaternaryTarget is { } quaternary ? Target(quaternary, document, "quaternaryTarget") : null);
    }

    /// <summary>A gang definition id that fits the core's <c>short</c>.</summary>
    public static short GangDefinitionId(int value, string document)
    {
        if (value is < short.MinValue or > short.MaxValue)
        {
            throw new MultiplayerProtocolException(
                $"{document} names gang definition {value}, which is outside the range this build reads");
        }
        return (short)value;
    }

    /// <summary>A gang id whose value the core can safely construct.</summary>
    public static GangId Gang(int value, string document, string field)
    {
        if (value < 0)
        {
            throw new MultiplayerProtocolException(
                $"{document} names {field} {value}, which is not a gang identifier");
        }
        return new GangId(value);
    }

    /// <summary>The refusal for an op kind this build has never heard of.</summary>
    public static MultiplayerProtocolException Unsupported(OrderOp op, string document)
    {
        ArgumentNullException.ThrowIfNull(op);
        return new MultiplayerProtocolException(
            $"{document} carries an op this client cannot apply: {op.Op}");
    }

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
        GangTarget gang => Core.GameModel.CommandTarget.Gang(Gang(gang.Id, document, $"{field} gang")),
        SectorTarget sector => Sector(sector.Id, document, $"{field} sector"),
        SiteTarget site => Site(site.Id, document, $"{field} site"),
        ItemTarget item => Item(item.Id, document, $"{field} item"),
        _ => throw new MultiplayerProtocolException(
            $"{document} carries a {field} kind this client cannot apply: {target?.Kind ?? "null"}"),
    };

    private static Core.GameModel.CommandTarget Sector(int value, string document, string field) =>
        BoundedTarget(value, MatchLimits.SectorCount, document, field, Core.GameModel.CommandTarget.Sector);

    private static Core.GameModel.CommandTarget Site(int value, string document, string field) =>
        BoundedTarget(value, MatchLimits.SiteCount, document, field, Core.GameModel.CommandTarget.Site);

    private static Core.GameModel.CommandTarget Item(int value, string document, string field) =>
        BoundedTarget(value, MatchLimits.ItemSlots, document, field, Core.GameModel.CommandTarget.Item);

    private static Core.GameModel.CommandTarget BoundedTarget(
        int value,
        int exclusiveMaximum,
        string document,
        string field,
        Func<int, Core.GameModel.CommandTarget> target)
    {
        if (value < 0 || value >= exclusiveMaximum)
        {
            throw new MultiplayerProtocolException(
                $"{document} names {field} {value}, which is outside 0 through {exclusiveMaximum - 1}");
        }
        return target(value);
    }
}
