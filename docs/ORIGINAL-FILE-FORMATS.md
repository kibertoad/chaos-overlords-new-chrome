# Original game file research log

This is the living specification for the original *Chaos Overlords* files.
Every implementation-relevant observation belongs here, including negative
results and unresolved questions. It is not a redistribution guide: users must
provide their own legal copy, and extracted output remains proprietary.

## Confidence scale

| Level | Meaning |
|---|---|
| **Verified** | Confirmed against the installed GOG files, arithmetic/file signatures, and working parser or test. |
| **High** | Confirmed by direct inspection and independent structure evidence, but not yet exercised end-to-end in gameplay. |
| **Medium** | Documented by the `re-chaos` reference or strongly inferred; this port has not independently validated every field. |
| **Low** | Working hypothesis, naming guess, or format not decoded. |

Last inspection: 2026-09-07. Test installation:
`C:\GOG Games\Chaos Overlords`.

The image-only original manual used for intended-rule research is 6,229,841
bytes, 30 PDF pages (56 numbered manual pages), and has SHA-256
`bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d`.
Manual claims are not marked Verified until confirmed against original binary
behavior or state changes.

## Installation identity

| Finding | Evidence | Confidence |
|---|---|---|
| The inspected GOG executable SHA-256 is `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`. | Direct SHA-256 over `Chaos Overlords.exe`. | **Verified** |
| The `re-chaos` research release identifies executable SHA-256 `0791e6209d573a79882675d1236737f5c9b369ea4af541a7dbd03cbadf4493d5`. | Reference README; differs from the installed GOG wrapper/build. | **High** |
| Executable hashes are research metadata only; the extractor does not require or copy an executable. | Only the asset pack is needed by the port. | **Verified** |
| The extractor verifies the three canonical gameplay-table SHA-256 values, validates table record boundaries, and stores a deterministic fingerprint over tables, PX16, sounds, and music in its manifest. | Extractor policy and implementation. | **Verified** |

The canonical full asset-pack fingerprint is
`ad958a934a691318f31a27a87252f420dd89a0ad03759457d8feaf49914d29e3`.
It is
SHA-256 over lexically sorted, lowercase relative paths, a NUL byte,
little-endian int64 file length, and raw file bytes for every file under
`DATA`, `HELP`, and `MUSIC`. The algorithm and value are **Verified** against
the inspected GOG pack.

Table hashes from the inspected copy are: `SITES`
`d642640b21228512d52c2db3c9e5b55d725ddf3efed1476e3bcad870b66e8785`,
`Gangs` `87397a2bb2aca59655d7adf6fb99e4e719727c7bdaba53a51c70df924f10b391`,
and `ITEMS` `52763fcf941abf5778c84011106f581275382a55f67bdd67b184cc7ad5d71b97`.

### Bundled-data fidelity statement

`src/Rechaos.Core/GameData/original-data.json` is a machine-generated decoding
of those three files. Every byte in each fixed-width name/description field and
every little-endian 16-bit value was consumed at its documented offset; the
reader rejects trailing or partial records. The generated values were not
normalized, rebalanced, completed, or guessed. Counts (22/90/64) and source
SHA-256 hashes are checked before extraction. This structural/value fidelity is
**Verified** bit-for-bit against the canonical inputs.

The exact generated JSON payload is itself pinned as SHA-256
`e65f80e4d9a99ceeffbbc7fb335ef7f57b368ef87c1c56af23bcd759cd4e8b3a`
in `GameplayDataProvenance` and verified by the test suite. String padding and
terminating NUL bytes are intentionally not bundled; decoded string content and
every numeric field are exact, while raw source identity is covered by the
three source-file hashes.

That statement does **not** elevate semantic labels or gameplay formulas. For
example, a value can be bit-perfect while our belief that it means “influence”
or that the original combines it in a particular formula is still **Medium** or
**Low** confidence. The JSON is therefore original table data; current gameplay
code using it is a clean-room interpretation and is separately documented.

## General encoding

| Finding | Evidence | Confidence |
|---|---|---|
| Multi-byte integers are little-endian. | Direct values in all three tables and BMP/WAV headers. | **Verified** |
| Fixed strings are ASCII, padded with spaces and frequently contain an early NUL. | Hex inspection and parsed names/descriptions. | **Verified** |
| Fixed strings should be decoded by reading the entire field, then trimming trailing NUL and spaces. | Actual first records (`GYM`, `RIGHT HANDS`, `METAL PIPE`). | **Verified** |

## Gameplay tables

All offsets below are relative to one record. All numeric fields are signed
16-bit little-endian unless stated otherwise.

### `SITES`

The file is 1,364 bytes: 22 records of 62 bytes. Its SHA-256 is recorded above.

