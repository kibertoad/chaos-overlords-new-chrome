using System.Security.Cryptography;
using System.Text;
using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Stable, serializable implementation of the original executable's statically
/// linked Visual C++ rand step and three-sample bounded-range wrapper. Initial
/// seed selection remains provisional until its source is recovered.
/// </summary>
public sealed class DeterministicRandom
{
    private const uint Multiplier = 0x343fd;
    private const uint Addend = 0x269ec3;
    private const int SelectionThreshold = 0x3ffe;
    private uint _state;

    public DeterministicRandom(int seed)
    {
        _state = unchecked((uint)seed);
    }

    public DeterministicRandom(uint state, long consumptionCount)
    {
        if (consumptionCount < 0) throw new ArgumentOutOfRangeException(nameof(consumptionCount));
        _state = state;
        ConsumptionCount = consumptionCount;
    }

    public uint State => _state;
    public long ConsumptionCount { get; private set; }

    public int NextRaw()
    {
        _state = unchecked(_state * Multiplier + Addend);
        ConsumptionCount++;
        return (int)((_state >> 16) & 0x7fff);
    }

    public int NextInt(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        return NextInclusive(exclusiveMaximum) - 1;
    }

    public int NextInclusive(int maximum)
    {
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        var first = NextRaw();
        var second = NextRaw();
        var selector = NextRaw();
        var selected = selector > SelectionThreshold ? first : second;
        return selected % maximum + 1;
    }
}

public sealed record PhaseBoundaryHash(
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    string Sha256);

/// <summary>Canonical little-endian encoding of all authoritative headless match state.</summary>
public static class MatchStateHasher
{
    private const int FormatVersion = 1;

