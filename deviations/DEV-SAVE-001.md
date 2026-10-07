# DEV-SAVE-001

- Departs from: FMT-SAVE-001, FMT-SAVE-002
- Replaces: FMT-SAVE-001, FMT-SAVE-002
- Reason: The rebuild neither reads nor writes the original's save files. It keeps its own save
  format, with a version number and bounded readers. Each player's selected sector
  (`cursor_sectors`, FND-SAVE-003) goes into the small file the save browser keeps beside each
  save, the autosave and the crash-recovery save, and a load restores it from there. A save whose
  companion file is missing or belongs to another file, and a load that falls back to the backup
  generation, start every player on the sector of its roster slot 0, as a new match starts.
  An online match the client takes up or resumes starts every player on that sector too, since
  the server keeps no selection (DEV-NET-001).
- Setting: None
- Default: mandatory
- Justification: What a player can do in a match is the same whichever format holds it, and the
  rebuild's format adds a version number and bounded readers. A setting would need a reader and
  writer for the original's format, which is a separate scope decision (2026-09-10).
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs
- Dropped: no

Decided 2026-09-10 ("Save compatibility scope").
