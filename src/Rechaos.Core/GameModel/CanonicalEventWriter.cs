using static Rechaos.Core.GameModel.MatchStateHasher;

namespace Rechaos.Core.GameModel;

internal static class CanonicalEventWriter
{
    public static GameEvent Freeze(GameEvent value) => value with
    {
        Resolution = value.Resolution is null ? null : value.Resolution with
        {
            Rolls = Freeze(value.Resolution.Rolls),
            RetaliationRolls = FreezeNullable(value.Resolution.RetaliationRolls),
            ItemIds = FreezeNullable(value.Resolution.ItemIds),
            ReplacedItemIds = FreezeNullable(value.Resolution.ReplacedItemIds)
        },
        PoliceAttack = value.PoliceAttack is null ? null : value.PoliceAttack with
        {
            Rolls = Freeze(value.PoliceAttack.Rolls)
        },
        MatchOutcome = value.MatchOutcome is null ? null : value.MatchOutcome with
        {
            Winners = Freeze(value.MatchOutcome.Winners),
            Standings = Freeze(value.MatchOutcome.Standings),
            Awards = Freeze(value.MatchOutcome.Awards.Select(award => award with
            {
                Recipients = Freeze(award.Recipients)
            }))
        }
    };

    public static void Append(Stream stream, GameEvent value)
    {
        using var writer = new BinaryWriter(
            stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Write(writer, value);
    }

    public static void Write(BinaryWriter writer, GameEvent value)
    {
        writer.Write(value.Sequence);
        writer.Write(value.Turn);
        writer.Write((byte)value.Phase);
        WriteNullableByte(writer, value.ExecutionPhase is { } phase ? (byte)phase : null);
        writer.Write((byte)value.Kind);
        writer.Write(value.Player.Value);
        WriteNullableInt(writer, value.Gang?.Value);
        writer.Write((byte)value.Action);
        WriteTarget(writer, value.Target);
        WriteNullableTarget(writer, value.SecondaryTarget);
        WriteNullableTarget(writer, value.TertiaryTarget);
        WriteNullableTarget(writer, value.QuaternaryTarget);
        WriteResolution(writer, value.Resolution);
        WriteEconomy(writer, value.Economy);
        WriteHire(writer, value.Hire);
        WriteHireOffer(writer, value.HireOffer);
        WriteElimination(writer, value.Elimination);
        WritePoliceAttack(writer, value.PoliceAttack);
        WriteBigManPoints(writer, value.BigManPoints);
        WriteOutcome(writer, value.MatchOutcome);
    }

    private static void WriteResolution(BinaryWriter writer, CommandResolutionDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write((byte)value.Code);
        WriteInts(writer, value.Rolls);
        writer.Write(value.Successes);
        WriteNullableInt(writer, value.PreviousValue);
        WriteNullableInt(writer, value.ResultValue);
        writer.Write(value.CashDelta);
        WriteNullableShort(writer, value.ItemId);
        WriteNullableShort(writer, value.ReplacedItemId);
        WriteNullableInt(writer, value.AttackValue);
        WriteNullableInt(writer, value.DefenseValue);
        WriteNullableInts(writer, value.RetaliationRolls);
        writer.Write(value.RetaliationSuccesses);
        writer.Write(value.Damage);
        writer.Write(value.RetaliationDamage);
        WriteNullableInt(writer, value.DetectionRoll);
        WriteNullableInt(writer, value.DetectionChance);
        WriteNullableInt(writer, value.ChanceRoll);
        WriteNullableInt(writer, value.ChanceSides);
        WriteNullableShort(writer, value.RetaliationItemId);
        WriteNullableShorts(writer, value.ItemIds);
        WriteNullableShorts(writer, value.ReplacedItemIds);
    }

    private static void WriteEconomy(BinaryWriter writer, EconomyResolutionDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write(value.PreviousCash);
        writer.Write(value.SectorIncome);
        writer.Write(value.SiteIncome);
        writer.Write(value.GangUpkeep);
        writer.Write(value.ResultCash);
    }

    private static void WriteHire(BinaryWriter writer, HireResolutionDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write(value.GangDefinitionId);
        writer.Write(value.SectorId);
        writer.Write(value.Cost);
        WriteNullableInt(writer, value.Gang?.Value);
        WriteNullableShort(writer, value.ReplacementOffer);
        WriteNullableInt(writer, value.InitialForce);
    }

    private static void WriteHireOffer(BinaryWriter writer, HireOfferDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        WriteNullableShort(writer, value.RemovedOffer);
        WriteNullableShort(writer, value.AddedOffer);
    }

    private static void WriteElimination(BinaryWriter writer, EliminationDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write(value.EliminatedPlayer.Value);
        writer.Write(value.RemainingPlayers);
    }

    private static void WritePoliceAttack(BinaryWriter writer, PoliceAttackResolutionDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write(value.SectorId);
        writer.Write(value.DetectionChance);
        writer.Write(value.DetectionRoll);
        writer.Write(value.Detected);
        writer.Write(value.AttackValue);
        writer.Write(value.DefenseValue);
        WriteInts(writer, value.Rolls);
        writer.Write(value.Successes);
        writer.Write(value.Damage);
        writer.Write(value.PreviousForce);
        writer.Write(value.ResultForce);
    }

    private static void WriteBigManPoints(BinaryWriter writer, BigManPointDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write(value.PreviousPoints);
        writer.Write(value.ControlledCentralSectors);
        writer.Write(value.ResultPoints);
    }

    private static void WriteOutcome(BinaryWriter writer, MatchOutcomeDetails? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        WriteOutcomeBody(
            writer, value.Scenario, value.Reason, value.CompletedTurn,
            value.Winners, value.Standings, value.Awards);
    }

    private static void WriteInts(BinaryWriter writer, IReadOnlyList<int> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) writer.Write(value);
    }

    private static void WriteNullableInts(BinaryWriter writer, IReadOnlyList<int>? values)
    {
        writer.Write(values is not null);
        if (values is not null) WriteInts(writer, values);
    }

    private static void WriteNullableShorts(BinaryWriter writer, IReadOnlyList<short>? values)
    {
        writer.Write(values is not null);
        if (values is null) return;
        writer.Write(values.Count);
        foreach (var value in values) writer.Write(value);
    }

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static IReadOnlyList<T>? FreezeNullable<T>(IEnumerable<T>? values) =>
        values is null ? null : Freeze(values);
}
