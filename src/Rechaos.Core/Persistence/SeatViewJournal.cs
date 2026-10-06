namespace Rechaos.Core.Persistence;

/// <summary>
/// Marks the refusal to replay a journal that was recorded on one seat's view of an online match.
/// </summary>
/// <remarks>
/// <para>
/// A client playing from views holds its seat's view and nothing else: no random state, no hidden
/// gang and no other seat's orders (docs/MULTIPLAYER.md, "Per-seat views"). The journal it records
/// is that view at the planning entry and the seat's planning of the turn on it, which is enough to
/// look into an interface fault and too little to resolve a turn. Replaying it as a match would
/// stop at the first resolution with an error about views, so
/// <see cref="MatchReplaySerializer.LoadAndReplay(Stream, Assets.OriginalData)"/> refuses it before
/// anything runs, with the reason. <see cref="MatchReplaySerializer.TryLoadResumable"/> still reads
/// it, as the view with the planning replayed on it.
/// </para>
/// <para>
/// <see cref="InvalidDataException"/> is sealed, so this is a marker on the instance, as
/// <see cref="IncompatibleSave"/> is.
/// </para>
/// </remarks>
public static class SeatViewJournal
{
    private const string SeatKey = "Rechaos.SeatViewJournalSeat";

    /// <summary>The refusal for a journal recorded on <paramref name="seat"/>'s view.</summary>
    public static InvalidDataException Refusal(int seat)
    {
        var exception = new InvalidDataException(
            $"This journal was recorded on seat {seat}'s view of an online match. It holds that "
            + "seat's planning of one turn on what the seat could see, without the random state or "
            + "the other seats, so it cannot be replayed as a match. A report filed after the match "
            + "ended carries the whole match instead.");
        exception.Data[SeatKey] = seat;
        return exception;
    }

    /// <summary>The seat whose view the refused journal was recorded on, or null for any other failure.</summary>
    public static int? SeatOf(Exception? exception) => exception?.Data[SeatKey] as int?;
}
