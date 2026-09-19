using Rechaos.Game;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineHistoryPresentationTests
{
    [Fact]
    public void RowNamesThePlayerTheCodeAndTheRole()
    {
        Assert.Equal("ADA  CODE1234  HOST", OnlineHistoryPresentation.Row(Recovery()));
        Assert.Equal(
            "ADA  CODE1234  PLAYER",
            OnlineHistoryPresentation.Row(Recovery() with { IsHost = false }));
    }

    /// <summary>A seat this build can take carries nothing beside it.</summary>
    [Fact]
    public void CompatibleSessionHasNoNote()
    {
        Assert.Null(OnlineHistoryPresentation.Note(Recovery()));
        Assert.Equal(OnlineHistoryPresentation.Hint, OnlineHistoryPresentation.Footer(Recovery()));
    }

    /// <summary>
    /// A session from another build is still listed, so the row itself has to say why the seat
    /// cannot be taken; the line under the list then spells it out for the selected one.
    /// </summary>
    [Fact]
    public void IncompatibleSessionIsMarkedBesideItsRow()
    {
        var incompatible = Incompatible();

        Assert.Equal(OnlineHistoryPresentation.IncompatibleNote,
            OnlineHistoryPresentation.Note(incompatible));
        Assert.Equal(OnlineHistoryPresentation.IncompatibleReason,
            OnlineHistoryPresentation.Footer(incompatible));
    }

    /// <summary>Nothing selected is nothing to explain.</summary>
    [Fact]
    public void FooterWithoutASelectionIsTheHint()
    {
        Assert.Equal(OnlineHistoryPresentation.Hint, OnlineHistoryPresentation.Footer(null));
    }

    /// <summary>
    /// The note is drawn right-aligned in the row and the membership is fitted to what is left of
    /// it, so the two can only meet if the note itself no longer fits.
    /// </summary>
    [Fact]
    public void NoteFitsBesideTheLongestMembershipARowCanHold()
    {
        var row = OnlineConnectLayout.HistoryRow(0);
        var note = OnlineHistoryPresentation.IncompatibleNote.Length * OriginalFontLayout.CellWidth;

        Assert.True(note + 16 < row.Width);
    }

    private static MultiplayerRecovery Incompatible() =>
        Recovery() with { SessionVersion = MultiplayerSessionVersion.Current + 1 };

    private static MultiplayerRecovery Recovery() => new(
        MultiplayerRecovery.CurrentFormatVersion,
        "https://games.example.test/",
        "match-1",
        "player-1",
        "cop_secret",
        "CODE1234",
        "ADA",
        IsHost: true,
        CleanExit: false,
        Completed: false);
}
