meta:
  id: fmt_data_003
  title: Item definition records in DATA/ITEMS
  license: MIT
  endian: le
doc: |
  DATA/ITEMS of Chaos Overlords: 64 item records of 166 bytes, back to back.
  Records 0 to 52 define items; records 53 to 63 are blank, with type 99.
  Every number is a signed 16-bit little-endian integer.
doc-ref: FMT-DATA-003, FND-DATA-003, SRC-RECHAOS-3561D41
seq:
  - id: records
    type: item
    repeat: eos
types:
  item:
    doc: One item definition, 166 bytes.
    seq:
      - id: name
        type: strz
        size: 30
        encoding: ASCII
        doc: The item's name. ASCII text, one NUL byte, then spaces; 30 spaces in a blank record.
        doc-ref: FND-DATA-003
      - id: id
        type: s2
        doc: The item's number, equal to the record's index in records 0 to 52, 0 in blank records.
        doc-ref: FND-DATA-003
      - id: description
        type: str
        size: 90
        encoding: ASCII
        doc: The item's description, padded with spaces, with no NUL byte.
        doc-ref: FND-DATA-003
      - id: type
        type: s2
        enum: item_type
        doc: The item's category.
        doc-ref: FND-DATA-003, SRC-RECHAOS-3561D41
      - id: research_difficulty
        type: s2
        doc: Research needed to complete the item; its low byte is each player's starting research progress.
        doc-ref: FND-RESEARCH-002
      - id: cost
        type: s2
        doc: Purchase price in dollars.
        doc-ref: FND-EQUIP-001, SRC-RECHAOS-3561D41
      - id: tech_level
        type: s2
        doc: Tech Level of the item.
        doc-ref: SRC-RECHAOS-3561D41
      - id: combat
        type: s2
        doc: Modifier to the Combat of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: defense
        type: s2
        doc: Modifier to the Defense of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: stealth
        type: s2
        doc: Modifier to the Stealth of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: detect
        type: s2
        doc: Modifier to the Detect of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: chaos
        type: s2
        doc: Modifier to the Chaos of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: control
        type: s2
        doc: Modifier to the Control of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: heal
        type: s2
        doc: Modifier to the Heal of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: influence
        type: s2
        doc: Modifier to the Influence of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: research
        type: s2
        doc: Modifier to the Research of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: strength
        type: s2
        doc: Modifier to the Strength of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: blade
        type: s2
        doc: Modifier to the Blade of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: range
        type: s2
        doc: Modifier to the Range of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: fighting
        type: s2
        doc: Modifier to the Fighting of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: martial_arts
        type: s2
        doc: Modifier to the Martial Arts of the gang that carries the item.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: attack_animation
        type: s2
        doc: Attacker's animation strip, PX07000 plus this value.
        doc-ref: FND-DATA-003, SRC-RECHAOS-3561D41
      - id: hit_animation
        type: s2
        doc: Target's animation strip, PX07100 plus this value.
        doc-ref: FND-DATA-003, SRC-RECHAOS-3561D41
      - id: sound
        type: s2
        doc: Attack sound, SND00500 plus this value.
        doc-ref: FND-AUDIO-002, SRC-RECHAOS-3561D41
      - id: combat_portrait_frame
        type: s2
        doc: Frame of the item's PX04xxx rotation strip shown in Detailed Combat.
        doc-ref: FND-AUDIO-002
enums:
  item_type:
    0:
      id: item_type_melee
      doc: A melee weapon.
    1:
      id: item_type_blade
      doc: A bladed weapon.
    2:
      id: item_type_ranged
      doc: A ranged weapon.
    3:
      id: item_type_armor
      doc: Armor.
    4:
      id: item_type_misc
      doc: A miscellaneous item.
    99:
      id: item_type_undefined
      doc: A blank record that holds no item.
