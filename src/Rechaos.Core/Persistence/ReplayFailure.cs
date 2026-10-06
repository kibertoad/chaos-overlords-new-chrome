namespace Rechaos.Core.Persistence;

/// <summary>
/// Marks an <see cref="InvalidDataException"/> that means this build's rules did not reproduce a
/// recorded step, as distinct from a journal that is damaged or written in another format.
/// </summary>
/// <remarks>
/// <see cref="InvalidDataException"/> is sealed, so this is a marker on the instance, as
/// <see cref="IncompatibleSave"/> is: every existing <c>catch</c> keeps catching these, and only a
/// caller that reports the difference has to ask for it.
/// </remarks>
public static class ReplayDivergence
{
    private const string StepKey = "Rechaos.ReplayDivergedStep";

    /// <summary>An <see cref="InvalidDataException"/> saying step <paramref name="step"/> diverged.</summary>
    internal static InvalidDataException Create(int step, string message)
    {
        var exception = new InvalidDataException(message);
        exception.Data[StepKey] = step;
        return exception;
    }

    /// <summary>
    /// The zero-based step whose result this build did not reproduce, -1 when the opening snapshot
    /// itself does not match the journal, or null when <paramref name="exception"/> is no divergence.
    /// </summary>
    public static int? StepOf(Exception? exception) => exception?.Data[StepKey] as int?;
}

/// <summary>Why a replay journal could not be opened for playback.</summary>
public enum ReplayFailureKind
{
    /// <summary>There is no journal at the path.</summary>
    Missing,

    /// <summary>The journal is intact but was written for another build (<see cref="IncompatibleSave"/>).</summary>
    Incompatible,

    /// <summary>The journal reads, but this build's rules do not reproduce one of its steps.</summary>
    Diverged,

    /// <summary>The journal is truncated, malformed or fails its own checks.</summary>
    Damaged,

    /// <summary>The file exists but could not be read, for example because access was refused.</summary>
    Unreadable
}

/// <summary>A replay failure classified for reporting to the player.</summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="Incompatibility">For <see cref="ReplayFailureKind.Incompatible"/>, why.</param>
/// <param name="DivergedStep">
/// For <see cref="ReplayFailureKind.Diverged"/>, the zero-based step that did not reproduce, or -1
/// for the opening snapshot.
/// </param>
public sealed record ReplayFailure(
    ReplayFailureKind Kind,
    IncompatibleSaveReason? Incompatibility = null,
    int? DivergedStep = null)
{
    /// <summary>Classifies an exception a replay load or playback threw.</summary>
    public static ReplayFailure Of(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (IncompatibleSave.ReasonOf(exception) is { } reason)
            return new ReplayFailure(ReplayFailureKind.Incompatible, Incompatibility: reason);
        if (ReplayDivergence.StepOf(exception) is { } step)
            return new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: step);
        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => new ReplayFailure(ReplayFailureKind.Missing),
            InvalidDataException => new ReplayFailure(ReplayFailureKind.Damaged),
            _ => new ReplayFailure(ReplayFailureKind.Unreadable)
        };
    }
}