    public static string ComputeSha256(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RCHS"));
            writer.Write(FormatVersion);
            WriteDefinitions(writer, state.Definitions);
            writer.Write((byte)state.Setup.Scenario);
            writer.Write((byte)state.Setup.Duration);
            writer.Write(state.Setup.InitialSeed);
            writer.Write(state.Setup.Players.Count);
            foreach (var player in state.Setup.Players)
            {
                writer.Write(player.Id.Value);
                WriteString(writer, player.Name);
                writer.Write((byte)player.Controller);
            }

            writer.Write(state.Coordinator.Turn);
            writer.Write((byte)state.Coordinator.Phase);
            WriteNullableByte(writer, state.Coordinator.ExecutionPhase is { } execution ? (byte)execution : null);
            WriteNullableInt(writer, state.Coordinator.ActivePlayer?.Value);
            writer.Write(state.Random.State);
            writer.Write(state.Random.ConsumptionCount);
            writer.Write(state.NextEventSequence);

            writer.Write(state.Players.Count);
            foreach (var player in state.Players.OrderBy(item => item.Id.Value)) WritePlayer(writer, player);
            writer.Write(state.Sectors.Count);
            foreach (var sector in state.Sectors.OrderBy(item => item.Id)) WriteSector(writer, sector);

            var commands = state.Commands.ExecutionPlan().OrderBy(item => item.Sequence).ToArray();
            writer.Write(state.Commands.NextSequence);
            writer.Write(commands.Length);
            foreach (var command in commands)
            {
                writer.Write(command.Sequence);
                WriteCommand(writer, command.Command);
            }

            foreach (var player in state.Players.OrderBy(item => item.Id.Value))
            {
                var notifications = state.NotificationsFor(player.Id);
                writer.Write(state.NextNotificationSequence(player.Id));
                writer.Write(notifications.Count);
                foreach (var notification in notifications) WriteNotification(writer, notification);
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }

    private static void WriteDefinitions(BinaryWriter writer, OriginalData definitions)
    {
        writer.Write(definitions.Sites.Count);
        foreach (var site in definitions.Sites.OrderBy(item => item.Id))
        {
            writer.Write(site.Id); WriteString(writer, site.Name); writer.Write(site.Resistance); writer.Write(site.Support);
            writer.Write(site.Frequency); writer.Write(site.Tolerance); writer.Write(site.Cash); WriteStatistics(writer, site.Stats);
            writer.Write(site.Special);
        }
        writer.Write(definitions.Gangs.Count);
        foreach (var gang in definitions.Gangs.OrderBy(item => item.Id))
        {
            writer.Write(gang.Id); WriteString(writer, gang.Name); WriteString(writer, gang.Description); writer.Write(gang.Force);
            writer.Write(gang.Upkeep); writer.Write(gang.TechLevel); WriteStatistics(writer, gang.Stats);
        }
        writer.Write(definitions.Items.Count);
        foreach (var item in definitions.Items.OrderBy(value => value.Id))
        {
            writer.Write(item.Id); WriteString(writer, item.Name); WriteString(writer, item.Description); writer.Write(item.Type);
            writer.Write(item.ResearchDifficulty); writer.Write(item.Cost); writer.Write(item.TechLevel); WriteStatistics(writer, item.Stats);
            writer.Write(item.AttackAnimation); writer.Write(item.HitAnimation); writer.Write(item.Sound); writer.Write(item.Unknown);
        }
    }

    private static void WritePlayer(BinaryWriter writer, MatchPlayerState player)
    {
        writer.Write(player.Id.Value); writer.Write((byte)player.Status); writer.Write(player.Cash); writer.Write(player.Support);
        writer.Write(player.BigManPoints);
        writer.Write(player.Gangs.Count);
        foreach (var gang in player.Gangs.OrderBy(item => item.Id.Value))
        {
            writer.Write(gang.Id.Value); writer.Write(gang.Owner.Value); writer.Write(gang.DefinitionId); writer.Write(gang.SectorId);
            writer.Write(gang.Force); writer.Write(gang.Hidden); writer.Write(gang.HiredThisTurn);
            WriteNullableShort(writer, gang.WeaponItemId); WriteNullableShort(writer, gang.ArmorItemId); WriteNullableShort(writer, gang.MiscellaneousItemId);
        }
        writer.Write(player.HirePool.Count); foreach (var id in player.HirePool) writer.Write(id);
        writer.Write(player.PendingHires.Count); foreach (var hire in player.PendingHires) { writer.Write(hire.GangDefinitionId); writer.Write(hire.TargetSectorId); }
        writer.Write(player.ResearchProgress.Count); foreach (var pair in player.ResearchProgress.OrderBy(item => item.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        writer.Write(player.ResearchedItems.Count); foreach (var id in player.ResearchedItems.Order()) writer.Write(id);
        writer.Write(player.Inventory.Count); foreach (var pair in player.Inventory.OrderBy(item => item.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        writer.Write(player.Statistics.CashEarned); writer.Write(player.Statistics.CashSpent); writer.Write(player.Statistics.DamageInflicted);
        writer.Write(player.Statistics.Casualties); writer.Write(player.Statistics.Overthrows);
    }

    private static void WriteSector(BinaryWriter writer, MatchSectorState sector)
    {
        writer.Write(sector.Id); WriteNullableInt(writer, sector.Owner?.Value); writer.Write(sector.Tolerance); writer.Write(sector.Chaos);
        writer.Write(sector.CrackdownActive); writer.Write(sector.Sites.Count);
        foreach (var site in sector.Sites.OrderBy(item => item.Slot))
        {
            writer.Write(site.Slot); writer.Write(site.DefinitionId); writer.Write(site.Resistance); WriteNullableInt(writer, site.InfluencedBy?.Value);
        }
    }

    private static void WriteCommand(BinaryWriter writer, GameCommand command)
    {
        writer.Write(command.Player.Value); writer.Write(command.Gang.Value); writer.Write((byte)command.Action);
        WriteTarget(writer, command.Target); writer.Write(command.Repeat);
        writer.Write(command.SecondaryTarget.HasValue); if (command.SecondaryTarget is { } target) WriteTarget(writer, target);
    }

    private static void WriteTarget(BinaryWriter writer, CommandTarget target) { writer.Write((byte)target.Kind); writer.Write(target.Id); }
    private static void WriteNotification(BinaryWriter writer, GameNotification notification)
    {
        writer.Write(notification.Sequence); writer.Write(notification.Turn); writer.Write((byte)notification.Phase);
        WriteNullableByte(writer, notification.ExecutionPhase is { } phase ? (byte)phase : null); writer.Write((byte)notification.Kind);
        WriteNullableInt(writer, notification.Gang?.Value); WriteNullableInt(writer, notification.SectorId); writer.Write(notification.RelatedEventSequence ?? -1);
    }

    private static void WriteStatistics(BinaryWriter writer, Statistics value)
    {
        writer.Write(value.Combat); writer.Write(value.Defense); writer.Write(value.Stealth); writer.Write(value.Detect);
        writer.Write(value.Chaos); writer.Write(value.Control); writer.Write(value.Heal); writer.Write(value.Influence);
        writer.Write(value.Research); writer.Write(value.Strength); writer.Write(value.Blade); writer.Write(value.Range);
        writer.Write(value.Fighting); writer.Write(value.MartialArts);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes);
    }
    private static void WriteNullableInt(BinaryWriter writer, int? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    private static void WriteNullableShort(BinaryWriter writer, short? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    private static void WriteNullableByte(BinaryWriter writer, byte? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
}
