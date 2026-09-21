using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The queue behind the rolling autosave: which capture reaches disk, and when the writer is
/// allowed to skip proving the primary it is about to demote to the backup generation.
/// </summary>
public sealed class RollingAutoSaveTests
{
    [Fact]
    public void TheFirstWriteOfAProcessDoesNotTrustTheExistingPrimary()
    {
        using var writer = new RecordingWriter();
        var autoSave = NewQueue(writer, out _);

        autoSave.Capture(CreateMatch());
        autoSave.Flush();

        Assert.False(Assert.Single(writer.Writes).TrustExistingPrimary);
    }

    [Fact]
    public void AGenerationThisProcessVerifiedIsTrustedByTheNextWrite()
    {
        using var writer = new RecordingWriter();
        var autoSave = NewQueue(writer, out _);
        var match = CreateMatch();

        autoSave.Capture(match);
        autoSave.Flush();
        autoSave.Capture(match);
        autoSave.Flush();

        Assert.Equal(new[] { false, true }, Trust(writer));
    }

    [Fact]
    public void AFailedWriteStopsTheNextOneFromTrustingThePrimary()
    {
        using var writer = new RecordingWriter();
        var autoSave = NewQueue(writer, out var failures);
        var match = CreateMatch();
        autoSave.Capture(match);
        autoSave.Flush();

        // The generation this process verified is exactly what the failure may have damaged: a
        // write can stop partway through promoting its own, and a trusted primary is moved over
        // the known-good backup without being read back first.
        writer.FailNextWith(new IOException("the volume went away"));
        autoSave.Capture(match);
        autoSave.Flush();
        autoSave.Capture(match);
        autoSave.Flush();

        Assert.Equal(new[] { false, true, false }, Trust(writer));
        Assert.Equal(match.Coordinator.Turn, Assert.Single(failures).Turn);
    }

    [Fact]
    public void ForgettingTheVerifiedGenerationStopsTheNextWriteFromTrustingIt()
    {
        using var writer = new RecordingWriter();
        var autoSave = NewQueue(writer, out _);
        var match = CreateMatch();
        autoSave.Capture(match);
        autoSave.Flush();

        // What the autosave load path reports after recovering, or failing to repair, the primary.
        autoSave.ForgetVerifiedPrimary();
        autoSave.Flush();
        autoSave.Capture(match);
        autoSave.Flush();

        Assert.Equal(new[] { false, false }, Trust(writer));
    }

    [Fact]
    public void TurnsCapturedWhileTheWorkerRunsCoalesceIntoTheNewestOne()
    {
        using var writer = new RecordingWriter();
        var autoSave = NewQueue(writer, out _);
        var match = CreateMatch();
        var first = MatchStateHasher.ComputeFingerprint(match);

        writer.BlockUntilReleased();
        autoSave.Capture(match);
        autoSave.Capture(match);
        match.FinishUpkeep();
        autoSave.Capture(match);
        writer.Release();
        autoSave.Flush();

        // Three turns went in behind one worker. The capture in the middle is dropped rather than
        // written: the newest one would have overwritten it the moment it landed.
        Assert.Equal(
            new[] { first, MatchStateHasher.ComputeFingerprint(match) },
            writer.Writes.Select(write => write.Fingerprint).ToArray());
    }

    [Fact]
    public void FlushingWaitsForTheWorkerToFinish()
    {
        using var writer = new RecordingWriter();
        var autoSave = NewQueue(writer, out _);
        writer.BlockUntilReleased();
        autoSave.Capture(CreateMatch());
        var releasing = new Thread(() =>
        {
            writer.WaitUntilEntered();
            writer.Release();
        });
        releasing.Start();

        autoSave.Flush();

        // The game thread reads this file itself, so a flush that returned early would let the
        // save browser open a file the worker is still renaming over.
        Assert.Single(writer.Writes);
        releasing.Join();
    }

    private static bool[] Trust(RecordingWriter writer) =>
        writer.Writes.Select(write => write.TrustExistingPrimary).ToArray();

    private static RollingAutoSave NewQueue(
        RecordingWriter writer, out List<(int Turn, Exception Failure)> failures)
    {
        var reported = new List<(int Turn, Exception Failure)>();
        failures = reported;
        return new RollingAutoSave(
            writer.Write, (turn, exception) => reported.Add((turn, exception)));
    }

    /// <summary>A stand-in for the durable writer, observed and steered from the test thread.</summary>
    private sealed class RecordingWriter : IDisposable
    {
        private readonly List<(string Fingerprint, bool TrustExistingPrimary)> _writes = [];
        private readonly ManualResetEventSlim _released = new(initialState: true);
        private readonly ManualResetEventSlim _entered = new(initialState: false);
        private Exception? _failure;

        public IReadOnlyList<(string Fingerprint, bool TrustExistingPrimary)> Writes
        {
            get { lock (_writes) return _writes.ToArray(); }
        }

        public void FailNextWith(Exception failure) => _failure = failure;

        public void BlockUntilReleased() => _released.Reset();

        public void Release() => _released.Set();

        /// <summary>Blocks until a worker is inside a write, so a release cannot come too early.</summary>
        public void WaitUntilEntered() => _entered.Wait();

        public void Dispose()
        {
            _released.Dispose();
            _entered.Dispose();
        }

        public void Write(
            ReadOnlyMemory<byte> snapshot, OriginalData definitions, bool trustExistingPrimary)
        {
            _entered.Set();
            _released.Wait();
            // The capture is taken on the calling thread precisely so that the worker sees the
            // turn as it was, not as it has since become; read it back to prove that it did.
            using var stream = new MemoryStream(snapshot.ToArray(), writable: false);
            var fingerprint = MatchStateHasher.ComputeFingerprint(
                NativeSaveSerializer.Load(stream, definitions));
            lock (_writes) _writes.Add((fingerprint, trustExistingPrimary));
            if (Interlocked.Exchange(ref _failure, null) is { } failure) throw failure;
        }
    }

    private static MatchState CreateMatch()
    {
        var definitions = BundledOriginalData.Load();
        return OriginalMatchFactory.Create(definitions, new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996,
            [
                new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human),
                new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Computer)
            ]));
    }
}
