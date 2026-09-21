using System.Text.Json;
using System.IO.Compression;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;

if (args.Length is < 1 or > 2)
{
    Usage();
    return 2;
}

try
{
    var snapshot = await File.ReadAllTextAsync(args[0]);
    var definitions = BundledOriginalData.Load();
    var state = MatchStateClone.FromBase64(snapshot.Trim(), definitions);
    Console.WriteLine(
        $"Snapshot is valid: hash={MatchStateHasher.ComputeSha256(state)} "
        + $"turn={state.Coordinator.Turn} phase={state.Coordinator.Phase}.");
    WriteSeats("Snapshot", state);

    if (args.Length == 1) return 0;

    var rows = await ReadSealedOrderRowsAsync(args[1]);
    var replay = new MatchReplayRecorder(state);
    foreach (var turn in rows.GroupBy(row => row.Turn).OrderBy(group => group.Key))
    {
        var first = turn.First();
        var players = turn.Select(row => new SealedPlayerOrders(
            row.PlayerId,
            row.Slot,
            DeserializeOrderDocument(row.Orders, row.Turn),
            row.OrdersHash)).ToArray();
        var expected = first.StateHash;
        if (turn.Any(row => !string.Equals(row.OrderSetHash, first.OrderSetHash, StringComparison.Ordinal)
                            || !string.Equals(row.StateHash, expected, StringComparison.Ordinal)))
            throw new InvalidDataException($"Turn {first.Turn} has inconsistent rows.");

        var before = SeatStates(replay.State);
        var actual = SealedTurnApplier.Apply(replay, new SealedOrdersView(
            first.Turn, first.OrderSetHash, players));
        var hashMatches = string.Equals(actual, expected, StringComparison.Ordinal);
        var roundTrippedHash = TryRoundTrip(replay.State, definitions, first.Turn);
        var saveRoundTripMatches = string.Equals(actual, roundTrippedHash, StringComparison.Ordinal);
        Console.WriteLine($"Turn {first.Turn}: state hash {(hashMatches ? "matches" : "MISMATCH")}; "
            + $"native save round-trip {(saveRoundTripMatches ? "matches" : "MISMATCH")}.");
        WriteSeatChanges(before, replay.State);
        if (!hashMatches)
            throw new InvalidDataException(
                $"Turn {first.Turn} replayed to {actual}, but production recorded {expected}.");
        if (!saveRoundTripMatches)
            throw new InvalidDataException(
                $"Turn {first.Turn} native save restored to {roundTrippedHash}, but the live state was {actual}.");
    }
    return 0;
}
catch (Exception exception) when (exception is IOException or JsonException or FormatException
    or InvalidDataException or ArgumentException or OverflowException)
{
    Console.Error.WriteLine($"Snapshot inspector failed: {exception.Message}");
    return 2;
}

