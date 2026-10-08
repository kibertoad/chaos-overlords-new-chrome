# DEV-HELP-003

- Departs from: FMT-HELP-001
- Reason: The help viewer lays out the help file's paragraphs (FND-HELP-006) on its pixel-font
  grid, where the Windows help program would draw them in Times New Roman at the sizes the font
  descriptors give. Every face and size is drawn with the one pixel font, 6 pixels a character
  and 9 pixels a line. A distance is first taken to twips as the stored half-points times 10
  less 5 (SRC-WINHLP32-WINE, SRC-HELPDECO). A left, right or first-line indent becomes whole
  character columns: its twips divided by 15, the help program's pixels at 96 DPI, then by the
  6-pixel cell, rounded to the nearest. A centred line is centred in whole columns. A vertical
  distance becomes pixels at 9 pixels, one line of the pixel font, for every 221.48 twips, the
  height of a 10-point Times New Roman line (its ascent and descent, 2,268 units of a
  2,048-unit em), rounded to the nearest pixel. Between two paragraphs the space after the
  first and the space before the second are added, then rounded; a negative space counts as
  none. The first paragraph's space before is not drawn, since the viewer starts a topic's text
  a fixed distance under its title. A paragraph's line spacing sets the distance between its
  lines as SRC-RTF-15 defines it, a minimum when positive and an exact distance when negative,
  but never less than the pixel font's 9-pixel line. An empty paragraph is one empty line. No
  capture of the help program and no source says whether it adds the space after one paragraph
  to the space before the next; the viewer adds them, as RTF defines each as a space of its
  own.
- Setting: None
- Default: mandatory
- Justification: The original never shows the help file (RULE-HELP-001), so no screen of the game
  exists to match, and the Windows help program it would have started is not part of current
  Windows. The viewer keeps every distance the file records, in proportion to a line of its
  text, and a setting for another rounding would give the player nothing to choose between.
- Tests: tests/Rechaos.Tests/ExtractedHelpStoreTests.cs
- Dropped: no
