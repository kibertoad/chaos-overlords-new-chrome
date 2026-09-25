meta:
  id: fmt_state_001
  title: Gang record, one per player and roster slot
  endian: le
  license: MIT
doc: |
  One 32-byte gang record. The game keeps 486 of them at 0x00498DA8,
  player-major: player * 0xA20 + roster slot * 0x20.
doc-ref: FMT-STATE-001, FND-HIRE-002, FND-UI-036, FND-PLATFORM-003, FND-STATE-002
seq:
  - id: player
    type: s1
    doc: Owning player slot, 0 to 5.
    doc-ref: FND-STATE-002
  - id: definition
    type: u1
    doc: Record number in the gang definition table read from DATA/Gangs.
    doc-ref: FND-STATE-002
  - id: sector
    type: s1
    enum: gang_sector
    doc: Sector 0 to 63, or 100 when the slot holds no living gang.
    doc-ref: FND-DETECT-001, FND-GANG-003, FND-HIRE-002, FND-TURN-004, FND-TURN-005, FND-UI-036
  - id: force
    type: s1
    doc: Current Force.
    doc-ref: FND-STATE-002
  - id: weapon
    type: s1
    doc: Item record number of the equipped weapon, or -1.
    doc-ref: FND-AI-007, FND-GANG-001, FND-GANG-003
  - id: armor
    type: s1
    doc: Item record number of the equipped armor, or -1.
    doc-ref: FND-GANG-001, FND-GANG-003
  - id: misc
    type: s1
    doc: Item record number of the equipped miscellaneous item, or -1.
    doc-ref: FND-GANG-001, FND-GANG-003
  - id: action
    type: u1
    enum: action
    doc: The action carried out this turn.
    doc-ref: FND-AI-007, FND-COMBAT-004, FND-HIDE-001, FND-TURN-002
  - id: target
    type: s1
    doc: First target byte of the action.
    doc-ref: FND-COMBAT-004, FND-EQUIP-007, FND-STATE-002, FND-TURN-002
  - id: target_2
    type: s1
    doc: Second target byte of the action.
    doc-ref: FND-COMBAT-004, FND-EQUIP-007, FND-STATE-002
  - id: repeat_action
    type: u1
    enum: action
    doc: Recurring action, copied into action at the start of each turn.
    doc-ref: FND-HIDE-001, FND-TURN-002, FND-TURN-004
  - id: repeat_target
    type: s1
    doc: Recurring action's target.
    doc-ref: FND-TURN-002, FND-TURN-004
  - id: visible_to
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: One byte per observing player; nonzero when that player can see the gang.
    doc-ref: FND-DETECT-001, FND-UI-036
  - id: combat
    type: s1
    doc: Effective Combat.
    doc-ref: FND-AI-004, FND-GANG-001
  - id: defense
    type: s1
    doc: Effective Defense.
    doc-ref: FND-AI-004, FND-GANG-001
  - id: stealth
    type: s1
    doc: Effective Stealth.
    doc-ref: FND-DETECT-001, FND-GANG-001
  - id: detect
    type: s1
    doc: Effective Detect.
    doc-ref: FND-DETECT-001, FND-GANG-001
  - id: chaos
    type: s1
    doc: Effective Chaos.
    doc-ref: FND-STATE-002
  - id: control
    type: s1
    doc: Effective Control.
    doc-ref: FND-AI-004, FND-GANG-001
  - id: heal
    type: s1
    doc: Effective Heal.
    doc-ref: FND-AI-004, FND-GANG-001
  - id: influence
    type: s1
    doc: Effective Influence.
    doc-ref: FND-STATE-002
  - id: research
    type: s1
    doc: Effective Research.
    doc-ref: FND-STATE-002
  - id: strength
    type: s1
    doc: Effective Strength.
    doc-ref: FND-STATE-002
  - id: blade
    type: s1
    doc: Effective Blade.
    doc-ref: FND-STATE-002
  - id: ranged
    type: s1
    doc: Effective Ranged.
    doc-ref: FND-STATE-002
  - id: fighting
    type: s1
    doc: Effective Fighting.
    doc-ref: FND-STATE-002
  - id: martial_arts
    type: s1
    doc: Effective Martial Arts.
    doc-ref: FND-AI-007, FND-GANG-001
enums:
  action:
    0: action_none
    1: action_attack
    2: action_bribe
    3: action_chaos
    4: action_control
    5: action_equip
    6: action_give
    7: action_heal
    8: action_hide
    9: action_influence
    10: action_move
    11: action_research
    12: action_sell
    13: action_snitch
    14: action_terminate
  gang_sector:
    100: gang_inactive
