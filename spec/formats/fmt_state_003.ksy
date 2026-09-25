meta:
  id: fmt_state_003
  title: Per-gang combat record of the last resolution
  endian: le
  license: MIT
doc: |
  One 10-byte combat record. The game keeps 486 of them at 0x004A11E8,
  10 * (player * 81 + roster slot).
doc-ref: FMT-STATE-003, FND-COMBAT-004, FND-COMBAT-008, FND-AI-010, FND-PLATFORM-003
seq:
  - id: definition
    type: u1
    doc: The gang's definition, copied from offset 0x01 of the gang record.
    doc-ref: FND-COMBAT-004, FND-COMBAT-008, FND-STATE-005
  - id: force_start
    type: s1
    doc: Force at the start of the combat phase.
    doc-ref: FND-COMBAT-004
  - id: force_final
    type: s1
    doc: Force after the combat phase.
    doc-ref: FND-COMBAT-004
  - id: force_shown
    type: s1
    doc: Force Detailed Combat draws.
    doc-ref: FND-COMBAT-004
  - id: damage_dealt
    type: s1
    doc: Damage of the opening attack, or -1 when the target evaded.
    doc-ref: FND-COMBAT-004
  - id: retaliation_taken
    type: s1
    doc: Retaliation damage taken from the target.
    doc-ref: FND-AI-010, FND-COMBAT-004
  - id: weapon
    type: s1
    doc: Weapon item at the time of the fight, or -1.
    doc-ref: FND-COMBAT-004
  - id: armor
    type: s1
    doc: Armor item at the time of the fight, or -1.
    doc-ref: FND-COMBAT-004
  - id: misc
    type: s1
    doc: Miscellaneous item at the time of the fight, or -1.
    doc-ref: FND-COMBAT-004
  - id: police_damage
    type: s1
    doc: Damage dealt by the police, or -1 for none.
    doc-ref: FND-COMBAT-004
