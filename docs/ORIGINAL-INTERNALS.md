# Original executable internals research

Status: active clean-room research log
Last updated: 2026-09-11
Reference executable SHA-256:
`a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`

This document records factual structure and testable interpretations of the
original executable. It does not contain copied decompiled source. Addresses are
valid only for the fingerprint above.

## Finding format

- **ID**: stable reference used by rules, code, tests, and the parity matrix.
- **Observation**: what is directly present in the executable or behavior.
- **Interpretation**: what the observation may mean.
- **Confidence**: Verified, High, Medium, or Low.
- **Next validation**: experiment needed before relying on semantics.

## PE image

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

### BIN-UI-031 - PX00129 uses role-specific copy modes

**Observation:** Startup loads `PX00129` into surface 6 with
`FUN_00464108(6, 0x81, ...)`. Calls that use surface 6 do not share one alpha
policy. For example, `0x00413012` repeatedly calls
`FUN_00427864(6, 1, ..., 1)` for exact-white keyed UI elements, while
`0x00457b7b` copies the 32-by-32 Overlord portraits from source y 480 through
512 with mode 0 for inactive seats on the separate `PX00146` setup flow, then
uses ordinary opaque `FUN_0042773e` for active seats. Other surface-6 regions
likewise include opaque copies. Scaling through the wrapper is opaque
regardless of its requested mode.

A complete call census finds 502 direct calls to opaque `FUN_0042773e`: 501
push a literal source surface, including 131 surface-6 calls. Its sole
non-literal caller is one-second startup blit benchmark `0x00432954`, whose
only caller supplies surface 1 to surface 0. All 77 direct calls to
`FUN_00427864` push literal source surfaces; 66 use surface 6. Auditing the 26
functions containing those 66 calls classifies 64 as mode 1 and two as mode 0.
There is therefore no unaccounted parameterized surface-6 path through either
wrapper.

Gang-status compositor `0x00412bf7` builds a 20-by-20 source at x 492 and
y `67 + state * 20`, then uses `FUN_00427864(6, 2, ..., 1)` at both of its
copy sites. The assigned, idle, and incoming frames used by the recreation are
therefore exact-white keyed, not opaque. Setup drag helper `0x0040f72e` first
scales the selected 32-by-32 Overlord portrait opaquely into a 40-by-40 scratch
cell, then overlays source `(150,386)-(190,426)` from surface 6 with mode 1
before copying the composed token to the pointer. The static setup renderer at
`0x0040ee8a` supplies the second mode-0 request, but its 32-by-30 source is
scaled to 64 by 60, so the wrapper deliberately takes its opaque scaling path
and never applies the requested pattern. The twelve 20-by-20 active-player
frames at source y 626 likewise use opaque `FUN_0042773e`.

The marker state is a three-bit composition while the active player has a gang
in the sector: detectable opposing-player presence contributes 1, an active
gang with action byte zero contributes 2, and a pending hire targeting the
sector contributes 4. This selects all eight frames at y 67 through 207. A
pending hire without an existing friendly gang uses the ninth frame at y 227.
Presence builder `0x004123cc` clears the 64-by-6 table and sets an owner/sector
entry only for an active gang whose visibility byte for the current player is
nonzero, so an undetected enemy does not disclose itself by turning the marker
red. Atlas pixels use dark green `(0,156,0)` for the uncontested circle, dark
red `(156,0,0)` for its contested counterpart, yellow `(247,247,0)` for the
idle question mark, and brighter reds for the incoming-hire decoration.

Mode compositor `0x00427e60` selects 1-bit bitmap resource 147, 143, or 146.
The sole selector `0x00449b20` maps an input below 86 to resource 147, 86
through 170 to resource 143, and 171 or above to resource 146. Its raster
operations reduce to `(destination AND pattern) XOR (source AND NOT pattern)`:
a set pattern bit preserves the destination, while a clear bit copies the
source. In the unscaled `PX00146` inactive-seat path, fixed color `0x2661`
selects resource 146 before this stencil is applied.

The executable resources themselves are 8-by-8, 1-bit DIBs with a black/white
palette. Read as top-down rows, resource 143 alternates `0x55/0xaa` (32 set
bits), resource 146 alternates `0x88/0x22` (16 set bits), and resource 147
alternates `0xdd/0x77` (48 set bits). These exact masks can therefore be
reproduced without executing the original.

**Interpretation:** `PX00129` cannot be decoded into one globally transparent
texture. In particular, global white alpha deletes legitimate white font and
portrait pixels. The recreation now keeps an opaque atlas for fonts,
portraits, and opaque frames; a separate exact-white-keyed atlas for the
mapped `HIRED` stamp, sector-back arrow, gang-status markers, setup drag
frame, and objective-sector pylons. The recreation records and tests the exact resource-146 stencil but
does not apply it to local setup: that effect belongs to the unsupported
legacy `PX00146` flow, while local player-card scaling is opaque.

**Confidence:** High for the mixed native copy modes, portrait source
rectangle, status-marker and drag-frame roles, mode-0 Boolean operation,
selector bands, embedded masks, and complete source-surface call attribution;
Medium for semantic naming of every rectangle not yet used by the recreation.

**Next validation:** Name the remaining unused surface-6 rectangles and
pixel-compare the classified keyed/pattern roles when runtime captures are
permitted.

### BIN-UI-032 - exact main-console hit, split, and pressed geometry

**Observation:** Main-console dispatcher `0x004718ee` handles pointer input for
the city and detailed-sector screen states. It tests eight half-open outer
rectangles and passes cases 0 through 7 to pressed-control helper `0x00419022`:
Events `(500,126)-(548,174)`, Comlink `(552,126)-(600,174)`, Combat
`(500,178)-(548,226)`, Finance `(552,178)-(600,226)`, Gangs/Hire
`(500,230)-(548,278)`, Ranking/Search `(552,230)-(600,278)`, Done
`(500,282)-(600,330)`, and Game Info `(588,41)-(614,75)`.

The helper copies matching opaque `PX00129` pressed artwork from `(0,512)`,
`(48,512)`, `(96,512)`, `(144,512)`, `(192,512)`, and `(240,512)`, each
48 by 48; Done uses `(288,512,100,48)` and Game Info uses
`(190,386,26,34)`. It plays general-effect slot 2 once, restores the baked
surface-1 pixels whenever the pointer leaves the same outer rectangle, redraws
the pressed tile on re-entry, and succeeds only on release inside that tile.

The city/sector Hire input handler at `0x00416c75` divides the three 66-pixel
dock cells from x=438. Its Reject operation does not use the full right footer:
each release gate is `(top=437,left=472 + 66*slot,bottom=450,right=504 +
66*slot)`, i.e. a 32-by-13 half-open target. The broad portrait drag flow is
separate from this compact reject gate.

Five 48-pixel tiles select a subroute from the original press x coordinate,
not from separate vertical rows. Comlink, Combat, Finance, and Gangs/Hire use
the left 33 pixels and right 15 pixels: View/Send, Results/Detailed, City/Sector,
and Gangs/Hire respectively. Ranking/Search uses left 25 and right 23 pixels.
The strict comparisons are x greater than tile-left +32 or +24, so the boundary
pixel remains in the left route. Events, Done, and Game Info use their complete
outer rectangles.

**Interpretation:** The former recreation inferred 50-pixel rectangles from
the visible frame and split paired controls vertically. That both admitted
border pixels and routed large regions to the wrong action. The client now uses
the exact native tiles, horizontal subcontrols, original press identity,
release-inside cancellation, slot-2 press cue, and opaque pressed sprites on
both city and detailed-sector screens.

**Confidence:** High from the complete dispatcher, complete sole pressed-helper
switch and input loop, exact rectangle-constructor arguments, known branch
handlers, and matching `PX00128`/`PX00129` artwork.

**Next validation:** Remaining main-screen static work is content-layer and
cursor-role classification rather than console hit geometry.

### BIN-UI-033 - exact Siege and Big Man objective-sector pylons

**Observation:** Complete city compositor `0x004123cc` first copies the neutral
map, replaces owned 52-by-50 interiors from the six ownership sheets, and then
tests gameplay scenario IDs 6 (**Siege**) and 8 (**Big Man**). For Siege it
compares every sector with the six dwords at `0x00494818`; initializer
`0x00439563` writes the fixed HQ candidates 9, 12, 30, 33, 51, and 54 there,
and setup permutation helper `0x00476726` assigns those same sectors to the six
players. For Big Man it tests literal sector IDs 27, 28, 35, and 36.

Both branches construct the same source rectangle with the executable's
top/left/bottom/right helper arguments `(15,344,67,398)`, which is ordinary
atlas rectangle `(344,15,54,52)`. The pixels are the two gray pylon structures
on exact-white background. Destination construction places the sprite over the
complete 54-by-52 city cell at `(4 + 53*column, 3 + 51*row)` on the map surface;
the later map placement adds screen offset `(2,44)`. Source and destination
dimensions match, so `FUN_00427864(6,2,...,1)` takes its unscaled mode-1 path
and removes exact white rather than falling through to an opaque stretch.

**Interpretation:** The pylons are one native objective marker shared by Siege
landmarks and Big Man's four scoring sectors. They are not procedural shapes,
and Big Man does not leave its special sectors visually unmarked. The
recreation now draws this exact keyed crop in both scenarios and removes its
former approximate Siege-only geometry.

**Confidence:** High from the complete renderer branches, fixed-sector table
writer and consumers, literal center-sector tests, rectangle-helper layout,
equal source/destination dimensions, keyed-copy mode, and visible atlas pixels.

**Next validation:** Golden-screen comparison remains useful corroboration;
the sprite identity, marked sector sets, copy mode, and placement are closed
statically.

### BIN-UI-034 - detailed-sector site and gang meters

**Observation:** Detailed-sector compositor `0x00410770` calculates each site
percentage as integer `progress * 100 / base Resistance`, with the
zero-Resistance case fixed at 100. It copies exactly that many pixels from the
100-by-3 green strip at `PX00129` `(354,0)`, whose rows are light green
`(148,255,148)`, green `(0,247,0)`, and dark green `(0,140,0)`. The underlying
site frame supplies the matching 100-by-3 red track. Gang-card compositor
`0x00410130` copies `Force * 6` pixels from the same green strip over the
60-by-3 red track embedded in the card frame. The red rows are
`(255,148,148)`, `(247,0,0)`, and `(148,0,0)`.

The same compositor first copies the complete 74-by-110 card source
`(162,15)-(236,125)`, overlays the active action's 64-by-9 source at
`(162,125 + 9*action)`, places the 64-by-64 gang portrait at card offset
`(5,20)`, and places three optional 20-by-20 equipment portraits at offsets
`(5,86)`, `(27,86)`, and `(49,86)`. Caller `0x00410770` places cards at
`x = 254 + 76*(index % 2)` and `y = 80 + 112*(index / 2)`, then draws the
player-color outline one pixel outside the finished card.

The gang-card roster is friendly-only. Every ordinary caller passes the active
player at `0x004abc84` as `0x00410770`'s second argument. The compositor clears
six card-slot entries at `0x004abc68`, then scans only that player's 81 gang
records at `0x00498da8 + player * 0xa20`. It renders records whose active sector
byte at `+2` matches the selected sector and whose active-player visibility byte
at `+12 + activePlayer` is nonzero, preserving roster-slot order. Because the
scan never visits another owner's record block, detected enemy gangs do not
appear among the detailed-sector cards. Enemy visibility instead affects the
red contested status marker and the separate Attack picker roster described in
`BIN-DETECT-001`.

**Interpretation:** Both detailed-sector meters are three-row bevels copied at
native length. They are not flat fills, site progress is truncated rather than
rounded, and gang Force advances in exact six-pixel steps. The original draws
site progress only for the active sector owner. The recreation retains its
requested violet enemy-site progress as an intentional visibility extension,
but applies the same highlight/center/shadow structure. The detailed-sector
gang cards are not a visibility roster: they show at most six active-player
gangs in native roster order. A red marker or an Attack target can therefore
legitimately identify detectable opposition that is absent from these cards.

**Confidence:** High from the complete site and gang compositors, all ordinary
`0x00410770` call sites and their active-player argument, exact record-block and
visibility-byte addressing, source and destination rectangles, arithmetic
branches, and decoded `PX00129` pixels. The friendly-only roster was also
confirmed in an original 1.1 runtime observation.

### BIN-UI-016 - Last Turn Events site-image treatment

**Observation:** Both bitmap stretch wrappers, `FUN_0042773e` and
`FUN_00427864`, call `SetStretchBltMode(destinationDc, 3)` immediately before
`StretchBlt` (call sites `0x004277fd`/`0x00427854` and
`0x004279a2`/`0x004279f9`). Win32 mode 3 is `COLORONCOLOR`. In the site
cooperation branch of `FUN_0044fd6c`, the source rectangle starts at x 12 and
the site's 64-pixel atlas row plus 1, with width 94 and height 62. After it is
stretched into the 242-by-158 aperture, `FUN_004266a6` and `FUN_00427e60`
combine it through pattern-bitmap resource 146. That 8-by-8 monochrome pattern
retains two pixels per row at x 0/4 on even rows and x 2/6 on odd rows, making
75 percent of the background pixels black. Resource 6004 is composited after
this operation and is therefore unaffected by the pattern.

**Interpretation:** The original effect is a color-preserving nearest-style
stretch followed by a fixed 25-percent ordered-dither mask over a centered,
borderless portion of `PX02000`. Linear filtering without that mask is a modern
presentation option, not the native default.

**Confidence:** High static evidence; agrees with supplied original captures.

**Next validation:** Pixel-compare several native site-event captures with the
recovered crop, `COLORONCOLOR` projection, and resource-146 mask.

### BIN-EVENT-001 - Last Turn report table, types, and lifetime

**Observation:** The original Last Turn store is a six-player table rooted at
`0x004aae08`, with player stride `0x140` and exactly 32 ten-byte records per
player. Each record contains an occupied byte, a two-byte report type, and
three two-byte arguments. Recorder `0x00477748` appends at the per-player count
in `0x004abca8`; when the count is already 32 it returns without shifting or
overwriting anything. Thus the first 32 reports survive and later reports in
the same resolution are discarded.

Reset wrapper `0x004726c0` clears every occupied byte immediately before it
enters the whole-turn resolver. The resolver `0x00472775` then zeroes all six
counts before any report calls. All 12 direct recorder call sites are inside
that resolver. The UI handler `0x0044f2fc` later scans all 32 occupied bytes for
the requested player in ascending slot order; it does not merge reports from
an older resolution.

Compositor `0x0044fd6c` has cases 0 through 9. Its exact Win32 string-table
labels and statically traced meanings are:

| Type | Original status | Trigger |
|---:|---|---|
| 0 | `NO EVENTS.` | Empty/default compositor case; not emitted by the resolver |
| 1 | `POLICE CRACKDOWN.` | Crackdown created |
| 2 | `SECTOR CONTROL ATTAINED.` | Sector captured |
| 3 | `SECTOR CONTROL LOST.` | Previous owner displaced |
| 4 | `SITE COOPERATION ACHIEVED.` | Influence completed |
| 5 | `RESEARCH COMPLETED.` | Research completed |
| 6/1 | `INSUFFICIENT CASH TO BRIBE.` | Bribe cash failure |
| 6/2 | `INSUFFICIENT CASH TO EQUIP.` | Equip cash failure |
| 6/4 | `INSUFFICIENT CASH TO HIRE.` | Hire cash failure |
| 7 | `UNABLE TO HIRE, SECTOR AT CAPACITY.` | Hire sector full |
| 8 | `UNABLE TO HIRE, MAX GANGS REACHED.` | Player gang limit reached |
| 9 | `PLAYER HAS BEEN ELIMINATED.` | Player eliminated; recorded for all six slots |

There is no generic command-failure or objective-update report type. Target
evasion, unavailable items, movement failure, and other rejection reasons do
not enter Last Turn Events through this table.

**Interpretation:** Last Turn Events is a bounded projection produced anew by
the immediately completed whole-turn resolution, not the complete mechanical
notification history. Its overflow policy is keep-first, not a ring buffer.

**Confidence:** High static evidence from table strides, both reset loops, the
complete recorder and caller inventory, the compositor switch, and executable
string-table resources 33 through 44. Runtime corroboration remains useful for
presentation timing, not for type membership or capacity.

**Implementation:** `LastTurnEventProjection` filters only the recovered native
types from the immediately completed turn, preserves emission order, and keeps
the first 32. The broader authoritative `NotificationQueue` remains a
recreation mechanism and is deliberately not misrepresented as the native
table. Status text now matches the executable strings exactly.

### BIN-UI-001 - combat animation cadence

**Observation:** `FUN_0042e040` references the four combat-strip resource bases:
7000 at `0x0042e812`, 7100 at `0x0042e86d`, 7300 at `0x0042eb41`, and 7200 at
`0x0042eb9f`, then enters the presentation loop in `FUN_00430c23`. That loop
reads and clears timer slot zero and advances its local phase; cases 3 through
10 render the eight consecutive 64-pixel frames. Cases 13 and 15 draw the
changed portions of the two force bars in white, cases 14 and 16 restore the
bar areas, and cases 17 through 21 retain the completed result before case 22
exits. During initialization,
`FUN_00460ccf` calls `FUN_004327dc(0, 6)`. The timer helper configures
`timeSetEvent` with an integer period of `1000 / rate` milliseconds.

For phases 3 through 10, `FUN_00430c23` copies each 64-by-64 source frame into
backing-buffer rectangles `(left=150,top=274,right=214,bottom=338)` and
`(223,274,287,338)`. The shared panel occupies backing-buffer y=144..353 and is
then copied to screen `(104,124,448,333)`, producing exact screen animation
apertures `(254,254,64,64)` and `(327,254,64,64)`. These are inset within the
wider 67-by-64 action cells; stretching a frame across a complete cell erases
the embedded green dividers.

The detailed-combat setup in `FUN_0042e040` copies the sector tile into the
backing-buffer rectangle `(left=31,top=155,right=85,bottom=207)` and draws its
code at `(52,210)`. Relative to the panel's backing-buffer top of 144, those are
local tile bounds `(31,11,54,52)` and local text point `(52,66)`.

The native capture confirms 64-by-64 gang destinations at local `(150,48)` and
`(223,48)`. Beneath them, Force is represented by two matching 60-by-3 beveled
tracks at local y=114 and y=121. Each track uses light, full-intensity, and dark
rows rather than a flat fill.

**Interpretation:** Combat presentation advances at 6 Hz: 166 milliseconds per
frame in the original integer timer configuration, or about 1.33 seconds for
one eight-frame attack/hit clip. Damage removed from each force bar flashes
white twice before settling into the missing-force color, followed by a
five-tick result hold.
The recreation therefore keeps the action-cell bounds for panel structure but
draws and clears animation pixels only inside the two exact 64-by-64 apertures.

**Confidence:** High static evidence. The recovered timer setup, timer-slot use,
and frame-phase sequence agree; behavioral observation also identified the
previous recreation playback as too fast.

**Next validation:** Capture an original combat sequence with frame timestamps
to quantify any scheduling jitter around the 166-millisecond nominal period.

### BIN-API-002 - audio and video

**Observation:** WINMM imports include `PlaySoundA`, `mciSendCommandA`, auxiliary
volume APIs, `timeGetTime`, `timeSetEvent`, and `timeKillEvent`. Sixteen Smacker
functions are imported by ordinal from `smackw32.dll`.

**Interpretation:** Effects likely use `PlaySoundA`, CD/music control likely uses
MCI, and Smacker owns intro/logo decoding. Multimedia timers may drive animation
or sound; their presence does not prove simulation timing.

**Confidence:** Verified imports; Medium API-role interpretation; Low timer role.

**Static follow-through:** The sound loader, all 110 calls to its gated playback
wrapper, and the CD-track selector/lifecycle are mapped in `BIN-SOUND-001` and
`BIN-MUSIC-001` below. The two movie files have also been structurally decoded
as the supported Smacker-v2 inputs documented in `ORIGINAL-FILE-FORMATS.md` and
`AUDIO-VIDEO.md`. Remaining media questions concern native presentation timing
and interruption behavior rather than ownership of these path literals.

### BIN-MUSIC-001 - CD track programs and lifecycle

**Observation:** `mciSendCommandA` is referenced by seven bounded helpers at
`0x00458b43` through `0x00458f3e`. The play helper at `0x00458b43` first asks
for the length of its final track with `MCI_STATUS`, then issues an
`MCI_PLAY` request with `FROM`, `TO`, and `NOTIFY`. The selector at
`0x004642bd` maps mode 0 to the inclusive range 2-2, mode 1 to 9-9, and mode 2
to 3-8. The complete 11-call selector inventory is:

- mode 0 at `0x004615d5` for initial title entry and at `0x004617ba`,
  `0x00461a33`, `0x00461be9`, `0x00461ed6`, and `0x00462098` after returning
  from the local/network/load game paths to the title application loop;
- mode 2 at `0x0046eb22` on entry to the outer game/turn function and at
  `0x0046f5ce` when a per-player endgame screen returns to a later active human;
- mode 1 at `0x0042b9f9` and `0x0042c40e`, on entry to the two endgame/award
  presentation functions; and
- the current mode at `0x00462ae8`, when the main event pump observes that MCI
  playback has stopped.

There are no selector calls on Title-to-Setup, Setup-to-Title, Options, Help,
or ordinary in-game panel transitions. Selector `0x004642bd` stops through
`0x00464385` only when its requested mode differs from `0x00487878`, records
the new nonnegative mode, and then starts that mode's range whenever music is
enabled. Thus the event-pump call repeats Track 2 or 9 and restarts the 3-8
range after Track 8. Deactivation and activation branches call pause
`0x00458c10` and resume `0x00458c5f`; shutdown closes the MCI device through
`0x00458f3e`.
The Options-application helper at `0x004652a0` treats byte `0x00487868` as a
0-10 music level. Zero clears the music-enabled flag and stops playback;
nonzero levels call `0x00458e68`, which writes `level * 25 * 256` to both
16-bit auxiliary-volume channels. The initialized level is 5, producing 32000
of 65535 per channel; level 10 produces 64000 rather than the absolute 65535
maximum.

**Interpretation:** Track 2 is the title/setup program, Tracks 3-8 are the
ordered gameplay program, and Track 9 is the endgame program. Each program is
restarted after its final track, without shuffle. Losing application focus
pauses music and regaining focus resumes it. The GOG-local `winmm.dll` adapts
these original CD-audio calls to the supplied Ogg files; the executable itself
still expresses the original physical-track policy.

**Confidence:** High static evidence for the exhaustive selector-call
inventory, track ranges, ordering, repeat, focus behavior, and precise
menu/program restart boundaries. Native audible timing and driver behavior
remain runtime-only.

**Recreation status:** `SoundtrackCatalog` encodes the three track programs and
`ChaosGame.Media.cs` switches them for title/setup, gameplay and endgame
screens, advances/repeats them, and pauses/resumes on focus changes. Playback
uses the recovered level-5 Music default and exact normalized volume conversion. The
title and in-game Options overlay exposes all 11 levels, with zero stopping music
and a later nonzero selection restarting the active program. A bounded,
versioned recreation-native preferences file remembers the selection safely.
This deliberately corrects the original registry writer described in
`BIN-OPTIONS-001`, whose read-only handle makes every attempted write fail.
Playback or preference-write failure remains presentation-only and cannot affect
deterministic simulation.

**Next validation:** Validate playback, focus changes, volume, and track
transitions on each supported native platform. Static menu ownership and
restart boundaries are closed.
The original preference-persistence behavior is now statically closed.

### BIN-SOUND-001 - effect slots, volume and setup cues

**Observation:** The loader at `0x0045867c` accepts a 0-47 memory slot and a
five-digit sound resource ID. Title initialization at `0x00460ccf` loads slots
0-4 from `SND00200`-`SND00204`, skips slot 5, and loads slots 6-9 from
`SND00205`-`SND00208`. The gated wrapper at `0x00464290` plays a loaded slot
only while byte `0x0048783c` enables effects. In both compact player-setup
handler `0x0040b9c0` and full local-setup handler `0x0040e0a0`, accepted
selector-arrow input plays slot 3 while rejected input plays slot 4. The image
loader wrapper at `0x00464108` independently identifies these screens: their
calls at `0x0040bc2e` and `0x0040e150` load `PX00145` and `PX00143`
respectively. Separate handlers `0x004677f0` and `0x00456f80` load `PX00144`
and `PX00146`; the four resources are distinct flows, not local-player-count
variants selected by one presenter.

The four-case compact-setup helper at `0x0040cba5` and full-setup helper at `0x0040eb5f`
draw a depressed push-button image, call slot 2 once, track whether the pointer
remains inside while the left button is held, restore the released image when
it leaves, and return whether release occurred inside. The setup destinations
match Add Player `(370,328)-(462,352)`, Remove Player `(468,328)-(560,352)`,
Start `(370,375)-(462,420)`, and Back `(468,375)-(560,420)`. If Add or Remove
cannot change the player count, the caller additionally plays rejected-input
slot 4 after the slot-2 press cue.

A complete slot-2 caller classification shows that this is the original's
general **pointer push** cue rather than a setup-only sound. Main-console helper
`0x00419022` plays it for each of its eight pressed-control cases; dispatcher
`0x004718ee` uses those cases for the Events, Comlink, Combat Results/Detailed,
gang/item, finance, ranking, Done, and Options routes, including paired
subcontrols inside several console tiles. Shared pointer helper `0x00418821`
normally plays slot 3, but its case 2 instead plays slot 2; its only case-2
callers are the two Hire rejection paths in `0x00416c75`, and a successful
rejection selection receives no subsequent slot-3 cue. The `PX00132` handoff
handler calls slot-2 helper `0x00439f7a` for Ready. Endgame/awards screens call
slot-2 helper `0x0042cb95` for their three visible controls. The remaining
direct sites belong to the compact/full setup helpers and unsupported legacy
`PX00144`/`PX00146` setup flows (`0x00438da5`, `0x00468e12`, and
`0x00457eed`). Thus all nine genuine direct slot-2 wrapper calls are assigned
to a pressed pointer-control family; a nearby Combat Results call at
`0x0045286c` is slot 3 and was only a scalar-window false positive.

The Options application helper at `0x004652a0` reads effect level byte
`0x00487864`, enables effects when it is nonzero, and passes `level * 25` to
`0x00458b05`. That helper shifts the value by eight and duplicates it into the
two 16-bit `auxSetVolume` channels. Thus the independently adjustable effects
scale is 0-10: level 5 produces 32,000 per channel and level 10 produces
64,000. The initialized data block is more specific than the Help text:
Effects byte `0x00487864` starts at level 6 (38,400 per channel), while Music
byte `0x00487868` starts at level 5 (32,000 per channel). The original Help
describes each independent slider as having a Medium default but does not assign
that label a number.

The panel-entry helper at `0x0041953e` is called by 23 original panel handlers.
It plays slot 0 immediately before its right-to-left copy loop, but only while
the Slide Panels preference byte at `0x00487840` is enabled. The matching exit
helper at `0x004196f5` has the same 23 callers and plays slot 1 before its
left-to-right copy loop under the same preference gate. Disabling Slide Panels
therefore suppresses both the motion and its paired cue; these are not generic
ungated dialog sounds.

A bounded inventory of all 110 direct calls to the gated wrapper confirms that
the executable passes only slots 0 through 8; no direct call passes slot 9.
Pairing those callers with the image-loader wrapper identifies rejected-input
slot 4 in the handlers for Hire (`PX05000`/`PX05016`), Item and Site Information
(`PX05001`/`PX05002`), Attack, Equip, Influence, Move, and Research
(`PX05003`-`PX05007`), City/Sector Financial (`PX05008`/`PX05019`), Last Turn
Events, Player Ranking, Combat Results and Detailed Combat
(`PX05010`-`PX05014`), Give (`PX05015`), incoming/outgoing Comlink
(`PX05017`, `PX05018`, and `PX05023`), Gangs in Sector (`PX05022`), and Search:
Sites (`PX05024`). These are conditional failure branches inside the panel
handlers, not panel-open sounds. Valid confirmation/cancellation paths instead
enter shared keyboard helper `0x00418ccc` or pointer helper `0x00418821`; both
play slot 3 before animating the accepted control. Invalid command and boundary
branches can bypass those helpers and call slot 4 directly. This indirect call
relationship explains why the command handlers have no paired direct slot-3
call while their successful submissions still make the accepted-control cue.

