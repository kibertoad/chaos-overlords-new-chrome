# DEV-UI-016

- Departs from: RULE-UI-013, RULE-UI-014, SCR-UI-009, FMT-DATA-004
- Replaces: FMT-DATA-004
- Reason: Only the 16-bit image set is drawn, and there is no Thousands of Colors option. The
  original draws the same set at any display deeper than 8 bits, and loads the 256-colour palette
  of `DATA/CLT00002` and the 8-bit set only on an 8-bit display, so the game never reads either.
  The extractor still copies both into the asset pack, and decodes the 8-bit set there, where
  nothing loads them.
- Setting: None
- Default: mandatory
- Justification: The original defaults to the 16-bit set. The 8-bit set holds the same pictures
  reduced for 256-colour displays, which no current display is, so a setting would switch to a
  poorer copy of the same pictures.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no
