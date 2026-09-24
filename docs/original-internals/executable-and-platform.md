# Executable image and platform boundaries

Status: active clean-room research log

What the reference executable is as a binary, and where it hands work to the
platform: the PE layout and section table, the toolchain fingerprint, the
DirectDraw, GDI, USER32, multimedia, file, configuration, and legacy-networking
imports that bound each subsystem, and the path and help-resource literals the
game looks its data up by.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Executable image](#executable-image)
  - [BIN-PE-001 - executable format](#bin-pe-001---executable-format)
  - [BIN-PE-002 - sections](#bin-pe-002---sections)
  - [BIN-TOOL-001 - compiler/runtime](#bin-tool-001---compilerruntime)
- [Platform boundaries visible in imports](#platform-boundaries-visible-in-imports)
  - [BIN-API-001 - rendering](#bin-api-001---rendering)
  - [BIN-API-002 - PX loading, palette, and copy modes](#bin-api-002---px-loading-palette-and-copy-modes)
  - [BIN-API-003 - files and persistence](#bin-api-003---files-and-persistence)
  - [BIN-API-004 - legacy networking](#bin-api-004---legacy-networking)
  - [BIN-API-005 - configuration](#bin-api-005---configuration)
  - [BIN-API-006 - audio and video](#bin-api-006---audio-and-video)
- [Resource lookup](#resource-lookup)
  - [BIN-ASSET-001 - data paths](#bin-asset-001---data-paths)
  - [BIN-ASSET-002 - WinHelp context maps](#bin-asset-002---winhelp-context-maps)
  - [BIN-ASSET-003 - WinHelp styled text and internal hotspots](#bin-asset-003---winhelp-styled-text-and-internal-hotspots)
<!-- doc-index:end -->

## Executable image

### BIN-PE-001 - executable format

**Observation:** The file is PE32 for Intel x86 (`Machine = 0x14c`), Windows GUI
subsystem, with preferred image base `0x00400000`, entry point `0x00478d00`,
image size `0x000c9000`, and no debug directory. Symbols, COFF line numbers, and
the symbol table are stripped. Linker version is 3.10. The COFF timestamp is
1996-07-19 22:26:46 in the dumper's local display.

**Interpretation:** This is a native 32-bit Windows build consistent with the
original 1996 release, not a managed GOG launcher.

**Confidence:** Verified for header values; High for interpretation.

### BIN-PE-002 - sections

| Section | RVA | Virtual size | Raw offset/size | Flags |
|---|---:|---:|---:|---|
| `.text` | `0x1000` | `0x7fac2` | `0x400` / `0x7fc00` | execute, read |
| `.rdata` | `0x81000` | `0xc4d` | `0x80000` / `0xe00` | read |
| `.data` | `0x82000` | `0x2b110` | `0x80e00` / `0x7a00` | read, write |
| `.idata` | `0xae000` | `0x19fa` | `0x88800` / `0x1a00` | read, write |
| `.rsrc` | `0xb0000` | `0x11bec` | `0x8a200` / `0x11c00` | read |
| `.reloc` | `0xc2000` | `0x6498` | `0x9be00` / `0x6600` | discardable, read |

**Confidence:** Verified from PE headers.

The large zero-initialized tail of `.data` is a candidate home for the global
match arrays described by the save format. This is only a hypothesis until
cross-references and runtime/save correlations are established.

### BIN-TOOL-001 - compiler/runtime

**Observation:** Linker version 3.10, 1996 timestamp, no imported MSVCRT DLL, and
native Win32 APIs.

**Interpretation:** A mid-1990s Microsoft Visual C++ toolchain with statically
linked runtime is plausible.

**Confidence:** Medium. Linker fingerprints and startup code still need matching
against known toolchain signatures.

## Platform boundaries visible in imports

### BIN-API-001 - rendering

**Observation:** The binary imports `DirectDrawCreate` from `DDRAW.dll`, plus GDI
palette/DIB/blit operations including `SetDIBits`, `BitBlt`, `StretchBlt`,
`CreatePalette`, `SetPaletteEntries`, `RealizePalette`, `UpdateColors`, and
`SetSystemPaletteUse`. USER32 imports include cursor, bitmap, menu, dialog,
window, key-state, paint, and message-loop functions.

**Interpretation:** Original rendering combines DirectDraw with Win32/GDI and
explicit palette management. The PX08/PX16 split and color-depth preference are
selected within this platform layer.

**Confidence:** Verified imports; Medium architecture interpretation.

### BIN-API-002 - PX loading, palette, and copy modes

**Observation:** Startup `0x00460ccf` probes both
`data\PX08\px00128` and `data\PX16\px00128`. The result selects an 8- or
16-bit global depth before normal image loading begins. Numeric loader
`0x00464155` starts with `data\PX08\PX00000`, replaces `08` with `16` at
16-bit depth, writes the requested five-digit resource number, and delegates
to `0x004273d5` with caller-supplied dimensions.

`0x004273d5` reads the 14-byte BMP file header and 40-byte DIB header, supplies
the caller's width and height plus one plane, retains the file's bit depth and
compression, and reads a color table only below 16 bits. It uploads indexed
pixels with `SetDIBits` color-use 0 (`DIB_RGB_COLORS`) and 16-bit pixels with
color-use 1 (`DIB_PAL_COLORS`); the latter unusual flag is harmless for a
direct-color DIB. It then copies the temporary surface to the requested one.
The older resource-or-file loader `0x00426f77` follows the same header repair
and upload sequence.

Palette loader `0x004282aa` reads the selected `data\CLT00000` file into a
256-entry logical palette. Entries 10 through 245 come from the CLT payload
and are marked explicit/no-collapse; the first and last ten preserve reserved
system entries. It selects and realizes that palette in the surface DC and,
when DirectDraw palette objects exist, mirrors it to the DirectDraw surface.

Copy wrapper `0x0042773e` uses `SRCCOPY`; when source and destination sizes
differ it first selects `COLORONCOLOR` and performs an opaque `StretchBlt`.
Wrapper `0x00427864` has three unscaled modes: 0 applies a resource-selected
pattern mask, 1 invokes keyed mask compositor `0x00427a09`, and every other
value is opaque `SRCCOPY`. A scaled copy bypasses all three modes and is always
opaque `COLORONCOLOR`.

The sole color comparison in the imported GDI path is `SetBkColor` inside
`0x00427a09`. Its exact key is `RGB(255,255,255)` at 8-bit depth and
`RGB(255,252,255)` at 16-bit depth, the GDI representation used for maximum
RGB555 white. No `TransparentBlt`, `MaskBlt`, `AlphaBlend`, or second
`SetBkColor` caller exists. Mode 1 is statically bound to the `PX00150` city
markers at `0x00412ac4` and the `PX06004` Last Turn illustration inside
`0x0044fd6c`. By contrast, the original loads `PX00300`, `PX04xxx`, and the
compact item/art sheets into scratch surfaces and copies their black pixels
opaquely with `0x0042773e`; black is not a native transparency key.

The complete city renderer `0x004123cc` loads ownership layers and `PX00150`,
but has no read of police duration, no `PX00300` load, and no patrol-car copy.
The sole constant `PX00300` load is in combat compositor `0x0042f98b`, where
its police cells are copied opaquely. A city/sector Crackdown-car overlay is
therefore a recreation invention, not original presentation.

**Interpretation:** Repaired PX16 pixels can be passed through without a
palette conversion step. Alpha is a recreation-side representation only for
the two proven exact-white keyed roles; broad per-sheet black alpha and map
Crackdown overlays alter native pixels. The recreation accepts both 248 and
255 as a decoded maximum five-bit channel because BMP decoders expand RGB555
maximums either by shifting or bit replication; this still identifies only
packed RGB555 value `0x7fff`.

**Confidence:** High static evidence from complete loader, palette, copy, mask,
and city/combat compositor dataflow. Exact display-driver conversion of the
16-bit white key remains runtime-dependent.

**Next validation:** Pixel-compare the two proven white-keyed roles and opaque
black apertures against native captures when runtime validation is permitted.

### BIN-API-003 - files and persistence

**Observation:** The executable imports `CreateFileA`, `ReadFile`, `WriteFile`,
`GetFileSize`, `SetFilePointer`, `FlushFileBuffers`, `GetOpenFileNameA`, and
`GetSaveFileNameA`. Embedded strings include `Save Files (*.SAV)`, `Please
specify save name`, and `Old Version of Saved Game.` Save dialog function
`0x0042b27a` creates/truncates the selected `.SAV`; Open dialog function
`0x0042afdd` selects an existing file. `0x0042ac7a` opens the tracked slot and
falls back from read/write to read-only access, while `0x0042ade9` closes it.
Wrappers `0x0042ae85` and `0x0042af31` return the byte counts produced by
`ReadFile` and `WriteFile` respectively.

Load function `0x0046381a` and save function `0x00463cc5` expose the complete
top-level transfer envelope. The first DWORD is one of these little-endian
ASCII markers:

| On-disk marker | Header DWORD | Payload | Exact file size | Load result |
|---|---:|---|---:|---:|
| `S40W` | `0x57303453` | 44 fixed state blocks | 45,305 (`0xB0F9`) | 1 |
| `N40W` | `0x5730344e` | The same 44 blocks plus six DWORD mappings | 45,329 (`0xB111`) | 2 |
| `M10W` | `0x5730314d` | One 12-byte auxiliary block only | 16 (`0x10`) | 3 |

The 44 shared blocks total 45,297 bytes (`0xB0F1`) and are transferred in this
exact address/size order:

```text
498da8:3cc0  4a08e8:0900  4abbf0:0018  4a5f00:0006
4ab638:0018  49ca68:0004  4abbe8:0004  4a5ef8:0004
4a25e8:0018  4abbc0:0012  4a27c8:0012  4a2608:0180
4abbe0:0006  48a250:1e60  482108:0006  482110:0018
48db48:0798  482158:0006  48e2f8:0018  482128:0018
482140:0018  4a2790:0018  4abcc0:0100  4aae08:0780
4a8888:2580  4a11e8:12fc  494830:0002  4a2588:0048
4a27a8:0018  4a5ed8:0018  4a25d0:0018  49ca78:0018
4ab620:0018  4a27e0:0018  4ab590:0090  4ab650:0018
4a2600:0006  4a2570:0018  4abc58:0006  preferences:0001x3
4ab588:0006  4a5ef0:0006
```

The preference bytes are written from live globals `0x00487850`,
`0x00487854`, and `0x00487858`; load stages them at `0x0049833c`,
`0x00498340`, and `0x00498300` before copying them back only after the trailer
marker succeeds. `N40W` then transfers the extra 24-byte block at `0x00498968`.
That block is populated as six DWORD participant mappings when legacy network
mode flag `0x00487b58` is enabled. Both full forms end with an exact copy of
their header marker. This is a structural sentinel, not a checksum: all 46/47
individual read or write return values are ignored. An invalid header or
trailer beeps through `0x00449c8e`; caller `0x004637b8` presents the old-version
message only when the load result is zero.

**Interpretation:** `S40W` is the standalone full-state envelope and `N40W` is
its legacy-network extension. `M10W` is an accepted auxiliary stub, not another
full-state layout; its downstream purpose remains unnamed because its sole
12-byte destination has no independent references. The exact envelope is now
closed, but field-by-field semantic naming and original-save interoperability
remain unnecessary for validating recreation state.

**Confidence:** High from the complete read/write bodies, exact transfer-size
sum, marker branches, I/O wrappers, network-mode writers, and sole caller.

**Recreation exception:** The original can partially mutate globals on a short
or failed read because it never checks the returned byte counts. The recreation
deliberately does not reproduce that unsafe behavior: its unrelated native JSON
format is bounded, versioned, hashed, and validated before reconstruction.

**Next validation:** None for the fixed transfer envelope. Runtime-created
original saves would only corroborate bytes and are outside the interoperability
scope.

### BIN-API-004 - legacy networking

**Observation:** The import table contains 18 WinSock functions by ordinal,
Windows Telephony API calls for modem setup/dialing, serial-port configuration,
overlapped file I/O, communication masks/events, threads, mutexes, critical
sections, and synchronization waits. Embedded strings reference modem Control
Panel setup and host windows.

**Interpretation:** WinSock, modem, and serial transports described by the manual
are native subsystems in this build and share synchronization infrastructure.

**Confidence:** Verified imports/strings; High interpretation.

**Scope decision:** This import-level inventory is the endpoint for legacy
network research. Original transports and wire formats are explicitly outside
the recreation scope and will not be ported, exposed, or supported for
interoperability.

### BIN-API-005 - configuration

**Observation:** Registry APIs are imported. Strings include
`SOFTWARE\Stick Man Games\Chaos Overlords\1.0` and the Windows App Paths key for
`Chaos Overlords.exe`.

**Interpretation:** Preferences are read from the product key; the separate App
Paths query locates the installation. The executable's own preference writes
cannot succeed because it opens the product key read-only.

**Confidence:** High for the preference key, complete load/save value inventory,
access masks, and failure boundary in `BIN-OPTIONS-001`; the App Paths role is
High from its separate bounded query.

**Static follow-through:** `BIN-OPTIONS-001` now maps every preference read and
write, including the original stale-buffer and read-only-handle defects. A VM
trace can corroborate Windows behavior but is no longer needed to identify the
schema or persistence boundary.

### BIN-API-006 - audio and video

**Observation:** WINMM imports include `PlaySoundA`, `mciSendCommandA`, auxiliary
volume APIs, `timeGetTime`, `timeSetEvent`, and `timeKillEvent`. Sixteen Smacker
functions are imported by ordinal from `smackw32.dll`.

**Interpretation:** Effects likely use `PlaySoundA`, CD/music control likely uses
MCI, and Smacker owns intro/logo decoding. Multimedia timers may drive animation
or sound; their presence does not prove simulation timing.

**Confidence:** Verified imports; Medium API-role interpretation; Low timer role.

**Static follow-through:** The sound loader, all 110 calls to its gated playback
wrapper, and the CD-track selector/lifecycle are mapped in `BIN-SOUND-001` and
`BIN-MUSIC-001` in [Audio, music, and video](audio-and-video.md). The two movie
files have also been structurally decoded as the supported Smacker-v2 inputs
documented in `ORIGINAL-FILE-FORMATS.md` and `AUDIO-VIDEO.md`. Remaining media
questions concern native presentation timing and interruption behavior rather
than ownership of these path literals.

## Resource lookup

### BIN-ASSET-001 - data paths

The following case-varying format/path literals are embedded in the executable:

| Literal | Likely role | Confidence |
|---|---|---|
| `data\Sites` | site definitions | High |
| `data\Gangs` | gang definitions | High |
| `data\Items` | item definitions | High |
| `data\PX08\PX00000` | formatted 8-bit image lookup | High |
| `data\PX08\px00128` | fixed 8-bit resource | High |
| `data\PX16\px00128` | fixed 16-bit resource | High |
| `data\snd00000` | formatted sound lookup | High |
| `Data\mvIntro` | intro movie | High |
| `Data\mvLogos` | logo movie | High |
| `.\Help\Chaos.hlp` | WinHelp content | Verified |
| `A:\CHAOS\CDTrack` | CD music path/template | Medium |

The `00000` suffix strongly suggests integer-to-five-digit resource formatting,
but the formatter and valid ranges must be located before this is marked
Verified.

### BIN-ASSET-002 - WinHelp context maps

**Observation:** The fingerprinted `Help\Chaos.hlp` contains an 80-entry
`|CONTEXT` B+ tree. Each leaf record is an unsigned 32-bit context-name hash
paired with a signed topic offset. All 59 symbolic targets in `CHAOS.CNT` hash
to entries in that tree. The file's `|CTXOMAP` stream contains a zero entry
count, so this build defines no numeric `[MAP]` context IDs. For example,
`INTRO` hashes to `0x053d9a5c`, `CITYVIEW` to `0x86ee9810`, and `ITEMINFO` to
`0xeb824ced`.

**Interpretation:** Context targets are native logical anchors and are not
required to equal topic-header positions. The modern viewer should retain the
raw hash/offset map and route screen help through the exact `CHAOS.CNT` symbols;
numeric-context routing is inapplicable to this help file.

**Confidence:** High. A bounded clean-room decoder verifies the B+ tree,
recomputes every contents hash, and reproduced 80 contexts, 59 named contexts,
zero numeric IDs, 80 topics, and 73 contents rows from the legal GOG files.

### BIN-ASSET-003 - WinHelp styled text and internal hotspots

**Observation:** The supported `Help\Chaos.hlp` contains nine legacy 11-byte
font descriptors. Interleaving its `LinkData1` commands with phrase-decoded
`LinkData2` text yields 779 normalized runs and 93 internal context-hash
hotspots: 67 topic jumps and 26 popup jumps. Every hotspot hash occurs in the
80-entry `|CONTEXT` tree. The file contains no display-table, embedded-picture,
external-file-link, or macro-hotspot use.

**Interpretation:** Internal link arguments are context hashes, not direct
topic indices. Their context offsets may point inside a topic, so resolution
selects the last topic-header logical offset at or before the target. This maps
all 26 popups to the otherwise-unlisted definition fragments and all 67 normal
jumps to authored topics without relying on titles.

**Confidence:** High. The bounded clean-room decoder reproduced the same 93
hotspots and 26/67 split as an independent parser, retained all supported font
attributes, and resolved every target in a fresh 686-asset legal extraction.