| Offset | Bytes | Field |
|---:|---:|---|
| `0x00` | 20 | name |
| `0x14` | 2 each | id, resistance, support, frequency, tolerance, cash |
| `0x20` | 2 each | combat, defense, stealth, detect, chaos, control, heal, influence, research, strength, blade, range, fighting, martial arts |
| `0x3C` | 2 | special: 0 none, 1 science center/tech 8, 2 research lab/tech 10, 3 factory discount |

Record size/count and parsing are **Verified**. Field semantics and special
labels are **Medium** because they originate in reverse-engineering notes and
have not all been behavior-tested.

### `Gangs`

The file is 14,040 bytes: 90 records of 156 bytes.

| Offset | Bytes | Field |
|---:|---:|---|
| `0x00` | 30 | name |
| `0x1E` | 2 | id |
| `0x20` | 90 | description |
| `0x7A` | 2 each | force, upkeep, combat, defense, tech level, stealth, detect, chaos, control, heal, influence, research, strength, blade, range, fighting, martial arts |

Record size/count and string/numeric decoding are **Verified**. Stat semantics
are **Medium** pending comparison with original UI calculations.

### `ITEMS`

The file is 10,624 bytes: **64**, not 160, records of 166 bytes. The 160 count
in the upstream notes is a typo (10,624 / 166 = 64).

| Offset | Bytes | Field |
|---:|---:|---|
| `0x00` | 30 | name |
| `0x1E` | 2 | id |
| `0x20` | 90 | description |
| `0x7A` | 2 each | type, research difficulty, cost, tech level |
| `0x82` | 2 each | combat, defense, stealth, detect, chaos, control, heal, influence, research, strength, blade, range, fighting, martial arts |
| `0x9E` | 2 each | attack animation, hit animation, sound id, unknown |

Record size/count and parser alignment are **Verified**. Type values (0 melee,
1 blade, 2 ranged, 3 armor, 4 miscellaneous, 99 undefined) and animation/sound
semantics are **Medium**.

## `PX16/PXxxxxx` graphics

These files are otherwise-valid, uncompressed Windows BMP containers whose
width, height, plane count, and bits-per-pixel fields were zeroed or poisoned;
the original executable supplied those values internally. The header begins
`BM`, pixel offset is 54, DIB header size is 40, compression is BI_RGB (0), and
pixels are 16 bits. For known files, `file size = 54 + width * height * 2`, which
also proves there is no scan-line padding for these even widths. **Verified.**

The extractor repairs offsets `0x12` (width, int32), `0x16` (height, int32),
`0x1A` (planes = 1, int16), and `0x1C` (bits = 16, int16), leaving pixel bytes
untouched. **Verified.** Comparison of all 12,065,806 paired PX08/PX16 pixels
strongly identifies RGB555: total absolute RGB error is 111,862,509 for RGB555
versus 744,334,623 for RGB565, with 7,413,122 versus 6,230,106 exact quantized
matches. No source pixel has bit 15 set. Channel interpretation is therefore
**High** confidence; transparency/color-key behavior still needs reference
observation.

Known dimension groups (**High**, from exact payload-size arithmetic and the
reference size map):

| Resources | Dimensions |
|---|---:|
| `PX00100`, `128`, `130`, `131`, `143`-`146` | 640 x 460 |
| `PX00129` | 512 x 646 |
| `PX00132` | 108 x 164 |
| `PX00137`, `PX00139` | 220 x 72 |
| `PX00138`, normal `PX04xxx` | 720 x 48 |
| `PX00140` | 312 x 282 |
| `PX00150` | 220 x 56 |
| `PX00200` | 428 x 410 |
| `PX00201` | 320 x 240 |
| `PX00202`, `PX00203` | 312 x 393 |
| `PX00300` | 324 x 64 |
| `PX02000` | 120 x 1408 |
| `PX03000` | 640 x 576 |
| `PX04999` | 20 x 1280 |
| `PX05xxx` | 344 x 209 |
| `PX06xxx`, 76,526-byte resources | 242 x 158 |
| `PX06xxx`, 76,042-byte resources | 242 x 157 |
| `PX07xxx` | 512 x 64 |
| `PX10000`-`PX10006` | 432 x 416 |

Some PX16 files have exceptional payload lengths despite sharing a numbered
family; dimensions should be established from executable behavior rather than
guessed. The extractor skips an unknown dimension rather than corrupting it.

## `PX08/PXxxxxx` graphics

These begin with `BM` and use a 40-byte DIB header, 8-bit indexed pixels, and a
256-entry BGRA palette ending at pixel offset `0x436` (1,078). Across the full
fingerprinted set, 207 files report compression value 1 (BMP RLE8) and seven
`PX05xxx` files report compression value 0 (uncompressed BMP). Width/height
fields are missing in both variants. The extractor retains every source and
produces a bounded, uncompressed 8-bit BMP derivative while preserving its
palette. All 214 sources decode and pass output size/hash verification; this is
**High** confidence pending pixel comparison against reference rendering.

