using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Hover help for the fifteen entries of the gang command overlay. The pointer has to
/// rest on one row for <see cref="HoverDwellTracker.Delay"/> before the lines are shown,
/// so the list stays readable while the cursor merely passes over it.
/// </summary>
public static class CommandActionTooltips
{
    /// <summary>The overlay row under <paramref name="point"/>, or null outside the action rows.</summary>
    public static int? RowAt(Point point, bool recurring)
    {
        var actions = CommandOverlayLayout.ActionsFor(recurring);
        for (var index = 0; index < actions.Count; index++)
            if (CommandOverlayLayout.ActionRow(index).Contains(point)) return index;
        return null;
    }

    public static IReadOnlyList<string> Lines(GangAction action) => [Name(action), .. Effect(action)];

    private static string Name(GangAction action) => action switch
    {
        GangAction.None => "NO ORDER",
        _ => action.ToString().ToUpperInvariant()
    };

    private static string[] Effect(GangAction action) => action switch
    {
        GangAction.Attack =>
        [
            "ROLLS FORCE PLUS COMBAT AGAINST A VISIBLE ENEMY GANG HERE.",
            "THE DEFENDER STRIKES BACK FOR HALF DAMAGE."
        ],
        GangAction.Bribe =>
        [
            $"PAYS ${ManualRules.OriginalBribeCost} TO RAISE THIS SECTOR'S TOLERANCE BY" +
            $" {ManualRules.BribeToleranceIncrease}.",
            "HIGHER TOLERANCE LETS CHAOS RUN LONGER BEFORE A CRACKDOWN."
        ],
        GangAction.Chaos =>
        [
            "ROLLS FORCE PLUS CHAOS PLUS SECTOR INCOME TO EARN CASH.",
            "THE CHAOS RAISED ALSO PUSHES THE SECTOR TOWARD A CRACKDOWN."
        ],
        GangAction.Control =>
        [
            "CLAIMS THIS SECTOR WITH FORCE PLUS CONTROL.",
            "DEFENDERS, SITE SUPPORT, AND SECTOR INCOME OPPOSE THE CLAIM.",
            "IT IS ILLEGAL DURING A CRACKDOWN OR IN A SECTOR YOU OWN."
        ],
        GangAction.Equip =>
        [
            "BUYS AN ITEM: ONE WEAPON, ONE ARMOR, AND ONE MISC PER GANG.",
            "REPLACING A FILLED SLOT DESTROYS THE ITEM ALREADY THERE."
        ],
        GangAction.Give =>
        [
            "HANDS AN EQUIPPED ITEM TO A FRIENDLY GANG IN THIS SECTOR.",
            "THE GIFT ARRIVES WHEN THE TRANSACTION PHASE RESOLVES."
        ],
        GangAction.Heal =>
        [
            $"ROLLS {ManualRules.HealBaseDice} DICE PLUS HEAL; SUCCESS RESTORES FORCE TO" +
            $" {ManualRules.MaximumForce}.",
            "IT CANNOT BE ORDERED WHILE THE GANG IS AT FULL FORCE."
        ],
        GangAction.Hide =>
        [
            "CONCEALS THE GANG FROM ENEMY DETECTION IMMEDIATELY.",
            "HIDDEN GANGS ARE HARDER TO ATTACK AND TO CRACK DOWN ON."
        ],
        GangAction.Influence =>
        [
            "ROLLS FORCE PLUS INFLUENCE AGAINST A SITE'S RESISTANCE.",
            "SEVERAL GANGS CAN WEAR THE SAME SITE DOWN TOGETHER.",
            "AT ZERO RESISTANCE ITS CASH AND BONUSES BECOME YOURS."
        ],
        GangAction.Move =>
        [
            "SENDS THE GANG TO ONE OF THE EIGHT NEIGHBORING SECTORS.",
            "A SECTOR HOLDS AT MOST SIX OF YOUR GANGS."
        ],
        GangAction.Research =>
        [
            "ROLLS FORCE PLUS RESEARCH TOWARD UNLOCKING AN ITEM.",
            "PROGRESS PERSISTS; GANG TECH LEVEL LIMITS WHAT IS LEGAL."
        ],
        GangAction.Sell =>
        [
            "SELLS EQUIPPED ITEMS BACK FOR CASH.",
            "THE CREDIT ARRIVES WHEN THE TRANSACTION PHASE RESOLVES."
        ],
        GangAction.Snitch =>
        [
            $"TIPS OFF THE POLICE FOR FREE, CUTTING TOLERANCE BY" +
            $" {ManualRules.SnitchToleranceDecrease}.",
            "LOWER TOLERANCE MAKES A CRACKDOWN HERE MORE LIKELY."
        ],
        GangAction.None =>
        [
            "CLEARS THE QUEUED COMMAND AND LEAVES THE GANG IDLE.",
            "AN IDLE GANG STILL COSTS ITS UPKEEP EACH TURN."
        ],
        GangAction.Terminate =>
        [
            "DISBANDS THE GANG PERMANENTLY, ENDING ITS UPKEEP.",
            "THE GANG AND EVERYTHING IT CARRIES ARE LOST."
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
}
