using System.Collections.Concurrent;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // A human seat of the recorded setup: slot, the name the setup flags give it and the portrait.
    private sealed record ReplaySeat(int Slot, string Name, short Portrait);

    // Everything of a recording that Play reads. Two recordings with equal inputs replay the same
    // game: EXP-EQUIP-003, EXP-ATTACK-003 and EXP-TURN-096 replay EXP-TURN-026, for example, and
    // only add what the probe recorded at the endpoint.
    private sealed record ReplayInputs(
        int Scenario,
        int TurnLimit,
        int Seed,
        int Mentality,
        IReadOnlyList<ReplaySeat> Humans,
        int HumanController,
        int DoneCount,
        IReadOnlyList<RecordedOrder> Orders,
        IReadOnlyList<RecordedHire> Hires,
        IReadOnlyList<RecordedPlanning> Planning)
    {
        public static ReplayInputs Of(RecordedRun recorded) => new(
            recorded.Term("scenario", 0),
            recorded.Term("turn_limit", 0),
            recorded.Seed,
            recorded.Term("mentality", 0),
            recorded.Humans.Select(slot => new ReplaySeat(
                slot.Value, Name(recorded, slot.Value), (short)recorded.Term("portrait", slot.Value))).ToArray(),
            recorded.Term("controller", recorded.Humans[0].Value),
            recorded.DoneCount,
            recorded.Orders,
            recorded.Hires,
            recorded.Planning);

        // The records serialize every value, in declaration order.
        public string Key() => JsonSerializer.Serialize(this);
    }

    // A replayed match of its own, with the Done presses the replay made and every roll(n) it made,
    // as the bound passed and the result (RULE-RNG-002).
    private sealed record Replay(MatchState Match, int DonePresses, IReadOnlyList<(int Bound, int Result)> Rolls);

    // The game played once, kept as a native save. Each caller gets a match loaded from those bytes,
    // so a test that changes its match changes nothing another test reads.
    private sealed class CachedReplay(byte[] snapshot, int donePresses, IReadOnlyList<(int Bound, int Result)> rolls)
    {
        public int DonePresses { get; } = donePresses;
        public IReadOnlyList<(int Bound, int Result)> Rolls { get; } = rolls;

        public MatchState Copy()
        {
            using var stream = new MemoryStream(snapshot, writable: false);
            return NativeSaveSerializer.Load(stream, BundledOriginalData.Load());
        }
    }

    // One entry per distinct game, filled on first use. Lazy runs the replay once even when
    // theories on several threads ask for the same game together.
    private static readonly ConcurrentDictionary<string, Lazy<CachedReplay>> Replays = new(StringComparer.Ordinal);

    /// <summary>
    /// The rebuild's replay of <paramref name="recorded"/> with every deviation switched off. A run
    /// whose inputs equal those of a run replayed before shares that replay.
    /// </summary>
    private static Replay Replayed(RecordedRun recorded)
    {
        var inputs = ReplayInputs.Of(recorded);
        var cached = Replays.GetOrAdd(inputs.Key(), _ => new Lazy<CachedReplay>(
            () => Record(inputs), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        return new Replay(cached.Copy(), cached.DonePresses, cached.Rolls);
    }

    private static CachedReplay Record(ReplayInputs inputs)
    {
        var rolls = new List<(int Bound, int Result)>();
        var observer = DeterministicRandom.RollObserver;
        DeterministicRandom.RollObserver = (bound, result) => rolls.Add((bound, result));
        MatchState match;
        int donePresses;
        try
        {
            match = Play(inputs, out donePresses);
        }
        finally
        {
            DeterministicRandom.RollObserver = observer;
        }

        var snapshot = NativeSaveStore.Serialize(match);
        var cached = new CachedReplay(snapshot, donePresses, rolls.ToArray().AsReadOnly());
        // A copy stands in for the replayed match only if the save keeps all of it: it has to hash
        // as the replayed match does and save to the same bytes again.
        var copy = cached.Copy();
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(copy));
        Assert.Equal(snapshot, NativeSaveStore.Serialize(copy));
        return cached;
    }
}
