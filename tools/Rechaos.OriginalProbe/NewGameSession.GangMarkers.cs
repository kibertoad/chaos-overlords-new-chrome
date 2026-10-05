namespace Rechaos.OriginalProbe;

/// <summary>
/// One drawing step of the gang-status markers (FND-UI-024), with the post-dump step it came in, 0
/// before the first: a full city redraw for <c>Player</c> (kind 0), frame <c>Frame</c> drawn at
/// <c>Sector</c> for a sector holding the player's gang (kind 1), the saved cell copied back over
/// <c>Sector</c>, the last sector given the incoming mark (kind 2), and the incoming mark, frame 8,
/// drawn at <c>Sector</c> (kind 3).
/// </summary>
internal sealed record GangMarkerDraw(int Step, int Kind, int Player, int Sector, int Frame);

internal sealed partial class NewGameSession
{
    private readonly List<GangMarkerDraw> _gangMarkers = [];
    private int _postDumpStep;

    // RULE-UI-006, FND-UI-024, EXP-UI-004: the marker function fn_00412BF7(player, sector) reaches
    // one of three copies for each sector it draws: the frame of a sector holding the player's gang
    // at 0x00412DFB, with the frame in [ebp-4]; the saved cell copied back over the sector at
    // 0x004906A4 at 0x00412EB5; and the incoming mark at 0x00412FF8. The full city redraw
    // fn_004123CC starts each map. The log keeps every drawing from the last full redraw before the
    // dump on, so it ends with the map the dump and each later step leave.
    private void ArmGangMarkers()
    {
        _process.SetBreakpoint(OriginalAddresses.CityRedraw, context =>
        {
            if (_postDumpStep == 0) _gangMarkers.Clear();
            _gangMarkers.Add(new GangMarkerDraw(_postDumpStep, 0, context.Argument(0), -1, -1));
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.GangMarkerFrame, context => _gangMarkers.Add(new GangMarkerDraw(
            _postDumpStep, 1, _process.ReadInt32(context.Ebp + 8), _process.ReadInt32(context.Ebp + 12),
            _process.ReadInt32(context.Ebp - 4))), quiet: true);
        _process.SetBreakpoint(OriginalAddresses.GangMarkerRestore, context => _gangMarkers.Add(new GangMarkerDraw(
            _postDumpStep, 2, _process.ReadInt32(context.Ebp + 8),
            _process.ReadInt32(OriginalAddresses.GangMarkerSavedSector), -1)), quiet: true);
        _process.SetBreakpoint(OriginalAddresses.GangMarkerIncoming, context => _gangMarkers.Add(new GangMarkerDraw(
            _postDumpStep, 3, _process.ReadInt32(context.Ebp + 8), _process.ReadInt32(context.Ebp + 12), 8)), quiet: true);
    }
}
