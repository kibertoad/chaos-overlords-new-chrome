namespace Rechaos.OriginalProbe;

/// <summary>
/// One movie the intro played (FND-VIDEO-002): its name, the frame count of its header, the frame
/// counter of the movie slot at each frame shown, the milliseconds from the first movie's first
/// frame to each, and the counter when the slot was closed.
/// </summary>
internal sealed record IntroMovieRecord(string Name, int Frames, List<int> Shown, List<long> Milliseconds)
{
    public int ClosedAt { get; set; } = -1;
}

internal sealed partial class NewGameSession
{
    private readonly List<IntroMovieRecord> _introMovies = [];
    private readonly System.Diagnostics.Stopwatch _introClock = new();

    // RULE-VIDEO-001: with --watch-intro the probe lets both movies play out and records each frame
    // the frame helper shows (EXP-VIDEO-001): the call of SmackDoFrame at IntroFrameCall, the
    // movie slot 0's frame counter at IntroFrameCounter, its Smack handle at IntroSmackHandle, whose
    // frame count is at offset 0xC, and the name the intro copied to IntroMovieName. The close
    // helper ends each movie.
    private void ArmIntro()
    {
        _process.SetBreakpoint(OriginalAddresses.IntroFrameCall, _ =>
        {
            var name = System.Text.Encoding.ASCII.GetString(_process.Read(OriginalAddresses.IntroMovieName, 13)).TrimEnd('\0');
            if (_introMovies.Count == 0 || _introMovies[^1].Name != name || _introMovies[^1].ClosedAt >= 0)
            {
                if (!_introClock.IsRunning) _introClock.Start();
                var smack = (uint)_process.ReadInt32(OriginalAddresses.IntroSmackHandle);
                _introMovies.Add(new IntroMovieRecord(name, _process.ReadInt32(smack + 0xC), [], []));
            }
            _introMovies[^1].Shown.Add(_process.ReadInt32(OriginalAddresses.IntroFrameCounter));
            _introMovies[^1].Milliseconds.Add(_introClock.ElapsedMilliseconds);
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.IntroMovieClose, _ =>
        {
            if (_introMovies.Count > 0 && _introMovies[^1].ClosedAt < 0)
                _introMovies[^1].ClosedAt = _process.ReadInt32(OriginalAddresses.IntroFrameCounter);
        }, quiet: true);
    }

    private bool IntroPlayedOut() => _introMovies.Count(movie => movie.ClosedAt >= 0) >= 2;
}