The Last Turn Events handler `0x0044f2fc`, Combat Results handler `0x00451f80`,
and incoming-Comlink handler `0x0045d61a` each implement the same bounded page
rule for keyboard and pointer input. Previous on page zero and Next on the last
page leave the index unchanged and play slot 4. A legal step invokes the
screen-specific arrow helper (`0x00451602`, `0x004543ee`, or `0x0045e7ce`),
which plays slot 3 before drawing the pressed arrow and changing the page.
There is no first-to-last or last-to-first wrap.

Combat Results is sector-paged rather than event-paged. Handler `0x00451f80`
scans sector IDs `0..63` and appends qualifying sectors in that order. Its
table at `0x004a8888` has a `0x96`-byte sector stride and a `0x18`-byte player
stride: each player row holds six four-byte combat-result pairs. The combat
resolver at `0x00472775` clears those six entries to `-1`, packs resolved gang
records into them, and maintains the per-player police flag at `0x004a8918`.
A sector qualifies when the viewer has a result there or currently occupies it,
and at least one player has a result. Renderer `0x00453a8d` walks all six row
entries and lays them out as the two-by-three `YOUR FORCES`/`ENEMY FORCES`
grids. Its callers at `0x0045351e` and `0x00453a78` pass origins `(0x67,0xad)`
and `(0xf6,0xad)`; the renderer adds 44 by entry parity and 52 by entry pair,
confirming local x origins 103/246 and global y origin 173. The center column
always represents the other five players in player-ID
order; it draws an alternate dim portrait for a player without a result in the
sector, defaults to the first available opponent, and accepts only populated
slots. Changing that opponent calls the gated sound wrapper with slot 3 before
redrawing, while clicking the already selected or an unavailable portrait does
not.

The completed-Research branch in event compositor `0x0044fd6c` loads `PX06005`,
loads the selected `PX04xxx` strip as a 48-by-720 surface, and copies an exact
48-by-48 frame into its exact 48-by-48 monitor destination. It performs no
opaque-pixel bounding or per-item centering, so asymmetrical objects such as the
Whip intentionally look off-center within their correctly aligned frame.

Detailed Combat at `0x0042e040` temporarily reuses otherwise-empty slot 5 for
each combatant. An equipped attack loads resource `500 + Item.Sound`. An
unarmed attack loads `SND00500`, or `SND00501` when the attacking gang's base
Martial Arts value is positive. A detected police attack loads `SND00518`;
undetected police attacks have no presentation. The evasion sentinel does not
load a valid attack sound. The timeline at `0x00430c23` calls the gated slot-5
wrapper immediately before advancing frames, and the presenter unloads slot 5
after that combatant's sequence.

The same `0x0042e040` branches recover the exact paired animation mapping.
For a normal-direction unarmed attack, base Martial Arts greater than zero
selects attacker strip `PX07001` and recipient strip `PX07118`; otherwise it
selects `PX07000` and `PX07102`. This is a positive-Martial-Arts test, not a
comparison between Strength, Fighting, and Martial Arts. An evaded hidden
target selects attacker strip `PX07027` together with recipient strip
`PX07100`; omitting that second strip leaves the target aperture black. For
both unarmed and equipped attacks, zero damage overrides the normal recipient
strip with `PX07101`. The mirrored retaliation path applies the same rules to
`PX072xx`/`PX073xx`. Detected police selects `PX07228` with `PX07320` on
damage or `PX07301` on zero damage, while the undetected branch never enters
the presenter.

**Interpretation:** Slots 0 and 1 are the Slide Panels entry and exit cues.
Slot 2 is the general push-button press cue; slots 3 and 4
are the accepted-selection and rejected-input cues. Slot 6 is the incoming
Comlink alert: the bounded Comlink recorder at `0x0045d2f0` plays it when
appending a message for the active player, and the city/planning entry paths at
`0x00462579` and `0x0046fd80` play it when their pending-message flag is set.
The recorder sets pending byte `0x0048781c`, plays slot 6 immediately, and
zeros repeat counter `0x00487808`. Planning entry rebuilds the pending byte by
scanning the active player's 16 records for an occupied unread entry, plays
slot 6 once, and also zeros the repeat counter. Timer slot zero runs at the
already recovered 6 Hz. On each tick the event pump advances an eight-step
animation counter; every eighth tick advances the repeat counter modulo three,
and its wrap to zero plays slot 6 while pending remains set. The resulting
repeat interval is exactly 24 timer ticks, or four nominal seconds. Message-view
helper `0x0045e04d` marks the current record read and rescans all 16 read bytes,
so the repeat stops after the final unread record is acknowledged rather than
merely when the Comlink panel opens.
Slot 9 is loaded but has no call site through the
only gated general-effect wrapper in this executable. Music and effects share
the same numeric conversion but have separate state and enable flags.

**Confidence:** High static evidence for slot/resource mapping, panel entry/exit gating, pointer-push,
full/compact setup selection, main-console, Hire rejection, handoff, endgame,
rejected-input panel coverage, and Comlink-alert roles, the lack of a
slot-9 wrapper call site, scale, enable boundary, initialized levels, and channel values; High manual
evidence for the independent controls.

**Recreation status:** all nine general resources are loaded through the
recovered slot table, whose known roles are named in code. The four recovered
full local-setup push controls use slot 2; setup selector changes
use slot 3, and a rejected player-count boundary additionally uses slot 4. The
main-console controls, Hire Reject controls, handoff Ready control, and endgame
Awards/Stats/Done controls use slot 2 on pointer press. A successful pointer
Hire rejection does not add slot 3; a rejected operation may still add slot 4.
Mapped management and command workflows now play slot 4 when the player asks
for an unavailable action, selects no required item/target, submits a rejected
command or transaction, or opens an empty Events, Combat Results, or Gangs in
Sector view. Successful submissions, standard panel confirmations/cancellations,
and the Search All/None controls use slot 3 through their matching accepted
input paths. The Last Turn Events, Combat Results, and incoming-Comlink pagers now stop at both
ends, use slot 3 for a legal page step, and use slot 4 for a rejected boundary
step. The four setup controls defer their action until release inside the originally
pressed rectangle and cancel a release outside. Their held-inside state uses
the exact four source rectangles from `PX00140`, while leaving the rectangle
restores the baked `PX00143` control. A human handoff into an unread Comlink
inbox plays slot 6 once before entering the planning UI.
While an inbox remains unread, a presentation-only cadence repeats slot 6 every
four seconds on match screens. Handoff defers the first alert until Ready, and
opening an unread save/replay directly into planning also starts the cadence.
The cadence clears when authoritative unread state clears and never enters the
canonical match hash. Opening Comlink View and paging now mark only the displayed
record read through the authoritative path, matching the recovered per-record
behavior and keeping the alert active while any retained record remains unread.
Every routed panel transition plays the recovered slot-0/slot-1 entry and exit
cues while Slide Panels is enabled; nested panel transitions close the old
panel and open the new one. The idle-gang confirmation follows the same
preference gate.
Equipped, unarmed, and detected-police combat events route their recovered
sounds, while evasion remains silent. Combat and general effects share the
independent recovered Effects level and its level-6 default, and both audio
levels persist in the recreation-native preferences file. Each Detailed Combat
clip carries its event-time cue; the player emits it on the recovered first
animation tick, so retaliation waits for its reversed second clip instead of
playing with the opening attack. Simple Combat does not enter this presenter.

**Next validation:** Validate per-record Comlink acknowledgement, the slot-6
cadence, and countdown-warning cadence at runtime,
then validate overlap/interruption and native amplitude behavior.

### BIN-SEARCH-001 - per-player site filters and city markers

**Observation:** Search handler `0x00448e32` loads `PX05024` and the 220x56
`PX00150` sheet. Its 22 row states live at `0x004a24e8 + player * 22`; ALL and
NONE write all 22 bytes, an individual row toggles one byte, and a row
double-click calls Site Information handler `0x0044c476` with the selected
definition. Fresh-game initialization at `0x0046e766` clears the complete
per-player table.

The sole city consumer at `0x00412990`, inside redraw routine `0x004123cc`,
walks the three physical site slots of each sector. A site controlled by the
active player is always visible. Any other site is visible only when its
definition's Search byte is set. Visible sites receive compact ordinals 0, 1,
and 2 and enter marker renderer `0x00412ac4`. That renderer copies a transparent
20x14 rectangle from `PX00150`: source x is `(definition % 11) * 20`, source y
is `(definition / 11) * 14 + (controlled ? 0 : 28)`. Its destination within
the 432x416 city buffer is x `(sector % 8) * 53 + 9` and y
`(sector / 8) * 51 + ordinal * 15 + 7`. The sheet's pure-white cell background
is the transparency key; it is not part of the white/gray controlled-site icon.

**Interpretation:** Search is a persistent per-player presentation filter for
individual city-site markers, not a sector highlight. Influence ownership is
the recreation's exact controlled-site predicate. With the overview filter
empty, only the active player's influenced sites appear using white/gray icons;
this includes the zero-resistance Headquarters in the player's starting sector,
which is controlled from the beginning without a separate Influence action and
contributes its `+2` Tolerance site effect.
Enabling site types through the 1.1 Search overview additionally reveals their
uninfluenced instances using amber icons, matching the 1.1 release notes. The
Search double-click shows definition-level information, including base rather
than live remaining Resistance.

**Confidence:** High static evidence from the complete handler, initializer,
sole selection-table consumer, and marker renderer, plus supplied original 1.1
city captures for marker colors and transparency.

**Recreation status:** The checklist now mutates an independent filter for each
player, draws the exact controlled/uncontrolled `PX00150` crops at recovered
city coordinates, compacts visible slots, and routes row double-clicks through
definition-level Site Information. Presentation filters reset on fresh match,
native-save load, and replay load because they are not authoritative state.

**Next validation:** Capture native Search and city golden screens to validate
palette transparency and pointer timing.

### BIN-SEARCH-002 - exact Search panel controls and row targets

**Observation:** Complete Search handler `0x00448e32`, reached from main-console
dispatcher `0x004718ee`, loads `PX05024`, initializes its display from the
active player's 22 bytes, and opens the standard panel. It treats these
panel-local rectangles as half-open:

| Control | Rectangle | Effect |
|---|---|---|
| ALL | `(33,16)-(82,39)` | Sets all 22 bytes to one, redraws the whole checklist. |
| NONE | `(33,48)-(82,71)` | Clears all 22 bytes, redraws the whole checklist. |
| Done | `(33,169)-(82,191)` | Uses the shared press/release helper and closes. |
| Site `n` | `(102 + 116*(n/11), 22 + 15*(n%11))` to `+ (114,15)` | Toggles just byte `n`; a double-click instead opens Site Information. |

ALL and NONE use distinct shared pressed-control sprites 4 and 5. The handler
constrains pointer input to `(104,124)-(448,333)` before translating it to
panel-local coordinates. Enter and Execute activate Done; right-click and all
other keyboard navigation behavior are not native Search-handler branches.

**Interpretation:** The previous recreation placed each row six pixels too far
left, four pixels too high, and made it one pixel too wide/short; it also used
an incorrect column/row pitch. The two native columns have a 116-pixel origin
stride and their rows have a 15-pixel pitch. ALL and NONE are 23 pixels high,
not generic 22-pixel command buttons.

**Confidence:** High from the complete handler, literal rectangle/point/size
arguments, per-byte mutation loop, direct double-click Site Information call,
and shared pressed-control call sites.

**Recreation status:** Search now uses the exact ALL/NONE and all 22 row hit
targets, with focused layout regression coverage. The recreation retains its
documented modern keyboard navigation; mouse behavior is native.

**Next validation:** Golden-screen and interaction captures for palette/cursor
treatment and click timing.

### BIN-COMLINK-001 - per-player message queue capacity and overflow

**Observation:** The Comlink recorder at `0x0045d2f0` stores one 166-byte record
in a player-strided table at `0x0049ca90`. The per-player count at
`0x004981e0 + player * 4` is compared with `0x10`. Counts below 16 append at the
current index. At capacity, the routine sets the index to 15, copies records
1 through 15 down into slots 0 through 14, writes the new record to slot 15,
and increments the count back to 16. It also decrements the player's visible
message cursor at `0x004981c8 + player * 4` when positive, otherwise retaining
zero.

All four direct callers have now been classified. Two calls in the legacy
transport dispatcher at `0x0046ba84` handle packet type 10 and pass either
message ID 0 or the received dynamic ID. The other two calls in the report
composition handler at `0x0045eab1` pass `-1`, which copies the already-formatted
global message buffer, and broadcast it to each enabled recipient. The recorder
itself sends packet type 10 for remote recipients. Consequently, Last Turn
Events filtering is unrelated to this capacity/overflow routine.

The composition handler loads `PX05018` (or alternate resource 5023), whose
template is labeled `COMLINK: SEND MESSAGE` and contains six recipient cells
plus four 40-character message rows. Its helper at `0x0045fdf1` renders six
recipient selectors and those four rows from offsets within the same 166-byte
buffer. The original Help independently states that Comlink View stores the 16
most recent messages sent by other human Overlords and that Send can target
multiple human recipients.

**Interpretation:** Each human player retains the newest 16 Comlink messages;
overflow deterministically drops exactly the oldest message and preserves the
viewer's logical position relative to the shifted entries. This bound does not
apply to the separate Last Turn Events panel.

**Confidence:** High static evidence from the complete bounded recorder,
literal capacity, record stride, copy bounds, count update, and cursor branch.

**Recreation status:** Authoritative local-human delivery now validates the
active command-phase sender and human recipients, supports deterministic
multi-recipient delivery, retains the newest 16 messages, and tracks unread
state. Version-23 saves, version-25 replays, and canonical hash version 26
include every inbox. The client routes the original `PX05017` View and
`PX05018` Send panels, including newest-first entry, paging, per-record read state,
six recipient cells, the four recovered 40-character rows, and the main-console
unread blink.

**Next validation:** Compare the routed panels against a native golden-screen
capture and recover the remaining legacy record fields; network transport
interoperability remains out of scope.

### BIN-COMLINK-002 - View navigation and hit geometry

**Observation:** View handler `0x0045d61a`, reached only from the left Comlink
half in main-console dispatcher `0x004718ee`, opens `PX05017` and copies it to
the standard panel destination. It reads the active player's count at
`0x004981e0 + player * 4`; before opening, it scans the 16 records at
`0x0049ca90 + player * 0xa60` in ascending storage order and selects the first
occupied record whose read byte is clear. If none is unread, it preserves the
existing cursor. An empty inbox plays general-effect slot 4 and returns without
opening the panel.

For a nonempty inbox, virtual keys Left (`0x25`) and Right (`0x27`) invoke the
bounded page helper, respectively refusing the first and last page with
slot 4. Enter (`0x0d`) and Execute (`0x2b`) invoke the standard pressed-control
helper over the dismiss rectangle. Pointer releases are first constrained to
the panel `(104,124)-(448,333)`, then translated to panel-local coordinates.
The half-open controls are Previous `(31,33)-(57,56)`, Next `(59,33)-(85,56)`,
and dismiss `(33,169)-(82,191)`. Each page change redraws the native dynamic
region `(104,124)-(448,333)` after rerendering the message. There is no Up,
Down, or Backspace navigation branch in this handler.

**Interpretation:** View starts at the oldest unread retained message, not
unconditionally at the newest record. Its arrows are 26-by-23 controls with a
two-pixel gap, and the 49-by-22 dismiss hit box is not the broader shared
command button inferred from nearby panels. The recreation now uses these exact
half-open bounds and the native Left/Right/Enter/Execute navigation set; its
global Escape/right-click panel return remains a documented modern navigation
convenience outside the original handler.

**Confidence:** High from the complete handler, its sole caller, literal
rectangle construction, virtual-key comparisons, cursor boundary branches, and
the already recovered Comlink record table.

**Recreation status:** Exact View hit geometry and keyboard navigation are
implemented and covered by `ComlinkUiTests`.

**Next validation:** Compare the rendered text/portrait fields and page-change
cadence with a native golden-screen capture; legacy record fields and transport
remain separate questions.

### BIN-COMLINK-003 - Send eligibility, controls, and composition cursor

**Observation:** Send handler `0x0045eab1`, the other Comlink branch from
`0x004718ee`, loads `PX05018` (or its alternate network template). It derives
six eligibility bytes from the enabled/player-controller arrays, clears the
active player, and refuses entry with general-effect slot 4 when no other human
recipient is eligible. Its six half-open, panel-local recipient controls are
`(98,20)-(198,52)`, `(98,54)-(198,86)`, `(98,88)-(198,120)`,
`(219,20)-(319,52)`, `(219,54)-(319,86)`, and `(219,88)-(319,120)`.
Eligible clicks toggle an independent selector byte; ineligible clicks reject.

The Cancel control is `(33,137)-(82,159)` and Send is `(33,169)-(82,191)`.
Both use shared held-button helper `0x00418821`: Cancel copies source
`PX00129 (50,409,50,23)` and Send copies `(50,386,50,23)` to a one-pixel-larger
50-by-23 destination while held inside, restores the baked face on pointer
exit, plays slot 3 on press, and activates only when released inside. Recipient
cards instead toggle directly after their hit test. Send with no selected
recipient rejects before entering the held-button helper.
Only virtual key Execute (`0x2b`) invokes Send; it rejects an empty recipient
selection. Enter (`0x0d`) sets the text cursor to column zero of the next row,
Backspace (`0x08`) deletes at the current cursor, and Left/Up/Right/Down move a
clamped four-row, 40-column cursor. Printable ASCII is uppercased before its
`0x20..0x5a` acceptance check. Cell writer `0x004600d2` paints each 6-by-7
glyph at `(199 + 6 * column,256 + 8 * row)`. Caret helper `0x0046023c` redraws
the focused cell from the ordinary `PX00129` glyph row at y=0 or the matching
inverse glyph row at y=441, using the same opaque surface-6 copy helper as its
panel artwork. Send begins with the normal row; each third consumed timer-0
event toggles this source row. The window-message callback
`0x004327c0` and direct signal helper `0x00432926` each raise one of the four
bytes at `0x00494810`; test/clear helpers `0x004328be`/`0x004328f8` make every
consumer observe a pending event once. The Send loop starts by signalling timer
0 and only advances its three-event counter when it consumes that flag.
Initialization routine `0x00460ccf` registers timer 0 through `0x004327dc` at
frequency 6; that helper calls `timeSetEvent(1000 / frequency, 20, ...)`, so
the timer period is integer `1000 / 6 = 166` ms and a glyph-row phase lasts
three timer events (498 ms).

**Interpretation:** Recipient selection is a 100-by-32 name-and-portrait target,
inset by one pixel within a 105-by-34 tile; it is not the narrower visual
portrait cell. Renderer `0x0045fdf1` paints every player card, including the
active player and computer/inactive cards, then uses the eligibility byte only
for its dimmed presentation and click rejection. Its backing color changes from
black to green when the selector byte is set. Enter is an editor navigation key,
not a message-submit shortcut. The original's composition is a fixed 4x40
editable grid rather than an append-only text field.

**Confidence:** High from the complete handler, literal rectangle construction,
eligibility/recipient loops, virtual-key switch, character-range branch, and
the `PX05018` resource call.

**Recreation status:** Send recipient hit testing now uses the exact wide native
rectangles, and only Execute can submit via keyboard. The text field is now the
recovered 4x40 overwrite grid: uppercase/range filtering, cursor-position
editing, Enter/arrow movement, horizontal wrap, row clamping, and Backspace
all follow the handler. Its normal glyph origin and 8-pixel row stride are also
exact. Recipient cards now use the recovered 105-by-34 backing/portrait/name
projection while eligibility remains authoritative. The managed cursor now
copies the recovered normal/inverse `PX00129` glyph cells and advances the
normal-to-inverse phase after each three 166-ms timer-zero events, without
making that presentation state part of a match. Cancel and Send now retain the
native pressed source sprites, release-inside action, and slot-3/slot-4 outcome
order; recipient selectors keep their direct toggle behavior.

**Next validation:** Compare the rendered caret phase, field, and recipient
tiles with a native golden-screen capture.

### BIN-COMLINK-004 - View record fields and projection

**Observation:** View projection helper `0x0045e04d` receives the active player
and retained-record count from `0x0045d61a`. It first sets byte `+1` of the
selected 166-byte record and then scans all 16 record read bytes to update the
global unread indicator. It copies the complete record from
`0x0049ca90 + player * 0xa60 + cursor * 0xa6` before formatting it. The copied
layout is:

| Offset | Size | Meaning | Evidence |
|---:|---:|---|---|
| `+0` | 1 | Occupied flag | View's unread-start scan tests it before byte `+1`; recorder appends/compacts complete records. |
| `+1` | 1 | Read/acknowledged flag | View sets it and all 16 rows are scanned for an unread record. |
| `+2` | 2 | Zero-based message turn | View formats `2050 + turn / 52` and `turn % 52 + 1`. |
| `+4` | 1 | Sender player slot | View indexes the 12-byte player-name table and active portrait/color tables with it. |
| `+5` | 160 | Message text | View copies four consecutive 40-byte rows and renders each separately. |
| `+165` | 1 | View-unconsumed tail | Completes the fixed 166-byte copy; it has no independent View consumer. |

The page header is rendered from the one-based selected record number and
record count. View selects the sender's fixed 10-character player name and
32-by-32 portrait/color record, then projects the four message rows. Its date
formula matches the city clock: record turn zero is `2050.01`.

**Interpretation:** The original Comlink record is a complete self-contained
view snapshot. It has no timestamp, recipient list, or wire packet ID in the
View-consumed portion; the only presentation state per record is its read bit.
The recreation's authoritative inbox already carries the equivalent occupied
queue position, read set, turn, sender, and bounded 160-character text rather
than exposing this native memory layout.

**Confidence:** High from the complete View renderer, the bounded recorder
`0x0045d2f0`, exact `0xa6` stride, four literal `0x28` row copies, and the
date/name/portrait table consumers.

**Recreation status:** Field semantics are represented and persisted in the
recreation-native Comlink inbox. The native in-memory arrangement remains
documentation only, as legacy save and transport interoperability are outside
scope.

**Next validation:** Golden-screen compare the View field locations, button
states, and palette treatment; no further static record-field recovery remains.

### BIN-OPTIONS-001 - registry keys, initialized defaults, and idle-gang warning

**Observation:** Loader `0x0046439a` opens
`HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` once with access mask
`0x20019` (`KEY_READ`) and queries thirteen four-byte values in this order:

| Registry value | Destination | Compiled default |
|---|---:|---:|
| `prefsVidDeep` | `0x00487844` | 1 |
| `prefsSlide` | `0x00487840` | 1 |
| `prefsBaseStats` | `0x0048784c` | 0 |
| `prefsCombat` | `0x0048785c` | 1 |
| `prefsFreeGang` | `0x00487860` | 1 |
| `commType` | `0x00487884` | 0 |
| `prefsVolumeSFX` | `0x00487864` | 6 |
| `prefsVolumeCD` | `0x00487868` | 5 |
| `prefsDiff` | `0x00487850` | 1 |
| `prefsTimeLimit` | `0x00487854` | 0 |
| `prefsObjective` | `0x00487858` | 0 |
| `prefsFullScreen` | `0x0048786c` | 1 |
| `serialNum` | `0x00487870` | 0 |

All twelve preference fields are written as `REG_DWORD` by `0x00464783`, but
that routine opens the same HKLM key with the same `KEY_READ` mask. It never
requests `KEY_SET_VALUE`, and ignores every `RegSetValueExA` result. Its three
callers are in the application flow at `0x00460ef9`, `0x00460f40`, and
`0x0046224a`; none can persist a change through this handle. The loader likewise
ignores every query result and reuses one DWORD without resetting it between
names. A missing or malformed later value can therefore consume stale data from
the preceding query instead of retaining its compiled default. When the final
effective serial is zero it consumes two bounded RNG calls and attempts to write
the generated value through the same read-only handle, so that write also fails.
An installer or external tool may still have pre-populated the machine-wide
values before launch.

Reads of the Slide byte occur directly in the panel-open and panel-close
functions at `0x0041953e` and `0x004196f5`; the Base Stats byte is consumed by
gang/statistics presentation paths; and the Combat byte is read by the
Done/combat path at `0x0046fd80`. That same city handler scans all 81 gang slots
on Done; an active slot whose action byte is zero is idle. When `prefsFreeGang`
is enabled and such a slot belongs to the active player, the path invokes the
two-choice modal at `0x00448718`. Its open and close paths use the panel-slide
functions above, which play general-effect slots 0 and 1. The original Help
independently says Done warns about gangs without commands unless Warn If Idle
Gangs is off.

**Interpretation:** The original defaults are Slide Panels on, Current rather
than Base gang statistics, Detailed Combat on, and Warn If Idle Gangs on.
The warning is a confirmation boundary around finishing planning, not a
simulation rule; continuing still permits unassigned gangs.

**Confidence:** High from the initialized data, complete ordered load/save API
calls and access masks, all three writer callers, direct consumer references,
bounded Done-path scan, dialog call graph, and matching Help description.

**Recreation status:** Options uses the recovered defaults and persists its
warning toggle, base/current gang-stat display, automatic Detailed Combat
playback, and bounded panel motion. The legacy 16-bit color choice is displayed
as always enabled by the modern renderer. Recreation-native global F11 switching
between windowed and borderless-fullscreen display is persisted in preference
version 6 without changing compatibility coordinates. Version-4 and version-5
preferences migrate without losing their earlier selections and safely default
the new display choice to windowed mode.
The safe local store deliberately does not reproduce the original read-only-HKLM
writer or cross-value stale-buffer behavior; see `DECISIONS.md`.
Finishing planning checks only the active player's living gangs and offers a
Continue/Go Back modal when any lacks a queued command. Opening and closing the
modal route the recovered general-effect slots 0 and 1.

