# DEV-GFX-002

- Departs from: FMT-GFX-001, SCR-EVENT-001
- Reason: PX06008 supplies 157 valid rows, but the original uploads 158 rows
  (FND-GFX-005), reading the top row from outside its allocated pixel buffer.
  The rebuild draws that top row black and positions the 157 valid rows below
  it without stretching. Its 242nd column remains the original black padding.
- Setting: None
- Default: mandatory
- Justification: Reading unrelated memory is unsafe and produces undefined
  artwork that no player can rely on. A setting cannot reproduce the original
  process's unrecorded memory. A black row preserves the valid pixels and their
  placement without inventing additional illustration content.
- Dropped: no
