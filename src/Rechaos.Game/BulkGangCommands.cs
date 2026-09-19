using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>One order, as it is put to every gang of a <see cref="GangMultiSelection"/>.</summary>
public readonly record struct BulkCommandIntent(GangAction Action, CommandTarget Target, bool Repeat);

/// <summary>
/// The share of a selection that can carry an order out. A gang the rules refuse is left out
/// rather than holding up the rest, so a selection always does as much of the order as it can.
/// </summary>
public sealed record BulkCommandPlan(IReadOnlyList<GameCommand> Commands, int Skipped)
{
    public int Total => Commands.Count + Skipped;
    public bool IsEmpty => Commands.Count == 0;
}

/// <summary>
/// Puts one order to a whole ctrl-picked selection: which orders may be given that way, which
/// targets the selection can reach, and who of it actually carries the order out.
/// </summary>
public static class BulkGangCommands
{
    /// <summary>
    /// The orders a selection may be given. Everything else stays one gang's business: equipping,
    /// researching, giving and selling all read a single gang's inventory, and bribing, snitching,
    /// raising chaos or terminating a whole selection in one click is a decision nobody makes by
    /// accident twice.
    /// </summary>
    public static readonly IReadOnlyList<GangAction> Actions =
    [
        GangAction.Attack, GangAction.Control, GangAction.Heal,
        GangAction.Hide, GangAction.Influence, GangAction.Move
    ];

    private static readonly IReadOnlySet<GangAction> Allowed = Actions.ToHashSet();

    public static bool Allows(GangAction action) => Allowed.Contains(action);

    /// <summary>The allowlisted orders a recurring or a one-off bulk command may choose from.</summary>
    public static IReadOnlyList<GangAction> ActionsFor(bool recurring) => recurring
        ? Actions.Where(CommandRules.CanRepeat).ToArray()
        : Actions;

    /// <summary>
    /// Every allowlisted command the selection can legally carry out, with one entry per distinct
    /// target so a target picker lists a target once however many gangs can reach it.
    /// </summary>
    public static IReadOnlyList<GameCommand> Options(
        MatchState state,
        PlayerId player,
        IReadOnlyList<GangId> gangs,
        bool recurring)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gangs);
        var options = new List<GameCommand>();
        var listed = new HashSet<(GangAction Action, CommandTargetKind Kind, int Id)>();
        foreach (var gang in gangs)
        foreach (var command in CommandOptionCatalog.LegalCommands(state, player, gang))
        {
            if (!Allows(command.Action)) continue;
            if (recurring && !CommandRules.CanRepeat(command.Action)) continue;
            if (listed.Add((command.Action, command.Target.Kind, command.Target.Id)))
                options.Add(command with { Repeat = recurring });
        }
        return options;
    }

    /// <summary>Works out who carries <paramref name="intent"/> out, and how many are left out.</summary>
    public static BulkCommandPlan Plan(
        MatchState state,
        PlayerId player,
        IReadOnlyList<GangId> gangs,
        BulkCommandIntent intent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gangs);
        if (!Allows(intent.Action))
            throw new ArgumentOutOfRangeException(nameof(intent), "A selection cannot be given this order.");
        var room = intent.Action == GangAction.Move
            ? RemainingRoom(state, player, intent.Target.Id)
            : int.MaxValue;
        var commands = new List<GameCommand>(gangs.Count);
        var skipped = 0;
        foreach (var gang in gangs)
        {
            var command = new GameCommand(player, gang, intent.Action, intent.Target, intent.Repeat);
            if (commands.Count >= room || !CommandValidator.Validate(state, command).IsValid)
            {
                skipped++;
                continue;
            }
            commands.Add(command);
        }
        return new BulkCommandPlan(commands, skipped);
    }

    /// <summary>
    /// How many more of the overlord's gangs a sector has room for.
    /// </summary>
    /// <remarks>
    /// The validator only refuses a move into a sector that is already full, which is the right
    /// answer for one gang and the wrong one for six heading to the same tile: the surplus would
    /// be turned back when the turn resolved. Counting the selection in here means the player is
    /// told at once how much of the move was taken.
    /// </remarks>
    private static int RemainingRoom(MatchState state, PlayerId player, int sectorId) =>
        Math.Max(0, MatchLimits.FriendlyGangsPerSector - (state.FindPlayer(player)?.Gangs
            .Count(gang => gang.IsActive && gang.SectorId == sectorId) ?? 0));

    /// <summary>The status console's account of a bulk order that at least one gang took.</summary>
    public static string Message(GangAction action, int ordered, int total)
    {
        if (ordered <= 0) throw new ArgumentOutOfRangeException(nameof(ordered));
        if (total < ordered) throw new ArgumentOutOfRangeException(nameof(total));
        var name = action.ToString().ToUpperInvariant();
        return CityStatusMessage.RequireFit(ordered == total
            ? $"{name} ORDERED FOR {total} GANGS"
            : $"{name} ORDERED FOR {ordered} OF {total}");
    }

    /// <summary>The status console's account of a bulk order no gang could take.</summary>
    public static string Rejection(GangAction action) =>
        CityStatusMessage.RequireFit($"NO GANG CAN {action.ToString().ToUpperInvariant()}");
}
