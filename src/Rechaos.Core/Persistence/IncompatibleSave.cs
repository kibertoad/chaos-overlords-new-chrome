namespace Rechaos.Core.Persistence;

/// <summary>Why a file this build cannot read is nevertheless intact.</summary>
public enum IncompatibleSaveReason
{
    /// <summary>The file was written by a build with a newer format version.</summary>
    NewerFormat,

    /// <summary>The file was written by a build with a format version this one no longer reads.</summary>
    OlderFormat,

    /// <summary>The file was written against a different set of bundled gameplay definitions.</summary>
    DifferentDefinitions
}

/// <summary>
/// Marks an <see cref="InvalidDataException"/> that means "not for this build" rather than "damaged".
/// </summary>
/// <remarks>
/// <para>
/// The difference decides what the recovery paths may do. A damaged primary may be replaced from
/// its backup generation; a primary that is merely newer than this build, or written against other
/// definitions, must be left exactly where it is. Treating the two alike let an older build
/// silently overwrite a newer save with the older generation beside it, just by opening the save
/// browser, and drew a perfectly good slot as empty so the player was invited to save over it.
/// </para>
/// <para>
/// <see cref="InvalidDataException"/> is sealed, so this is a marker on the instance rather than a
/// derived type. That is on purpose: every existing <c>catch</c> on the load path keeps catching
/// these the way it always did, and only the handful of callers that care about the distinction
/// have to ask for it.
/// </para>
/// </remarks>
public static class IncompatibleSave
{
    private const string ReasonKey = "Rechaos.IncompatibleSaveReason";

    /// <summary>An <see cref="InvalidDataException"/> carrying <paramref name="reason"/>.</summary>
    public static InvalidDataException Create(IncompatibleSaveReason reason, string message)
    {
        var exception = new InvalidDataException(message);
        exception.Data[ReasonKey] = reason;
        return exception;
    }

    /// <summary>Why the file is not for this build, or null when it is simply unreadable.</summary>
    public static IncompatibleSaveReason? ReasonOf(Exception? exception) =>
        exception?.Data[ReasonKey] as IncompatibleSaveReason?;

    /// <summary>Whether the file is intact but does not belong to this build.</summary>
    public static bool IsIncompatible(Exception? exception) => ReasonOf(exception) is not null;
}