static async Task<IReadOnlyList<SealedOrderRow>> ReadSealedOrderRowsAsync(string path)
{
    var rows = JsonSerializer.Deserialize<SealedOrderRow[]>(await File.ReadAllTextAsync(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("The sealed-order export is empty.");
    if (rows.Length == 0) throw new InvalidDataException("The sealed-order export has no rows.");
    if (rows.Any(row => string.IsNullOrWhiteSpace(row.PlayerId) || string.IsNullOrWhiteSpace(row.Orders)
                        || string.IsNullOrWhiteSpace(row.OrdersHash) || string.IsNullOrWhiteSpace(row.OrderSetHash)
                        || string.IsNullOrWhiteSpace(row.StateHash)))
        throw new InvalidDataException("A sealed-order export row is incomplete.");
    return rows;
}

static string TryRoundTrip(MatchState state, OriginalData definitions, int turn)
{
    try
    {
        return MatchStateHasher.ComputeSha256(MatchStateClone.Of(state, definitions));
    }
    catch (InvalidDataException)
    {
        var restored = ReadWithStaleFingerprint(MatchStateClone.ToBase64(state), definitions);
        var restoredHash = MatchStateHasher.ComputeSha256(restored);
        Console.Error.WriteLine($"Turn {turn}: native save fingerprint mismatch; live="
            + $"{MatchStateHasher.ComputeSha256(state)}, restored={restoredHash}.");
        WriteNativeDocumentDifferences(state, restored);
        return restoredHash;
    }
}

static MatchState ReadWithStaleFingerprint(string body, OriginalData definitions)
{
    var archive = Convert.FromBase64String(body);
    if (!archive.AsSpan().StartsWith("RCHS"u8))
    {
        using var legacy = new MemoryStream(archive, writable: false);
        return NativeSaveSerializer.LoadRewritten(legacy, definitions);
    }
    if (archive.Length < 12) throw new InvalidDataException("Native snapshot archive is truncated.");
    var length = BitConverter.ToInt32(archive, 8);
    if (length is < 0 or > NativeSaveSerializer.MaximumSaveBytes)
        throw new InvalidDataException("Native snapshot archive declares an unusable payload size.");
    var payload = new byte[length];
    using var compressed = new MemoryStream(archive, 12, archive.Length - 12, writable: false);
    using (var brotli = new BrotliStream(compressed, CompressionMode.Decompress))
    {
        brotli.ReadExactly(payload);
        if (brotli.ReadByte() != -1)
            throw new InvalidDataException("Native snapshot archive expands beyond its declared size.");
    }
    using var native = new MemoryStream(payload, writable: false);
    return NativeSaveSerializer.LoadRewritten(native, definitions);
}

static void WriteNativeDocumentDifferences(MatchState live, MatchState restored)
{
    using var expected = JsonDocument.Parse(SerializeNative(live));
    using var actual = JsonDocument.Parse(SerializeNative(restored));
    var differences = Rechaos.Core.Validation.JsonStateDiffer.Compare(
        expected.RootElement, actual.RootElement)
        .Where(difference => difference.Path != "$.stateSha256")
        .Take(20)
        .ToArray();
    foreach (var difference in differences)
        Console.Error.WriteLine($"  {difference.Kind}: {difference.Path}: "
            + $"live={difference.Expected ?? "<missing>"}, restored={difference.Actual ?? "<missing>"}");
}

static byte[] SerializeNative(MatchState state)
{
    using var stream = new MemoryStream();
    NativeSaveSerializer.Save(stream, state);
    return stream.ToArray();
}

static OrderDocument DeserializeOrderDocument(string orders, int turn) =>
    JsonSerializer.Deserialize<OrderDocument>(orders)
    ?? throw new InvalidDataException($"Turn {turn} has an empty order document.");

static IReadOnlyDictionary<int, SeatState> SeatStates(MatchState state) => state.Players.ToDictionary(
    player => player.Id.Value,
    player => new SeatState(player.Setup.Controller, player.Status, player.Gangs.Count(gang => gang.IsActive)));

static void WriteSeats(string label, MatchState state) => Console.WriteLine(
    $"{label} seats: " + string.Join(", ", SeatStates(state).OrderBy(entry => entry.Key).Select(
        entry => $"{entry.Key}={entry.Value.Controller}/{entry.Value.Status}/gangs:{entry.Value.ActiveGangs}")) + ".");

static void WriteSeatChanges(IReadOnlyDictionary<int, SeatState> before, MatchState after)
{
    var current = SeatStates(after);
    var changes = current.Where(entry => !before.TryGetValue(entry.Key, out var prior) || prior != entry.Value)
        .OrderBy(entry => entry.Key)
        .Select(entry => $"slot {entry.Key}: {before.GetValueOrDefault(entry.Key)} -> {entry.Value}")
        .ToArray();
    if (changes.Length > 0) Console.WriteLine("  " + string.Join("; ", changes));
}

static void Usage() => Console.Error.WriteLine(
    "Usage: Rechaos.SnapshotInspector <snapshot-base64.txt> [sealed-orders.json]");

file sealed record SealedOrderRow(
    int Turn,
    string OrderSetHash,
    string StateHash,
    string PlayerId,
    int Slot,
    string OrdersHash,
    string Orders);

file sealed record SeatState(
    PlayerController Controller,
    Rechaos.Core.GameModel.PlayerStatus Status,
    int ActiveGangs)
{
    public override string ToString() => $"{Controller}/{Status}/gangs:{ActiveGangs}";
}
