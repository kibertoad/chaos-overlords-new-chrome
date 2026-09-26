meta:
  id: fmt_data_001
  title: Site definition records in DATA/SITES
  license: MIT
  endian: le
doc: |
  DATA/SITES of Chaos Overlords: 22 site definition records of 62 bytes,
  back to back. Every number is a signed 16-bit little-endian integer.
doc-ref: FMT-DATA-001, FND-DATA-001, SRC-RECHAOS-3561D41
seq:
  - id: records
    type: site
    repeat: eos
types:
  site:
    doc: One site definition, 62 bytes.
    seq:
      - id: name
        type: strz
        size: 20
        encoding: ASCII
        doc: The site's name. ASCII text, one NUL byte, then spaces to the end of the field.
        doc-ref: FMT-DATA-001, FND-DATA-001
      - id: id
        type: s2
        doc: The site's number, equal to the record's index, 0 to 21.
        doc-ref: FND-DATA-001
      - id: resistance
        type: s2
        doc: Influence progress needed before the site counts as influenced.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: support
        type: s2
        doc: Support the site adds to its sector once influenced.
        doc-ref: SRC-RECHAOS-3561D41
      - id: frequency
        type: s2
        doc: Named frequency by the source; city generation does not read it.
        doc-ref: FND-CITY-002, SRC-RECHAOS-3561D41
      - id: tolerance
        type: s2
        doc: Tolerance the site adds to its sector once influenced.
        doc-ref: SRC-RECHAOS-3561D41
      - id: cash
        type: s2
        doc: Cash the site adds to its sector's income once influenced.
        doc-ref: SRC-RECHAOS-3561D41
      - id: combat
        type: s2
        doc: Combat modifier for the sector owner's gangs in the sector once the site is influenced.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: defense
        type: s2
        doc: Defense modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: stealth
        type: s2
        doc: Stealth modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: detect
        type: s2
        doc: Detect modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: chaos
        type: s2
        doc: Chaos modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: control
        type: s2
        doc: Control modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: heal
        type: s2
        doc: Heal modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: influence
        type: s2
        doc: Influence modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: research
        type: s2
        doc: Research modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: strength
        type: s2
        doc: Strength modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: blade
        type: s2
        doc: Blade modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: range
        type: s2
        doc: Range modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: fighting
        type: s2
        doc: Fighting modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: martial_arts
        type: s2
        doc: Martial Arts modifier, applied like combat.
        doc-ref: FND-GANG-001, SRC-RECHAOS-3561D41
      - id: special
        type: s2
        enum: special
        doc: The site's special effect.
        doc-ref: FND-DATA-001, SRC-RECHAOS-3561D41
enums:
  special:
    0:
      id: site_special_none
      doc: No special effect.
    1:
      id: site_special_research_tech_8
      doc: Research effect the source ties to Tech Level 8.
    2:
      id: site_special_research_tech_10
      doc: Research effect the source ties to Tech Level 10.
    3:
      id: site_special_discount
      doc: Lowers the price of equipment bought in the sector.
