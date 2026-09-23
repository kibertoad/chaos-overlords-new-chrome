# Audio, music, and video

Status: active clean-room research log
Last updated: 2026-09-23

The media layer: CD track programs and their lifecycle, and the sound-effect
slots, volume handling, and setup cues that drive them. The imports these paths
cross are
[BIN-API-006](executable-and-platform.md#bin-api-006---audio-and-video); the
derived media map is [AUDIO-VIDEO.md](../AUDIO-VIDEO.md).

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Music](#music)
  - [BIN-MUSIC-001 - CD track programs and lifecycle](#bin-music-001---cd-track-programs-and-lifecycle)
- [Sound effects](#sound-effects)
  - [BIN-SOUND-001 - effect slots, volume and setup cues](#bin-sound-001---effect-slots-volume-and-setup-cues)
  - [BIN-SOUND-002 - turn-start cue and effect interruption](#bin-sound-002---turn-start-cue-and-effect-interruption)
<!-- doc-index:end -->

## Music

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

## Sound effects

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
the executable passes only slots 0 through 8; no direct wrapper call passes slot 9.
The direct lower-helper caller for slot 9 is documented in `BIN-SOUND-002`.
Pairing those callers with the image-loader wrapper identifies rejected-input
slot 4 in the handlers for Hire (`PX05000`/`PX05016`), Item and Site Information
(`PX05001`/`PX05002`), Attack, Equip, Influence, Move, and Research
(`PX05003`-`PX05007`), City/Sector Financial (`PX05008`/`PX05019`), Last Turn
Events, Player Ranking, Combat Results and Detailed Combat
(`PX05010`-`PX05014`), Give (`PX05015`), incoming/outgoing Comlink
(`PX05017`, `PX05018`, and `PX05023`), Gang Definition Information (`PX05022`), and Search:
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

The final word of each 166-byte `ITEMS` record, previously retained as an
unnamed value, is the combat portrait-frame index. Its only six direct reads
are in the two detailed-combat side compositors `0x0042ee46` and `0x0042f98b`.
For every equipped weapon, armor, and miscellaneous item, they load that
item's `PX04xxx` 720-by-48 rotation strip and opaque-copy its indexed 48-by-48
frame into the matching equipment aperture. It is therefore presentation data,
not an economic or statistic modifier. The recreation names the field
`CombatPortraitFrame`, preserves its historical `Unknown` JSON property name,
and uses the recovered frame rather than scaling the compact `PX04999` icon in
detailed combat. The backing-buffer apertures are `(100,192)`, `(100,241)`,
`(100,290)` on the left and `(289,192)`, `(289,241)`, `(289,290)` on the right;
the combat panel buffer begins at y=144, yielding panel-local y positions 48,
97, and 146.

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
gated general-effect wrapper in this executable. Music and effects share
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
panel and open the new one. Without the original's slide-out between them, the
open cue interrupts the close cue in the same frame, as recorded in
[Do not animate the panel slide-out](../DECISIONS.md#2026-09-23--do-not-animate-the-panel-slide-out). The idle-gang confirmation follows the same
preference gate.
Equipped, unarmed, and detected-police combat events route their recovered
sounds, while evasion remains silent. Combat and general effects share the
independent recovered Effects level and its level-6 default, and both audio
levels persist in the recreation-native preferences file. Each Detailed Combat
clip carries its event-time cue; the player emits it on the recovered first
animation tick, so retaliation waits for its reversed second clip instead of
playing with the opening attack. Simple Combat does not enter this presenter.

**Next validation:** Validate per-record Comlink acknowledgement, the slot-6
cadence, countdown-warning cadence, and native amplitude behavior at runtime.

### BIN-SOUND-002 - turn-start cue and effect interruption

**Observation:** The gated general-effect wrapper `0x00464290` calls the lower
play helper `0x0045851a` with a requested slot and priority. The outer turn
function `0x0046e766` also calls that lower helper directly at `0x0046f201`,
passing slot 9 and priority 1 when local-game flag `0x00482178` or legacy-network
flag `0x00487b58` is set. This branch follows the per-sector turn-start financial
work and precedes player planning. Its enclosing loop initializes `local_8` to
one and only reaches the branch when `local_8` is zero; the first turn therefore
does not play the cue, while later turn starts can.

The lower helper checks the requested slot and loaded memory pointer, then
tracks its channel bookkeeping and calls `PlaySoundA(pointer, 0, 7)` when sound
output is available. Flags 7 are `SND_ASYNC | SND_MEMORY | SND_NODEFAULT`.
They omit `SND_NOSTOP`, so a new `PlaySound` call can interrupt the sound already
playing; the separate MCI music path is unaffected. The helper does not consult
the wrapper's effects-enable byte for this direct caller. Microsoft's
[PlaySound documentation](https://learn.microsoft.com/en-us/windows/win32/multimedia/the-playsound-function)
and [flag reference](https://learn.microsoft.com/en-us/previous-versions/ms713269%28v%3Dvs.85%29)
describe the flags and interruption behavior.

**Interpretation:** `SND00208` is a turn-start cue after the initial turn in
local and legacy-network play. General and combat effects use one interrupting
playback path, while CD music has its own playback path. The native helper's
priority/channel bookkeeping and host driver timing still need audible
validation.

**Confidence:** High for the direct call, branch conditions, turn position,
flags, and separation from MCI music; Medium for audible interruption timing
on specific native systems.

**Recreation status:** The client plays slot 9 when a local turn counter advances
without an endgame outcome while a local human is still playing, and routes
combat and general effects through one active effect voice. Once every local
human is out, the computers play on at one turn per frame, so those turns stay
silent instead of restarting the cue every frame. Starting a new effect stops
the previous effect voice. Music remains separate. Changing the Effects level
replaces that voice with its confirmation cue at the new amplitude, and level
zero stops it. Cancelling a Detailed Combat presentation stops the skipped
clip's cue with it. These are presentation-only operations.

**Next validation:** Compare the cue and rapid successive effects against a
native reference capture, including local and legacy-network turn boundaries.