The local `re-chaos` dimension tool's `76,526`-byte PX16 case is listed
as 242x157, but the exact 54-byte header plus 16-bit payload size is
`54 + (242 * 158 * 2) = 76,526`, and the paired PX08 RLE stream also contains
158 rows. The recreation therefore uses 242x158 for that size and retains
242x157 only for the 76,042-byte case. This is a confirmed contradiction in
the secondary source, not an original-format ambiguity.

## Audio and music

`DATA/SND00xxx` files are ordinary RIFF/WAVE despite lacking extensions.
Inspected `SND00200` is PCM format 1, mono, 22,050 Hz, 8-bit, with a conventional
44-byte header. This example is **Verified**; uniformity across all sounds is
**High**. The extractor copies them byte-for-byte with `.wav` extensions.

`MUSIC/Track02.ogg` through `Track09.ogg` are supplied as Ogg files by the GOG
release. Their role as music is **Verified** by location/extension; track usage
and looping rules are **Low**. They are copied byte-for-byte.

## Video

`MVINTRO` (8,592,724 bytes) and `MVLOGOS` (1,832,372 bytes) begin `SMK2`, the
Smacker v2 signature. Header values appear to give 480 x 256 dimensions at
offsets 4 and 8; frame counts appear to be 1,150 and 200 at offset 12. Container
identity is **Verified**; field interpretation is **High**; playback/timing and
audio tracks are **Low**. The extractor copies both videos byte-for-byte, but
the MonoGame client has no Smacker decoder yet.

## Help

The supported `HELP/Chaos.hlp` is a 60,208-byte WinHelp container with magic
`0x00035F3F`. Its `|SYSTEM` stream identifies minor version 33 and topic flags
4. The directory B-tree exposes compressed `|TOPIC`, `|PhrIndex`, and
`|PhrImage` streams. The companion 1,806-byte `CHAOS.CNT` contains 73 contents
entries, 59 of which name topics. Bounded LZ77 and Hall phrase decoding recovers
80 meaningful text topics; 21 are linked explanatory topics not listed in the
contents file. No topic bitmap or table records occur in the supported file.

The extractor converts those user-owned inputs into local
`help/contents.json`, preserving normalized readable text, topic titles, the
contents hierarchy, and which topics are listed. Before recreation-authored
clarifications are applied, the decoded text totals 57,640 characters and has
SHA-256 `c212f3909b177093863b8a59af1830d8e65359fa452f572ff581e48f01bc7609`
after newline normalization. An independent parser produced the same character
count and hash. Container structure, decompression, topic count, and source text
are therefore **High** confidence. Inline formatting, link targets, and context
IDs are not yet preserved and remain **Low** confidence.

The generated Attack topic appends a clearly labeled New Chrome clarification:
the effective roll is gang Combat plus current Force minus defender Defense,
and simultaneous attacks use start-of-round Force even when a gang is eliminated
during the round. This intentionally corrects the original manual's omitted
Force term for players without altering the preserved source files.

## Other files

| File | Finding | Confidence |
|---|---|---|
| `CLT00002` | 944-byte color-related lookup/table; begins repeated four-byte entries resembling B, G, R, flag/index. Not decoded. | **Low** |
| `DATA.Z` | 7,676,546-byte opaque binary. It does not expose a recognized signature in its first bytes (`13 5D 65 8C ...`). Purpose and compression unknown. | **Low** |
| `HELP/` | Original WinHelp sources are copied for provenance and decoded locally into the modern topic document described above. | **High** for text/topics; **Low** for formatting/link metadata |

## Save games (historical reference only; unsupported)

No save sample was present in the inspected installation. The `re-chaos` notes
describe magic `0x57303453` with 45,305-byte saves and magic `0x5730344E` with
45,329-byte saves. They outline six-player gang arrays, 64 sectors, cursor and
portrait state, turn/objective/cash, hire pools, research, statistics,
notifications, preferences, and a repeated trailing magic. This historical map
is **Medium** confidence and may help interpret executable state, but original
save import/export is an explicit non-goal and is not an implementation or
release gate.

## Open questions / next experiments

1. Identify every PX resource semantically and verify transparency/color keys
   against original rendering.
2. Compare decoded PX08 palettes/pixels with PX16 variants and original rendering.
3. Determine `CLT00002` entry layout and consumer.
4. Identify/decompress `DATA.Z` and inventory its contents.
5. Implement Smacker playback or a legal local transcode during extraction.
6. Trace original economy, combat, AI, objective, and RNG behavior against the
   parsed fields; present gameplay behavior is a deterministic playable slice,
   not yet a claim of simulation parity.
