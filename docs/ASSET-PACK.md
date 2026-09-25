# Asset pack and bundled data

How the rebuild identifies the player's copy of the original's files and what it ships decoded
from them. The file formats themselves are in the spec: `SITES`, `Gangs` and `ITEMS` in
FMT-DATA-001 to FMT-DATA-003, and every file of the GOG release, with its size and xxh3 hash, in
[BLD-GOG-EN-1.1](../spec/builds/BLD-GOG-EN-1.1.md). The hashes below are the SHA-256 values the
extractor has always checked; the spec's xxh3 hashes identify the same files.

<!-- doc-index:begin toc depth=2 -->
- [Asset pack fingerprint](#asset-pack-fingerprint)
- [Bundled gameplay data](#bundled-gameplay-data)
<!-- doc-index:end -->

## Asset pack fingerprint

The extractor copies the files under `DATA`, `HELP` and `MUSIC` into a local asset pack and never
needs or copies the executable. It accepts a source only when its fingerprint is
`ad958a934a691318f31a27a87252f420dd89a0ad03759457d8feaf49914d29e3`: SHA-256 over every file under
those three directories, in lexical order of their lower-case relative paths, each contributing
its path, a NUL byte, its length as a little-endian 64-bit integer, and its bytes. The manifest
of an extracted pack records a fingerprint over the tables, the `PX16` images, the sounds and the
music computed the same way.

The extractor also checks the three gameplay tables against these SHA-256 values and rejects a
table whose length is not a whole number of records:

| File | SHA-256 |
|---|---|
| `DATA/SITES` | `d642640b21228512d52c2db3c9e5b55d725ddf3efed1476e3bcad870b66e8785` |
| `DATA/Gangs` | `87397a2bb2aca59655d7adf6fb99e4e719727c7bdaba53a51c70df924f10b391` |
| `DATA/ITEMS` | `52763fcf941abf5778c84011106f581275382a55f67bdd67b184cc7ad5d71b97` |

## Bundled gameplay data

`src/Rechaos.Core/GameData/original-data.json` is generated from those three tables. Every byte
of each fixed-width text field and every 16-bit value is read at its offset in the format
entries, and the reader rejects a trailing or partial record. The values are copied as they are:
nothing is normalized, rebalanced, completed or guessed. The record counts (22 sites, 90 gangs,
64 items) and the source hashes are checked before generation. Text padding and terminating NUL
bytes are left out; the decoded text and every number are exact.

The generated JSON is pinned as SHA-256
`e65f80e4d9a99ceeffbbc7fb335ef7f57b368ef87c1c56af23bcd759cd4e8b3a` in
`GameplayDataProvenance`, and the test suite checks it.
`Rechaos.Extractor --generate-game-data <json> --source <install>` writes it: it verifies the
whole source pack before reading the tables, writes UTF-8 JSON through a temporary file in the
same directory, and replaces the old file in one step. Regenerating from the GOG installation
gives the pinned hash. `OriginalDataValidator` rejects wrong record counts, IDs out of order,
duplicate or missing names, wrong special-site mappings, item categories or media numbers out of
range, and malformed unused item records, both when it reads the original tables and when it
loads the embedded payload.

Exact bytes say nothing about what a field means. Which field is a gang's Influence, or how the
original combines two fields, is a claim of the format and rule entries in the spec, with the
status their evidence supports.
