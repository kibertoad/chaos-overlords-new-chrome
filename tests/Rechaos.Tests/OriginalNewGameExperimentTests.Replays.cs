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
    // so a test that changes its match changes nothing another test reads. A game too long to save
    // within NativeSaveSerializer.MaximumSaveBytes is played again for each caller instead; the
    // longest recorded game, EXP-TURN-108's 124 turns, fits.
    private sealed class CachedReplay(Func<MatchState> copy, int donePresses, IReadOnlyList<(int Bound, int Result)> rolls)
    {
        // The definitions are read-only records, so every copy can share one parsed set instead of
        // deserializing and validating the embedded data again for each caller.
        private static readonly OriginalData Definitions = BundledOriginalData.Load();

        public int DonePresses { get; } = donePresses;
        public IReadOnlyList<(int Bound, int Result)> Rolls { get; } = rolls;

        public MatchState Copy() => copy();

        public static MatchState Load(byte[] snapshot)
        {
            using var stream = new MemoryStream(snapshot, writable: false);
            return NativeSaveSerializer.Load(stream, Definitions);
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

    // RULE-RNG-002: every roll(n) made on this thread while play runs, as the bound and the result.
    // The observer that was installed before is put back afterwards.
    private static IReadOnlyList<(int Bound, int Result)> ObservingRolls(Action play)
    {
        var rolls = new List<(int Bound, int Result)>();
        var outer = DeterministicRandom.RollObserver;
        DeterministicRandom.RollObserver = (bound, result) => rolls.Add((bound, result));
        try
        {
            play();
        }
        finally
        {
            DeterministicRandom.RollObserver = outer;
        }

        return rolls.AsReadOnly();
    }

    private static CachedReplay Record(ReplayInputs inputs)
    {
        MatchState? match = null;
        var donePresses = 0;
        var rolls = ObservingRolls(() => match = Play(inputs, out donePresses));

        byte[] snapshot;
        try
        {
            snapshot = NativeSaveStore.Serialize(match!);
        }
        catch (InvalidDataException)
        {
            // NativeSaveSerializer.Save refuses a save over its size limit with this exception and
            // has no other reason to throw it.
            return new CachedReplay(() => Play(inputs, out _), donePresses, rolls);
        }

        var cached = new CachedReplay(() => CachedReplay.Load(snapshot), donePresses, rolls);
        // A copy stands in for the replayed match only if the save keeps all of it. Load refuses a
        // copy that does not hash as the replayed match did, and the copy has to save to the same
        // bytes again.
        var copy = cached.Copy();
        Assert.Equal(snapshot, NativeSaveStore.Serialize(copy));
        return cached;
    }
}
