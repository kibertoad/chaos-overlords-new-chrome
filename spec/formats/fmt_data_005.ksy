meta:
  id: fmt_data_005
  title: Compressed archive DATA/DATA.Z
  license: MIT
  endian: le
doc: |
  DATA/DATA.Z of Chaos Overlords: an InstallShield 3 archive left by the
  original installer. A 255-byte header, the compressed files back to back,
  then the directory entries and the file entries. The game never reads it.
doc-ref: FMT-DATA-005, FND-DATA-005, FND-DATA-009
seq:
  - id: signature
    contents: [0x13, 0x5d, 0x65, 0x8c]
  - id: unk_04
    size: 8
  - id: file_count
    type: u2
  - id: date
    type: u2
    doc: MS-DOS date of the archive.
  - id: time
    type: u2
    doc: MS-DOS time of the archive.
  - id: archive_size
    type: u4
    doc: Length of the whole file.
  - id: unk_16
    size: 19
  - id: dir_table_offset
    type: u4
  - id: unk_2d
    size: 4
  - id: dir_count
    type: u2
  - id: file_table_offset
    type: u4
  - id: file_table_size
    type: u2
  - id: unk_39
    size: 198
instances:
  directories:
    pos: dir_table_offset
    type: directory
    repeat: expr
    repeat-expr: dir_count
  files:
    pos: file_table_offset
    type: file_entry
    repeat: expr
    repeat-expr: file_count
types:
  directory:
    seq:
      - id: file_count
        type: u2
      - id: entry_size
        type: u2
      - id: name_length
        type: u2
      - id: name
        type: str
        size: name_length
        encoding: ASCII
      - id: padding
        size: entry_size - 6 - name_length
  file_entry:
    seq:
      - id: unk_00
        type: u1
      - id: dir_index
        type: u2
      - id: expanded_size
        type: u4
      - id: compressed_size
        type: u4
      - id: offset
        type: u4
        doc: Offset of the file's compressed block.
      - id: date
        type: u2
        doc: MS-DOS date of the file.
      - id: time
        type: u2
        doc: MS-DOS time of the file.
      - id: attributes
        type: u4
        doc: MS-DOS file attributes.
      - id: entry_size
        type: u2
      - id: unk_19
        size: 4
      - id: name_length
        type: u1
      - id: name
        type: str
        size: name_length
        encoding: ASCII
      - id: padding
        size: entry_size - 0x1e - name_length
