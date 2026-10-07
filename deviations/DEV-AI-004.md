# DEV-AI-004

- Departs from: RULE-AI-013
- Reason: A hire placement anchor of 164 (sector 100) always fails the rebuild's keep test, so the
  fixed scans replace it at the next refresh. The original's keep test reads the owner byte of
  sector 100, which lies past the end of the sector list, and keeps the anchor or not by whatever
  that byte holds.
- Setting: None
- Default: mandatory
- Justification: Anchor 164 is stored only for a player whose Right Hands slot is empty when the
  match is set up, which the original's setup never produces, so no match the original can play
  reaches the read. What the stray byte holds depends on memory outside the sector list that the
  rebuild does not lay out, and keeping an anchor that names no sector is logic that plainly does
  not do what it was written to do; a setting would choose between a rescan and a guess.
- Dropped: no
