meta:
  id: fmt_data_002
  title: Gang definition records in DATA/Gangs
  license: MIT
  endian: le
doc: |
  DATA/Gangs of Chaos Overlords: 90 gang definition records of 156 bytes,
  back to back. Every number is a signed 16-bit little-endian integer.
doc-ref: FMT-DATA-002, FND-DATA-002, SRC-RECHAOS-3561D41
seq:
  - id: records
    type: gang
    repeat: eos
types:
  gang:
    doc: One gang definition, 156 bytes.
    seq:
      - id: name
        type: strz
        size: 30
        encoding: ASCII
        doc: The gang's name. ASCII text, one NUL byte, then spaces to the end of the field.
        doc-ref: FND-DATA-002
      - id: id
        type: s2
        doc: The gang's number, equal to the record's index, 0 to 89.
        doc-ref: FND-DATA-002
      - id: description
        type: str
        size: 90
        encoding: ASCII
        doc: The gang's description, padded with spaces, with no NUL byte.
        doc-ref: FND-DATA-002
      - id: force
        type: s2
        doc: The gang's Force as defined for hiring.
        doc-ref: FND-AI-008
      - id: upkeep
        type: s2
        doc: Upkeep paid each turn for the gang.
        doc-ref: FND-AI-008
      - id: combat
        type: s2
        doc: Base Combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: defense
        type: s2
        doc: Base Defense.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: tech_level
        type: s2
        doc: Tech Level.
        doc-ref: SRC-RECHAOS-3561D41
      - id: stealth
        type: s2
        doc: Base Stealth.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: detect
        type: s2
        doc: Base Detect.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: chaos
        type: s2
        doc: Base Chaos.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: control
        type: s2
        doc: Base Control.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: heal
        type: s2
        doc: Base Heal.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: influence
        type: s2
        doc: Base Influence.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: research
        type: s2
        doc: Base Research.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: strength
        type: s2
        doc: Base Strength.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: blade
        type: s2
        doc: Base Blade.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: range
        type: s2
        doc: Base Range.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: fighting
        type: s2
        doc: Base Fighting.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: martial_arts
        type: s2
        doc: Base Martial Arts.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
