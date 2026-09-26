meta:
  id: fmt_help_001
  title: WinHelp container HELP/Chaos.hlp
  license: MIT
  endian: le
doc: |
  A WinHelp 3.1 container: a 16-byte header, internal files each led by a
  9-byte header, and a directory B+ tree with one leaf page naming them. The
  contents of the internal files other than the directory are opaque.
doc-ref: FMT-HELP-001, FND-HELP-003
seq:
  - id: magic
    contents: [0x3f, 0x5f, 0x03, 0x00]
  - id: directory_offset
    type: s4
  - id: first_free_block
    type: s4
  - id: file_size
    type: u4
instances:
  directory:
    pos: directory_offset
    type: directory_file
types:
  internal_file_header:
    seq:
      - id: reserved_size
        type: u4
        doc: Bytes the internal file occupies, this header included.
      - id: used_size
        type: u4
        doc: Bytes of content after this header.
      - id: file_flags
        type: u1
        doc: 4 for the directory, 0 otherwise.
  internal_file:
    seq:
      - id: header
        type: internal_file_header
      - id: content
        size: header.used_size
  directory_file:
    seq:
      - id: header
        type: internal_file_header
      - id: btree_magic
        contents: [0x3b, 0x29]
      - id: btree_flags
        type: u2
      - id: page_size
        type: u2
      - id: structure
        type: strz
        size: 16
        encoding: ASCII
      - id: must_be_zero
        type: u2
      - id: page_splits
        type: u2
      - id: root_page
        type: u2
      - id: must_be_minus_one
        type: s2
      - id: total_pages
        type: u2
      - id: levels
        type: u2
      - id: total_entries
        type: u4
      - id: leaf
        type: leaf_page
        size: page_size
        doc: Valid for this file, whose directory has one page and one level.
  leaf_page:
    seq:
      - id: unused_bytes
        type: u2
      - id: entry_count
        type: u2
      - id: previous_page
        type: s2
      - id: next_page
        type: s2
      - id: entries
        type: directory_entry
        repeat: expr
        repeat-expr: entry_count
  directory_entry:
    seq:
      - id: name
        type: strz
        encoding: ASCII
      - id: offset
        type: u4
    instances:
      file:
        io: _root._io
        pos: offset
        type: internal_file
