namespace Rechaos.Game;

/// <summary>RULE-AUDIO-001, RULE-AUDIO-002: keep a program's first/last tracks,
/// except that a positionless resume removes its end bound.</summary>
public sealed class SoundtrackProgramCursor<TTrack>(IReadOnlyList<TTrack> orderedDisc)
    where TTrack : class
{
    private TTrack[] _tracks = [];
    private int _index;

    public TTrack? Current => _tracks.Length == 0 ? null : _tracks[_index];
    public bool AtEnd => _tracks.Length == 0 || _index == _tracks.Length - 1;

    public void Start(IReadOnlyList<TTrack> program)
    {
        _tracks = program.ToArray();
        _index = 0;
    }

    public bool Advance()
    {
        if (AtEnd) return false;
        _index++;
        return true;
    }

    public void ResumeThroughDiscEnd()
    {
        if (Current is not { } current) return;
        var remainder = orderedDisc.SkipWhile(track => !EqualityComparer<TTrack>.Default.Equals(track, current))
            .ToArray();
        if (remainder.Length == 0) throw new InvalidOperationException("Current track is outside the disc.");
        _tracks = remainder;
        _index = 0;
    }
}
