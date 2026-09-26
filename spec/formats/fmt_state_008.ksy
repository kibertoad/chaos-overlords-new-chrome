meta:
  id: fmt_state_008
  title: Combat result row of one sector
  endian: le
  license: MIT
doc: |
  One 150-byte combat result row. The game keeps 64, one per sector, at
  0x004A8888 + sector * 0x96.
doc-ref: FMT-STATE-008, FND-COMBAT-008, FND-SAVE-001
seq:
  - id: players
    type: player_row
    repeat: expr
    repeat-expr: 6
    doc: One row per player slot, in slot order.
    doc-ref: FND-COMBAT-008
  - id: police_hit
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Per player slot, 1 when the police found one of the player's gangs in the sector.
    doc-ref: FND-COMBAT-008
types:
  player_row:
    seq:
      - id: entries
        type: entry
        repeat: expr
        repeat-expr: 6
        doc: The player's gangs that fought in the sector, in roster order.
        doc-ref: FND-COMBAT-008
  entry:
    seq:
      - id: gang
        type: s2
        doc: The gang's index, player * 81 + roster slot, or -1 for an empty entry.
        doc-ref: FND-COMBAT-008
      - id: target
        type: s2
        doc: The index of the gang's Attack target, or -1 when its action was not Attack.
        doc-ref: FND-COMBAT-008