The panel's wording is baked into `PX05020`: `SYSTEM WARNING:`, `IDLE GANG
DETECTED`, and `AT LEAST ONE OF YOUR GANGS HAS NOTHING TO DO. ARE YOU SURE YOU
WANT TO END YOUR TURN?` The Cancel button is above OK. Handler `0x00448718`
loads resource 5020, draws it at `(104,124,344,209)`, and hit-tests panel-local
Cancel `(33,137)-(82,159)` before OK `(33,169)-(82,191)` using Win32-exclusive
right/bottom bounds. Keyboard event type 2 confirms on virtual key `0x0d`
(Enter) or legacy `0x2b` (`VK_EXECUTE`) and cancels only on `0x1b` (Escape).
It contains no Y, N, or Backspace shortcut. The recreation now follows those
bindings and exact pointer rectangles; its missing-asset fallback reproduces
the complete baked wording.

**Confidence:** High from the complete handler, exact instruction comparisons,
rectangle-helper layout, `PtInRect` wrapper, and decoded `PX05020` pixels.

#### Panel-slide geometry and speed calibration

The panel-open function at `0x0041953e` and reverse close function at
`0x004196f5` copy a 344-by-209 panel horizontally from or toward the right
edge. The rectangle helper at `0x00425edf` packs its arguments as
`(top, left, bottom, right)`, confirmed by the `BitBlt` coordinate extraction
at `0x0042773e`; this also matches the decoded `PX050xx` dimensions. Their
primary form travels 344 pixels from source-buffer x=0 into screen x=104..448.
Its destination is exactly `(top=124,left=104,bottom=333,right=448)`. The
recreation now uses that native destination through one shared panel-local
coordinate system: background, baked-field clearing, dynamic content, buttons,
and hit regions all derive from origin `(104,124)`. This atomic migration avoids
the exposed placeholder strokes and displaced borders caused by moving only the
background while leaving descendants on the older y=125 basis.
An alternate form reads a 320-pixel source region beginning at buffer x=344
and moves it toward the same right-edge destination. Exhaustive literal-argument
classification identifies six open/close caller pairs using that form:
`0x0044b699` loads Item Information `PX05001`, `0x0044c476` loads Site
Information `PX05002`, `0x0044d1bb` loads City/Sector Financial
`PX05008`/`PX05019`, `0x004546c5` loads Hire comparison `PX05016`,
`0x0045519d` loads Game Info `PX05021`, and `0x00455b6b` loads Gangs in Sector
`PX05022`. The other seventeen caller pairs pass zero and use the primary
344-pixel form. The Hire handler makes the distinction explicit: it first loads
the 344-by-209 image into `(top=144,left=344,bottom=353,right=688)`, then
calls the nonzero slide mode. That mode copies only source x=344..664 to screen
x=128..448, so the final panel is the same 320-pixel alternate crop as the
other five callers rather than a shared-panel presentation.
Both calculate a step from the startup blit benchmark at `0x00432954`. That
benchmark counts identical copies for just over one second. The transition
divides the count by four, divides its travel by that result, and clamps the
step to at least 16 pixels. The intended uncapped duration is therefore about
one quarter second, while the minimum step prevents excessive intermediate
copies on faster hardware. Slide enabled plays general-effect slot 0 before
opening and slot 1 before closing; disabled mode skips intermediate copies and
still presents the final state.

The recreation now uses a bounded 250 ms time-based horizontal entrance over
the recovered per-screen 344- or 320-pixel travel. It intentionally avoids the
original startup-speed dependency. Runtime capture must still validate close
timing/interruption behavior.

#### Planning timer

The same preference block also initializes byte `0x00487854`, corresponding to
`prefsTimeLimit`, to zero. New-match initialization at `0x0046e766` maps values
0, 1, 2, and 3 to `-1` (disabled), 30,000 ms, 120,000 ms, and 300,000 ms.
The original Game Settings Help independently describes None, 30 seconds,
2 minutes, and 5 minutes and says the limit exists to constrain slow turns in
multiplayer games. The four controls occupy the right-hand rows alongside AI
Mentality in `PX00143`.

For a human planning entry, `0x0046fd80` calls the start helper at `0x0041b8bc`,
which records `timeGetTime`; computer planning skips it. The expiry helper at
`0x0041bdd5` returns true once elapsed milliseconds exceed the selected limit,
and the human loop then exits as if Done had been accepted. This check occurs
after the user-triggered idle-gang confirmation path, so timer expiry does not
open that confirmation. The drawing helper at `0x0041b8fc` first computes
integer elapsed percent as `(elapsedMilliseconds * 100) / limitMilliseconds`,
then computes visible width as `60 - (elapsedPercent * 60) / 100`; both
divisions truncate. This percent-first quantization differs from scaling the
remaining duration directly. It calls general slot 7 while remaining time is
strictly between 1 and 10 seconds and slot 8 while remaining time is greater
than zero and at most 1 second. In the input pump at `0x00462579`, counter
`0x00487898` is decremented before comparison; a zero calls the helper and
resets the counter to 6. Timer drawing and warning checks therefore recur every
sixth eligible pump call. The sound wrapper at `0x00464290` forwards every call
to the low-level player at `0x0045851a`; it has no timer-specific suppression.
Exact wall-clock cadence still depends on the original pump rate and needs a
controlled capture. In the supported asset pack, those two PCM clips last
approximately 0.117 and 1.189 seconds.

**Recreation status:** Setup exposes the four original choices at the original
hit regions and safely persists the selection, defaulting to None. A bounded
presentation-only timer starts when a human accepts the private handoff, remains
active through planning panels, renders the original percent-quantized 60-by-3
aperture, and checks the recovered warning slots every sixth fixed update. Expiry
submits the normal replay-recorded finish-planning operation and deliberately
bypasses the idle-gang confirmation. The timer itself is absent from Core state,
state hashes, snapshots, and replay payloads; only its resulting ordinary
operation is authoritative.

**Next validation:** Capture the original wall-clock warning cadence,
deactivation behavior, and whether modal dialogs perceptibly pause the timer.

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

## Resource lookup literals

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

## Timing and RNG candidates

### BIN-RNG-001 - original process seed

**Observation:** `GetTickCount`, `timeGetTime`, periodic multimedia timer APIs,
and asynchronous key state are imported. No external C runtime DLL appears in
the import table, so the C runtime RNG is statically linked. Process initializer
`0x00465620` calls `timeGetTime` at `0x004658fb`, zero-extends only the returned
AX/low 16 bits, and calls `0x00478cc0` at `0x00465905`. That adjacent runtime
function obtains the current thread data through `__getptd` and writes its sole
argument directly to `_holdrand`; it has no other incoming reference.

**Interpretation:** The original initializes its gameplay RNG once per process
from `timeGetTime() & 0xffff`, producing an unsigned seed from 0 through 65,535.
The other clock consumers remain presentation/network/timing candidates until
their individual data flow is classified.

The common startup caller `0x00460ccf` invokes this initializer at
`0x00460cf7`, then preference loader `0x0046439a` at `0x00460d14`. The loader
queries registry value `serialNum`; only when the resulting shared DWORD buffer
is zero, calls at `0x00464726` and `0x00464739` draw two inclusive `1..16384`
values and combine their zero-based forms into a legacy serial number. Each
bounded draw consumes three raw RNG values. The attempted registry write cannot
succeed because the key was opened with `KEY_READ`, and its result is ignored.
Moreover, failed queries do not have an isolated default buffer: `serialNum`
follows `prefsFullScreen` and can inherit stale query data when absent. Startup
therefore consumes either zero or six raw RNG values according to the effective
buffer value, not a reliable first-run/later-run distinction.

**Confidence:** High static evidence for the seed source, truncation, zero
extension, single writer, and process-initialization placement.

**Implementation:** Local-game startup captures the low 16 bits of the analogous
process-uptime millisecond clock when `ChaosGame` is constructed. Explicit
replay/test and multiplayer seeds remain full-width deterministic inputs.

**Intentional exception:** The recreation does not let an obsolete
installation-level network serial number perturb authoritative simulation RNG.
Its explicit seed always denotes the initial simulation stream. Native launch
fixtures must record the effective registry-query state, especially
`prefsFullScreen` and `serialNum`, and advance the predicted native stream by
two bounded calls only when the loader's final shared DWORD is zero.

**Next validation:** Correlate the first generated city against a native launch
whose low-16-bit seed and prior `serialNum` state are both captured.

### BIN-RNG-002 - runtime random step

**Observation:** Ghidra 12.1.3 identifies the function at virtual address
`0x00478cd0` as the statically linked Visual Studio 1998 `_rand`. It updates the
calling thread's 32-bit hold state using multiplier `0x343fd` and addend
`0x269ec3`, then returns bits 16-30 of the new state. Ghidra found one direct
game caller, at `0x0045d227`.

**Interpretation:** The recurrence and 15-bit output are the original build's
raw random-number step.

**Confidence:** High from function identification, constants, state write, and
single-caller cross-reference. Runtime output still needs black-box correlation.

### BIN-RNG-003 - bounded random wrapper

**Observation:** The function at `0x0045d227` clamps an input below one to one,
calls `_rand` three times, uses the third result as a selector, chooses the first
result when the selector is greater than `0x3ffe` and otherwise the second, then
returns the chosen value modulo the clamped input plus one.

**Interpretation:** Gameplay requests an inclusive random integer from one
through the input and consumes exactly three raw RNG values for each request.

**Confidence:** High for control flow, constants, range, consumption count, and
complete direct call-site ownership. Runtime output correlation remains open.

**Implementation:** `DeterministicRandom.NextRaw` and `NextInclusive` reproduce
these address-level facts. `DeterministicRandom.SeedFromTimerMilliseconds`
reproduces the original seed narrowing for local games; `MatchSetup.InitialSeed`
also remains an explicit modern deterministic input for tests, replays, and
multiplayer. `NextInclusive` also preserves the native below-one clamp and its
three raw draws; the recreation-only zero-based `NextInt` API retains strict
positive-bound validation.

Ghidra reports exactly 61 direct calls to `0x0045d227` in 24 containing
functions. All are now classified: 46 calls in 15 recovered AI dispatcher,
family, sector-target, and placement functions; 13 calls in eight simulation
functions covering setup portraits/reactions, city/site/HQ generation, hire
refill/Force, shared dice, and the whole-turn resolver; and the two conditional
startup `serialNum` calls above. No unknown direct gameplay wrapper caller
remains.

**Next validation:** correlate a controlled dice sequence with predicted
outputs; static call ownership is complete.

### BIN-RNG-004 - AI planning callers

**Observation:** The 46 direct wrapper calls in the AI group are confined to
dispatcher `0x00432da0`, shared sector/placement helpers `0x00408642` and
`0x00408214`, and the twelve recovered family-handler containers documented in
the AI sections below. Their action-selection branches, target pools, retry
loops, and persistent state writes have now been bounded individually. None is
an unclassified Combat or board-resolution caller.

**Interpretation:** Original-AI random consumption remains part of the same
global deterministic stream, but it is distinct from the whole-turn resolver's
dice and tie-break calls. The recreation keeps that separation while sharing
one serialized RNG state.

**Confidence:** High static evidence from the exhaustive incoming-reference
inventory and completed family/helper analyses. Fixed original-runtime AI
traces remain the missing black-box corroboration.

### BIN-RNG-005 - accepted local setup through initial city

**Observation:** Local setup handler `0x0040e0a0` accepts Begin, scans player
slots 0 through 5, and calls `0x00468c8e` only for an empty slot. That helper
draws bounded `1..15`, subtracts one, and retries while any slot already has the
portrait. After the handler returns true, its sole caller `0x00460ccf` loads a
status string and toggles UI state before calling outer match function
`0x0046e766` with the fresh-game flag. The intervening helpers and the outer
function's pre-fresh calls have no direct-call path to bounded RNG wrapper
`0x0045d227` within the checked eight-level call graph.

Inside fresh initializer `0x0046dc10`, the first RNG site is `0x0046dc83`: one
bounded `1..4` reaction draw for each of the six players, unless Homicidal
Maniac skips all six. It then calls city generator `0x00475fe1`, whose RNG paths
are the 40 density-center X/Y pairs followed by sector/site proposal draws, and
finally calls HQ permutation routine `0x00476726`. The bounded call-path report
finds exactly those direct/transitive wrapper paths from `0x0046dc10` before
the function returns. The offer filler remains later in `0x0046e766`.

The portrait helper's other caller is network/setup function `0x004677f0`; the
ordinary local path above does not pass through it. Interactive local setup
starts player zero at portrait zero, while configured human portrait changes
are explicit UI state rather than hidden random selections.

**Interpretation:** Given the RNG state at accepted local Begin, the original
pre-city stream is fully ordered: omitted-player portrait attempts, six
reaction draws (or zero at Homicidal), density centers, site proposals, and HQ
permutation. Rejection attempts consume normally at each stage. This does not
make the once-per-process seed equivalent to the state at Begin: an earlier
match or another RNG-using workflow in the same process can already have
advanced the original stream.

**Confidence:** High static evidence for the accepted-local-setup call chain,
consumer order, bounds, skips, and absence of an intervening bounded-RNG path.
The exact original state at Begin and city output still require a runtime
fixture because the seed is clock-derived and process-global.

**Implementation:** `OriginalMatchFactory.Create` treats `InitialSeed` as its
entry state, completes omitted slots in ascending order, then initializes AI,
generates the city, and assigns HQs in this recovered order. Fixed seed vectors
lock the recreation's complete setup result and final RNG state. Configured
human portraits are inputs and therefore do not replay prior UI interactions.

**Next validation:** capture the original RNG state context or an initial-city
fixture at accepted Begin; static analysis cannot recover a particular
clock-derived seed or prior process history.

### BIN-AI-001 - per-gang command dispatcher and action handlers

**Observation:** Focused Ghidra 12.1.3 analysis identifies `0x00432da0` as a
dispatcher reached from `0x00458fa0` at call site `0x004594df`. Its two
arguments index arrays with player stride `0x510` and gang stride `0x10`. The
dispatcher writes a selected family byte at `0x0048a250 + player * 0x510 +
gang * 0x10`, then dispatches that value through the following handler table:

| Value | Handler |
|---:|---:|
| 0 | `0x00428ef0` |
| 1 | `0x00434080` |
| 2 | `0x0041fef0` |
| 3 | `0x00435bd0` |
| 4 | `0x00401000` |
| 5 | `0x0043a1d0` |
| 6 | `0x00431c60` |
| 7 | `0x00436c70` |
| 9 | `0x004605e0` |
| 10 | `0x0042a6e0` |
| 11 | `0x00420950` |
| 12 | `0x004353a0` |
| 13 | `0x0040abc0` |
| 14 | `0x00466910` |

No case for value 8 appears in this handler switch. Several handlers write the
chosen command and parameters both to a `0x10`-stride array rooted near
`0x0048a250` and to a second projection with player stride `0xa20` and gang
stride `0x20` rooted near `0x00498daf`. Target-sector results are repeatedly
split using quotient and remainder by `0x51` (81).

**Interpretation:** The first byte is an AI strategy/action-family selector,
the handler switch maps it to command planners, and the two projections are
the original per-gang planning record and live gang-state projection. Division
by 81 encodes a player/sector or owner/sector pair. The later bounded handler
analyses identify every dispatched family, public command write, and target
encoding.

**Confidence:** High for addresses, call site, strides, switch values, handler
mapping, mirrored writes, division constant, record semantics, public actions,
and target encodings. The supporting evidence is completed in `BIN-AI-003`
through `BIN-AI-005` and the family-specific sections below.

**Static follow-through:** `0x00458fa0`, the relevant `0x00432da0` selectors,
all fourteen dispatched handlers, and all public command/target writes are now
bounded. Controlled original-runtime traces remain corroboration, not a gap in
the static mapping.

### BIN-AI-002 - scenario-sensitive family selection

**Observation:** Query selector 0 returns `DAT_004abbe8`; setup/save analysis
identifies this byte as the scenario ID in the same 0-through-9 order used by
`ScenarioId`. Query selector `0x7c` returns the per-player word at
`0x00482128 + player * 4`. When query `0x48` reports that the planning record's
byte at +1 is nonzero, `0x00432da0` maps scenario and query `0x7c`'s hire role to
the family below. A dash means the switch performs no assignment and preserves
the record's current family.

| Scenario | Mode 0 | Mode 1 | Mode 2 | Mode 3 | Mode 4 | Mode 5 | Mode 6 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Greed | 0 | 0 | 3 | 2 | 6 | - | 7 |
| Power | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Acceptance | 0 | 0 | 5 | 2 | 6 | - | 7 |
| Dominance | 0 | 0 | 5 | 2 | 6 | 3 | 7 |
| Kill 'Em All | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Big 40 | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Eliminate | 0 | 13 | 14 | - | 6 | 5 | 7 |
| Siege | 10 | 0 | 3 | 11 | 12 | - | 7 |
| Big Man | 0 | 13 | 14 | 3 | - | - | - |
| Armageddon | 0 | 1 | 3 | 2 | 6 | 3 | - |

Every assigning mode-4 branch also copies the signed query-`0x5a` gang
projection into the planning record's +12 word. Out-of-range modes likewise
preserve the current family. `OriginalAiFamilyRules` implements this table and
side-effect flag in isolation. The same function separately compares the
scenario global with values 6, 7, and 8 after family dispatch.

**Confidence:** High for selector storage, scenario identity/order, table
values, preserve behavior, mode-4 copy, and global comparisons. Hire roles 0
through 6 are internal schedule indices rather than named public concepts; their
complete scenario schedules, adjustment rules, ranking modes, and family
effects define their semantics in `BIN-AI-003B`.

**Static follow-through:** The outer planner's complete family-count,
affordability, standing, prior-role, and late-turn adjustments are recovered
below. `AiPlanningState` persists the six role pairs and all six-by-81 family
records in hashes, saves, and replays.

### BIN-AI-003 - outer AI planning pass and command history

**Observation:** `0x00458fa0`, the sole normal caller of the per-gang dispatcher,
is itself reached from `0x0040ab20` at `0x0040abac` and from the initialization
routine `0x0046e766` at `0x0046f4ec`. On first use for a player it calls
`0x00409de1` for all 81 indices and initializes per-player state. On subsequent
passes it iterates all 81 records with player stride `0x510` and record stride
`0x10`, copies bytes at offsets +5..+7 into +2..+4, clears +8..+10, and
decrements the two 16-bit fields at +12 and +14 only when their associated
queries are nonnegative. It skips records whose mirrored `0x20`-stride byte at
`0x00498daa` is decimal 100. It later iterates the same 81 records, seeds family
9 under a separate condition, and calls `0x00432da0` for every non-100 record.

After the per-record pass, `0x00458fa0` performs a ten-way switch on the same
state-query value 0 through 9 seen by `0x00432da0`. Each branch starts from a
deterministic current-turn schedule, adjusts that slot against existing family
counts, calls `0x004078d9` with an offer-ranking mode, and writes the selected
hire role to `0x00482128`. The function also iterates
64 entries in a separate sector-sized pass before dispatching gangs.

**Interpretation:** `0x00458fa0` is the outer AI planning pass. The +2..+4 and
+5..+7 triples are older/previous command projections, +8..+10 is the newly
planned triple, decimal 100 marks an unused gang slot, and +12/+14 are the
weapon/armor replacement cooldowns. The post-dispatch ten-way switch is the
scenario-specific hiring strategy, while the 64-entry pass prepares sector
weights. Save/load references and complete handler dataflow establish these
field meanings without requiring runtime deltas.

**Confidence:** High for callers, loop bounds, addresses, strides, copies,
clears, decrements, sentinel, dispatcher call coverage, command-history fields,
equipment cooldowns, and the complete hire-role schedule.

**Static follow-through:** The three action/target generations, both cooldowns,
and their save/load paths are mapped in `BIN-AI-004` and the family sections.
The independent four-valued setup selection is AI Mentality, whose staging,
persistence, and consumers are also bounded in `BIN-AI-004`.

### BIN-AI-003B - base hire-role schedule

**Observation:** Before its family-count and affordability adjustments,
`0x00458fa0` seeds a schedule slot from the current turn (query `0x2f`). Nine
scenarios use `turn % 10`; Dominance uniquely uses `turn % 11`. Each table cell
below is `ranking mode / hire role`, directly matching the call argument to
`0x004078d9` and the dword written to `0x00482128` by the final switch.

| Scenario | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Greed | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 0/1 | 2/2 | 3/3 | - |
| Power | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Acceptance | 0/1 | 4/6 | 3/4 | 0/1 | 2/2 | 3/3 | 2/2 | 0/1 | 2/2 | 0/1 | - |
| Dominance | 0/1 | 0/1 | 2/5 | 2/2 | 4/6 | 3/3 | 2/2 | 0/1 | 2/5 | 0/1 | 3/4 |
| Kill 'Em All | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Big 40 | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Eliminate | 0/0 | 1/2 | 0/0 | 1/2 | 4/6 | 1/1 | 1/2 | 0/0 | 1/1 | 2/5 | - |
| Siege | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 3/4 | 2/2 | 5/3 | - |
| Big Man | 0/0 | 1/2 | 0/0 | 1/1 | 1/1 | 2/3 | 1/2 | 0/0 | 1/1 | 1/2 | - |
| Armageddon | 0/0 | 1/1 | 0/0 | 3/3 | 0/0 | 3/4 | 0/0 | 3/3 | 0/0 | 2/5 | - |

The Power, Kill 'Em All, and Big 40 switch bodies are identical. Static
constants at `0x00481018` onward decode as floats 52, 4, 2, 100, 3, and 6.
The planner computes `total duration turns / 52` and retains that factor in an
x87/local-stack value used by quota comparisons. Ghidra misleadingly renders
several later uses as multiplication by `0.0`; instruction windows confirm they
still consume the saved factor. `OriginalAiHireRoleRules` implements the exact
pre-adjustment schedule. It also implements the identical Power/Kill 'Em All/
Big 40 adjustment block: late turns remap slots 4 and 8; selector `0x9a`,
selector `0x5f`, the prior role, and family-5/family-7 presence redirect slot 6;
family counts cap slots 8, 6, 9, and 4 at respectively `factor * 4`,
`factor * 4`, `factor * 3`, and `factor`; and fewer than four family-0-or-4
gangs forces slot zero.
The same class now contains instruction-verified adjustments for every
scenario. These preserve strict versus inclusive quota
boundaries and original statement order: Siege's cash floor is strict and its
missing-family-6-or-12 fallback runs last; Big Man redirects four slots only
when the family-0-or-4 count is strictly above five; Armageddon's missing-family-2
override is last even after another rule has reset the slot. Greed, Acceptance,
and Dominance retain their distinct late-turn windows, selector-driven fallback
chains, quota multipliers, and Dominance's unique slot 10.

The enclosing attempt gates are also scenario-specific. Greed requires the
active gang count to be at or below its computed limit and requires remaining
turns to be strictly greater than integer `total duration / 8`. Power,
Acceptance, and Dominance require the inclusive gang limit plus more than two
remaining turns. Kill 'Em All, Big 40, Eliminate, Siege, and Armageddon use only
the inclusive gang limit. Big Man enters its schedule without that limit check.
`OriginalAiHireRoleRules.ShouldAttemptHire` implements these boundaries.

The limit feeding those gates is now instruction-verified too. Selector
`0x22` returns one when any sector is neutral and not under Crackdown; selector
`0x23` counts sectors owned by the requested player. When no such neutral
sector remains, cash above 300 yields the fixed limit 80, while cash at or
below 300 yields `active gangs + owned sectors`. While a neutral sector does
remain, Greed uses the integer truncation of `owned sectors * 1.5`, Power,
Acceptance, and Dominance use `owned sectors * 2`, and scenarios 4 through 9
use `owned sectors * 4`. Every result is capped at 80. The `1.5` is the decoded
double at `0x00481010`; the emitted x87 conversion truncates the nonnegative
count. `OriginalAiHireRoleRules.CalculateHireGangLimit` implements the block.

Query `0x2f` is the zero-based elapsed-turn counter at `0x0049ca68`; query 2
returns total scenario duration minus that counter. The counter initializes to
zero and increments once after the outer planner loop, so recreation turn 1
feeds schedule slot zero and the full duration as turns remaining.

Selector `0x90` scans every other player's 81 gang slots for a gang whose
mirrored sector byte equals the requested sector and whose per-observer
visibility byte is set. It returns 10 when that gang's owner has state 0 or 3
and the observer-to-owner attitude is negative, 1 for another visible opposing
gang, and 0 when none exists. Selector `0x9a` returns the requested ordinal
sector whose cached `0x90` value is 10, or sentinel 100. With ordinal 1 in the
hire planner, it therefore identifies the first sector containing a visible
hostile opposing gang. Selector `0x5f` scans active family-6 gangs and returns
one whose current sector or cached destination matches that sector, otherwise
`-1`. The adjustment input now names these as visible-hostile-sector presence
and family-6 coverage rather than retaining raw selector numbers.

Selector `0x8f` is now closed as well. Its case in `0x00402d70` returns the
per-player dword at `0x00482160`; `0x00458fa0` copies the current hire role from
`0x00482128` into that array before computing the next role. It therefore means
**previous hire role**, not previous schedule slot. The five family-6 scheduling
blocks nevertheless compare it with their scenario-specific special slot number:
Power/Kill 'Em All/Big 40 use 6, Greed and Armageddon use 5, Acceptance uses 2,
and Dominance uses 10. Every one of those slots ultimately writes hire role 4,
which the family table maps to family 6. Dominance's comparison can never be true
for a role bounded to 0..6, and the other constants suppress unrelated prior
roles by numerical coincidence. This is a shipped slot-versus-role indexing bug,
not an unknown field interpretation. The recreation deliberately compares the
previous role with 4 in all five blocks, preserving the apparent no-consecutive-
family-6 intent without reproducing the defect. See `DECISIONS.md`.

The remaining Greed-only byte read is also semantic. The scenario scorer at
`0x0047712a` stores, for each active player, the count of players with a
strictly greater scenario score at `0x004abc08 + player`; tied leaders therefore
both hold zero. In schedule slot 9, only a player with a nonzero value (behind
at least one higher-scoring player) is reset to slot 0 when fewer than ten
turns remain or cash is below 100. `OriginalAiHireAdjustmentInputs` exposes
that exact condition as `HasHigherScoringPlayer`.

**Confidence:** Verified for scenario order, periods, every ranking-mode call,
every hire-role write, shared scenario bodies, constants, retained factor, x87
comparison direction, equality boundaries, adjustment ordering, and the
Greed-only scenario-standing predicate, selector `0x8f`'s previous-role storage,
plus the complete hire-limit inputs, multipliers, cash boundary, and cap. The
role-4 comparison is a documented recreation correction rather than a claim
about the shipped instruction stream.

The recreation's `AiPlanningState` now preserves the verified six current-role
words, six previous-role words, and six-by-81 family slots in canonical hashes,
native saves, and replays. Save/replay version 7 migrates earlier snapshots to
role zero and family sentinel 99 without advancing the RNG. `PrepareAiPlanning`
now performs the verified role rollover and applies the scenario/role family
table to each active gang before the existing hostility pass. The separate
post-command `PrepareAiHiring` pass derives the verified gate and adjustment
inputs, writes the next current role, and returns the prepared offer choice;
replay version 8 records that mutation.

The first short of the related 14-byte auxiliary record at `0x0048c0ba` is now
represented as an authoritative polymorphic focus value. Family 11 uses it as
a formation sector. Family 7 writes a sector while attacking or establishing a
research position, an item while researching, and `-1` after equipment or
routing. The same six-by-81 storage is hashed, saved, and replayed; its existing
`formationSectors` serialized name is retained as a pre-1.0 implementation
detail. The record's second short is now represented independently as the
family-6 coverage sector. Every assigning hire-role-4 dispatch initializes it
to the gang's current sector. Family 6 writes a selected strategic destination
before routing, then overwrites it with the actual one-step Move destination.
Selector `0x5f` uses this persisted value only when the first short equals
`-1`; otherwise it tests the gang's live sector. Both shorts are authoritative,
hashed, saved, and replayed.

### BIN-AI-003A - strategic hire-offer ranking

**Observation:** `0x00458fa0` calls `0x004078d9` after choosing a role for the
next hire. The helper scans exactly three offer bytes at `0x004abbc0 +
player * 3`, returns an offer-slot index, and only then compares that winner's
raw Force (`gang +0x00`, selector `0x8d`) with player cash. An unaffordable
winner returns `-1`; it does not fall back to another offer. If requested mode
0 has cash strictly above 200 and the scenario is not Greed, the helper first
substitutes mode 3.

| Requested mode | Ranking and eligibility |
|---:|---|
| 0 | Lowest Upkeep at or below 3, with nonnegative Control; later ties win |
| 1 | Highest Heal from a zero baseline; Greed also requires Upkeep <= 3; later ties win |
| 2 | Highest Research from a zero baseline; Greed also requires Upkeep <= 3; later ties win |
| 3 | Highest Combat plus only positive Blade, Range, Fighting, and Martial Arts; zero baseline; later ties win |
| 4 | Highest Stealth + Strength. Greed requires Upkeep <= 4, Strength >= 0, and Stealth > 3 and gives later ties priority; other scenarios require Strength >= 0 and replace only on a strict improvement, so the first maximum wins |
| 5 | Highest Detect at or above the initial baseline 10; later ties win |

The field identities follow the decoded 156-byte gang record: Upkeep at +2,
Combat through Martial Arts at +4 through +30, and raw Force at +0. Direct
instruction inspection was required for modes 1, 2, and 4 because Ghidra's
decompiler reused the player parameter as a local accumulator and emitted
misleading pseudocode. `OriginalAiHireRules` implements the instruction-level
behavior, and the live AI planner uses the selected post-command role's ranking
mode.

When no offer survives ranking/affordability, selector `0x8e` chooses the slot
passed to `0x004078b8`, which writes `0xfe` into that offer's per-player state.
Greed always chooses slot zero (confirmed from the emitted instructions; the
decompiler renders its zero-iteration loop misleadingly). Every other scenario
chooses the first strict minimum of this integer score, initialized to 5000:

`Stealth * 20 * positive-stat-sum / (Force + Upkeep + 1)`

The sum includes positive Combat, Defense, Control, Heal, Influence, Research,
Strength, Blade, Range, Fighting, Martial Arts, and Tech; it excludes Stealth,
Detect, and Chaos. `OriginalAiHireRules.SelectRejectedOfferIndex` implements
this exact failure-path choice. The live post-command AI preparation returns
that choice when ranking or affordability fails, and the replay recorder applies
the snub as a separate authoritative mutation.

**Confidence:** Verified for all comparisons, eligibility boundaries, tie
directions, the cash-200 override, post-selection affordability, and no-fallback
behavior, plus the failed-hire rejection formula and tie direction.

**Static follow-through:** `BIN-AI-003B` now supplies every scenario-specific
role schedule and adjustment in `0x00458fa0`; the live post-command hiring pass
uses the resulting ranking mode, preserves failed-offer rejection, and records
the authoritative mutation for replay.

### BIN-AI-003C - AI hire destination and persistent placement anchor

**Observation:** The hire-destination helper `0x00408214` takes player, mode,
and offer slot. Values at least `0x40` directly write and return sector
`mode - 0x40`, without validation or RNG. Mode 1 builds a weighted multiset in
exact order: every owned sector from 0 through 63 once, followed by every
active gang slot from 0 through 80 once at that gang's current sector.
Duplicate sectors remain duplicated, and one bounded draw selects a one-based
ordinal. An empty multiset still calls the original wrapper with its bound
clamped to one and resolves destination `-1`.

Mode 0 first scans occupied gang records to find the minimum and maximum sector
and each extreme's multiplicity. When an offer slot is present and either
extreme contains fewer than six gangs, it consumes one bounded draw over two
choices, tentatively picks that extreme, and falls back to the other when the
chosen extreme is full. Execution then deliberately falls through mode 1 and
overwrites the tentative result. Thus the extreme choice changes RNG state but
not the final destination. If both extremes are full or the offer slot is
negative, that preliminary draw is skipped. Modes 2 through 63 return sentinel
99 without writing a destination or consuming RNG.

The outer planner `0x00458fa0` maintains one per-player encoded placement anchor
at `DAT_0048e2f8`. New-match initialization seeds it to the Right Hands sector
plus `0x40`. It retains a proposed anchor only while that owned sector has at
least one neutral, available immediate neighbor, occupancy at most five, and
the scenario is not Big Man; otherwise selector `0x25` chooses a replacement,
which is stored plus `0x40`. The anchor is part of the original save/load state.

Selector `0x24(player, center)` returns zero unless `center` is player-owned,
then counts neutral cells whose Crackdown-duration byte is zero in its 3-by-3
neighborhood in dy-major, dx-minor order. Its row-wrap check uses the literal linear bound
`0 <= candidate < 65`. Index 64 is not a fixed sentinel: its owner and
Crackdown-offset reads alias bytes at `0x004a11e8` and `0x004a11f7` in the
following 486-by-10-byte runtime block. Bounded decompilation of the resolver at
`0x00472775` shows that byte zero of record zero is the mirrored owner for
player-zero gang slot zero. The index-64 availability read aliases byte five of
record one, a retaliation-damage accumulator for player-zero gang slot one.
Fresh recreation matches initialize the owner alias to player zero, so the
neutral-neighbor predicate never consults the availability alias at index 64.
Arbitrary save-loaded alias values are irrelevant because original saves are
unsupported.
For ordinary scenarios selector `0x25` makes three deterministic ascending-
sector passes over owned sectors with occupancy below six:

1. choose the first strict maximum positive selector-`0x24` count (baseline
   zero, so zero never qualifies and equal values retain the earlier sector);
2. if none, choose the first sector whose selector-`0x5b` previous-Chaos count
   is zero; and
3. if none, choose the first strict minimum nonzero selector-`0x26` count from
   baseline nine. Selector `0x26` counts non-player-owned cells in the same
   literal 3-by-3 bounds and does not test availability.

If all three passes fail, selector `0x25` returns `-1`. Big Man (scenario 8)
instead has an empty radius-zero list, then tests `[18, 26, 19, 27]`, then
`[9, 17, 25, 33, 10, 18, 26, 34, 11, 19, 27, 35, 12, 20, 28, 36]`, taking the
first player-owned sector with occupancy below six. If neither list succeeds,
it preserves the incoming anchor even when that anchor is full. Selector
`0x25` consumes no RNG.

The normal failure value `-1` is stored as encoded anchor 63. A later refresh
subtracts 64 passes raw `-1` to selector `0x24`, whose center-owner read occurs
before its neighborhood bounds checks and therefore aliases adjacent memory as
well. Big Man is the only selector-`0x25` path that reads the incoming anchor;
when its scans fail it returns `anchor - 64`, so the outer re-encoding preserves
63, ordinary sector encodings, or the inactive encoding 164 unchanged.

Normal planner calls to `0x00408214` pass this encoded anchor, so they always
take the direct `>= 0x40` path and consume no placement RNG. The role-4 paths
for Kill 'Em All, Power, Greed, Big 40, Acceptance, Dominance, and Armageddon
(scenario IDs 0 through 5 and 9) can instead override the anchor with the first
visible-hostile sector returned by selector `0x9a`, again encoded with
`0x40`. Siege hire-role slots 5 and 7 override it with the Right Hands sector
plus `0x40`. Resolution at `0x00472775` later consumes the selected destination
during the internal Hire phase.

The ten normal `0x00408214` call sites are `0x00459bc8`, `0x0045a08c`,
`0x0045a55c`, `0x0045aafe`, `0x0045af7e`, `0x0045b3fe`, `0x0045b671`,
`0x0045ba56`, `0x0045bc23`, and `0x0045bfe9`. The seven visible-hostile
overrides occur at `0x00459bb1`, `0x0045a075`, `0x0045a545`, `0x0045aae7`,
`0x0045af67`, `0x0045b3e7`, and `0x0045bfd2`; Siege's Right Hands override is
at `0x0045ba3f`. All 18 direct xrefs are inside the outer planner.

**Interpretation:** ordinary AI hiring does not use mode 0 or mode 1's random
placement result: the planner has already reduced the choice to a persisted,
encoded sector. The otherwise surprising mode-0 draw is relevant only if an
unencoded caller reaches that helper.

**Confidence:** High static evidence for `0x00408214` mode control flow, exact
mode-1 multiset order and RNG use, encoded direct mode, anchor initialization
and persistence, selectors `0x24` through `0x26`, all direct call sites,
scenario overrides, and the planner/resolver call path. The behavior is
recovered and wired into the recreation's live AI planner.

**Implementation:** the six encoded anchors are authoritative `AiPlanningState`
members covered by canonical hashes, native saves, and replays. Fresh local
matches create all six original player slots and initialize each anchor from
gang slot zero. `OriginalAiHirePlacementModeRules` isolates the exact transient
role/scenario override, including the raw-100 visible-hostile sentinel. Live AI
preparation now preserves or refreshes the anchor, counts previous Chaos actions
at the candidate sector, selects the first visible hostile regardless of its
controller type, applies Big Man and Siege overrides, and feeds the encoded
zero-RNG result to deferred Hire resolution.

**Implementation:** `AiPlanningState` now persists and hashes the older,
immediately previous, and newly planned action bytes for all six-by-81 slots.
Active slots roll at AI planning entry; accepted computer commands update the
planned byte, and cancellation clears it. Hire resolution now reuses the first
inactive roster slot and clears that slot's family and three action generations.
The six first-plan flags are now authoritative, hashed, and persisted: first
preparation resets all 81 records and skips rollover, while subsequent passes
roll active records. The recovered duplicate cleanup is live: for each sector
with more than one previous Chaos, selector `0x70(..., 1)` rewrites only the
first ascending matching slot to None; it then does the same for previous
Influence through selector `0x71`, rewriting the first match to Snitch. The two
command-dependent target bytes now roll, reset, hash, save, and replay with
their action generation. Accepted computer commands encode them using the
resolver-confirmed meanings documented in `BIN-AI-004`.

**Next validation:** capture fixed original placement decisions across ordinary,
Big Man, visible-hostile, and Siege paths while preserving pass order and the
zero-RNG encoded path. Variation caused only by arbitrary original-save alias
bytes is outside the supported scope.

### BIN-AI-004 - global AI Mentality byte and first consumers

**Observation:** The Win32 string table in EXE-GOG-1.1 maps resource IDs 46,
47, 48, and 49 to `GOON`, `CRIMINAL`, `CRIME LORD`, and `HOMICIDAL MANIAC`.
The setup presenter `0x0045519d` loads the displayed choice at call site
`0x00455502` using resource ID `46 + (signed byte)[0x00487850]`. In the central
state-query function `0x00402d70`, case `0x36` returns that same signed byte.
This distinguishes it from the scenario-like global `0x004abbe8` and from
query selectors `0x2f` and `0x31` used elsewhere in the planner.

The six focused writes now have bounded data-flow classifications:

- `0x00464618` is preference initialization. `0x0046439a` obtains the registry
  value named `prefsDiff` with `RegQueryValueExA` and copies its low byte into
  `0x00487850`.
- `0x00439542` is the setup-panel apply path. `0x00438da5` snapshots the global
  into a local selection, changes that local from the setup hit regions, and
  writes it back alongside the selected scenario and duration fields.
- `0x00461bf6` and `0x00461fea` are two commit paths in the same UI/event
  handler. The first path initially copies `0x00487850` to the one-byte staging
  field `0x0049833c`; both paths later copy that staging byte back.
- `0x00463bc7` and `0x00463bf1` restore the same one-byte staging field after
  `0x0046381a` serializes/deserializes it with adjacent setup fields. They are
  selected by two distinct four-byte format markers. Whether those formats are
  file, local IPC, or legacy-network envelopes is intentionally left unlabeled.

There are eight genuine selector-`0x36` calls, all in two functions: calls at
`0x0040a734` and `0x0040a7b8` in `0x0040a1a7`, plus calls at `0x0043466a`,
`0x004346d3`, `0x00434da6`, `0x00434dd9`, `0x00435090`, and `0x004350c3` in
family-1 handler `0x00434080`. A nearby call at `0x0040950f` is not a consumer:
its actual selector argument is `0x21`; `0x36` only appears in a preceding
comparison.

`0x0040a1a7` rebuilds a 24-byte record for each ordered player pair. For the
active observer, offset `+0` counts every sector owned by the other player.
Offset `+2` counts those sectors where the observer's gangs present there have
a strictly greater combined effective Combat + Defense total than the visible
defending owner's gangs. Selectors `0xb0`/`0xb1` enumerate only defenders whose
per-observer visibility byte is 1; selectors `0x5e`/`0x47` enumerate the
observer's own local gangs. The runtime gang bytes at `0x00498dba` and
`0x00498dbb` are effective Combat and Defense: the attack resolver independently
adds the former to Force at `0x00473dd3` and consumes the latter as Defense at
`0x00473b17`.

When both counts are positive, the exact test at `0x0040a816` is signed integer
`(advantaged sectors * 100) / owned sectors > 75`; exactly 75 percent does not
qualify. Target controller types 0 or 3 (local or legacy-remote human) enter the
test at Mentality 1 or higher. Other controller types enter it only below
Mentality 2. On success, `0x0040a859` sets the ordered pair's byte at `+20`, and
`0x0040a86d` writes `-10` to `attitude[observer, other]`. The row stride `0x90`,
pair stride `0x18`, and the same observer-major indexing in the flag's only
external consumer at `0x0042085d` establish the direction. The flag is an
ephemeral planner predicate; its consumer can admit a later branch when the
owner is already hostile and no visible defending gang is available.

The six family-handler calls form three paired gates. Four formerly anonymous
state selectors are now bounded:

- selector 3 returns the active player's cash. The turn resolver at
  `0x00472775` compares this field with action and equipment costs, subtracts
  those costs, and adds received income back into the same per-player word;
- selector 4 returns the selected sector's signed Tolerance byte at sector
  stride `0x24`. The right-panel presenter `0x004120ef` independently renders
  this byte on the `TOLERANCE` row;
- selector `0x21` returns the sector owner, or `-2` for a disabled sector; and
- selector `0x35` tests whether that owner has player type 0 or 3. Setup and
  initialization paths identify those types as local-human and legacy-remote
  human respectively, so the selector is a human-owner predicate. This is a
  planner classification only: original networking remains an explicit
  non-goal and no transport or protocol behavior is being recreated.

The raw planned-action bytes are also identified. The command atlas
`PX00129.bmp` lists the fourteen commands in numeric order, while the turn
resolver groups byte 10 by destination sector and accounts byte 3 using sector
income. Together these independently map byte 3 to **Chaos**, byte 10 to
**Move**, and byte 13 to **Snitch**.

The bounded family-1 blocks now establish several observable decisions. One
path keeps Snitch only when cash is greater than 50 and otherwise writes Move.
Another path requires at least 50 cash before entering its crime choice, then
writes Chaos when the target sector has Tolerance below 4 and Snitch otherwise;
its fallback writes Move. For a human-owned target, Mentality 1 or higher can
enter that crime choice. The paired non-human-owner path admits Mentality 0,
while another pair has an exact-Mentality-2 override. Mentality 3 shares the
`>= 1` branches but not that exact-value override.

The remaining selectors in those gates are now structurally identified:

- `0x3c` reads the gang's Force byte;
- `0x3d` reads its queued-action byte;
- `0x51` reads its effective Heal statistic; and
- `0x2a` tests the current sector's Crackdown record and returns nonzero while
  police are active; and
- `0x2c` is a strict single-gang Control feasibility predicate. It rejects
  disabled, unavailable, or already-owned sectors. For a neutral sector it
  tests whether gang Force + Control exceeds sector Income + Support. For an
  enemy sector it adds every defending gang's Force + Control to that defense
  and performs the same strict comparison. Its nested selector `0x91`
  enumerates other players' gangs in that sector only when the querying
  player's per-gang visibility byte is nonzero, so unseen defenders are not
  included in the AI estimate. The field at record offset `+23` is
  independently used by the Control resolver at `0x00472775`; offset `+24`
  returned by selector `0x51` is the following Heal statistic.

These are verified branch facts, not yet a complete policy table: the target
enumeration and earlier guards still determine which gang/sector pair reaches
each gate. The AI planning records begin at `0x0048a250`, use a 16-byte stride,
and retain three three-byte action/target generations: older at offsets
`+2..+4`, immediately previous at `+5..+7`, and newly planned at `+8..+10`.
Selectors `0x3f`, `0x3e`, and `0x3d` read their action bytes at `+2`, `+5`,
and `+8` respectively. The raw action values 0 through 14 are the public
command IDs; this is planner history, not a separate internal action enum.

For each active slot, `0x00458fa0` performs the exact rollover before planning:
`+2..+4 <- +5..+7` at `0x004590a9`/`0x004590c2`, then
`+5..+7 <- +8..+10` at `0x0045913f`/`0x00459158`, followed by clears of the
new tuple at `0x004591d5`, `0x004591ef`, and `0x00459209`. Strategic refresh
then runs at `0x0045936f`. The duplicate-Chaos cleanup queries selector `0x5b`
at `0x0045939c` and may rewrite the immediately previous action at
`0x004593d6`/`0x00459424`; only afterward does the dispatcher visit all active
gangs at `0x004594df` and write their new actions at `+8`. The hire-anchor
selector-`0x24` check at `0x00459502` and selector-`0x25` fallback at
`0x00459553` therefore observe the `+5` immediately previous action. There is
no second promotion at the end of planning.

Selector `0x5b`'s dispatcher case at `0x004048b8` counts same-sector roster
members whose `+5` action is 3 (**Chaos**); selector `0x6f` at `0x00404949`
does the analogous count for previous action 9 (**Influence**). Inactive
records use sector 100. Their tuples are not shifted or cleared by rollover,
but the dispatcher resets a slot when it is reused, and selector `0x5b` omits
inactive slots naturally through its same-sector test.

Reset/initialization helper `0x00409de1` clears offsets `+2..+10`, family 99,
and the other per-record planning fields. The complete six-by-81-by-16-byte
record block (length `0x1e60`) and the six first-plan flags at `0x00482108`
are both serialized by save/load paths `0x0046381a` and `0x00463cc5`. Thus the
history survives save/load independently of the command resolver and has an
explicit first-planning lifecycle.

Hire resolution in `0x00472775` scans gang records upward from slot zero while
the mirrored sector byte is not 100, with a strict usable bound of 80. When it
finds the first inactive slot it copies the complete new 32-byte gang record
into that slot; a full first 80 slots takes the failure path. This establishes
ascending inactive-slot reuse, not append-only roster growth. The planner's
reused-record reset prevents the new gang from inheriting the prior occupant's
family or action history.

The branches above are therefore command-continuity decisions, including the
case entered after a prior Snitch command.

All four action-7 (**Heal**) assignments in this handler are now bounded.
Every path first requires effective Heal at least `-3`; three require Force
below 9, while the path following no prior action or prior Chaos requires Force
below 8. No recovered family-1 path heals at Force 9. For that previous-None or
previous-Chaos path, the complete terminal branch is recovered: below the
strict Force/Heal boundary it writes Heal when selector `0x2a` reports no
Crackdown and Move through mode 5 when police are active; outside that boundary
an older Snitch writes Chaos and every other older action writes mode-5 Move.
The recreation executes this action-level branch through
`OriginalAiFamilyOneRules`. Replay-recorded preparation now supplies its exact
mode-5 destination and tie RNG, including the all-zero post-filter path. Only
the fallback when the selected action/step has no legal recreation candidate
remains provisional.

The other family-1 paths use the common Force-below-9 Heal gate. The isolated
rules also guard two distinct cash comparisons: the strict continuation changes
from Move at cash 50 to Snitch at 51, while the post-equipment crime branch
admits cash 50 and changes from Chaos to Snitch as Tolerance moves from 3 to 4.

The post-equipment continuation at `0x004345d0..0x00434712`, reached after
prior Control, Equip, or Snitch when no preceding equipment opportunity writes
Equip, is now complete. Selector `0x35` splits on whether the current sector
owner is human. A human owner takes the crime branch at cash at least 50 and
Mentality at least Criminal. A non-human owner takes it only when the raw owner
differs from the active player, is strictly greater than zero, cash is at least
50, and Mentality is exactly Goon. The literal positive-owner comparison means
player zero is deliberately excluded on this side. The crime branch writes
Chaos at Tolerance at most 3 and Snitch from 4; every failed gate writes Move
through mode 5. `OriginalAiFamilyOneRules.SelectPostEquipmentContinuation`
preserves these comparisons in the live planner.

The preceding equipment decision is also recovered and live. Selectors `0x39`
and `0x3a` read the equipped weapon and armor. Selector `0x61` chooses the
weapon candidate first. Selector `0x64` then chooses a researched type-3 armor
candidate whose Tech requirement is within the gang's raw Tech, whose Defense
at item-record offset `+0xc` strictly improves on the current armor, and whose
cost is strictly less than cash. Selectors `0x65` and `0x66` read signed
planning-record cooldown shorts at offsets `+12` and `+14`. A successful Equip
writes raw item cost times three to the matching cooldown. At the start of each
later planning pass, an equipped slot decrements its cooldown while an empty or
inactive slot resets it to zero.

Selector `0x6c` scans the acting gang's 3-by-3 neighborhood, clipping horizontal
wrap but permitting linear index 64. In Greed, a nearby cell qualifies when its
cached weight is 10 and its owner is the acting player. In other scenarios, a
cell qualifies when it has any different nonnegative owner or its cached weight
is 10. Such a cell opens the gate only while the gang lacks a weapon or armor.
Independently, a current sector owned by the acting player with weight 10 opens
the gate even when both equipment slots are filled. Weight 10 denotes a visible
hostile human gang. Index 64 preserves the original array aliases: its owner
reads player zero gang-slot-zero ownership storage, and its per-player weight
reads the next player's sector-zero weight (zero for the final player). The
recreation carries these comparisons, exact item target, cooldowns, and the
fallback continuation through authoritative planning, command resolution,
saves, canonical hashes, and replays.

The previous-Heal case is also complete at the action level. It repeats Heal
under the common Force-below-9/effective-Heal-at-least-`-3` gate. Otherwise it
queries selector `0x2c` for the acting gang's current sector, writes Control
when that strict solo-control predicate succeeds, and writes Move with a mode-5
destination when it fails. `OriginalAiFamilyOneRules.SelectHealContinuation`
and the live planner preserve this branch and its complete mode-5 target
selection.

**Interpretation:** `0x00487850` is the original match-global, zero-based AI
Mentality setting, seeded from a persisted preference and then carried through
setup staging/serialization. Family handler 1 uses it to redirect cash-qualified
crime behavior between human and non-human owners and between Chaos, Snitch,
and Move. A player-pair scoring pass also changes paths by mentality.

**Confidence:** High for resource IDs, address, display expression, query
selector, all six write classifications, all eight genuine consumer call sites,
cash/Tolerance/owner/human-owner selector meanings, command-byte mappings,
comparison constants, pair counters, integer ratio, observer-to-target write
direction, resulting raw record writes, the global's identity and persistence,
effective-stat labels, the three-generation action-history lifecycle, and the
complete target enumerators. A prepared recovered tuple rejected by modern
validation now remains unsubstituted, matching the native handler's absence of
any rejected-command policy.

The resolver at `0x00472775` establishes the complete public-command decoding
of those two bytes. Attack uses target player and that player's roster slot.
Equip and Research use an item ID in byte one. Influence uses the local site
slot in byte one. Move uses the destination sector. Give uses an equipment-slot
mask (`1` weapon, `2` armor, `4` miscellaneous) followed by the friendly target
roster slot. Sell uses the same mask in byte one. Commands without an explicit
target leave both bytes zero. Direct family-handler writes independently
confirm the Move, Equip, Attack, Influence, and Research cases; resolver lines
151-207, 346-352, 557-616, and 713-715 provide bounded decode evidence.

**Static follow-through:** The shared sector selector, site selectors, visible-
gang pools, objective targeting, family-11 formation routing, and every handler's
earlier guards are now bounded below and represented by executable regression
vectors. Native handlers write their tuples directly and have no corresponding
rejected-command path. Original policy therefore preserves an exact prepared
tuple but submits nothing when modern validation cannot represent it, rather
than invoking recreation-only scoring. Controlled original-turn traces remain
useful corroboration for the recovered decisions.

### BIN-AI-005 - shared weighted sector selector

**Observation:** `0x00408642` is the shared sector-target routine used by the
recovered family handlers. Its parameters are the active player, a selection
mode, and the active gang. Mode 0 chooses one of the eight immediate neighbors
(`-9`, `-8`, `-7`, `-1`, `+1`, `+7`, `+8`, `+9`) with uniform calls to the
original bounded RNG, rejecting row-wrap and off-board results.

For nonzero modes the routine clears an 8-by-8 integer score map, obtains the
gang's current sector through selector `0x5a`, and examines successively larger
clipped squares around it, from radius 1 through 7. Each radius rescans the full
square rather than only its perimeter, and the search stops after the first
square which contributes any candidate. The current sector is removed before
final selection. Modes 1 through 5 have bounded scoring rules:

- mode 1 scores a neutral sector `+1` only when selector `0x2c` says the gang
  can take it by strict solo Control;
- mode 2 scores an owned sector `+1`;
- mode 3 scores a sector owned by another player `+1`;
- mode 4 scores `+1` when player-pair predicate `0x2d` accepts its owner; and
- mode 5 scores a solo-controllable neutral sector `+5`, an owned sector with
  no previous-Chaos gang assignment (selector `0x5b` equals zero) `+2`, and an
  enemy-owned sector `+1`.

The remaining modes are structurally bounded but not all subordinate fields
are named yet. Selector `0x32` counts players whose controller type is 0 or 3,
so mode 6 and mode 10 branch on the number of human players. Selector `0x2d`
compares two players' positions in the six-byte player-order table, rejecting
neutral and self comparisons. Selector `0x2e` returns the sole player whose
scenario standing byte at `0x004abc08` is zero, or `-1` when zero or multiple
players share that value. `0x0047712a` builds the scenario score at
`0x004a2790`, then sets each active player's standing byte to the number of
players with a strictly greater score and inactive slots to `0xff`; selector
`0x2e` therefore returns the unique current leader. Selector `0x5e` counts one
player's nonempty gang records in a sector.

The scorer uses cash for Greed; controlled-sector count for Power, Big 40, and
Armageddon; accumulated current Support for Acceptance; and the duration-scaled
Dominance numerator followed by signed integer division by ten. Kill 'Em All
and Siege give every active player the same count of inactive player slots.
Eliminate counts ownership of the six generated Headquarters sectors, while
Big Man is the deliberate exception to the scorer's normal reset: it retains
each player's prior score and adds one for each currently owned sector among
27, 28, 35, and 36. Its score is therefore the accumulated objective total,
not a fresh projection of current center control. These scores and the exact
zero-based competition standings are now isolated in
`OriginalAiScenarioStandingRules` and feed live mode-6 movement.

The same table is not AI-private. End-turn evaluator `0x00476857` calls
`0x0047712a` before testing the active-player count and every scenario-specific
completion condition. It sets the end flag immediately when exactly one of the
six active-state bytes is nonzero, before entering the scenario switch. The
`PX05011` Player Ranking path at `0x004518d9` reads
the score and standing arrays directly. Endgame awards/statistics path
`0x0042ce61` iterates standing values 0 through 5 in order, visiting player
slots 0 through 5 within each tied standing, then appends inactive (`0xff`)
players in slot order. Thus objective games use the same scenario score table,
ties use competition standings, and eliminated players are displayed after the
ranked active players. The six fixed inactive score sentinels remain `-32000`
while standings are counted, so an extreme active score below that value can
retain an unusually low numeric place even though inactive rows render last.

### BIN-RANKING-001 - player-rail portrait positions

**Observation:** `PX05011` handler `0x004518d9` iterates the six player slots
and copies a portrait only when that slot's standing byte is not `-1`. Its
portrait destinations use local rail x values 98, 138, 178, 218, 258, and 298,
with a 32-by-32 aperture and vertical position determined by the standing.

**Interpretation:** Eliminated slots do not receive a ranking-panel portrait;
the post-ranked inactive ordering used by endgame award processing is a
separate presentation concern. The recreation uses the recovered rails while
retaining competition-standing vertical placement for active players.

**Confidence:** High static evidence for slot predicate, rail coordinates,
aperture, and standing-driven placement from `0x004518d9`.

### BIN-ENDTURN-001 - elimination cleanup, reports, and objective order

**Observation:** The sole writer that clears player-active bytes is
`0x00476f3b`, called exactly once at line 936 near the end of whole-turn resolver
`0x00472775`. In scenario 7 (**Eliminate**), it scans players in slot order. If
roster slot zero no longer contains the active Right Hands record, it scans all
64 sectors, writing owner `-1` and zeroing all three site-progress bytes wherever
that player owned the sector, then scans all 81 gang records and writes only
inactive sector sentinel 100. It does not erase Force, definition, equipment,
or the remaining raw record fields.

The helper then applies the ordinary elimination predicate to every scenario:
a player stays active if it owns any sector or has any gang whose sector is not
100. Back in the resolver, lines 933-944 compare pre/post active bytes and append
type-9 elimination reports for all six recipients, player order first and
recipient order second. Only then does line 945 call end evaluator `0x00476857`.
That evaluator rebuilds scenario standings, tests the one-survivor rule, and
then tests the scenario-specific end condition. In Big Man, scorer `0x0047712a`
retains the persistent score instead of clearing it, adds current ownership of
the four center sectors, and the evaluator ends the match at score 40 or above.

**Interpretation:** Eliminate bulk retirement differs from combat death in its
trigger, reporting, and casualty accounting, but both leave stale equipment in
inactive records. That payload is neither recoverable nor credited back to
inventory, and normal roster-slot reuse resets it. The recreation has no
sector-100 gang state, so it represents the same terminal state with Force zero
and clears live queue/Hidden state, but preserves the three equipment fields for
native parity and final-state inspection. The Force/queue representation
difference has no playable effect; accumulated Big Man score, by contrast, is
visible in AI, ranking, and victory decisions and is preserved exactly.

**Confidence:** High static evidence from the unique active-byte writer, sole
call site, complete compact helper, end-of-resolver window, report loops, and
complete compact objective evaluator.

### BIN-AWARDS-001 - thresholds, priority, ties, and visible slots

Award builder `0x0042b9e0` scans all six fixed player slots for each category,
without consulting active-state bytes. It processes the award table in visible
priority Fist, Skull, Big Fat Chicken, Dollar Sign, Safe. The first three use
initial maxima 5 Overthrows (`0x004a27a8`), 50 direct Damage
(`0x004a5ed8`), and 10 Hide resolutions (`0x004a25d0`); values below those
baselines receive no award. Dollar uses Cash Spent (`0x0049ca78`) from a zero
maximum, while Safe uses the same array from an initial minimum of 999,999.
Second-pass equality writes preserve every tied player in ascending slot order.

The per-player award table can retain all five assignments, but renderer
`0x0042ce61` reads only its first three entries for each displayed player row.
Thus result state may retain every superlative while presentation must cap icons
at three in the builder's priority order. These observations replace the former
manual-derived zero-activity and five-visible-icon assumptions.

The Hide counter has one resolver write: action dispatcher `0x00472775` reaches
the action-8 case at `0x00472d00`, which increments the current player's
`0x004a25d0` counter without testing any hidden-state predicate. Consequently
every resolved Hide counts; in the recreation's ordinary lifecycle, recurring
Hide counts again after the next Upkeep has revealed the gang.

Mode 6 is now bounded. If at least one human participates, a sector owned by a
player whom the active AI views negatively receives `+2` only when that owner
is human. It then adds one independent leader-routing point: with a unique
leader other than the active player, only that leader's sectors receive `+1`;
with no unique leader, every sector owned by a player tied at standing zero
receives `+1`; when the active player is the unique leader, every other
player-owned sector containing fewer than four active-player gangs receives
`+1`. These additions feed the same nearest-square, maximum-tie RNG, and
x-then-y step logic as the other nonzero modes.

The site-data offsets used by modes 7 through 9 align exactly with the decoded
62-byte `SITES` record: selectors `0x0c`, `0x0d`, and `0x10` return Support,
Cash, and Stealth for a sector's selected site slot. Selector `0x1c` tests
whether that site's definition Resistance minus its accumulated influence is
below one. Modes 7 and 8 score the Support and Cash of not-yet-influenced sites
in owned sectors. Mode 9 scores Stealth for already-influenced sites in owned
sectors. Mode 7 additionally requires selector `0x6f` to be zero; `0x6f`
counts a player's gangs in the sector whose immediately previous action at
planning-record offset `+5` is 9 (**Influence**). Before dispatch, the outer
planner also rewrites one prior-action byte when more than one gang retained
Influence in the same sector, preventing duplicate continuity assignments.

Mode 10 changes its owner test according to the human-player count. With no
human players it scores every non-neutral sector not owned by the active
player `+1`; with at least one human player it scores every human-owned sector
`+1`. The mode-specific admission test does not consult the directional
attitude table, but the common block at `0x004098d4..0x004099ad` subsequently
multiplies an admitted hostile human-owned sector by five. Mode 11
gives `+1` to the sector returned by selector `0x5a` for the active player.
Modes 12 and 14 restrict selection to Big Man's central sectors 27, 28, 35,
and 36; modes 13 and 15 restrict it to Eliminate's six headquarters candidates
9, 12, 30, 33, 51, and 54. These four modes require the active player's gang
count in that sector to be below 6. Modes 12 and 13 also exclude sectors already
owned by the active player. Instruction-level inspection at
`0x004093d3..0x004099ad` corrects the earlier shorthand about their weight: a
hostile human-owned objective receives `+5` inside the mode case and is then
multiplied by five again in the common post-switch block, for an effective
weight of 25. Every other admitted objective receives `+1`.
Mode 16 gives `+1` to the sector returned by selector `0x77`. Its case at
`0x004045bb..0x0040465b` scans all 81 planning slots in ascending order,
without testing whether the gang is active, counts only records whose family
byte is 11, makes ordinals 0, 6, 12, and so on block leaders, and returns the
first auxiliary short of the most recent leader when it reaches the acting
slot. Selector `0x76` at `0x00404539..0x004045b6` independently returns one
only when the acting slot itself is such a leader. A mode above `0x3f`
directly adds `+1` to sector `mode - 0x40`.

The post-score loop at `0x004099f8..0x00409b6c` first clears every sector whose
unavailable byte at sector-record offset `+15` is nonzero. Its second filter
reads the acting planning record's family byte at offset `+0`: only literal
families 0 and 1 call selector `0x2c`, and a non-owned destination is cleared
when that gang cannot strictly Control it. Family 11 therefore applies the
unavailable-sector filter to modes 10 and 16 but deliberately bypasses the
strict-Control filter. This closes the former uncertainty around their late
guards.

All 48 direct references to `0x00408642` have also been enumerated. The static
mode arguments map to the recovered family handlers as follows:

| Selector mode | Handler/family call sites |
|---:|---|
| 2 | family 4 (`0x00401000`), four calls |
| 3 | family 9 (`0x004605e0`), two calls |
| 5 | family 0 (`0x00428ef0`), eight calls; family 1 (`0x00434080`), seven calls; family 7 (`0x00436c70`), one call |
| 6 | family 2 (`0x0041fef0`), two calls |
| 7 | family 5 (`0x0043a1d0`), four calls |
| 8 | family 3 (`0x00435bd0`), four calls |
| 9 | family 10 (`0x0042a6e0`), two calls; dispatcher `0x00432da0`, one call |
| 10 and 16 | family 11 (`0x00420950`), two mode-10 calls and one mode-16 call |
| 12 and 13 | family 13 (`0x0040abc0`), one call each |
| 14 and 15 | family 14 (`0x00466910`), one call each |

The one mode-0 call belongs to runtime function `0x00476a94`, outside the
family table. Family 6 (`0x00431c60`) dynamically uses mode 2 when selector
`0x60` finds no stored target and otherwise encodes that target as
`sector + 0x40`. Family 7 and family 12 also contain encoded-sector calls.
No direct call carries literal mode 1, 4, or 11; any live use must therefore
come through a computed argument. `tools/ghidra/ReportCallArguments.java`
provides a repeatable bounded inventory of the three pushed arguments at each
direct call.

The complete family-6 handler is live. With cached current-sector weight below
one it Moves: selector `0x60` enumerates weight-10 sectors in ascending order
and chooses the first not covered according to selector `0x5f`, or falls back
to mode 2. It clears the first auxiliary short and stores the routed one-step
destination in the second. With positive weight it makes one preliminary
target draw using the human-only pool for a hostile-owned weight-10 sector and
the complete visible pool otherwise; selector `0x2b` applies the established
quarter-strength comparison through the same ordinal in the complete visible
list. A passing draw Attacks immediately. A failure tries weapon then armor,
with nonpositive cooldowns and replacement cooldown `cost * 3`. It then retains
literal Heal and local Control/Move branches which are unreachable while the
cached positive weight remains unchanged, followed by up to five more target
draws and an Attack of the final target after all comparison failures. Attack
stores the current sector in the first auxiliary short; equipment and Move
clear it. Greed with fewer than four turns remaining overwrites the result with
Terminate.

Focused inspection of family 0 at `0x00428ef0` establishes a switch on the
immediately previous action (`0x3e`) and a final comparison against the older
action (`0x3f`). Previous None applies the Force-8/effective-Heal-`-3` gate,
then selector `0x5b` counts previous Hide assignments in the gang's current
sector: zero chooses Hide and a positive count chooses mode-5 Move. Previous
Attack moves immediately unless cached opponent weight is 10; at weight 10 it
makes one human-pool/full-pool asymmetric draw and uses selector `0x2b` for the
quarter-strength comparison, then Attack, strict-solo Control, or Move.

Previous Hide or Equip makes up to five such draws at weight 10 and attacks the
last selected target even if every comparison fails. When no Attack was
prepared, positive selector `0x6c` enables weapon then armor equipment with
nonpositive cooldown and `cost * 3` replacement, followed by owned-sector
Heal/Hide or non-owned mode-5 Move. Previous Control Hides in owned territory
and Moves elsewhere. Previous Heal, Snitch, or Move first applies the same Heal
gate. At weight 10 it makes exactly one target draw: success Attacks, while
failure writes None and clears both auxiliary shorts to `-1`. Without weight
10 it uses strict solo Control where possible, otherwise selector `0x5b`
chooses Hide or Move. Previous Research Moves; unlisted action values retain
None. Finally, a newly planned Move with an older Move changes the family byte
to 11 in Siege and 2 otherwise. This literal older-action test is broader than
an inferred three-consecutive-Move rule. The complete handler, target draws,
mode-5 calls, cooldowns, auxiliary behavior, no-action path, and transition are
live and replay-wired.

Family 4 at `0x00401000` is the final dispatcher handler. It switches on the
same previous-action byte but uses mode 2 for all routing. Previous None,
Control, or Heal applies the Force-8/effective-Heal-`-3` gate, then selector
`0x5b` chooses Hide at count zero or Move otherwise. Previous Attack, Snitch,
or Move makes one asymmetric target draw at weight 10; selector `0x2b` success
Attacks, while failure writes None and clears both auxiliary shorts. Without
weight 10, owned territory chooses Hide at count zero and Move otherwise;
non-owned territory chooses Control only when previous and older actions are
both Move and strict solo Control succeeds, otherwise Move.

Previous Hide or Equip makes up to five weight-10 draws and attacks the last
selection even after all failures. Without Attack, selector `0x6c` enables the
same weapon-before-armor equipment opportunity and `cost * 3` cooldowns. The
remaining owned-sector branch Hides when selector `0x5b` is below two and
otherwise Moves; non-owned territory Moves. No handler-local family transition
or Greed override exists. Although no mapped scenario/hire-role table cell
assigns family 4, an unmapped cell preserves a pre-existing family value. The
complete handler and a live Greed-role-5 preservation/replay path are now
implemented.

Both mode-6 calls belong to family 2. That handler writes public action byte 10
(**Move**) with the selected mode-6 destination when its current/selected
sector branch cannot proceed locally. Its visible-gang path writes action byte
1 (**Attack**). At the later local-sector decision, the pair flag from
`0x0040a1a7` supplies one of two exact action-byte-4 (**Control**) gates: the
sector owner must already be viewed negatively, no defending owner gang may be
visible, and the observer-to-owner combat-advantage flag must be set. The other
Control gate is restricted to a hostile human-owned sector with zero visible
human gangs and rejects a previous-turn Control action. Thus the pair flag does
not directly select an attack; it permits Control after the territorial
Combat + Defense test has established overwhelming local advantage.

Focused inspection of the complete family-2 handler at `0x0041fef0` establishes
its exact action order. Selector `0x64`'s armor opportunity precedes selector
`0x61`'s weapon opportunity. Each requires a nonpositive corresponding
cooldown, a different affordable item, and an immediately previous action other
than Attack; either Equip writes raw item cost times three to its cooldown and
clears the first auxiliary short. Failed equipment Heals only below Force 8,
at effective Heal at least `-3`, and with cached current-sector opponent weight
strictly below 5. An owned current sector then Moves through mode 6.

In a non-owned sector, positive cached opponent weight and at least one visible
hostile gang enter a five-attempt Attack loop. Weight 10 draws the actual target
from all visible human-controlled gangs; other weights draw from visible gangs
whose owner is viewed negatively. Selector `0x2b` still resolves the same
ordinal through the complete visible-opponent list for the quarter-strength
comparison. A passing comparison stops early, but five failures still Attack
the final actual target. The handler stores the current sector in the first
auxiliary short for Attack and clears it for its other ordinary actions.

Without that Attack path, previous Control, Armageddon, or failed strict solo
Control writes mode-6 Move; otherwise the handler writes Control. The two late
hostility gates described above can overwrite any earlier ordinary action with
Control and clear the auxiliary short. Finally, Greed with fewer than four
turns remaining overwrites the action with Terminate. The recreation now wires
this complete branch order, mode-6 target, action target, cooldown, auxiliary
write, and RNG consumption into replay-recorded planning.

The surrounding action writes give the site modes public command semantics.
Family 5 uses mode 7 after writing Move; when the selected Support-priority
site is already local, the same branches write Influence instead. Family 3
has the identical shape around mode 8 but ranks sites by Cash. These are thus
Support-focused and Cash-focused Influence strategies, not distinct movement
rules. Family 10 calls mode 9, compares selector 8 for the chosen and current
sectors, and moves only when the chosen sector has the larger value. Selector
8 sums Stealth across completed/influenced sites, so this path seeks a stronger
local Stealth modifier before its Hide-or-Chaos decision. The dispatcher also
contains a scenario-specific mode-9 Move for gang slot zero.

Focused inspection of family 3 at `0x00435bd0` now bounds its cash-site
continuations. Immediately previous actions None, Control, Equip, and Heal
share one branch. It first writes Heal only below Force 8 with effective Heal
at least `-3`. Otherwise, in a sector owned by the acting player, it scans the
three sites in slot order and retains the first strict maximum positive Cash
whose remaining Resistance is positive, writing Influence with that local site
slot. With no qualifying site it writes Control when selector `0x2c` accepts
the current sector, and otherwise Move through mode 8. Previous Snitch writes
that mode-8 Move directly.

Previous Influence first runs the same selector-`0x6c` equipment opportunity
used by family 1, including weapon-before-armor selection and the cost-times-
three cooldown. Without Equip it applies the same Force-8/Heal-`-3` gate. It
then retains the previous Influence site while that site remains unfinished
and the sector remains owned; otherwise it rescans for the first strict
maximum positive-Cash unfinished site, or moves through mode 8 when none
exists. The mode-8 selector block independently confirms that each owned
candidate sector is scored by the sum of every positive Cash value among its
unfinished sites, before the common nearest-square, maximum-tie RNG, and
x-then-y routing logic.

Previous Attack, Hide, and Move share an opponent-continuation branch. When the
cached visible-opponent weight is not 10, it returns to the owned cash-site,
strict Control, or mode-8 Move sequence without the earlier Heal gate. At
weight 10 it makes one bounded draw. Hostile-owned sectors draw an actual
target from visible gangs belonging to human-controller players; other sectors
draw from every visible opponent. Both pools scan player-major, then gang-slot
order.

The comparison selector preserves a notable original asymmetry: it receives
the selected ordinal but resolves that ordinal through the every-visible-
opponent pool even when the actual target came from the human-only pool. It
permits Attack when
`(target Force + target Combat) / 4 - attacker Defense` is no greater than
`attacker Force + attacker Combat - target Defense`; failure leaves None and
clears the auxiliary target fields. A successful comparison attacks the actual
selected player/slot tuple, not necessarily the gang used for the comparison.

After the switch, three consecutive Move actions change the stored family to
11 in Siege and 2 in every other scenario. Finally, with fewer than four turns
remaining in Greed, the handler unconditionally overwrites the planned action
with Terminate. Switch cases without a recovered action body intentionally
leave None rather than invoking a generic fallback.

**Recreation status:** the complete family-3 handler is live and replay-
recorded, including local site targets, equipment cooldowns, mode-8 Move
targets, opponent-pool selection and comparison, terminal family transitions,
and the late Greed Terminate override.

**Confidence:** High for the switch cases, comparisons, action writes, pool and
site scan order, tie behavior, mode-8 score sum, and call order from the
fingerprinted version-1.1 executable; runtime corroboration remains pending.

Focused inspection of family 5 at `0x0043a1d0` establishes that it has the
same complete action-switch and terminal shape as family 3, with Support-site
selection and mode 7 substituted for Cash-site selection and mode 8. Previous
None, Control, Equip, and Heal use the same Force-below-8 and effective-Heal-
at-least-`-3` gate, then select the first strict maximum positive-Support
unfinished site in an owned sector, attempt strict solo Control, or Move.
Previous Influence uses the same weapon-before-armor equipment opportunity and
cost-times-three cooldowns, Heal gate, previous-site retention, and local site
rescan. Previous Snitch moves directly through mode 7.

The previous Attack/Hide/Move branch is also instruction-for-instruction
equivalent in public behavior: visible-opponent weight 10 performs one bounded
draw, optionally chooses the actual target from visible human-controller gangs,
resolves the comparison ordinal through the full visible-opponent pool, and
uses the same quarter-strength combat predicate before writing Attack or None.
Its terminal block applies the same three-consecutive-Move family change (11 in
Siege, 2 otherwise) and the same final-three-turn Greed Terminate overwrite.

Mode 7 adds one distinction beyond its Support score. Selector `0x6f` scans all
81 planning records for the acting player and counts records whose mirrored
gang sector matches the candidate and whose immediately previous action at
offset `+5` is Influence. A candidate owned sector is scored only when that
count is zero; its score is then the sum of positive Support values for all
unfinished sites. Selector `0x41` reads planning offset `+6`, confirming that
the previous-Influence continuation retains the previous target's first byte,
which is the local site slot.

**Recreation status:** the complete family-5 handler and mode-7 selector are
live and replay-recorded, including duplicate previous-Influence destination
exclusion.

**Confidence:** High static evidence for the listed branches, selector fields,
Support scan/sum, target-pool asymmetry, terminal order, and RNG call order in
the fingerprinted version-1.1 executable; runtime corroboration remains
pending.

Family 7 at `0x00436c70` is now completely bounded at the action level. It
starts from the current sector's cached visible-opponent weight. Weight 10
makes one bounded draw: a hostile-owned sector selects the actual target from
visible human-controller gangs, while other sectors use every visible
opponent. The comparison ordinal still resolves through the full visible list
and uses the shared quarter-strength combat predicate. Attack is written only
when that comparison succeeds and the actual selected owner is hostile; the
first auxiliary short then stores the current sector. A failed comparison
continues into the normal research sequence rather than ending at None.

Without weight 10, family 7 applies selector `0x6c`'s equipment-need gate and
the family-1 weapon-before-armor choice. Both slots require a nonpositive
cooldown, a different affordable item, and write a raw-cost-times-three
cooldown plus focus `-1`. A prepared Equip, Move, Attack, or Influence skips
the remaining decision body. Otherwise an immediately previous Equip, Move,
Attack, or Influence clears the first byte of its previous target, and Force
below 8 with effective Heal at least `-3` writes terminal Heal.

Selector `0x30` begins with the current sector and replaces it only with an
owned sector having a strictly greater cached Research score. That cache is
the signed sum of all three site definitions' Research modifiers; ties retain
the earlier candidate and the current sector is not required to be owned. A
changed best sector is encoded as `sector + 0x40` for one-step Move and clears
the focus. At the selected local sector, the first slot-order site with
positive Research and positive remaining Resistance receives Influence. With
no such site, the focus becomes the current sector and item Research begins.

Pending previous Research repeats the same item. A completed previous ranged,
blade, or armor item next requests blade, armor, or the fixed miscellaneous
priority `[44, 41, 42, 43, 46, 50, 49, 52]`; every other type next requests
ranged. Type scans choose the first positive item ID whose type matches, whose
Tech does not exceed selector `0x62`'s gang/local-site cap, and whose per-player
research value remains positive. A failed continuation retries ranged, blade,
melee, armor, then the fixed miscellaneous list. If every category is
exhausted, the handler changes to family 0, moves through mode 5, and clears
the focus. Greed's final three turns overwrite any result with Terminate.

**Recreation status:** the complete family-7 equipment, Heal, Attack,
Research-site selection, Influence, encoded Move, item continuation/fallback,
family-0 exhaustion transition, focus state, and Greed override are live and
replay-recorded.

**Confidence:** High static evidence for branch order, score and site fields,
item scan order, target-pool asymmetry, target/focus writes, and RNG order in
the fingerprinted version-1.1 executable; runtime corroboration remains
pending.

Family 9 at `0x004605e0` is now completely bounded at the action level. It
first tries selector `0x61`'s weapon and selector `0x64`'s armor. Each candidate
must differ from the equipped item and be affordable. Unlike the equipment
branches that query selectors `0x65` and `0x66`, family 9 does not inspect the
existing weapon or armor cooldown before replacing the item; a successful
Equip overwrites the matching cooldown with three times raw item cost.

With neither upgrade available, a gang in its own sector always writes Move
through shared sector mode 3, which seeks another player's owned territory.
In a non-owned sector, cached visible-opponent weight 10 enters a five-draw
target loop. Its hostile-human-owner pool choice, full-visible-list comparison
ordinal, quarter-strength predicate, early success exit, and final Attack after
five failed comparisons match family 12. When the weight is not 10, an
immediately previous Control writes mode-3 Move; every other previous action
writes Control. This handler has no Heal, miscellaneous equipment, or Greed
terminal override.

**Recreation status:** the complete family-9 weapon/armor, mode-3 Move,
five-draw Attack, and Control sequence is live and replay-recorded, including
the absence of an equipment-cooldown gate.

**Confidence:** High static evidence for branch order, comparisons, action
writes, selector arguments, target-pool order, and RNG call order in the
fingerprinted version-1.1 executable; runtime corroboration remains pending.

Family 10 at `0x0042a6e0` is now completely bounded at the action level. It
first calls selector `0x72`, which scans researched type-3 armor within the
gang's raw Tech and retains the first strict maximum Defense improvement. An
unequipped gang uses item 1 as its zero-Defense sentinel. The handler accepts
the returned armor only when its offset-`+14` cooldown is at most zero and raw
item cost is at most cash, then writes Equip and the literal cooldown 2. This
differs from the cost-times-three cooldown used by families 1, 3, 5, and 11.

If that opportunity fails, an empty miscellaneous slot plus a clear research
flag for item 44 writes Equip for **Smoke Bombs**. This special branch performs
no separate raw-Tech or cash comparison in the handler. Otherwise it writes
Heal only below Force 10, with effective Heal at least `-3`, and with cached
visible-opponent weight exactly zero in the current sector.

The remaining path calls sector mode 9 and compares selector 8 for the returned
sector against selector 8 for the current sector. Mode 9 scores only owned
sectors and sums each strictly positive Stealth value whose site is already
finished. If the returned sector's sum is strictly larger, the handler writes
Move and calls mode 9 a second time for the actual destination. It does not
reuse the probed destination, so a maximum tie can consume two bounded draws
and choose a different tied sector on the second call. If the probe is not
strictly better, selector `0x5b` counts same-sector records with previous
Chaos; zero writes Chaos and any positive count writes Hide.

**Recreation status:** the complete family-10 decision sequence, selector
`0x72`, fixed Smoke Bombs branch, literal armor cooldown, mode-9 score, double
selection, and Chaos/Hide fallback are live and replay-recorded. As with other
recovered handlers, behavior after a prepared command is unavailable under the
recreation's validator remains provisional.

**Confidence:** High static evidence for comparisons, scan/tie order, action
writes, and RNG order in the fingerprinted version-1.1 executable; runtime
corroboration remains pending.

Family 12 at `0x004353a0` is now completely bounded at the action level. It
starts from cached selector `0xaf` for the current sector. When no opposing
gang is visible, it tries selector `0x61`'s weapon, selector `0x64`'s armor,
and selector `0x74`'s maximum-Chaos miscellaneous upgrade in that order. The
weapon and armor must differ from the current item, their matching cooldown
must be nonpositive, and raw cost must be at most current cash. Unlike families
1, 3, 5, and 11, a successful weapon or armor Equip writes a cooldown equal to
the raw item cost rather than three times that cost. Miscellaneous equipment
uses the same inclusive cash gate and writes no cooldown.

After failed equipment opportunities, Force below 10 and effective Heal at
least `-3` writes Heal. Otherwise the handler passes `current sector + 0x40`
to the shared sector selector and writes Move. The encoded mode adds one point
to the current sector, but the common selector then clears the source-sector
score. Its maximum is consequently zero: it draws among all 64 tied sectors
and applies the normal x-then-y one-step capacity routing toward that draw.
This apparently indirect random movement is the literal shared-selector path,
not a direct encoded destination like the separate hire-placement selector.

With a visible opponent, family 12 makes up to five bounded target draws. A
hostile human-owned current sector with weight 10 draws the actual target from
visible human-controller gangs; otherwise it uses every visible opponent. As
in families 3 and 5, selector `0x2b` resolves the same ordinal through the full
visible list for the quarter-strength combat comparison. A passing comparison
stops the loop early. Five failed comparisons do not cancel the command: the
final selected actual target is still written as Attack. Finally, Greed with
fewer than four turns remaining overwrites any prepared action with Terminate.
There is no three-consecutive-Move family transition in this handler.

The recreation additionally guards the possible state in which the current
sector has a hostile human owner but its only visible opposing gangs are
computer-controlled. The filtered actual-target pool is then empty. Planning
still consumes one bounded draw and preserves None instead of calling the
recreation RNG with zero and aborting the turn. The reference executable's
observable outcome for this sparse three-player edge remains uncorroborated.

**Recreation status:** the complete family-12 equipment, Heal, encoded Move,
five-draw Attack, target-ordinal asymmetry, and Greed override sequence is live
and replay-recorded. Prepared commands still pass through the recreation's
normal validator.

**Confidence:** High static evidence for comparisons, action writes, selector
arguments, target-pool order, and RNG call order in the fingerprinted
version-1.1 executable; runtime corroboration remains pending.

Families 13 and 14 both use the fixed objective sets as Move destinations:
scenario value 8 selects modes 12/14 and is Big Man, while scenario value 6
selects modes 13/15 and is Eliminate. Family 13 uses the variants that exclude
already-owned objectives; family 14 uses the variants that retain them. This
establishes an original Eliminate movement bias toward every possible
headquarters location, not merely an attack-score bonus against a currently
visible Right Hands gang.

Instruction-level inspection of the terminal blocks at
`0x0040b87d..0x0040b9a6` and `0x004675c8..0x004676f1` establishes their exact
outer guard. Selector `0x1f` returns one only when a Big Man gang is in sector
27, 28, 35, or 36, or an Eliminate gang is in headquarters candidate 9, 12,
30, 33, 51, or 54. When that selector is not one and the handler has not already
written Equip, family 13 unconditionally replaces the planned command with Move
through mode 12/13; family 14 does the same through mode 14/15. Both handlers'
equipment blocks are themselves inside selector-`0x1f` branches, so an
off-objective gang cannot have written Equip before reaching this override.
The recreation therefore safely applies the complete off-objective terminal
path during replay-recorded planning and submits its exact one-step destination
through normal Move resolution.

Family 14 continues at `0x00467700..0x004677df` when selector `0x1f` is one
or the newly planned action is Equip. It changes the command to Heal and the
stored family to 13 exactly when the immediately previous action is Control,
Force is below 10, and effective Heal is at least `-3`. The repeated previous-
action query then rejects Attack, but that comparison is redundant after the
exact-Control guard. The recreation applies this terminal override during
replay-recorded preparation, including the family transition and normal Heal
resolution.

The shared owned-objective branch at family-13 lines `100..110` and family-14
lines `103..114` is also bounded. Strategic refresh `0x0040a1a7` caches
selector `0x90` per player and sector, and selector `0xaf` reads that cache.
Selector `0x90` scans other players and their 81 gang slots in ascending order;
for the first observer-visible gang in the requested sector it returns 10 for
a hostile human owner and 1 otherwise, or zero when no visible opponent is
present. When an objective sector belongs to the acting player and this cache
is zero, both handlers choose Heal before their equipment/site branches at
Force below 10 and effective Heal at least `-3`. This shared Heal path is now
live and replay-verified for both objective scenarios. Family 14 still applies
its later family-13 transition when the previous action was Control.

The preceding contested-objective branch is also bounded in both handlers.
Selector 2 returns scenario turns remaining. Only even absolute parity with a
nonzero cached selector-`0x90` weight enters target selection; the other path
writes Control immediately. Target selection makes as many as three inclusive
bounded draws. A hostile human-owned sector with cached weight 10 draws from
visible human gangs in the sector; otherwise it draws from visible gangs owned
by the sector owner. Selector `0x2b` evaluates each drawn ordinal against the
same ordinal in the full ascending visible-opponent list using
`(target Force + target Combat) / 4 - attacker Defense <= attacker Force +
attacker Combat - target Defense`. Its result controls only whether the retry
loop stops early: after three attempts the final selected gang is still used.
At Force 5 or higher that gang becomes the exact Attack target. With no selected
gang or lower Force, the handler chooses Heal when Force is below 10 and
effective Heal is at least `-3`, otherwise Control. Replay-recorded planning now
preserves the selected owner/roster-slot tuple, resolves it back to the stable
gang ID, and submits the normal validated Attack command. This branch may
attack a visible non-hostile sector owner, so recovered commands require
visibility but do not inherit the provisional fallback planner's hostility
filter.

When the acting player already owns the objective and visible opponents are
present, both handlers use the full ascending visible-opponent pool without the
remaining-turn parity gate. The shared loop starts from zero rather than two,
so this branch makes at most five bounded draws before applying the same Force,
Heal, and Control result selection. It is live for both families with exact
target replay.

When the owned objective has no visible opponent and the Heal gate fails, both
handlers try selector `0x61`'s weapon and selector 100 (`0x64`)'s armor in that
order. Each requires a nonpositive matching cooldown, a different item, enough
cash, and a previous action other than Attack; unlike family 1/11, a successful
objective Equip writes the literal cooldown 2. Selector `0x75` then chooses a
researched type-4 miscellaneous item within raw gang Tech whose Chaos bonus
strictly improves on the current item (item zero is the empty-slot baseline).
The caller applies the inclusive cash gate and no cooldown. If no affordable
miscellaneous upgrade exists, the handler scans the three local sites in slot
order, retaining the first strict maximum positive Support whose remaining
Resistance is positive, and writes Influence with that exact slot; otherwise
it writes None. These Equip/Influence targets now pass through normal validated
commands and authoritative replay. No Research action appears anywhere in
either complete handler.

`0x00408553` sorts the 64 sector scores descending while retaining their sector
indices. The caller chooses uniformly among every sector tied for the maximum;
a unique maximum consumes no RNG, while a tie consumes one bounded call (three
raw `rand()` calls). If record zero's strategic target is outside the immediate
3-by-3 neighborhood, the routine moves first along x and then independently
along y, retaining each component only when the resulting sector contains at
most five of the active player's gangs. It can therefore return a diagonal
neighbor without exceeding the six-friendly-gang capacity. If record zero is
adjacent and positive, the randomly selected maximum-tied sector is returned
directly. Candidate sectors are also removed late when marked unavailable or
when the active gang cannot strictly Control a non-owned destination under the
relevant gang-state branch. If those late filters leave a maximum below 1, the
routine still counts the maximum-tied entries, which are then all 64 zero-score
sectors, consumes one bounded draw, and runs the same x-then-y capacity routing
toward the drawn sector. It does not resume the radius search or return a
special sentinel.

`0x0040a1a7` proves the capacity field's identity: it clears all 64 integers at
`0x00489950 + player*0x100`, scans the player's 81 gang records, and increments
the integer indexed by each active gang's sector. The selector's four routing
comparisons against 5 therefore test destination occupancy, not terrain or a
pathfinding cost.

**Interpretation:** mode 5 is the general movement fallback recovered in the
family-1 continuity paths. Its exact neutral/owned/enemy ratio is 5:2:1, and
the routine separates strategic target scoring from the capacity-checked
single-tile Move ultimately queued. Replay-recorded AI preparation now uses
this kernel for all three live family-1 continuations. Randomness is used only
for mode-0 neighbor selection and equal-best final scores in the bounded paths
inspected here.

**Confidence:** High for the address, ring expansion, score-map sorting,
mode-0 directions, modes 1 through 5 weights, site-field offsets, human-player
count, per-player sector gang counts, unique-leader selector, mode-6 weights and owner
branches, fixed sector sets, direct call inventory, maximum-score tie
randomization, x-then-y step return, family-11 leader/anchor grouping, and the
family-gated late candidate filters. Medium remains only for the player-order
predicates and dynamic call arguments.

**Next validation:** validate the complete recovered family inventory with
controlled runtime traces before claiming runtime parity; the remaining
mode-10/mode-16 uncertainty is no longer a static-analysis item.

### BIN-AI-006 - directional attitude and hostility matrix

**Observation:** `0x004ab590` is a six-by-six signed integer matrix indexed as
`observer * 6 + other player`. Initialization at `0x0046dc10` depends on the
global AI Mentality. At Homicidal Maniac, every cell whose target player has
human controller type 0 or 3 is initialized to `-10`, while cells targeting a
computer player are initialized to `+10`. At all lower mentalities every cell
starts at zero. This changes preferences only; it grants no resources,
statistics, rolls, or visibility.

At the start of turn resolution at Mentalities 0 through 2, every entry below
`+10` increases by one. Homicidal Maniac skips the entire recovery loop.
Later resolution paths subtract from one directed cell and clamp the result at
`-10`. At non-Homicidal mentalities, initialization gives every player a fixed
reaction value from one bounded RNG draw, `Next(4) + 2`, producing 3 through 6.
Homicidal Maniac assigns reaction zero and consumes no such draw. No later
writer modifies these values. The combat path lowers the defender owner's
attitude toward the attacker by `max(reaction, damage dealt)`. A sector-control
transfer lowers the previous owner's attitude toward the new owner by exactly
twice that previous owner's reaction value.
The player-pair ratio pass at `0x0040a1a7` can also force a directed entry to
`-10` under its mentality-dependent guards.

Negative entries are the hostility boundary used by central target queries.
Selector `0x92` enumerates visible gangs in a requested sector only from
players whose observer-relative entry is negative; selector `0xab` performs a
corresponding count for visibility state 1. Selector `0x90` returns weight 10
for a visible human gang belonging to a negatively viewed player and weight 1
for other visible gangs. Numerous AI handlers read the same `< 0` predicate
directly, including shared sector-selector mode 6.

Family 11 (`0x00420950`) supplies the clearest consumer. Instruction-level
inspection is required here because the decompiler drops the assignments after
the entry calls: selector `0x5a` is saved at stack local `-0x4` and is the
active gang's current sector; selector `0x61` is independently saved at `-0x8`.
Selector `0x61` chooses a weapon upgrade. Its subordinate selector `0x6d`
admits only a requested weapon class whose tech requirement does not exceed
selector `0x62`'s local research ceiling, whose player research flag is clear
(complete), and whose cost does not exceed current cash. It compares the first
eligible item Combat
bonus for the melee, blade, and ranged classes after adding the matching
effective skills: Strength for melee, Strength + Blade for blade, Range for
ranged, and Strength + Fighting + Martial Arts for bare hands. It then scans
all 64 items in the winning class and retains only a strictly larger Combat
bonus subject to the same research and cash gates, but this second pass compares
item Tech against the gang's raw Tech rather than selector `0x62`. Class-score
ties resolve ranged, then melee, then blade, then bare hands. It returns `-1` when
bare hands win, no upgrade beats the baseline, or the result is already the
equipped weapon.

Selector `0x65` reads the first of two planning-record shorts at offsets
`+12/+14`. Planning initialization sets the first to zero for an unarmed gang
and otherwise decrements it by one; a weapon Equip writes `item cost * 3` back
to it. Family 11 admits the selector-`0x61` weapon only when this replacement
cooldown is at most zero, the item is affordable, and the previous action is
not **Attack**. It then writes **Equip**, the item ID, and the new three-times-
cost cooldown. The following analogous opportunities use selectors `0x64` and
`0x74` for the other equipment slots before the Heal gate.

After those Equip and Heal opportunities, the handler reads the owner of its
current sector. In an active-player-owned sector it always writes **Move** and
calls mode 10 at `0x00420e7b`. The subsequent write at `0x00420eea` leaves the
first auxiliary short equal to the current sector rather than replacing it
with the chosen destination.

In any other sector, selector `0xac(active player, current sector, 0)` scans
other players in ascending slot order and their gangs in ascending slot order.
It returns the first encoded `player * 81 + gang` whose sector equals the
current sector, whose observer-specific visibility/status byte is nonzero, and
whose raw gang-state byte at record offset `-1` from the sector field is zero.
The handler writes **Attack** against that decoded player/gang whenever the
result is nonnegative. If no such target exists, selector `0x76` determines
formation leadership: considering only family-11 gangs in ascending gang-slot
order—including inactive slots whose planning family remains 11—ordinals 0,
6, 12, and so on return 1. Those anchors write **Move** with mode 10 at
`0x00421085` and replace their stored formation-sector short with the chosen
destination. Other family-11 gangs write **Move** with mode 16 at `0x00421157`
and retain their current-sector short. Selector `0x77` finds the corresponding
block anchor and returns its stored formation-sector short, so mode 16 awards
that sector `+1` and feeds it through the common ring/path selection. Neither
path is subjected to the selector's family-0/1 strict-Control late filter.

Thus mode 10 is not a generic hostile-target rule: with humans present it seeks
human-owned territory regardless of attitude, and with no humans it seeks any
other non-neutral owner's territory. Mode 16 is the follower path for five of
each six family-11 gangs, while the first gang in each block establishes the
formation destination.

**Recreation status:** the equipment priority/cooldowns, Heal gate, and exact
first-visible local Attack target are live and replay-wired. Modes 10 and 16 are
also live; the separate six-by-81 formation-sector shorts preserve inactive
family records and are included in authoritative hashes, saves, and replays.

**Interpretation:** this is an attitude/hostility system, not a scalar combat
bonus. Homicidal Maniac begins maximally hostile toward human players and
maximally friendly toward computer players; ordinary interactions can create
directed hostility at other mentalities, and hostility decays toward
friendliness by one point per turn. The earlier scalar attack score only
approximated part of that outcome; the current recreation persists and resolves
the matrix, applies the recovered recovery and combat/Control mutations, and
uses negative hostility in attack targeting.

**Confidence:** High for matrix dimensions and direction, `[-10,+10]` bounds,
initial values, mentality-gated per-turn recovery, reaction range/immutability, combat and
Control decrements, negative-hostility target gating, controller classification,
mode-10 target ownership, mode-16 group semantics, and both formation modes'
late filters. Low only for the original public/internal name of the reaction
value.

The exact new-match order is now bounded. For non-Homicidal games the six
reaction draws are the only RNG calls between entry to `0x0046dc10` and city
generation at its decompiled line 85. Homicidal games skip those draws. The
offer filler remains later in the outer setup caller. The recreation now
initializes at this point, recovers attitudes at the Command-to-Execution
whole-turn resolver boundary, applies the recovered combat and Control changes,
uses hostility for AI attack candidates, and includes the state in canonical
hashes, native saves, and replays.

**Next validation:** capture fixed original traces proving the combat-advantage
threshold, reaction, recovery ordering, and family-11 weapon replacement
cooldown through the first complete turns.

### BIN-EFFECTIVE-STATS-001 - gang, equipment, and controlled-site aggregation

**Observation:** Turn-start function `0x0046e766` first rebuilds every 36-byte
sector record through `0x004782c5`. That helper scans the sector's three site
definition/progress pairs. For every site whose progress has reached its base
Resistance, it accumulates the site's Support, Cash, Tolerance, all fourteen
stat modifiers, and special-site flag into the sector record. The normal path
then scans all six players and all 81 gang slots and calls `0x0047781f` for
every active gang before planning begins.

Helper `0x0047781f` reconstructs each of the gang's fourteen effective-stat
bytes independently. Each byte starts with the corresponding field from the
156-byte gang-definition record, then adds the same field from each of the
three equipped item IDs that is not `-1`, using the 166-byte item-record
stride. It adds the matching sector aggregate only when the gang's player is
the current owner of the gang's sector. The rebuilt 32-byte gang record is
written back immediately. This same sequence also runs after a completed turn
when the match continues.

The whole-turn resolver `0x00472775` copies that rebuilt gang record before
dispatch. Its Heal case reads effective Heal and passes `Heal + 4` directly to
the common dice helper. Its Research case reads effective Research and passes
`Force + Research`, subject only to the per-player difficulty adjustment
documented in `BIN-AI-007`. Thus item and completed-site modifiers are part of
the shipped action pools rather than display-only values. A site completed
during the current Instant pass is absent from the already-built sector and
gang records, so it cannot affect another action until the following planning
boundary.

The executable stores site progress and derives completed benefits for the
sector owner; it has no independent site-owner field. The recreation's
explicit `InfluencedBy` field is therefore valid only with zero remaining
Resistance and a matching sector owner. Authoritative construction now rejects
positive-Resistance influenced sites, while zero-Resistance sites with no
influencer remain valid as pending completions (and for the Headquarters
special case).

**Confidence:** High static evidence for all fourteen fields, gang/item/site
source strides, three equipment slots, completion and sector-owner gates,
turn-start ordering, Heal and Research consumers, and the recreation
representation invariant.

**Recreation status:** `EffectiveStatisticsCalculator` performs the same base,
three-item, and controlled completed-site aggregation. Focused tests cover Heal
equipment/site pools, Research equipment aggregation and site-stat activation,
ownership scope, delayed same-Instant activation, and invalid influenced-site
state.

### BIN-AI-007 - per-player difficulty resolution band

**Observation:** new-match initialization also fills a six-entry integer table
at `0x004a2570`. Every slot starts at 1. At Goon, computer-controlled slots are
changed to 0; Criminal leaves all slots at 1; Crime Lord and Homicidal Maniac
change computer-controlled slots to 2. Human-controlled slots remain 1 at all
four mentalities.

The whole-turn resolver `0x00472775` reads this table repeatedly while resolving
gang actions. Its helper `0x00475f70(pool, threshold)` rolls `pool` inclusive
d6 values with `0x0045d227(6)` and counts results greater than or equal to the
threshold. Every one of the nine band-table reads is now bounded:

- Heal uses `Heal + 4` dice. Bands 0/1 succeed on 5+, while band 2 succeeds on
  4+; successes add Force, capped at 10.
- Influence uses `Force + Influence`. Band 0 removes `trunc(pool/5)` dice and
  succeeds on 5+; band 1 uses the full pool at 5+; band 2 uses the full pool at
  4+.
- Research uses `Force + Research`. Band 0 removes `trunc(pool/5)` dice and
  succeeds on 6; band 1 uses the full pool at 6; band 2 uses the full pool at
  5+.
- Each Chaos gang separately includes sector Income in
  `Income + Force + Chaos`. Band 0 removes one fifth of the pool at 5+, band 1
  uses the full pool at 5+, and band 2 uses the full pool at 4+. When the band-2
  player owns that sector, only `successes - trunc(successes/4)` contributes to
  the Crackdown comparison.
- A hidden target evades when an inclusive d20 roll is below
  `Stealth + 14 - Detect` for attacker bands 0/1 or
  `Stealth + 10 - Detect` for band 2.
- Main Attack reduces a band-0 defender's Defense by one quarter. It then rolls
  `Force + CombatRating - adjusted Defense` at 6+/5+/4+ for attacker bands
  0/1/2. Positive pools impose minimum damage `trunc(pool/4)`.
- Retaliation first requires the target's action byte not to be 8 (**Hide**).
  It is then allowed when the attacker has effective Martial Arts zero, when
  the attacker has a weapon equipped, or when the defender has both positive
  effective Martial Arts and no weapon equipped. Equivalently, a bare-handed
  positive-Martial-Arts attacker suppresses retaliation unless the defender is
  also a bare-handed positive-Martial-Arts gang. Eligible retaliation rolls
  its corresponding pool at 5+ for defender bands 0/1 or 4+ for band 2, then
  halves successes using integer truncation.

The recreation implements these bands and formulas in
`OriginalResolutionRules` and routes the corresponding resolution paths
through them. This is a mechanical resolution calibration, not merely a
planning preference.

The compound operands are direct gang-record fields. Offset `+7` is the public
action byte; selector `0x39` exposes offset `+4`, the equipped-weapon item or
`-1`; and selector `0x58` exposes offset `+31`, the last of the fourteen
effective statistics and therefore Martial Arts. The attack block uses those
same offsets from both its copied attacker record and the targeted live record.

**Confidence:** High for initialization, controller/mentality mapping, helper
semantics, all nine reads, formulas, thresholds, integer truncation, and the
complete retaliation-eligibility predicate.

**Next validation:** capture fixed original traces at bands 0, 1, and 2 for
every affected action, including Hide and both Martial Arts branches.

### BIN-DETECT-001 - cooperative sector visibility aggregation

**Observation:** Visibility rebuild `0x0046fa11` runs once from each of the
computer- and human-planning entry paths in outer turn function `0x0046e766`.
For each observer it initializes 64 sector strengths to -32000 (or 1000 for the
`SMGHUBBLE` modifier), then scans that observer's 81 gang slots. Active records
are those whose sector byte `+2` is not 100. A strict-greater comparison selects
the first highest effective Detect byte `+21` in each occupied sector as the
base and remembers that gang slot.

A second 81-slot scan adds every other active gang as a helper. Each helper
always contributes 1. When its signed Detect is greater than 9, it additionally
contributes `(Detect - 8) / 2` using signed integer truncation. The exact helper
sequence is therefore: all values through 9 add 1, 10–11 add 2, 12–13 add 3,
14–15 add 4, 16–17 add 5, 18–19 add 6, and higher pairs continue without a cap.
Negative helpers still add 1. Finally, the routine scans every other player's
active gang records and marks a target visible exactly when effective Stealth
byte `+20` is less than or equal to that observer-sector strength. Friendly
active records are marked visible unconditionally.

Attack picker `0x0043b290` calls roster builder `0x0043d132` for a selected
enemy owner and the acting gang's sector. That builder scans the owner's 81
records and includes one only when its sector matches and its observer-indexed
visibility byte at `+12 + observer` is nonzero. Only those included roster slots
can be written back as the Attack target.

**Interpretation:** The shipped helper arithmetic differs from the manual's
printed 0–10/11–12/.../19+ capped bands at boundaries, negative values, and
above 19. Authoritative compatibility uses the executable formula. Ordering
only chooses which equal-best gang is excluded from helper treatment; because
equal values contribute identically, roster order cannot change the aggregate.
Hide is not read by this routine and does not affect cooperative visibility.

**Confidence:** High from the complete compact function, signed byte loads,
loop bounds, exact branch/division arithmetic, field offsets, threshold, and
both direct call sites.

**Recreation status:** `ManualRules.SectorDetectionStrength` implements the
native base/helper calculation, and `MatchState.CanPlayerDetectGang` supplies
active same-sector effective statistics plus the explicit omniscience rule.
Boundary tests cover negative helpers, every transition around Detect 10–12,
the printed cap boundary, and an above-cap value. Both the Attack picker and
authoritative command validation exclude undetected targets; the latter also
protects multiplayer and replay submission paths that bypass presentation.

### BIN-HIDE-LIFECYCLE-001 - active and recurring action boundary

**Observation:** Each 32-byte public gang record stores its active action at
offset `+7` (`0x00498daf` base) and its recurring action at offset `+10`
(`0x00498db2` base). In outer turn function `0x0046e766`, decompiler lines
154-192 validate recurring actions and copy `+10` to `+7` for all six players
and all 81 roster slots before planning. Hide is action 8. Human recurring-menu
handler `0x0041462f` writes both fields immediately; its None branch sets both
to zero. One-off handler `0x00414d8c` clears `+10` for non-recurring choices
and writes the selected action to `+7`. Combat and police subsequently test the
same `+7` byte to decide whether the gang is hiding.

**Interpretation:** There is no independent delayed Hide flag in the native
record. Assigning Hide makes the gang hidden immediately. At the next turn
boundary, one-off Hide is replaced by recurring None, while recurring Hide is
copied back as the active action and remains hidden. Replacing or cancelling
Hide during planning changes the active action immediately and reveals the
gang. Resolution still increments the Hide statistic every turn that recurring
Hide executes.

**Confidence:** High from the bounded field writes, whole-roster turn-start
copy, action-8 dispatcher, and combat/police consumers in executable SHA-256
`A1430159BBE20869E277A5000311344F4EC141AB77C96B385336617149E97D89`
under Ghidra 12.1.3.

**Recreation status:** `MatchGangState.Hidden` now mirrors the active-action
lifecycle at assignment, cancellation, and Upkeep. Tests distinguish one-off
expiry from recurring retention and verify immediate replacement/cancellation.

**Next validation:** capture the targetability transition in a fixed native
hot-seat turn before and after replacing recurring Hide.

### BIN-COMMAND-ASSIGN-001 - recurring menus and replacement writes

**Observation:** The complete reference inventory for recurring action byte
`0x00498db2` finds human assignment writes only in individual-gang handler
`0x00414d8c` and sector-wide handler `0x0041462f`. The individual recurring
submenu maps exactly to Chaos (3), Control (4), Heal (7), Hide (8), Influence
(9), Research (11), and None (0). The sector-wide recurring submenu maps to the
same set except Research is absent. Neither recurring menu offers Bribe (2) or
Snitch (13).

The individual handler writes a selected recurring action and target to `+10`
and `+11`, then writes the same action to active field `+7`. Selecting None
zeros `+10`. Its direct one-off path zeros recurring action and target except
for its explicit recurring Influence shortcut. The sector-wide handler starts
its recurring value at zero; ordinary selections therefore overwrite active
action/target while clearing recurrence, whereas recurring selections write the
selected action to both active and recurring fields for each eligible gang.

**Interpretation:** A new assignment replaces the complete prior assignment;
it does not inherit the old repeat flag or target. Bribe and Snitch are ordinary
one-off actions even though raw recurring values 2 and 13 would survive the
turn-start terminal switch if introduced outside these human assignment paths.
The recreation rejects those UI-unreachable recurring forms rather than
promoting stale/raw-state behavior into a supported command.

**Confidence:** High from the exhaustive recurring-byte reference inventory,
both bounded assignment handlers, and their active/recurring target writes.

**Recreation status:** authoritative validation and the individual recurring
picker expose the recovered six actions. Queue replacement and cancellation
overwrite or clear the prior action, target, and repeat state atomically.

### BIN-REPEAT-001 - turn-start terminal recurring-command cleanup

**Observation:** Before copying recurring action `+10` to active action `+7`,
outer turn function `0x0046e766` scans all six players and all 81 roster slots
in fixed order. Its switch contains terminal checks for exactly four retained
actions:

- Control (4) clears when the acting player already owns the gang's sector or
  the sector's Crackdown byte is positive;
- Heal (7) clears when Force equals 10;
- Influence (9) clears when site progress has reached base Resistance or the
  sector owner is no longer the acting player; and
- Research (11) clears when the selected item's remaining-progress byte is zero.

After that switch, every recurring action clears when the gang's sector byte is
the inactive sentinel 100. The surviving recurring action and target are then
copied into the active fields. Chaos and Hide have no terminal case and continue
until explicitly replaced/cancelled or the gang becomes inactive. Bribe and
Snitch also lack terminal cases, but the native human assignment menus cannot
place either value in the recurring field. The cleanup runs before the same
outer function's Crackdown-duration update and pre-planning site-benefit rebuild.

**Interpretation:** A Control order that becomes illegal because police appeared
after submission fails during resolution but does not wait out the police; it is
discarded at the following turn start even if that update would expire the
Crackdown. Likewise, unfinished recurring Influence is discarded after an
overthrow rather than resuming automatically if ownership is later regained.

**Confidence:** High static evidence for action IDs, predicates, player/roster
order, inactive sentinel, copy order, and placement before Crackdown/site updates.

**Recreation status:** turn-start normalization now implements all four native
terminal checks. Successful Control, Heal, Influence, and Research may still be
released immediately after resolution as an unobservable internal optimization;
the next planning state is identical.

### BIN-GANG-RETIRE-001 - death and Terminate preserve inactive record payload

**Observation:** After player attacks and police damage have accumulated against
phase-start snapshots, whole-turn resolver `0x00472775` applies damage in its
player/81-slot loop at decompiler lines 530-540. When resulting Force is below
one, line 537 writes only sector byte `+2` (`0x00498daa`) to inactive sentinel
100 and line 538 increments the owner's casualty statistic. The preceding
combat-report copy records Force and all three equipment bytes, but this death
branch contains no writes to weapon `+4`, armor `+5`, or miscellaneous `+6`.

The separate Terminate pass at lines 678-701 copies the complete 32-byte record,
tests action 14, changes only the copied sector byte to 100, and copies all eight
dwords back. It therefore preserves every other raw field, including equipment.
This matches Eliminate-scenario bulk retirement helper `0x00476f3b`, which also
writes only sector 100. Normal hire insertion later copies a complete new gang
record into an inactive slot, replacing rather than recovering that payload.

**Interpretation:** Equipment on a dead or terminated native gang is stale,
inaccessible record state—not returned inventory and not a usable stash. The
manual's statement that Terminate removes its items is true at the gameplay
level but not a literal erasure of the record. The recreation keeps the three
item IDs for parity and final-state inspection while using Force zero and
clearing live queue/Hidden state to represent native inactivity safely.

**Confidence:** High from the bounded post-damage and Terminate loops, exact
field writes, full-record copy boundaries, Eliminate helper, and hire overwrite
path.

**Recreation status:** combat death, police death, Terminate, and Eliminate bulk
retirement all retain inaccessible equipment fields. Regression tests cover all
four paths; active-gang predicates prevent the stale items from contributing to
simulation state.

### BIN-COMBAT-ORDER-001 - player/roster attack and police rolls

**Observation:** The action-1 (**Attack**) block in `0x00472775` is nested in
the player 0-through-5 and roster 0-through-80 scan beginning at lines 337-338;
the action test occurs at line 346 and the attack/retaliation calculation stays
inside that iteration. After the gang-combat pass, lines 448-470 run another
player/roster scan for active gangs in Crackdown sectors and perform each
police detection and damage roll there.

**Interpretation:** Attack RNG and result order are fixed player slot then
persistent roster slot, independent of command submission order. Reciprocal
orders still form one encounter, with the first gang reached by that scan as
the opening attacker. Police likewise visit gangs in player/roster order, not
sector or gang-ID order.

**Confidence:** High static evidence for both scan bounds, action dispatch,
and placement of their RNG consumers. Runtime seed correlation remains pending.

### BIN-POLICE-COMBAT-001 - exact detection and damage formulas

In whole-turn resolver `0x00472775`, the police pass calculates a threshold
immediately before its inclusive 1-through-100 RNG call at `0x0047419b`. The
instructions at `0x00474173`-`0x00474199` recognize current action byte 8
(**Hide**), multiply that boolean by 20, multiply effective Stealth by 5, and
compare the roll with `115 - 5 * Stealth - (Hide ? 20 : 0)`. Because the roll
is bounded to 1 through 100, probabilities outside that range are effectively
clamped without skipping the RNG call.

When detected, the call at `0x004741d6` passes `25 - effective Defense` and
success threshold 5 to the shared dice routine `0x00475f70`. The 25 combines
the manual-listed Police Force 5 and Combat 20. The resolver therefore does
not use the manual's visible “certain through Stealth 5” table or a separate
Police Detect 12 hidden-hit calculation.

**Confidence:** High from bounded decompiler dataflow and instruction-context
reports against executable SHA-256
`A1430159BBE20869E277A5000311344F4EC141AB77C96B385336617149E97D89` in
Ghidra 12.1.3. Runtime boundary captures remain useful for notification timing,
not for selecting the implemented arithmetic.

### BIN-COMBAT-STATS-001 - full opening damage is credited

The player-indexed Damage Inflicted array at `0x004a5ed8` has one resolver
write. In attack dispatcher `0x00472775`, the opening damage value is finalized
at decompiler lines 381-393, accumulated into the target's phase damage at
lines 395-396, and immediately added to the attacker's statistic at instruction
`0x00473ccc` / lines 397-398. No target-Force or remaining-damage comparison
intervenes. The later retaliation calculation has no write to this array.

Therefore each resolved opening attack credits its complete computed damage,
including excess beyond the target's Force and multiple same-phase attacks that
collectively overkill it. Only Force application is floored at zero;
retaliation damage is deliberately excluded from the statistic.

**Confidence:** High from the sole-write inventory and local dataflow.

### BIN-INSTANT-001 - roster-order actions and cumulative Influence

**Observation:** The Instant-action switch in `0x00472775` executes while the
resolver scans player slots 0 through 5 and each player's 81 gang slots in
ascending order. Cases 2, 7, 8, 9, 11, and 13 dispatch Bribe, Heal, Hide,
Influence, Research, and Snitch respectively. In the Influence block at lines
151-185, the resolver compares the site's current progress with its base
resistance, then rolls only the current gang's `Force + effective Influence`.
It immediately adds that gang's successes, clamps progress to the base value,
and records completion before the roster scan continues. Once progress equals
the base resistance, a later Influence command skips its roll.

**Interpretation:** Instant actions resolve in fixed player/roster-slot order,
not submission order. Friendly Influence is cumulative rather than pooled:
each gang consumes its own roll stream and mutates the site before the next
gang acts. A gang encountered after completion consumes no Influence RNG.

The `PX05005` Influence picker is handled by `0x0043f692`, the sole code site
that requests resource ID 5005. For each of the sector's three definition/
progress pairs at `+7/+8`, `+9/+10`, and `+11/+12`, it enables selection only
when the definition's base Resistance differs from current progress. Completed
sites therefore have no selectable command path. The native 36-byte sector
record contains one owner byte and these definition/progress pairs; it has no
per-site influencer identity.

The Control pass in the same resolver changes owner only at lines 801-821.
Whenever the selected owner differs, it writes the new sector owner and zeros
all three site-progress bytes before recording attained/lost-control reports.
The separate neutralization branch at lines 313-319 likewise writes owner
`-1` and zeros every progress byte. Consequently the original has no direct
takeover of an already cooperative site: sector capture first removes all site
cooperation, and the new owner must build progress again from zero. The
recreation represents native completed progress as zero remaining Resistance
and keeps an explicit derived `InfluencedBy` identity, but enforces the same
completed-site rejection and ownership-change reset.

The site Tolerance definition field at `0x004ab684` has only two code
references: AI selector evaluation in `0x00402d70` and sector recomputation
helper `0x004782c5`. It is not consumed by the Influence action block. The same
helper rebuilds Support, Cash, local stat modifiers, and special-site flags, and
its normal match-loop call occurs before planning rather than inside the
whole-turn resolver. A site completed during Instant therefore remains pending
through that turn's later Combat, Transaction, Chaos, and Control passes; its
benefits become active at the following pre-planning rebuild.

**Confidence:** High static evidence for action dispatch, scan order,
per-gang Influence pools, immediate progress mutation, clamping, completion
guard, picker eligibility, absence of a site-owner field, ownership-change
reset, definition-field reference inventory, and delayed benefit boundary.
Runtime seed correlation remains pending.

### BIN-INFLUENCE-001 - Influence picker targets and detail entry

**Observation:** The `PX05005` handler `0x0043f692` uses three guarded,
half-open panel-local selection rectangles: site 0 `(106,17)-(226,81)`, site 1
`(208,73)-(328,137)`, and site 2 `(106,127)-(226,191)`. These are not all
identical to the staggered site-card artwork apertures. A normal click selects
only an unfinished site; Cancel abandons the picker, while confirmation rejects
an unset selection. The handler's double-click branch uses the same guarded
targets and opens details for the clicked eligible site.

**Interpretation:** Site-card rendering and selection geometry are deliberately
separate. The exact target map must be used for both single-click selection and
double-click detail entry, preserving completed-site inertness.

**Confidence:** High static evidence for all three target bounds, completed-site
guards, control branches, and double-click detail path from `0x0043f692`.
Runtime capture remains useful for selection-border presentation.

### BIN-ATTACK-001 - Attack picker selector and target hit map

**Observation:** Interactive Attack selection is handled by `0x0043b290`; the
other `PX05003` resource user, `0x0043d132`, has no pointer-processing path.
The handler tests five enabled opponent portraits at `(98,16 + 36*n)` with
32-by-32 bounds. It separately partitions the target area
`(135,16)-(337,193)` by X boundaries 202 and 270 and Y boundary 105, yielding
six half-open regions: widths 67, 68, 67 in the first row and heights 89 then
88. Disabled opponent/target entries remain inert. Changing opponent resets
the selected target; confirmation requires both selections. The common Cancel
control exits the picker.

**Interpretation:** The attack input map is not the gang-card artwork map:
portrait buttons use a 36-pixel vertical pitch and targets use a complete,
slightly uneven 3-by-2 partition that includes surrounding card whitespace.

**Confidence:** High static evidence for both resource users, interactive
ownership, all selector/target bounds, enabled guards, selection reset, and
confirmation predicate from `0x0043b290`. Runtime capture remains useful for
pressed-state and reticle presentation.

### BIN-RESEARCH-000 - initial progress and Armageddon completion

**Observation:** Fresh-game initializer `0x0046dc10` owns the only setup writes
to the 384-byte item-major/player-minor Research array at `0x004a2608`. For
ordinary scenarios it scans all 64 item records and all six player slots and
copies byte `0x004a5f84 + item * 0xa6` into each player's entry. The loaded
`ITEMS` record is 166 (`0xa6`) bytes and its research-difficulty word begins at
offset `0x7c`; the byte copied here is its low byte. When active scenario dword
`0x004abbe8` is 9 (Armageddon), the alternate branch writes zero to all 64 by 6
entries instead. Neither branch calls the RNG.

The resolver's Research case and the item-selection consumers read the same
`item * 6 + player` bytes. A zero value is therefore already complete: ordinary
matches begin with precisely the real zero-difficulty technologies available,
while Armageddon begins with every item-table entry complete. The executable
also zeroes type-99 padding entries because it blindly covers all 64 records.
The recreation deliberately excludes padding IDs from its researched-item set;
they are not technologies and cannot be selected, so this avoids invalid
authoritative state without changing observable mechanics.

**Confidence:** High static evidence for array address and layout, loop bounds,
source field/stride, scenario branch, zero semantics, and absence of RNG.

### BIN-RESEARCH-001 - same-phase completion suppresses later rolls

**Observation:** Case 11 in the Instant switch at `0x00472775`, lines 186-208,
reads the player's remaining value for the selected item and enters the dice
calculation only while that value is nonzero. A successful gang subtracts its
successes immediately and clamps the value at zero before the fixed roster scan
continues.

**Interpretation:** Multiple gangs may queue Research for the same item, but an
earlier player/roster-slot completion suppresses every later roll for that item
in the same Instant phase. Submission order cannot change which gang consumes
the final research roll or the following RNG state.

**Confidence:** High static evidence for the remaining-value guard, immediate
mutation, zero clamp, and its placement within the player/roster scan.

### BIN-BRIBE-001 - shipped three-dollar cost and direct tolerance delta

**Observation:** Case 2 in the Instant switch at `0x00472775`, lines 114-127,
compares the player's cash with 3. Below 3 it calls the failure-report helper
without mutation. Otherwise it subtracts 3 from cash, directly adds 3 to the
sector tolerance byte, and adds 3 to the player's spending statistic. The
block contains no 40-point clamp. This contradicts the manual's printed $5
cost and capped description.

**Interpretation:** Compatibility play uses the shipped $3 threshold and cost.
Successful Bribe adds 3 directly to effective sector tolerance, including
values above 40; insufficient cash produces the existing ordered failure and
no mutation. The printed $5/cap helper remains isolated as manual evidence and
is not used by authoritative resolution, finance projection, or AI budgeting.

**Confidence:** High static evidence for threshold, cash/statistic deltas,
tolerance delta, and failure branch. Exact original message wording remains
unverified.

### BIN-SNITCH-001 - debt-independent delta and post-Instant floor

**Observation:** Case 13 in `0x00472775`, lines 210-212, subtracts 3 directly
from the sector tolerance byte and marks the sector changed. It contains no
cash read or failure branch. After the complete Instant player/roster scan,
the sector loop at lines 224-226 raises every tolerance below 1 to exactly 1.

**Interpretation:** Snitch executes even while its player is in debt and first
applies a direct -3 delta. The single global post-Instant floor, rather than a
per-command base-zero clamp, then prevents any sector from entering later
phases below tolerance 1. This also prevents commandless negative-tolerance
Chaos triggers in the shipped turn path.

**Confidence:** High static evidence for the direct delta, absence of a cash
gate, global clamp value, and clamp placement after all Instant actions.

### BIN-CHAOS-001 - roster-order rolls and grouped uncontrolled payout

**Observation:** The action-3 (**Chaos**) pass in `0x00472775` scans player
slots 0 through 5 and each player's 81 gang slots in ascending order. Lines
245-265 calculate each participating gang's pool from the persistent sector
Income byte at `0x004a08ec + sector * 0x24`, current Force, and effective Chaos.
They roll each gang separately, store its success count by gang slot, and add
that count to a player-by-sector aggregate.
After all rolls, the sector pass at lines 268-327 evaluates Crackdown totals and
zeroes participating gang results when the sector triggers.

The later payout pass at lines 645-672 rebuilds player-by-sector totals from
the stored per-gang successes. When the player does not own that sector, lines
668-670 divide the completed aggregate by two once; line 672 then adds it to
cash. The division is not performed per gang.

The passes between those two halves are not Chaos work: action-1 Combat begins
at lines 329-346, the police scan follows, and actions 5, 6, and 12 are handled
by the Transaction pass beginning around lines 557, 587, and 605. After the
Chaos payout, the resolver runs Terminate and Move at lines 678-725, followed by
Control from line 733 onward. The physical native order is therefore Instant;
Chaos rolls/Crackdown creation; Combat; Transactions; Chaos payout; Terminate;
Move; Control.

**Interpretation:** Chaos RNG order is fixed player slot, then persistent roster
slot, regardless of submission order or intervening sectors. Same-player gangs
in one sector still share the final success result and payout. Uncontrolled
income is `trunc(total successes / 2)`, preserving an odd success contributed
across multiple gangs rather than rounding each gang independently. A new
Crackdown exists before the same turn's police scan, while its income remains
suppressed and all surviving Chaos income waits until after Transactions. The
success arrays are resolver-local state: the pass does not write a persistent
sector-Chaos value. In particular, the pool's sector component is the generated
3-7 Income byte, not the separate owner Cash byte recomputed from controlled
sites.

**Confidence:** High static evidence for scan order, per-gang rolls/storage,
player-sector aggregation, Crackdown suppression, ownership comparison, and
single post-aggregation division. Runtime seed correlation remains pending.

### BIN-UI-035 - sector Income and owner-only Cash rows

**Observation:** The city and detailed-sector field renderer `0x004120ef`
reads the 36-byte sector record rooted at `0x004a08e8`. It renders offset `+4`
(`0x004a08ec`) on the **INCOME** row, offset `+5` on **TOLERANCE**, and offset
`+6` on **SUPPORT** only when the selected sector owner equals the active
player. Its final row reads offset `+3` (`0x004a08eb`) under the artwork's
literal **CASH** label, again returning zero for a non-owner.

The pre-planning sector rebuild helper `0x004782c5` preserves offset `+4`, sets
offset `+3` to the base sector tax of 1, and adds the Cash field of each
completed site. The later Upkeep scan in `0x0046e766`, lines 216-230, adds that
same signed offset-`+3` byte once to the owning player's cash and statistics.
The action-3 pass independently consumes offset `+4` for Chaos dice.

**Interpretation:** Income and Cash are distinct original fields. Income is the
generated 3-7 sector value used by Chaos and Control. Cash is the owner-visible
and owner-collected `1 + completed-site Cash` value. The shipped panel has no
sector-Chaos row; Chaos successes exist only in the whole-turn resolver's
temporary arrays.

**Confidence:** High static evidence from all four renderer reads, the
rebuild helper, the Upkeep owner scan, and the independent Chaos-pool read.

### BIN-POLICE-001 - occurrence window, neutralization, and duration order

**Observation:** In `0x00472775`, each sector's two signed Crackdown occurrence
shorts live at `0x004abcc0` and `0x004abcc2`. Before evaluating a new trigger,
lines 268-276 replace a non--100 slot with -100 only when it is strictly less
than `current turn - 5`. Lines 303-312 fill the first empty slot and then the
second. If neither is empty, lines 313-321 notify the owner, set the sector
owner to -1, clear its three influence-derived totals, and write the current
turn into both occurrence slots.

Only after that history/neutralization block, the bounded call at `0x0047419b`
requests 1 through 3, adds 2, and adds the resulting 3 through 5 to the sector's
existing police-duration byte. The earlier lines 291-301 have already zeroed
the triggering sector's per-gang Chaos results and emitted notifications for
participating players.

The new duration is visible to the later police read at `0x0047415e`. Near the
end of the whole-turn resolver, `0x00475e74` decrements every positive duration
below the permanent sentinel 100, after the same turn's police attacks.

**Interpretation:** The occurrence window is inclusive: a trigger exactly five
turns before the current one still counts. A third retained trigger resets both
fixed history slots to the current turn, so another trigger while those slots
remain recent can neutralize reacquired control again. Duration extends rather
than replaces existing police presence, and its one bounded draw occurs after
history mutation and any ownership cleanup. A newly triggered Crackdown attacks
in that turn, then the shared final decrement leaves two through four future
police Combat phases from the initially drawn three through five.

**Confidence:** High static evidence for sentinels, strict expiration comparison,
slot-fill/reset order, cleanup fields, duration range/addition, and RNG call
order. Runtime corroboration of notification presentation remains pending.

### BIN-EQUIP-001 - Factory price division and rounding

**Observation:** In EXE-GOG-1.1, the Equip resolver inside `0x00472775` loads
the selected item's signed raw Cost at `0x00474998`. When sector flag
`+0x0e` is set and the sector owner at `+0x00` matches the purchasing player,
`0x004749e1..0x004749f3` performs signed integer division by three and subtracts
that quotient from the raw Cost. The same sector flag has only four direct read
sites: `0x0043f36d`, `0x0044d498`, `0x0044dc36`, and this resolver read at
`0x004749b2`; the equipment-selection path at `0x0044d1bb` independently shows
the same `cost - cost / 3` operation. The manual identifies Factory as the
controlled/influenced site that lowers item purchase prices.

**Interpretation:** Factory pricing is `Cost - trunc(Cost / 3)`. It is not a
30-percent discount and is not `floor(Cost * 70 / 100)`. All item costs are
nonnegative, so C# integer division reproduces the original truncation. Thus a
$11 Katana costs $8, while a $12 item costs $8.

**Confidence:** High static evidence for the branch, divisor, operation order,
owner check, and rounding. A runtime capture remains useful corroboration but is
not required to choose between the former provisional formulas.

### BIN-EQUIP-002 - fixed transaction scan, deferred gifts, and Sell overwrite

**Observation:** The transaction pass in `0x00472775` loops player slots 0
through 5 and, inside each player, all 81 gang-record slots in ascending order.
It initializes three 81-entry pending arrays to -1 before the gang scan. For
action 5 (**Equip**) it immediately checks and subtracts cash, then replaces the
copied gang record's weapon, armor, or miscellaneous byte. For action 6
(**Give**) it writes selected item bytes into those pending arrays at the
recipient slot and clears the giver's copied item bytes. Only after every gang
for that player has been processed does the resolver copy non--1 pending values
into recipients at decompiled lines 633-643.

Action 12 (**Sell**) tests the fixed selection-mask bits in weapon, armor, then
miscellaneous order. Each selected branch clears that copied item byte and
assigns `Cost / 2` to the same local value at decompiled lines 605-617; the cash
and earned-cash updates at lines 618-621 occur once after all three branches.
The branches do not accumulate their values.

**Interpretation:** Transactions resolve by player slot and persistent roster
slot, independent of command submission order. Incoming gifts are applied only
after the recipient's own transaction, so they overwrite a same-turn purchase
or surviving same-slot item; later roster-slot givers overwrite earlier pending
gifts to the same target slot. A multi-slot Sell destroys every selected item
but pays only half the raw Cost of the highest selected fixed slot
(miscellaneous, else armor, else weapon). This last behavior is retained as an
original compatibility quirk rather than corrected to the manual's apparent
combined-value intent.

**Confidence:** High static evidence for loop bounds/order, action dispatch,
pending-array lifecycle/application, selection-mask order, item clearing, and
single Sell credit. Runtime corroboration remains useful.

### BIN-EQUIP-003 - Give item-selection hit targets

**Observation:** The `PX05015` Give handler at `0x00445a4f` first translates
pointer input into its shared panel-local coordinates. Its three item-toggle
checks use the half-open rectangles `(103,15)-(155,67)`,
`(103,79)-(155,131)`, and `(103,143)-(155,195)`. These surround the smaller
portrait apertures by one pixel. The same handler presents up to five eligible
friendly recipients in roster order at `(209,16 + 36*n)-(241,48 + 36*n)` and
accepts Enter/Execute only when at least one eligible item and a recipient have
been selected; Escape and the Cancel control abandon the dialog.

**Interpretation:** The selection targets are fixed 52-by-52 cells with a
64-pixel vertical pitch. Artwork sizing must not be substituted for input
geometry: the visual item aperture is slightly smaller than its selectable
cell. Recipient selection belongs to that same panel rather than a second
target-picker screen. The recreation follows that interaction; Up/Down
recipient cycling is retained as an explicit keyboard-navigation quality-of-life
addition.

**Confidence:** High static evidence for the three bounds, pitch, control
branches, and confirmation predicate from `0x00445a4f`; native interactive
capture remains useful for pressed-state presentation.

### BIN-EQUIP-004 - Sell item-toggle hit targets

**Observation:** The adjacent `PX05013` Sell handler at `0x00443bbd` follows
the shared panel pointer translation and tests three half-open item rectangles:
`(111,15)-(301,67)`, `(111,79)-(301,131)`, and
`(111,143)-(301,195)`. Each toggles the corresponding equipped fixed slot only
when it is populated. Its Cancel and confirmation controls use the common
panel-local positions; confirmation rejects an empty item mask.

**Interpretation:** Sell's three interaction rows are fixed 190-by-52 cells
with a 64-pixel pitch. They do not cover the full-width rendered label and
price row, so wide artwork must not widen the click target.

**Confidence:** High static evidence for all item bounds, slot guards, toggle
behavior, and confirmation branch from `0x00443bbd`; native interactive
capture remains useful for pressed-state presentation.

### BIN-EQUIP-005 - Equip and Research category/list targets

**Observation:** The `PX05004` Equip handler at `0x0043dad9` translates
pointer input into panel-local coordinates. Its four category checks are the
half-open 32-by-32 cells `(104,16)-(136,48)`,
`(104,52)-(136,84)`, `(104,88)-(136,120)`, and `(104,124)-(136,156)`.
Selecting a category clears the current choice and rebuilds the list through
`0x0043f136`. That helper clears sixteen fixed list entries before examining
the 64 catalog records; the original extracted catalog has at most fifteen
records in any resulting category. The item-list pointer region is
`(148,26)-(328,169)`, and the handler derives its row directly as
`floor((y - 26) / 9)`. Its list-render helper `0x0043efe5` uses the same
nine-pixel cadence.

The separate `PX05007` Research handler at `0x004427fa` uses the same shared
entry table and calculates the clicked row from that same 26-pixel baseline,
but its accepted pointer rectangle is `(148,19)-(328,162)`. Thus its first
row includes the seven pixels above the rendered list baseline while its final
row ends at the same relative list position.

**Interpretation:** This is a fixed sixteen-row list, not a scrolling
twelve-row projection. The category artwork can remain slightly larger than
the hit cells, but input must use the inset 32-by-32 rectangles. Equip and
Research have intentionally distinct list hit boxes even though they render at
the same nine-pixel cadence. The recreation retains its existing keyboard
navigation.

**Confidence:** High static evidence for category/list bounds, reset/rebuild,
sixteen-entry backing table, and row calculation from `0x0043dad9`,
`0x0043f136`, `0x0043efe5`, and `0x004427fa`; native interactive capture
remains useful for pressed-state presentation.

### BIN-FINANCE-001 - alternate Financial panel destination and close face

**Observation:** The City/Sector Financial handler at `0x0044d1bb` loads
`PX05008` through resource id `0x1390` and follows the alternate 320-pixel
panel route. Its off-screen render coordinates use source left 344, which is
copied to final screen x=128 through x=448 at y=124. The player portrait is
drawn at source `(370,161)-(434,225)`, yielding final `(154,141)-(218,205)`.
The value column at source x=610 yields final right edge 394, with final rows
at y 151, 160, 178, 196, 214, 223, 241, and 268. The handler translates
pointer coordinates from the same 128-pixel left edge and tests its close face
as `(161,293)-(210,315)`.

**Interpretation:** Financial panels are an explicit exception to the normal
344-by-209 shared panel destination: they draw the native 320-by-209 source
area at `(128,124)`. The City/Sector choice is made before opening the panel;
the modal's recovered pointer branch closes only through its own face, so the
recreation does not expose the city-console split controls inside Finance.

**Confidence:** High static evidence for the resource, alternate-path source
and destination relationship, portrait/value coordinates, pointer translation,
and close rectangle from `0x0044d1bb`; a native capture remains useful for
color and pressed-state presentation.

### BIN-SECTOR-GANGS-001 - compact all-gangs sector roster

**Observation:** The `PX05009` Gangs in Sector handler at `0x0044e6ed` scans
all 81 roster records for active gangs in the selected sector. It advances one
display index for every match and draws each portrait into a 32-by-32 cell at
`(144 + 32*n,158)-(176 + 32*n,190)`. It then writes Tech Level, Upkeep, and
fourteen statistic values in that same column at x=`154 + 32*n`, with rows
192, 201, 211, 220, 229, 238, 248, 257, 266, 275, 284, 294, 303, 312, 321,
and 330. The six-gang sector capacity bounds the rendered columns to six.

**Interpretation:** `PX05009` is a simultaneous compact sector roster, not a
one-gang detail browser. The recreation renders every active gang in its
fixed-width card column while retaining keyboard selection as a non-destructive
quality-of-life shortcut for the city/sector workflow.

**Confidence:** High static evidence for roster scan, card dimensions/pitch,
all sixteen value rows, and ordering from `0x0044e6ed`; native capture remains
useful for confirming color treatment and over-cap corruption behavior.

### BIN-COMBAT-RESULTS-001 - results pager, selection map, and bottom control

**Observation:** The `PX05012` Combat Results handler at `0x00451f80`
translates pointer input through shared panel origin `(104,124)`. Its previous
and next arrows are the half-open local rectangles `(31,33)-(57,56)` and
`(59,33)-(85,56)`. The five other-player portrait targets are
`(202,16 + 36*n)-(234,48 + 36*n)`, for `n=0..4`, and accept only players with
a populated result row. The viewer-force selector is not six separate
40-by-40 portrait hits: one local `(101,27)-(189,183)` region maps x greater
than 144 to the right column and y greater than 78 and 130 to the second and
third rows. Its bottom confirmation face is `(33,169)-(82,191)`.

**Interpretation:** The native page arrows use 26-by-23 pointer targets even
though their presentation is smaller. The broad force selector assigns one of
six packed result slots, including its gutters, while other-player selection
remains exact 32-by-32 portrait cells. `PX05012` has no in-panel Detail
control; the bottom face exits the panel, while the separate console Detailed
Combat route opens `PX05014`.

**Confidence:** High static evidence for all input bounds, row/column
thresholds, slot-population guards, bounded pager branches, and bottom-control
exit from complete handler `0x00451f80`; native capture remains useful for
pressed-state presentation.

### BIN-EVENTS-002 - Last Turn Events pager and exit control

**Observation:** The complete `PX05010` handler `0x0044f2fc` translates a
pointer inside the shared panel and tests only three controls: previous local
`(31,33)-(57,56)`, next `(59,33)-(85,56)`, and bottom exit
`(33,169)-(82,191)`. It applies the same bounded page behavior as Combat
Results and Comlink View: first/last boundary attempts play slot 4, while a
legal step uses helper `0x00451602` and slot 3. Enter and Execute activate the
bottom exit face. No Delete rectangle or report-clearing pointer branch occurs
in this handler.

**Interpretation:** Last Turn Events has one 49-by-22 bottom control, not a
generic Cancel/Delete pair. Reports remain available until the normal review
completion lifecycle processes them; the recreation does not add a destructive
per-panel delete path.

**Confidence:** High static evidence for all three rectangles, keyboard and
pointer branches, sound outcomes, and absence of a Delete branch from complete
handler `0x0044f2fc`; native capture remains useful for pressed-state
presentation.

### BIN-GAME-INFO-001 - alternate panel crop and field origins

**Observation:** Game Information handler `0x0045519d` loads `PX05021` into
the alternate backing area and its close transition copies source
`(344,144)-(664,353)` to final screen `(128,124)-(448,333)`. Its pointer
translation uses that 128-pixel destination left edge and tests the bottom face
as local `(33,169)-(82,191)`, or screen `(161,293)-(210,315)`. Dynamic text
is written at backing x=444 for the three header values, x=456 for player names, and right-aligned to backing x=624 for intelligence; these map to screen x=228, x=240, and right edge 408. Header y values map to 151, 169, and 187, while the six player rows map to y=214 + 9*n.

**Interpretation:** `PX05021` is a 320-by-209 alternate panel, not the normal
344-pixel shared template. Its source crop must be retained when drawing the
imported panel rather than stretching it, and all dynamic x origins move 24
pixels right from the former shared-template approximation.

**Confidence:** High static evidence for alternate source/destination rectangles, close target, field origins, alignment, and row stride from complete handler `0x0045519d`; native capture remains useful for palette and text clipping.

### BIN-GANG-DEFINITION-001 - `PX05022` alternate definition panel

**Observation:** The definition-information handler `0x00455b6b` loads
`PX05022` into the alternate backing region and closes by copying source
`(344,144)-(664,353)` to `(128,124)-(448,333)`. It copies the 64-by-64 gang
portrait to backing `(370,161)-(434,225)`, yielding screen `(154,141)-(218,205)`. Name text is written at backing `(444,171)`, followed by three 30-character description rows at y=189, 198, and 207. The force/current-value column ends at backing x=516; upkeep, Tech Level, and the right statistics column end at x=612. These map to screen x=300 and x=396. Statistics use screen rows 243, 252, 270, 279, 288, 297, and 306. Both keyboard confirmation and the sole pointer exit face use local `(33,169)-(82,191)`.

**Interpretation:** Hire-offer definition inspection is not the normal
`PX05000` live-gang panel. It has no instance equipment, uses 30-character
description rows, and must preserve the alternate 320-pixel crop and its
24-pixel rightward field shift.

**Confidence:** High static evidence for resource identity, crop/destination, portrait, all recovered text/value origins, description length, statistic rows, and exit control from complete handler `0x00455b6b`; native capture remains useful for palette and label clipping.

### BIN-MOVEMENT-001 - Terminate pass before roster-ordered Move

**Observation:** The whole-turn resolver `0x00472775` has two distinct movement
passes. Lines 678-701 scan player slots 0 through 5 and each player's 81 roster
slots, resolving action 14 (**Terminate**). Only after that pass finishes do
lines 702-725 repeat the same player/roster scan and resolve action 10
(**Move**) from its stored destination sector. At the start of each player's
Move pass, line 703 calls `0x00476a94`.

That helper repeatedly rebuilds two 64-entry count arrays. Every active
non-mover increments its current-sector count; every active action-10 record
increments its proposed-destination count and is appended to an ascending
roster-slot list. Its sector scan retains the last total above six, making the
highest-numbered overcrowded sector the next one repaired. It first finds the
earliest mover into that sector whose source's projected total is below six and
rewrites the destination to that source. If no such mover exists, it rewrites
the earliest mover into the sector anyway. When that record is already a
rewritten source no-op, the helper instead calls sector selector `0x00408642`
with literal mode 0. Mode 0 assigns no positive weights: it consumes one
bounded draw over all 64 tied sectors and applies the common x-then-y one-step
capacity routing. The count/rewrite loop restarts until no projected sector
exceeds six. The outer resolver then copies every remaining action-10
destination without another capacity test.

**Interpretation:** Every Terminate resolves before any Move, regardless of
command submission order. Within each pass, results follow ascending player
slot and persistent roster slot. Competing Moves do not use sequential
first-mover priority. When otherwise legal moves overfill a destination, the
earliest qualifying roster slot is rewritten first and becomes a resolved
no-op, leaving later roster slots to move. Multiple overcrowded sectors are
repaired from highest sector ID down, with a complete recount after every
rewrite. Terminate changes only the copied gang record's sector byte to inactive
sentinel 100; it does not erase equipment or other stale payload before copying
the record back.

**Confidence:** High static evidence for pass precedence, action identities,
loop bounds, player/roster ordering, simultaneous projected counts,
repair-sector and mover selection, mode-0 fallback/RNG behavior, final Move
target decoding, and Terminate's exact record mutation. Runtime corroboration
remains useful.

### BIN-MOVEMENT-002 - Move panel neighborhood target mapping

**Observation:** The `PX05006` Move handler at `0x004413ef` accepts a pointer
inside the shared panel and then tests the panel-local neighborhood rectangle
`(132,26)-(294,182)`. It subtracts that origin and maps the resulting point
into three 54-pixel columns and three 52-pixel rows. The native index is
`column + 3 * row`; index 4 (the actor's center sector) is explicitly excluded,
and out-of-city cells are disabled before any destination write. Cancel and
confirmation use the common shared-panel controls; confirmation rejects when
no valid destination was selected.

**Interpretation:** `PX05006` is an exact 3-by-3 162-by-156 neighborhood
target, not nine independently sized art apertures. Its eight legal cells map
directly to the surrounding sector offsets in row-major order; the center is
display-only and must remain inert.

**Confidence:** High static evidence for the panel, neighborhood bounds, cell
pitch, row-major mapping, center exclusion, invalid-edge guard, and control
branches from `0x004413ef`. Runtime capture remains useful for selection-border
presentation.

### BIN-CONTROL-001 - cross-player winner and zero-margin neutral candidate

**Observation:** The Control block in the whole-turn resolver `0x00472775`
first accumulates action-4 strength in the player/81-slot scan at lines 733-744,
then resolves sectors in ascending board order in the loop beginning at line
751. Within each sector, it initializes its best margin to zero, winner to -1,
and the first candidate to -1. It then scans player slots 0 through 5,
subtracting the phase sector Income, defending-gang strength, and
influenced-site Support from each player's pooled Control strength. A strictly
larger margin replaces the candidate list; an
equal margin appends that player. When more than one candidate exists, the call
at `0x004756d9` passes the candidate count to the recovered one-based bounded
RNG wrapper. The selected value indexes through a four-byte slot immediately
before the player-candidate array, so value 1 selects candidate zero. Capture
proceeds only when the selected candidate is not -1.

**Interpretation:** Control tie-break RNG is consumed in ascending sector order,
independent of command submission order. Participants aggregate by ascending
player and persistent roster slot. Negative margins cannot capture. A unique
positive leader captures without a random draw. Equal positive leaders are chosen uniformly in
ascending player-slot order. At best margin zero, the original neutral -1 entry
remains ahead of every tied player: the one-based result 1 means no capture and
results 2 onward select the tied players in ascending slot order. Thus one
zero-margin challenger has the manual's 50-percent chance, while `n` tied
zero-margin challengers each have probability `1 / (n + 1)` and the remaining
outcome leaves ownership unchanged. Every random selection consumes the usual
three raw RNG values.

**Intentional exception:** The candidate scan at lines 778-793 does not test
whether a player issued Control. All six strength cells start at zero, and only
actual commands add to them. Ordinarily positive defense leaves nonparticipants
below the leading candidates, but if sector Income plus defending strength plus
Support is negative, every zero-strength nonparticipant has a positive margin
and can tie or beat the real challenger. A selected nonparticipant is then
written directly to the owner byte at lines 801-813. This is a clear shipped
bug: the recreation considers only players represented by actual Control
commands. A distinct-site fixture using Research Lab, Science Center, and
Headquarters produces total defense -1 and proves an idle player is never
awarded ownership in the corrected model.

**Confidence:** High static evidence for board/player/roster scan order,
initialization, comparison behavior, candidate order, one-based RNG call,
neutral sentinel, nonparticipant bug, and capture predicate. The manual independently corroborates
the single-player zero-margin probability; multi-player runtime capture remains useful.

The same focused owner-field audit establishes retained control of an empty
sector. Within the complete whole-turn resolver, the owner byte at
`0x004a08e8 + sector * 0x24` is written only at decompiled lines 313-318, where
the third qualifying Crackdown sets it to -1 and clears influence totals, and at
lines 801-815, where a non--1 Control winner replaces the prior owner and clears
those totals. The Movement and Terminate passes contain no owner write.

**Interpretation:** Moving or terminating the last friendly gang in a sector
does not itself abandon control. Ownership persists until another explicit
ownership-changing rule runs. A later Control attempt still includes sector
Income and influenced Support but naturally has no defending-gang contribution.

**Confidence:** High static evidence within the whole-turn resolver; moving and
terminating last-gang recreation fixtures guard the negative behavior.

### BIN-UPKEEP-001 - flat sector tax and influenced-site Cash share one byte

**Observation:** Cash updater `0x0046e766` scans players 0 through 5. Its active
gang-record loop at decompiled lines 194–215 subtracts each gang definition's
Upkeep. The following sector loop at lines 216–232 adds signed sector byte `+3`
once when owner byte `+0` matches the player. This is not the generator's
density-derived 3–7 value during playable turns. The enclosing loop initializes
`local_8` to one, skips cash collection on its first iteration, and calls
`0x004782c5` for every sector before the first planning phase. That helper sets
byte `+3` to one, adds site-table Cash offset `+8` for each completed site, and
the `0x0046f246` call site copies the full 36-byte result back. Later iterations
collect cash before recomputing the sector records again.

**Interpretation:** The shipped cash result is the Help-described flat $1 per
controlled sector plus Cash from its influenced sites. The executable stores
those components combined as `1 + completed-site Cash`; the recreation exposes
them separately in events and Finance projections. Generated city density must
not be substituted for sector tax. This combined byte is also operational
sector Income: `0x004120ef` renders sector `+3` on the owner-gated Income row,
AI selector case 6 in `0x00402d70` returns that same byte, and the whole-turn
resolver consumes sector Income in both Chaos and Control. Consequently the
generated 3-7 value is not the playable Control/Chaos value after the first
pre-planning recomputation; it establishes the sector's base Tolerance before
the operational byte is replaced. Because the first pass skips the entire cash
collection branch, players enter their initial planning turn with setup cash
unchanged; recurring Upkeep begins on the next outer-loop iteration.

The same loop also supplies the complete Upkeep statistics boundary. For every
active gang, lines 197-212 subtract signed Upkeep from current cash
`0x004a25e8`; a negative value adds its magnitude to Cash Earned
`0x004a27e0`, while zero or positive Upkeep adds directly to Cash Spent
`0x0049ca78`. For every owned sector, lines 217-229 add its signed combined
Income byte to current cash; values below one subtract that signed value from
Cash Spent, while positive values add to Cash Earned. These branches execute
per gang and per sector, so positive and negative components are classified
before totals can cancel. Cross-reference inventories confirm the arrays are
the saved/rendered Cash Earned and Cash Spent fields and locate their other
resolver writers at successful Chaos/Sell and Bribe/Equip/Hire paths.

**Confidence:** High static evidence for arrays, offsets, initial skip,
recomputation/write-back, UI and AI consumers, player/gang/sector scan order,
per-component statistics branches, and arithmetic;
controlled runtime corroboration remains pending.

### BIN-TURN-PLAYER-ORDER-001 - fixed ascending planning slots

**Observation:** After Upkeep preparation, outer turn function `0x0046e766`
clears six per-player presentation bytes at lines 309-311, then loops
`local_c` from 0 through 5 at lines 312-348. Eligible controller value 1 calls
the computer planning path `0x00458fa0`; value 0 calls human handler
`0x0046fd80`; inactive/elimination states do not enter either ordinary planning
handler. The alternate result/handoff loop at lines 433-448 likewise scans
slots 0 through 5. Only after the ordinary planning loop does the function call
the monolithic whole-turn resolver, whose action and hire passes independently
scan player slots in the same ascending order.

**Interpretation:** Human/computer mixtures do not reorder a turn: participating
players plan in fixed player-slot order, eliminated slots are skipped, and all
deferred resolution follows after the planning scan. The recreation exposes
Command, Execution, and Hire as explicit deterministic boundaries, but advances
the same ascending slot sequence and automatically records transitions across
eliminated slots.

**Confidence:** High static evidence for both outer-loop bounds, controller
dispatch branches, inactive-state exclusion, and the resolver call boundary;
recreation turn-flow fixtures cover automatic skips.

**Hire-boundary corroboration:** The outer turn function performs this cash/
Upkeep scan at the top of each non-initial loop iteration, before entering the
six player planning handlers. After all planning handlers return, its direct
call at `0x0046f706` enters whole-turn resolver `0x004726c0`; that wrapper's
call at `0x00472750` enters hire resolver `0x00472775`. A successful hire copies
a complete 32-byte gang record into the first free roster entry and writes its
sector byte at record offset `+2` to the selected destination rather than the
inactive sentinel 100. On the next outer-loop iteration, the Upkeep scan's
active predicate sees that new record and subtracts its definition's signed
Upkeep field. Therefore the original charges no Upkeep before or inside the
hire resolver, but it does charge the recruit at the immediately following
turn-start Upkeep. The original UI returns control only after that scan, which
can make the contract price and first Upkeep deduction appear simultaneous.

## New-game initialization

### BIN-CITY-001 - density-derived sector income and tolerance

**Observation:** In EXE-GOG-1.1, `0x00475fe1` constructs a 32 by 32 integer
density field. Forty pairs of bounded `1..32` draws select zero-based centers.
Four nested-radius passes add clipped 2 by 2, 4 by 4 and 6 by 6 footprints,
capping cells at four. Each board sector sums the corresponding 4 by 4 cells.
Its income byte is rounded from the average and offset by three; its initial
tolerance byte is `17 - income`.

**Interpretation:** Generated base income is 3 through 7 and is independent of
the three sites' cash benefits. The recreation therefore stores sector income
explicitly rather than deriving it from site definitions.

**Confidence:** High static evidence; runtime reference fixture pending.

### BIN-CITY-002 - three-site rejection sampling

**Observation:** `0x004764b6` draws a site ID from 0 through 20. In scenario
index 9 it rejects IDs 4 and 8 before returning the proposal. Slot zero accepts
its first proposal without balance validation. Slot one rejects a duplicate of
slot zero before validating the partial combination; slot two compares slot
zero and then slot one before validation. Every rejected proposal therefore
consumes its bounded draw before retrying. `0x00476516` rejects a partial
combination when the sum of any of the 14 site statistic modifiers is outside
-6 through +6.

**Interpretation:** Proposals are uniform and this generation path has no
frequency or class selector; duplicate and statistic rejection condition the
accepted second and third slots. Scenario 9 is Armageddon; its excluded Research
Lab and Science Center agree with the scenario's initially completed research.

**Confidence:** High static evidence; runtime reference fixture pending.

### BIN-CITY-003 - headquarters and Right Hands

**Observation:** `0x00439563` writes sector IDs 9, 12, 30, 33, 51 and 54.
`0x00476726` creates a six-value permutation by repeated bounded `1..6` draws
with duplicate rejection, maps players through that table, assigns ownership,
and replaces site slot zero with definition 21. `0x0046dc10` then initializes
gang definition zero in each player's assigned sector at Force 10.

**Interpretation:** Those six fixed sectors are the only new-game HQ candidates;
Right Hands is definition zero and always starts at maximum Force. In a local
new game all six slots are participants by the time this routine runs; setup
slots omitted by the local players have already become computer players.

**Confidence:** High static evidence; a runtime reference fixture remains pending.

### BIN-SETUP-000 - fresh setup defaults to Kill 'Em All

**Observation:** In EXE-GOG-1.1, the initialized preference byte at
`0x00487858` is `0`. Preference loader `0x0046439a` replaces it with the
registry value `prefsObjective` when that value exists. Setup initializer
`0x004384c0` copies the signed byte into the active scenario dword at
`0x004abbe8` before rendering the full local setup screen. The earlier analysis
incorrectly mapped that zero through the recreation's gameplay enum rather
than the original setup's visual-button order.

**Runtime observation:** On 2026-09-13 the fingerprinted original opened its
full local setup with Kill 'Em All selected. Its first visual-button light was
the only bright scenario light, and the description panel named the same
objective. Five desktop-copy frames taken 120 ms apart were byte-identical
(PNG SHA-256
`ab81528fa770e991140d5133c5dbb092751c7c3f723195cb33dcb41740410275`).
Targeted searches found no `prefsObjective` value in that user's HKCU or the
machine hive, excluding a stored objective override for this observation.

**Interpretation:** A fresh original setup selects the first visual scenario,
Kill 'Em All. The initialized zero is a setup/original ordering value and must
not be interpreted through the recreation's enum, where Greed happens to be
zero. A stored `prefsObjective` may still restore another selection when it
exists.

**Confidence:** High from the verified GOG 1.1 initialization path plus a
stable original-runtime observation with the override value absent. This
corrects the earlier static interpretation without changing the recovered
initialized byte or preference-loader facts.

### BIN-SETUP-001 - `SMGFUNDAGE` starting cash override

**Observation:** Fresh-game initialization assigns each player $500 in scenario
9 and $20 otherwise at `0x0046e766`. The adjacent name scan sets a transient
per-player byte only for the exact uppercase name `SMGFUNDAGE`. After the city
and player setup calls return, `0x0046ec72` reads that byte and overwrites the
player's cash with 0x5dc ($1,500). The transient byte has no save/load references;
the resulting cash value is what persists.

**Interpretation:** `SMGFUNDAGE` overrides both ordinary and Armageddon starting
cash. Case variants do not match, and no authoritative cheat flag is needed
after fresh-game bootstrap.

**Confidence:** High static evidence for the exact trigger, value, ordering, and
transient lifetime; runtime corroboration remains pending.

### BIN-SETUP-002 - local missing slots become computer players

**Observation:** The local setup handler `0x0040e0a0` keeps six player-type
dwords at `0x004ab638`. Type -1 is an empty setup slot, type 0 is a local human,
and type 1 is a computer. When Begin is accepted it scans slots 0 through 5;
every -1 slot is changed to type 1, receives a portrait from `0x00468c8e`, and
receives the default name for that portrait through `0x0046d1f7`. The portrait
helper repeatedly draws bounded `1..15`, subtracts one, and rejects any portrait
already present in any of the six slots. Thus all six players are active before
`0x0046dc10` generates the city, HQ permutation, and six Right Hands gangs.

The Win32 string-table names at resource IDs 62 through 76, indexed by portrait
0 through 14, are `ROCK`, `GECKO`, `RAZOR`, `SOUL TEAR`, `HEDIN VISE`, `REDD`,
`VECTOR`, `ICE`, `TORQ`, `KANSER`, `ECLYPSE`, `CRETIN`, `SCREAMER`, `PSYCHO`,
and `BLAKHART`. Portrait 15 is the empty-slot presentation image and is not a
candidate returned by the helper.

**Interpretation:** The original local setup count is the number of explicitly
configured local players, not the final match participant count. The recreation's
`OriginalMatchFactory` now completes omitted slots in ascending order and consumes
their portrait-selection RNG before AI state and city generation.

**Confidence:** High static evidence for types, ascending fill order, portrait
range, duplicate rejection, resource-name mapping, and placement before city
generation. A runtime setup fixture with captured seed and prior `serialNum`
state remains pending; `BIN-RNG-001` and `BIN-RNG-005` now recover the seed and
accepted-Begin call order statically.

The original Help further specifies the interactive local-player side of this
state machine: setup begins with one local human; Add introduces another local
human and Remove reverts the last one; clicking the name under a face edits at
most 10 characters. The resource-name helper at `0x0046d1f7` also passes an
exact length of 10 to `0x00466673` when filling an omitted computer's 12-byte
name record. The client now starts with one configured human, adds/removes
humans rather than synthetic CPU toggles, restricts selectable portraits to 0
through 14, and exposes the bounded name field. Face dragging now moves a human
identity into an empty color or exchanges two human colors; the transient sparse
setup is normalized into ascending slots before recovered empty-slot completion.
Exact name/drop-field coordinates still require native capture.

### BIN-SETUP-003 - `SMGISLANDS` permanent neutral-sector Crackdown

**Observation:** The same exact, case-sensitive fresh-name scan sets transient
byte `0x004abc10` for `SMGISLANDS`. After city generation, assignment of all six
HQ owners, and creation of all six Right Hands gangs, `0x0046dc10` scans sectors
0 through 63 once for each flagged player. Every sector whose owner byte is -1
receives byte 100 at sector offset `+0xf` (`0x004a08f7`); owned HQ sectors are
unchanged. The whole-turn resolver reads this same byte as police duration and
decrements only positive values below the permanent sentinel 100. The flag is
cleared at fresh setup/teardown and has no save/load references.

**Interpretation:** `SMGISLANDS` starts every neutral non-HQ sector under a
permanent Crackdown. It does not initialize a sector-Chaos value. The override
runs after the six HQ candidates become owned, so those starting sectors remain
unaffected.

**Confidence:** High static evidence for the exact trigger, ordering, owner
predicate, value, and transient lifetime; runtime corroboration remains pending.

### BIN-SETUP-004 - extra-gang and global-visibility name modifiers

**Observation:** Fresh-game initializer `0x0046dc10` compares every Pascal player
name case-sensitively with three additional uppercase strings. `SMGSPANK` sets
byte `0x004abbd8`; after the normal slot-zero Right Hands is created, slots 1
through 5 receive identical definition-zero, Force-10, unequipped Right Hands in
that player's HQ sector. `SMGKICKASS` sets byte `0x004a2788`; its slots 1 through
5 instead receive gang definition 59 (GROUND ZERO), Force 10, weapon item 23
(PLASMA GENERATOR), armor item 37 (BATTLE SUIT), and miscellaneous item 52
(EMPATHIC ENHANCER), again in the HQ. The flags are transient and the resulting
gang records are ordinary persisted state.

`SMGHUBBLE` sets per-player byte `0x004ab588`. Visibility rebuild routine
`0x0046fa11` normally initializes each of that viewer's 64 sector-detection
entries to -32000, but initializes all of them to 1000 when the byte is set. It
then folds in the viewer's active-gang Detect values using the exact algorithm
documented in `BIN-DETECT-001` and marks every opposing active gang visible when
its Stealth is no greater than the sector value. The
six-byte modifier array is transferred by save writer/reader references
`0x00463b5b` and `0x00464060`.

**Interpretation:** Exact `SMGSPANK` and `SMGKICKASS` give mutually exclusive
six-gang openings without RNG draws. Exact `SMGHUBBLE` grants its player global
opponent visibility; the recreation derives this persistent behavior from the
persisted exact player name rather than adding a redundant flag.

**Confidence:** High static evidence for exact triggers, gang slots, definitions,
Force, loadouts, sector placement, visibility baseline, and persistence; runtime
corroboration remains pending.

### BIN-SETUP-005 - exact local player-card interaction geometry

**Observation:** Full local-setup handler `0x0040e0a0` constructs six card
origins at `(397,94)`, `(480,94)`, `(397,168)`, `(480,168)`, `(397,242)`, and
`(480,242)`, then tests a 64-by-68 half-open rectangle at each origin. Within
an occupied human card, vertical offset below 58 enables the portrait controls:
horizontal offset below 16 calls decrement helper `0x00468d87`, while offset
strictly greater than 48 calls increment helper `0x00468cfc`. Vertical offset
58 through 67 calls the ten-character name editor at `0x0040f63d`. Therefore
the native hit regions are 16-by-58 on the left, 15-by-58 on the right, and
64-by-10 along the bottom; they intentionally exceed the visible 12-by-18
arrows and 64-by-8 text baseline.

Before applying those click actions, sole-call drag helper `0x0040f72e` waits
while the pointer stays in the half-open four-pixel box from initial offset -2
through +1. Leaving that box while the button remains down begins a drag. The
helper draws a 40-by-40 portrait token centered at the pointer, clamps its
center to x 20..620 and y 20..440, and on release tests the same six 64-by-68
card rectangles. A valid destination swaps the complete type, portrait, and
12-byte Pascal-name record, including an empty slot; release outside every card
leaves the roster unchanged. A click action is selected from the original press
point only when the drag helper reports that no drag began.

Global selected-card index `0x004854c4` starts at zero. A click on another
occupied card only selects and redraws it; arrow/name actions occur only when
the pressed card was already selected. Add selects the newly occupied slot.
Remove deletes the selected slot and selects the highest remaining occupied
slot. A successful drag selects its destination after moving or exchanging the
complete identity record.

Renderer `0x0040ee8a` copies every 32-by-32 top-strip portrait opaquely. For
each occupied local card it takes source `(32 * portrait,480,32,30)` and scales
it opaquely to `(cardX,cardY + 3,64,60)`. Only the selected card then receives
the exact-white-keyed `PX00140` arrow overlay from `(220,138,64,62)` at that
same destination origin. The non-selected branch requests mode 0, but because
its source and destination sizes differ the wrapper's opaque scaling branch
precedes and bypasses mode selection.

**Interpretation:** Bitmap apertures describe drawing, not input. The former
recreation reused the smaller visible glyph rectangles for input, began setup
drags only after four pixels of absolute motion, used a 48-pixel token, and did
not permit a drag to begin over arrow/name bands. It also acted on every card's
subcontrols immediately, drew arrows on every card, stretched all 32 portrait
rows over the full face, and incorrectly applied the `PX00146` inactive-seat
stencil to empty local top slots. The input and compositor now follow the
recovered selected-card state, source/destination rectangles, and keyed arrow
overlay.

**Confidence:** High from the complete local handler, the drag helper's sole
caller and bounded loop, the two portrait helpers, and the name editor.

**Next validation:** Native cursor imagery and drag-target feedback remain
visual capture work; card selection, hit testing, composition, and drag/drop
mechanics are statically closed.

### BIN-HIRE-001 - initial and replacement offers

**Observation:** `0x0046e766` initializes each player's three fixed offer bytes
at `0x004abbc0` to signed -100 and the matching action bytes at `0x004a27c8` to
-1. `0x004078b8` writes snub action -2 to the selected slot. The resolver at
`0x00472775` scans players 0..5 and slots 0..2, negates that same slot's offer
for a snub or successful hire, clears its action, and creates a hired gang with
a bounded `1..5` result plus four. Failed hires clear the action without
negating the offer. The planning-entry helper `0x004716eb` later scans slots
0..2 and fills each negative slot in place using repeated bounded `1..89` draws,
rejecting all currently positive slot values and that slot's negated old ID.
Its only callers are the computer and human planning-entry paths at `0x0046f4e3`
and `0x0046fe8b`; the resolver does not call it. The human handler at
`0x00416c75` assigns the selected slot and clears both other action bytes, so
hire and snub are mutually exclusive selections rather than two actions in one
turn. Its Reject branch toggles the selected slot from -1 to -2 or from -2 to
-1; clicking Reject on a slot holding a sector destination changes it to -1
rather than directly snubbing it. Dragging an offer writes the new destination
to that slot and unconditionally clears the other two slots, so a later drag
replaces the earlier selection and dragging the same offer can retarget it.
The handler does not read or update cash or cumulative cash spent. The resolver
first counts all gang records in the target sector at `0x0047592b`-
`0x004759a8`; six causes a failure with no random draw. It then checks
then-current cash at `0x004759bd`-`0x00475a15`. Only after those checks does it
roll Force at `0x00475ac4`; it subsequently searches the player's 80 gang slots
at `0x00475bdb`-`0x00475c2d`, so a full roster failure consumes the Force draw.
After successful gang creation, it updates cumulative cash spent at
`0x00475c88`-`0x00475ca8` and subtracts cash at
`0x00475cba`-`0x00475ce4`. Rejected, cancelled, and failed hires do not change
either value.

**Interpretation:** Initial offers are populated on planning entry. Successful
hire or snub leaves a signed tombstone in its permanent slot until that player's
next planning entry; refill never compacts or shifts slots. Multiple malformed
vacancies would refill in ascending slot order. Replacement selection is
rejection sampling over gang IDs 1 through 89; a just-hired or snubbed gang
cannot immediately replace itself. Hired Force is uniformly 5 through 9 through
the recovered bounded wrapper. Selecting a hire reserves no cash: affordability
is evaluated during resolution, and an unaffordable action is cleared without
creating a vacancy.

Hire resolution is reached from the outer turn loop through the direct call
chain `0x0046f706` -> `0x004726c0`, then `0x00472750` -> `0x00472775`. The
successful path subtracts only the gang definition's initial hire cost at
`0x00475cba`-`0x00475ce4`; it does not read the definition's Upkeep field.
Because the newly copied gang record has a real destination sector instead of
inactive sentinel 100, it participates in the separate Upkeep scan at the top
of the next `0x0046e766` outer-loop iteration. This statically distinguishes
"hire cost at the end of turn N" from "first Upkeep at the start of turn N+1,"
even though the normal UI exposes no player-controlled pause between them.

The six-byte array at `0x004a5ef0` is a persistent per-player modifier. Fresh
local-game setup clears each byte, compares that player's Pascal name with the
exact uppercase string `SMGMILK` at `0x0046e23f`-`0x0046e272`, and sets the
matching byte. The save reader/writer transfers all six bytes at `0x00463b6c`
and `0x00464071`; no per-turn or post-hire reset exists. At `0x00475aaa` the
resolver gives a flagged player's recruit Force 10 and bypasses the normal
bounded Force draw. The same name scan identifies adjacent original cheat
flags, corroborating that this is a name-triggered modifier rather than a
scenario or controller rule.

**Confidence:** High static evidence for arrays, sentinels, selected-slot writes,
mutual exclusion, resolver/refill order, call sites, RNG bounds, and the exact
`SMGMILK` trigger/effect; a controlled runtime sequence remains useful
corroboration.

## Toolchain hypothesis

### BIN-TOOL-001 - compiler/runtime

**Observation:** Linker version 3.10, 1996 timestamp, no imported MSVCRT DLL, and
native Win32 APIs.

**Interpretation:** A mid-1990s Microsoft Visual C++ toolchain with statically
linked runtime is plausible.

**Confidence:** Medium. Linker fingerprints and startup code still need matching
against known toolchain signatures.

## Remaining static-analysis queue

The first nine items in the former queue are complete: data-table loading, the
full action-phase dispatcher, PRNG and dice reduction, core resolvers, all
scenarios, city/HQ generation, and every live AI family now have address-level
findings and linked implementation/parity notes above. The native PX loader,
header repair, indexed palette path, opaque/stretch, pattern, exact-white key,
and proven mixed-copy resource handling are also closed. A smaller semantic
rectangle/UI tail remains below.

Useful static work which remains is narrower:

1. Resolve remaining semantic PX rectangle roles where callers can distinguish
   them; the loader, palette conversion, copy modes, color keys, and embedded
   pattern masks are closed.
2. Continue exact UI geometry/hit-map work where it can be derived from draw and
   pointer call arguments for remaining panels. Full local-setup card, arrow,
   name, drag-threshold, token, clamp, and drop geometry is closed.
3. Match the linker/runtime fingerprints against a known compiler signature only
   if this becomes useful to interpret generated-code artifacts; it is not a
   gameplay-parity dependency.

The fixed original save/load envelope is closed above. Every live AI family,
including family 1's unavailable-command policy and family 11's late guards,
is closed statically; reopen those areas only if a new contradiction appears.
The complete soundtrack-selector inventory also closes static menu/program
restart boundaries; remaining playback checks require native runtime evidence.

Runtime captures listed elsewhere are corroboration work and deliberately are
not included in this static queue. Every completed static item must add its
address-level findings here, focused regression coverage where behavior changes,
and a linked parity-matrix update.
