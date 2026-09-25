# Static validation plan

Work that needs no one to play the original: reading the executable and the data files of
BLD-GOG-EN-1.1 in Ghidra or a hex editor, and checking the spec against what they hold. Each item
names the spec entries it concerns and what to record. The procedure for the tools is in
[docs/GHIDRA.md](docs/GHIDRA.md).

An item is closed by recording what was read as a new static finding (`FND-*`, `method: static`,
with addresses in the build's notation), citing it in the entries it concerns, raising or
correcting their statuses, and deleting the item from this file. A reading that contradicts an
entry makes the entry `disputed` until it is settled. Never paste decompiler output or listings
into the repository.

Items are grouped by the part of the game they concern. Within a group they are in no particular
order of priority, unless a group says otherwise.

## Executable, platform and file formats

- All `.ksy` files of this task (fmt_data_001..003, fmt_gfx_001..003,
  fmt_audio_001..002, fmt_video_001, fmt_help_001, fmt_save_001..002): every definition
  now compiles with kaitai-struct-compiler 0.11; parse the shipped files with the
  generated readers and compare every field with the entry. `fmt_gfx_002` names a process routine
  `rule_gfx_001` that has to be supplied; `fmt_audio_002` and `fmt_video_001`
  use `_index` in repeated sizes. Changes the Coverage sections of every
  format.
- FND-PLATFORM-001..008, FND-SAVE-001, FND-ASSET-001: several functions are
  located by their entry address alone. Give the full address range of each.
- FMT-GFX-001, FMT-GFX-002, FND-GFX-003: collect the width and height the
  executable passes to the image loader for each image number, in particular
  `PX00202`, `PX00203` and `PX06008`, whose file data give 311, 311 and 241
  where the older notes say 312 and 242. Look at the callers of the loader
  and the constants they push.
- FMT-DATA-004, FND-PLATFORM-007: find which `CLT` number the palette loader
  opens (the template reads `CLT00000`; the only file is `CLT00002`), and
  whether the colour table inside each PX08 file is ever used or replaced by
  the CLT palette.
- FMT-DATA-001..003: list the field offsets the executable reads from the
  `SITES`, `Gangs` and `ITEMS` records (starting from `0x004782C5`, the
  equipment resolver, and `0x0042E040`, which reads an item's sound byte) and
  the in-memory addresses the tables are copied to. Rows still resting on the
  source alone would become supported.
- FMT-DATA-005: confirm that no code path, including the installer
  components, opens `DATA.Z`.
- FND-PLATFORM-004: map the WinSock and `smackw32.dll` imports taken by
  ordinal to names, and find the callers of the TAPI and serial functions.
- FND-ASSET-001: explain the strings with a leading space
  (`" data\PX08\px00128"`, `" data\PX16\px00128"`, `" A:\CHAOS\CDTrack"`):
  whether the space is skipped by the code that uses them.
- FND-PLATFORM-002: give the exact test that chooses the 8-bit or 16-bit image
  set (display depth query and threshold).
- FMT-SAVE-002: find what writes an `M10W` file, if anything, and what the
  caller does with the load result 3; identify the 12-byte global.
- FMT-SAVE-001: identify blocks 16, 17, 18, 21, 36 and 37, confirm blocks 3,
  4, 8, 13, 27, 33 and 39 (now resting on the source), and name which
  preference each of the three preference bytes is.

## Shared in-memory structures

- FMT-STATE-002 `unk_01`, `unk_02`, `tolerance`: find where city generator
  0x00475FE1 writes the generated Income (3..7) and the starting Tolerance
  (17 - Income), and whether rebuild helper 0x004782C5 recomputes `tolerance`
  from a base byte plus site Tolerance.
- FMT-STATE-002 `tolerance`: give the offset of the Tolerance byte the Bribe
  case (old lines 114-127), the Snitch case (old lines 210-212) and the
  post-Instant floor loop (old lines 224-226) of 0x00472775 write.
- FMT-STATE-002 `unk_0D`, `unk_10`..`unk_15` and the fourteen `site_*` rows:
  list every write in 0x004782C5 with its offset, to confirm the site-bonus
  offsets and statistic order (now from SRC-RECHAOS-3561D41 only) and find the
  purposes of the unknown bytes.
- FMT-STATE-001 `player`, `definition`, `force`: find their offsets in this
  build. Leads: the Right Hands creation in 0x0046DC10 (definition 0, Force
  10), the hire copy in 0x00472775 at 0x00475BDB..0x00475C2D and the Force
  roll at 0x00475AC4, the damage application (old lines 530-540), and AI
  selector 0x3C (Force byte).
- FMT-STATE-001 `chaos`, `influence`, `research`, `strength`, `blade`,
  `ranged`, `fighting`: confirm the statistic order at 0x12..0x1F from the add
  sequence in 0x0047781F against the gang definition and item field offsets.
- FMT-STATE-001 `target`, `target_2`: record what each action's picker writes
  to +8 and +9 and what the resolver reads: Attack 0x0043B290, Move
  0x004413EF, Equip 0x0043DAD9 (the queued item byte), Influence 0x0043F692,
  Give 0x00445A4F, Sell 0x00443BBD (selection mask), Research 0x004427FA.
- FMT-STATE-003 `definition` (read as the definition by FND-COMBAT-004): record the instruction that writes
  byte 0 of each combat record in 0x00472775 (old lines 487-496) and what it
  stores. Also whether records of gangs that did not fight are cleared each
  resolution, and the instruction addresses of every byte's write.
- Glossary `player_active`: give the address of the player-active bytes that
  0x00476F3B clears; candidate six-byte blocks in the save list are
  0x004A5F00, 0x004ABBE0, 0x00482108, 0x00482158, 0x004A2600, 0x004ABC58 and
  0x004AB588.
- Glossary (new term needed by combat and police rules): the address and type
  of the per-player casualty statistic that the damage application (old line
  538) and police deaths increment.
- Glossary `hire_phase`, `resolution`: confirm that the hire block of
  0x00472775 (0x0047592B..0x00475CE4) runs after the Control block
  (0x004756D9); the order now rests only on block addresses.
- Glossary `turn_end`: order of the Crackdown decrement at 0x00475E74 against
  the call to elimination helper 0x00476F3B. FND-TURN-004 also mentions a
  Crackdown-duration update in outer turn function 0x0046E766; reconcile it
  with FND-POLICE-001, which places the decrement at the end of resolution.
- Glossary `elapsed_turns`: check whether the Crackdown occurrence window
  (FND-POLICE-001, "current turn - 5") reads 0x0049CA68 or another counter,
  and where 0x0049CA68 is incremented relative to `turn_end`.
- Glossary `roster_slot`: which 80 slots the hire search at
  0x00475BDB..0x00475C2D covers (1..80 is expected, leaving slot 0 to the
  Right Hands).
- Glossary `player_names`: what byte 0 of each 12-byte record at 0x004A2588
  holds (a length is expected for a Pascal string) and the text encoding.
- Glossary `research_remaining`: whether the Research case and pickers load
  the bytes signed or unsigned.
- Last Turn report record (EVENT area needs a FMT-STATE entry): the offsets of
  the occupied byte, the 2-byte type and the three 2-byte arguments in the
  10-byte records at 0x004AAE08 (the listed fields make 9 bytes, so one byte of
  padding sits somewhere), and the width of the per-player count at
  0x004ABCA8. Once known, core-state (or the lead) adds FMT-STATE-006.
- AI planning record (AI area): if the AI rules need it as a format, record the
  full 16-byte layout at 0x0048A250 (player stride 0x510): +0 family byte,
  +1, +11 and +15 unknown, +12 and +14 16-bit fields.
- The six-byte player-order table that AI selector 0x2D compares (FND-AI-005):
  its address, and whether it is related to `turn_order` or to the ranking.
- FMT-STATE-005 `unk_A5` and `text`: read recorder 0x0045D2F0 and the Send
  path for the last byte and the text encoding and padding.

## Random numbers and the order of a turn

- FND-RNG-001..005, FND-TURN-001..005, FND-HIDE-001: every function location is
  a single entry address. Give each function's full range (`0x00465620`,
  `0x00478CC0`, `0x00460CCF`, `0x0046439A`, `0x00478CD0`, `0x0045D227`,
  `0x00432DA0`, `0x00408642`, `0x00408214`, `0x0040E0A0`, `0x00468C8E`,
  `0x0046E766`, `0x0046DC10`, `0x00475FE1`, `0x00476726`, `0x004677F0`,
  `0x00472775`, `0x0043F692`, `0x004782C5`, `0x00402D70`, `0x00414D8C`,
  `0x0041462F`, `0x00476F3B`, `0x00476857`, `0x0047712A`, `0x00458FA0`,
  `0x0046FD80`, `0x004726C0`).
- RULE-RNG-001 (open question), FND-RNG-001: check which thread each caller of
  `fn_0045D227` runs on, and whether any thread other than the one that runs
  `fn_00465620` can draw (it would start from the runtime default state 1).
- FND-RNG-004: list the addresses of the twelve family-handler functions whose
  wrapper calls make up the 46 AI calls.
- FND-TURN-001: record the instruction addresses of the Influence case, the
  Control block's owner write and the Chaos pass's neutralizing branch in
  `fn_00472775` (the old text gave decompiler line numbers 151-185, 801-821 and
  313-319). Also the base address of the in-memory site definition table that
  `0x004AB684` points into, for the glossary term `site_definitions`.
- RULE-TURN-003: whether the Instant scan skips records whose `sector` is 100
  before the switch on `action`. RULE-HIDE-001: whether the Hide case at
  `0x00472D00` does anything besides incrementing `hide_count`.
- RULE-TURN-004, FND-TURN-004: how `repeat_target` encodes the site of a
  recurring Influence and the item of a recurring Research (compare the
  pickers `fn_0043F692` and `fn_004427FA` with the cleanup's reads). Reconcile
  the Crackdown-duration update in `fn_0046E766` with the decrement at
  `0x00475E74` (RULE-POLICE-003): one update or two.
- RULE-TURN-005, FND-TURN-002: which of a sector's gangs `fn_0041462F` applies
  an order to; whether the recurring paths write `target` (individual) and
  `repeat_target` (sector-wide); what the individual handler's recurring
  Influence shortcut is.
- RULE-TURN-006, FND-TURN-003: the test `fn_00476F3B` uses for "slot 0 no
  longer holds the active Right Hands" (sector byte, definition or both);
  whether the Eliminate scan skips inactive players; the instruction addresses
  of the call to `fn_00476F3B`, the report loops and the call to `fn_00476857`
  (old decompiler lines 933-945); where they fall against the Crackdown
  decrement at `0x00475E74`; the address of the player active bytes.
- RULE-TURN-001, FND-TURN-005: how the planning loop in `fn_0046E766` tells an
  inactive slot (active byte or controller value) and what it does for
  controller 3; the instruction addresses of the six-byte presentation clear,
  the planning loop and the second slot loop (old lines 309-348 and 433-448);
  where `elapsed_turns` (`0x0049CA68`) is incremented; whether a match loaded
  from a save enters the loop with the first-pass Upkeep skip; the order of the
  offer refill and the visibility rebuild at a planning entry.
- RULE-TURN-002: confirm by control flow, not only block addresses, that the
  hire block (`0x0047592B..0x00475CE4`) runs after the Control block; the order
  of the Crackdown-report snapshot (RULE-POLICE-004) against the second loop of
  RULE-EVENT-001 at the start of resolution; where RULE-TOLERANCE-001 runs in
  the turn.
- FND-HIDE-001, RULE-HIDE-001: search for other reads of record byte `+7`
  compared with 8 (Control strength, visibility, the computer players) to list
  every consumer of `is_hidden`.

## Hire, Influence, Research, Bribe, Snitch, Tolerance and sites

- FND-BRIBE-001, FND-SNITCH-001, FND-RESEARCH-001, RULE-HEAL-001,
  RULE-INFLUENCE-001, RULE-TOLERANCE-002: record the instruction ranges of
  the instant-phase cases 2 (Bribe), 7 (Heal), 9 (Influence), 11 (Research)
  and 13 (Snitch) in `0x00472775` (old decompiler lines 114-127, 151-185,
  186-208, 210-212) and of the post-instant floor loop (old lines 224-226),
  and add them as locations. While there: the sector offset each case writes
  for Tolerance (expected `+0x05`), whether the Tolerance add/subtract is done
  at byte width, what the Snitch case's "changed" mark is and whether Bribe
  sets it too, and whether the Heal case tests Force before calling the dice
  helper.
- All my findings: give full ranges for `0x00472775`, `0x0046E766`,
  `0x004716EB`, `0x004078B8`, `0x00416C75`, `0x004546C5`, `0x0043F692`,
  `0x0046DC10` and `0x004427FA`; the findings give only entry addresses.
- RULE-HIRE-001: list every field the hire block writes into the new gang
  record (between the free-slot search `0x00475BDB..0x00475C2D` and the cash
  update at `0x00475C88`): equipment -1 or not, action, recurring action,
  `visible_to`, effective statistics. Also: the free-slot search bounds
  (slots 0-79 or 1-80); the operand order of the `CMP` at `0x00475A0E`
  (confirms `cost > cash` fails, and so the zero-cost-in-debt case); where the
  offer is negated in the success path.
- RULE-HIRE-002: read the full rejection test in `0x004716EB` for any other
  exclusion (other players' offers, gangs already hired).
- RULE-HIRE-003 / FND-HIRE-004: confirm that the Reject branch of
  `0x00416C75` clears the other two slots, and record what the drop handler
  tests before writing a sector (owned, occupied, capacity). Record what
  `0x004078B8` is (a Reject path, the AI, or both).
- FMT-STATE-001 `target`: record what `0x0043F692` (Influence) and
  `0x004427FA` (Research) write on confirmation, and which field the Influence
  and Research cases of `0x00472775` read.
- RULE-SITE-001: list every write in `0x004782C5`, including whether it
  clears `support` and the fourteen sums first, and where it puts each
  completed site's Tolerance and special flag (`tolerance`, `unk_02`,
  `factory`, `unk_0D`).
- RULE-TOLERANCE-001: find the code that moves Tolerance one point toward
  normal each turn (candidates: the turn-start loop in `0x0046E766`, the
  rebuild `0x004782C5`, the end of `0x00472775`). The rule is `sourced` until
  then.
- SCR-HIRE-001: record the resource load in `0x004546C5` (expected 5016), the
  destination rectangle, the portrait and value field positions, the order of
  the ten modifier rows, and the close control and its handler.
- SCR-HIRE-002: find the main-console code that draws the three offers and
  the hit tests for dragging, Reject and double-click (and whether the
  double-click reaches `0x00455B6B`, the `PX05022` definition panel).
- SCR-INFLUENCE-001: the shared panel's screen origin, the Cancel and
  confirmation rectangles and keys, and where the three site pictures are
  drawn.
- SCR-RESEARCH-001: whether `0x004427FA` has category cells and where, the
  filter its list builder applies (Tech Level, the manual's Tech 5 limit and
  the Science Center / Research Lab caps, items already researched), the
  columns a row draws, the confirmation and Cancel controls, and the
  double-click to `PX05001`.
- FMT-DATA-002: find the hire-cost field the hire block reads at
  `0x00475A0E` / `0x00475CBA`.

## Movement, Control, gangs, equipment and money

- All findings in these areas: give the full address range of `0x00472775`,
  `0x0046E766`, `0x004782C5`, `0x0047781F`, `0x00476A94`, `0x00476F3B`,
  `0x00474BF3`, `0x004756D9`, `0x00408642`, `0x004413EF`, `0x00443BBD`,
  `0x00445A4F`, `0x0043DAD9`, `0x0044D1BB`, `0x00455B6B`, `0x00449E80`,
  `0x00414187` and `0x004142E7`, which the findings give as entry addresses
  only.
- FND-EQUIP-002, RULE-EQUIP-002, FND-MOVE-001, RULE-MOVE-001, FND-GANG-003,
  RULE-TERMINATE-001, FND-CONTROL-001, RULE-CONTROL-001 (replacing decompiler
  line positions in the old text): instruction addresses of the transaction
  scan, the gift delivery loop, the Terminate pass, the Move normalization and
  application loops, the Control pool build and winner scan, and the death path
  inside `0x00472775`.
- RULE-EQUIP-002, RULE-TERMINATE-001, RULE-MOVE-001: whether each pass tests
  that the gang is active (sector byte not 100) before acting, and whether a
  gift delivery tests the recipient.
- RULE-CONTROL-001, FMT-STATE-002 `income`: the load instruction and
  displacement of the sector byte the Control pool subtracts (`0x03` or
  `0x04`), and whether sectors with no Control order or under a Crackdown are
  skipped by the winner scan (this decides how often BUG-CONTROL-001 fires).
- RULE-CONTROL-001: the terms of a gang's strength and of the defense (Force
  plus Control, hidden defenders left out), where the type-2 and type-3 reports
  are recorded and in which order, what the capture clears besides site
  progress, and whether `overthrow_count` is raised.
- FND-UPKEEP-001, FMT-STATE-002: read case 6 of `fn_00402D70` and confirm or
  drop the old claim that the computer player reads byte `+3` as Income.
- RULE-MOVE-002, RULE-AI-007, FND-MOVE-001, FND-AI-028: settle what mode 0 of
  the sector selector `0x00408642` does (a bounded draw over 64 sectors, or a
  pick among the eight neighbours), and whether the normalization stores the
  selector's result as the new destination.
- RULE-GIVE-001, RULE-SELL-001, RULE-EQUIP-001, RULE-MOVE-001, FMT-STATE-001:
  in the Give, Sell, Equip and Move panel handlers (`0x00445A4F`,
  `0x00443BBD`, `0x0043DAD9`, `0x004413EF`), the stores into the planning
  record: which byte holds the destination, item, recipient and mask, and which
  bit stands for which slot.
- RULE-SELL-001: the starting value of the payout local and whether each
  branch tests that the slot holds an item.
- RULE-EQUIP-001: how the resolver picks the slot from the item's type, and
  whether it checks Tech Level or research again.
- RULE-EQUIP-003, FND-EQUIP-001: which sector index the Factory test uses (the
  buying gang's sector), and tie the reads at `0x0043F36D` and in
  `0x0044D1BB` to the same price computation.
- RULE-EQUIP-004, FND-EQUIP-006: the exact comparisons of the list filter
  (category, Tech Level, research, already carried), the item type to category
  mapping, and where the gang's Tech Level is read.
- RULE-GANG-002, FND-GANG-003: the address and type of the casualty counter.
- RULE-GANG-001, FND-GANG-001: whether the rebuild in `0x004782C5` runs for a
  gang hired in the same turn, and the order of the fourteen statistic fields.
- RULE-FINANCE-001, SCR-FINANCE-001, FND-FINANCE-001: in `0x0044D1BB`, which
  amount goes to which of the eight rows, how each is computed (New Recruits,
  City Officials, Chaos estimate, Cash Adjustment), the Sector variant's scope
  and how it picks `PX05019`, and the row of the contract count.
- SCR-MOVE-001, SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-GANG-001,
  SCR-GANG-002: positions of the Confirm, OK and Cancel controls, the keys each
  panel handler accepts, and the item picture and portrait resources.
- SCR-GIVE-001, RULE-GIVE-001: the recipient eligibility test in `0x00445A4F`
  (same sector, Tech Level, active).
- SCR-GANG-002, FND-GANG-004: how `PX05000` is opened, its y offset, and
  whether it also shows offers with the unknown Force marker.

## Attack, combat, detection, Chaos and police

- FMT-STATE-003, RULE-COMBAT-002, FND-COMBAT-004, FND-AI-010: record the
  instruction in `0x00472775` that stores byte 0 of each combat record
  (`0x004A11E8 + 10 * i`), and which byte of the gang record copy it loads
  (offset `0x00` or `0x01`). That settles "definition" against "owner".
- All findings in these areas: give the full address range of `0x00472775`,
  `0x0043B290`, `0x0043D132`, `0x00451F80`, `0x0042E040`, `0x0043087E`,
  `0x00430C23`, `0x0046FA11`, `0x00477748`, `0x00475E74`, `0x00475F70`.
- FND-COMBAT-001, FND-CHAOS-001, FND-POLICE-001, FND-POLICE-002,
  FND-COMBAT-004 (replacing decompiler line positions): instruction addresses
  of the attack block and its action test, the police scan, the Chaos pass,
  the sector pass (history expiry, trigger test, report calls, slot fill and
  reset), the payout pass, the combat-record fill and the per-sector row fill,
  the presence table build at the start of the resolver, and the stores into
  `crackdown_history`.
- FND-POLICE-001, RULE-POLICE-002: the address of the duration draw
  (bounded wrapper with argument 3). The old text put it at `0x0047419B`, which
  FND-POLICE-003 gives to the police detection draw.
- FND-POLICE-003, RULE-POLICE-001: the comparison instruction after the call at
  `0x0047419B`: less than or less than or equal.
- FND-CHAOS-001, FMT-STATE-002 `income`: the load instruction and displacement
  for the sector byte in the Chaos pool, and the same for the Control pass.
- RULE-COMBAT-001: how the attack block computes the combat rating (which
  statistics per weapon class, base or effective), and which `type` values of
  `DATA/ITEMS` mean melee, blade and ranged.
- RULE-ATTACK-001: the retaliation pool's operands (whether a band-0
  attacker's Defense is reduced, any minimum damage), whose Stealth and Detect
  the evasion roll uses, the Martial Arts test (`== 0` or `<= 0`), the order of
  the draws instruction by instruction, what an evaded attack writes and
  credits, and whether the target's activity or sector is checked.
- RULE-ATTACK-001, RULE-COMBAT-002: where the phase damage accumulator and the
  fighting-marker array live, and exactly which gangs the marker is set for
  (attacker, target, evaded fights, police).
- RULE-COMBAT-002, format request FMT-STATE-006: the layout of the four-byte
  entry of the result rows at `0x004A8888`, when the police flags at
  `+0x90` are written, whether records are cleared for gangs that did not
  fight, and the order of the damage, record, row and death steps.
- RULE-COMBAT-004: whether the presenter's "gangs whose target is the focal
  gang" test also checks the action byte; whether the displayed-Force reset is
  per focal list or once; what is subtracted for an evaded attack; the caller
  of `0x0042E040` at planning entry and the Detailed Combat option test.
- RULE-CHAOS-001, RULE-CHAOS-002: whether the history expiry runs for every
  sector; whether a sector already under Crackdown pays Chaos; how the payout
  finds a gang's sector and whether a gang that died in combat is paid; whether
  the payout adds to `cash_earned`; the order of the type-1 report calls.
- RULE-POLICE-002: which three values the neutralization clears, which turn
  counter the history stores, and what the type-3 call does when the owner is
  -1.
- RULE-DETECT-001: whether the observer loop covers all six slots or only
  active players, and whether an unseen gang's byte is written 0.
- SCR-ATTACK-001: the picker's screen origin, which player each opponent cell
  shows, which listed target each cell holds, the Confirm and Cancel controls,
  the acting gang portrait, the target marker, and keyboard input in
  `0x0043B290`.
- SCR-COMBAT-001: the renderer `0x00453A8D` positions of the page text, the
  sector tile (docs give local `(31,67,54,52)`) and code, the police art, and
  the keys the handler pages with; what selecting a slot with the force
  selector changes.
- SCR-COMBAT-002: the x positions of the Force tracks, the portrait resource,
  the Cancel cell and any key handling in `0x0042E040`/`0x00430C23`.
- FMT-STATE-001: which of `target` and `target_2` holds the Attack target's
  player and which its roster slot.

## Last Turn Events, Comlink and Search

- FMT-STATE-006 (requested), RULE-EVENT-002: in the recorder `fn_00477748`,
  read the offsets it writes inside the 10-byte record (occupied, type, three
  arguments) and what the tenth byte is. Give the width of the count at
  `0x004ABCA8`.
- RULE-EVENT-002 to RULE-EVENT-011: list the 12 call sites of `fn_00477748`
  inside `fn_00472775` by instruction address, with the type and the three
  arguments each passes; say which argument holds 1, 2 or 4 for a type-6 cash
  failure. Include the call sites for types 2 and 3 (sector control) and for
  the Equip cash failure, so those rules can emit report events.
- RULE-EVENT-004: find how the resolver knows which players had a gang in the
  sector at the start of resolution (the copy RULE-CHAOS-001 calls
  `sector_presence`), and the order of the Crackdown reports.
- RULE-EVENT-005, SCR-EVENT-001: in `fn_0044F2FC` and `fn_0044FD6C`, read what
  happens with no occupied record, the mapping of type and cash-failure
  argument to string IDs 33 to 44, the positions of the status line, the
  242-by-158 illustration area, the counter and the footer fields, and the
  source of the pressed arrow faces. Confirm the panel origin (104, 124) for
  this handler.
- SCR-EVENT-001: which frame of the `PX04xxx` strip the Research report copies,
  and whether it animates.
- RULE-COMLINK-001: in `fn_0045D2F0`, the order of the store, the pending flag,
  the sound and the repeat reset; whether a message for a remote recipient is
  also stored locally; what a network-received message fills in.
- RULE-COMLINK-002, FND-COMLINK-003: the address of the enabled array and the
  exact eligibility test in `fn_0045EAB1` (which `controller` values count as
  human); whether opening Send clears the selection, the draft and the cursor.
- RULE-COMLINK-003: the order in which recipients are visited; how the draft's
  `occupied`, `read`, `turn` and `sender` are set; whether Send closes the
  panel and clears the draft. Give the addresses of the global message buffer
  (`comlink_draft`), the cursor row and column, and the eligibility and
  selection bytes.
- RULE-COMLINK-005: whether the unread rescan in `fn_0045E04D` tests
  `occupied`; the page header's format.
- RULE-COMLINK-006: Backspace's effect (value stored, cursor movement), cursor
  movement after a typed character and after column 39, Enter on the last row.
- SCR-COMLINK-001, SCR-COMLINK-002: positions of the header, date, name,
  portrait and message rows; which player slot each recipient cell holds; the
  exact destination of the 50-by-23 pressed faces; where each character's cell
  lies in `PX00129`; when resource 5023 (no file in the build) would be loaded.
- RULE-SEARCH-001: whether the save writer includes `0x004A24E8`; the exact
  flip operation.
- RULE-SEARCH-002, FND-SEARCH-003: the "controlled by the active player" test
  at `0x00412990` in `fn_004123CC`, and the sector visiting order.
- SCR-SEARCH-001: how a row is drawn, whether ALL and NONE play the accepted
  sound, what triggers the handler's rejected-input branch, and what the first
  click of a double-click does.
- All findings FND-EVENT-001..003, FND-COMLINK-001..005 and
  FND-SEARCH-001..003 locate functions by entry address only. Give each
  function's full range: `fn_00477748`, `fn_004726C0`, `fn_00472775`,
  `fn_0044F2FC`, `fn_0044FD6C`, `fn_00451602`, `fn_0045D2F0`, `fn_0046BA84`,
  `fn_0045EAB1`, `fn_0045FDF1`, `fn_0045D61A`, `fn_004718EE`, `fn_00418821`,
  `fn_004600D2`, `fn_0046023C`, `fn_004327C0`, `fn_00432926`, `fn_004328BE`,
  `fn_004328F8`, `fn_00460CCF`, `fn_004327DC`, `fn_0045E04D`, `fn_00448E32`,
  `fn_0044C476`, `fn_0046E766`, `fn_004123CC`, `fn_00412AC4`.

## Setup, city generation, objectives and awards

- SCR-SETUP-001, RULE-SETUP-002, RULE-OBJECTIVE-004: find the local setup
  handler's rectangles for the scenario buttons, time limit, AI Mentality and
  turn time controls, and the globals they write (the time limit gives
  `turn_limit`; its address is unknown). Also the positions of the top
  portrait strip, the names and the selection lights.
- RULE-OBJECTIVE-002, RULE-OBJECTIVE-004, glossary `scenario_*`: settle the
  scenario numbering. FND-SETUP-009 and FND-SETUP-012 make 0 Kill 'Em All; the
  glossary makes 6 Siege and 7 Eliminate; FND-AI-005 reads 0 as cash (Greed)
  and 6 as the headquarters count (Eliminate). Read the switch in `0x0047712A`
  against the setup button order to give each scenario its number.
- RULE-OBJECTIVE-004: write out the Dominance numerator in `0x0047712A` and
  the end tests of each scenario in `0x00476857` (thresholds 40 and 64, Siege
  test, timed end test), which the rule takes from the manual.
- RULE-SETUP-001, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007: record the
  addresses of the five `modifier_name_*` strings and of the name-compare
  helper; confirm whether the scan is one pass or several.
- RULE-SETUP-003, RULE-SETUP-009, RULE-SETUP-010: find the `portrait` table's
  address and the stepping rules of `fn_00468D87` and `fn_00468CFC` (skipping
  used portraits, wrapping at 0 and 14).
- RULE-SETUP-004: find the address of `reaction`, and whether computer and
  human players both get a draw (coordinate with the AI task, FND-AI-006).
- RULE-SETUP-008: find where `new_local_game` lives and is cleared, and what
  `fn_0045519D` and `fn_00451F80` show.
- RULE-OBJECTIVE-001, RULE-OBJECTIVE-005: addresses of `match_over`,
  `player_retired` and `player_active`, and where the elimination walk sits in
  the turn relative to resolution and the next planning scan.
- RULE-AWARDS-001, SCR-AWARDS-001: address and layout of `player_awards`, the
  icon cells in `PX00201`, the Awards and Stats rectangles in `0x0042CE61`,
  which statistic each of the five fields holds, and which tab shows first.
- SCR-AWARDS-002, RULE-AWARDS-002: where `PX00202` is drawn, how it is
  dismissed, whether the shared results follow it, and whether the human count
  includes controller 3.
- SCR-OBJECTIVE-001: the vertical position formula of the rankings portraits
  in `0x004518D9`, the portrait source, and how the panel closes.
- SCR-SETUP-002: position of `PX00132`, the portrait drawn on it and the Ready
  rectangle in `0x004396C0` and `0x00439F7A`.
- SCR-NET-001, SCR-NET-002, SCR-NET-003: settle the order of the four numbers
  in the held-button rectangles (read the helper's comparisons), and name each
  action.
- SCR-NET-004, SCR-NET-005: the progress-to-width mapping in `0x0040D3C0` and
  `0x0046D77B`, the spinner cell arithmetic and timer period, and the
  `PX00139` destination.
- All screens: where the 640-by-460 drawing surface sits on the 640x480
  window (menu bar offset).

## Computer players

- Scenario numbering (RULE-AI-002, RULE-AI-010, RULE-AI-011, RULE-AI-013,
  RULE-AI-019 to RULE-AI-031, BUG-AI-001; FND-AI-002, FND-AI-005): FND-AI-002
  and FND-AI-005 read scenario 0 as Greed, 6 as Eliminate and 7 as Siege;
  FND-UI-033 and FND-TURN-003 read 6 as Siege and 7 as Eliminate; FND-SETUP-009
  and FND-SETUP-012 read 0 as Kill 'Em All. The AI rules use numeric
  literals and were not renumbered. Settle the numbering from the scenario
  global `0x004ABBE8`'s writers in the setup panel and from the name resource
  the setup presenter loads for each value.
- Selector `0x5B` (RULE-AI-019, RULE-AI-023, RULE-AI-028; FND-AI-019,
  FND-AI-030, FND-AI-031, FND-AI-037): read the call sites in `0x00428EF0` and
  `0x00401000` and check whether the selector takes the counted action as an
  argument, or always counts previous Chaos at `0x004048B8`.
- Full function ranges for every finding that gives only an entry address:
  the family handlers (`0x00428EF0`, `0x00434080`, `0x0041FEF0`, `0x00435BD0`,
  `0x00401000`, `0x0043A1D0`, `0x00431C60`, `0x00436C70`, `0x004605E0`,
  `0x0042A6E0`, `0x00420950`, `0x004353A0`, `0x0040ABC0`, `0x00466910`), the
  dispatcher, `0x00458FA0`, `0x0040A1A7`, `0x00402D70`, `0x00408642`,
  `0x0046DC10`, `0x00472775` (FND-AI-001 to FND-AI-040).
- Family-9 seeding in the outer pass (RULE-AI-001, RULE-AI-027; FND-AI-003):
  the condition under which `0x00458FA0` writes family 9 before the dispatch.
- Writer of planning record byte +1 (`unk_01`) and meaning of +11
  (RULE-AI-002, RULE-AI-001; FND-AI-002, FND-AI-003).
- Dispatcher tail (RULE-AI-002; FND-AI-001): the scenario 6, 7, 8 comparisons
  after the handler and the mode 9 Move for roster slot 0.
- Reuse of a roster slot's planning record (RULE-AI-001): where a hire resets
  the record so the new gang does not inherit family and history.
- Hire-role schedule adjustments for every scenario, and what a family-6 guard
  does when it fires (RULE-AI-010, BUG-AI-001; FND-AI-009, FND-AI-014).
- Selector `0x62` (`local_tech_cap`, RULE-AI-005, RULE-AI-026; FND-AI-024): how
  the Tech ceiling is computed.
- Selectors `0x64`, `0x74` and `0x75` (RULE-AI-005): which candidate wins when
  several qualify; the weapon class `type` numbers of the item table.
- Selector `0x2D` table and mode 4 of the sector selector (RULE-AI-006), and
  mode 0 (RULE-AI-007; conflict with FND-MOVE-001).
- Family 1 (RULE-AI-020; FND-AI-020): the branches for previous Attack, Bribe,
  Give, Hide, Influence, Move, Research, Sell and Terminate; the Snitch branch
  gated on cash above 50; the Mentality 2 branch; the selector `0x35` test for
  a neutral owner.
- Family 0 and family 4 target pools (RULE-AI-019, RULE-AI-023): whether the
  single and five-draw loops choose the human pool by the owner's hostility, as
  family 3 does.
- Family 2 (RULE-AI-021; FND-AI-032): whether the attack step tests selector
  `0xAB` or the pool size; whether the late Control gates replace an Equip or
  Heal.
- Family 6 (RULE-AI-025; FND-AI-029): the thresholds of the unreachable Heal and
  Control branches; whether the equipment step is gated by selector `0x6C`;
  whether selector `0x5F` counts the planning gang itself.
- Family 7 (RULE-AI-026; FND-AI-035): whether the fixed item list is Tech
  capped; whose owner the Attack hostility test reads.
- Family 11 (RULE-AI-029; FND-AI-024): the armor, miscellaneous and Heal tests.
- Families 13 and 14 (RULE-AI-031; FND-AI-039): instruction addresses of the
  owned-objective Heal branch; the contested pool (owner's gangs or every
  visible opponent); whose Force the Force-5 test reads.
- Attitude updates (RULE-AI-015, RULE-AI-016, RULE-AI-017; FND-AI-006):
  instruction addresses in `0x00472775` of the recovery loop, the combat
  decrement and the takeover decrement; whose reaction the combat decrement
  uses; which ownership changes call the takeover decrement; the address of
  the reaction values.
- Difficulty band reads (RULE-AI-018; FND-AI-007): instruction addresses of the
  nine reads in `0x00472775`; which controller values count as computer.
- Addresses of `sector_weight` and of the player-pair records holding
  `combat_advantage` (RULE-AI-003; FND-AI-018, FND-AI-039), and the order of
  the three parts of `0x0040A1A7`.
- `solo_control_ok` (RULE-AI-004; FND-AI-004): what makes a sector disabled
  (owner -2); whether the unavailable test is the Crackdown byte; the offsets of
  the Income and Support bytes selector `0x2C` reads.
- The auxiliary record block's base and stride (FND-AI-015) and whether the
  planning and auxiliary records are saved.
- Placement anchor (RULE-AI-013; FND-AI-010): the element type at
  `0x0048E2F8`; the order of the keep tests; what the byte at `0x004A08C4`
  holds; what the hire resolver does with sector -1; where `seed_anchor` runs.
- FMT-STATE-003 byte 0 (see notes.md): find the instruction that writes byte 0
  of a combat record, to settle definition against owner.

## Screens, options, planning timer and sound

Gaps more reading of `Chaos Overlords.exe` or the data files could close.
### Addresses and ranges
- Give full function ranges for every finding location written as a single
  entry address in FND-UI-*, FND-AUDIO-001..003, FND-AUDIO-010..013,
  FND-OPTIONS-*, FND-TIMER-001 (the extents were not in the old notes).
- Find the addresses of `music_enabled`, `effect_slots`,
  `sound_output_available` (and its writer), `pointer_shape`,
  `presentation_tick_pending` (timer slot 0), `comlink_blink_step`,
  `planning_limit_ms`, `planning_start_ms` and `blit_benchmark_count`, and add
  them as locations and glossary addresses. Rules: RULE-AUDIO-003, 004, 005,
  008; RULE-UI-003, 007, 008; RULE-TIMER-001, 002, 003.
- Record the widths of `local_game` (`0x00482178`), `network_game`
  (`0x00487B58`) and `comlink_alert_repeat` (`0x00487808`), and read the
  writers of the two game-type flags to confirm their meaning (RULE-AUDIO-006).
### Behaviour
- RULE-OPTIONS-001: what the loader does when `RegOpenKeyExA` fails; the
  shared buffer's value before the first query; how the two serial draws are
  combined and whether the result is kept in `serial_number`; the effect of a
  value of another type or size.
- RULE-OPTIONS-002: which player actions reach the writer's three callers
  (`0x00460EF9`, `0x00460F40`, `0x0046224A`).
- RULE-OPTIONS-003: confirm the idle scan covers only the active player's 81
  slots.
- RULE-TIMER-001/002/003: whether the limit mapping is a table or compares;
  where the -1 "no limit" case is tested (a signed compare would expire at
  once); the types in the expiry compare; what happens to an open modal panel
  on expiry; the countdown's value at planning start; whether the bar is drawn
  with no limit; whether a loaded game recomputes the limit from the saved
  choice byte.
- RULE-AUDIO-001..003: the effect of a nonzero music level after 0 on
  `music_enabled`; the order of music and effects application; which auxiliary
  device the volume call addresses; which callers run the level helper.
- RULE-AUDIO-004/005: behaviour on a missing sound file; the lower helper's
  slot bounds check; what the priority and channel record do.
- RULE-AUDIO-009: what the evasion branch leaves in slot 5; whether "base"
  Martial Arts is the definition's or the effective value.
- RULE-UI-003: the last partial slide step; the divisor when
  `blit_benchmark_count` is below 4; whether the slide-out redraws what is
  underneath.
- RULE-UI-004: cell walk direction and overflow; the red digits' source x.
- RULE-UI-006: whether incoming hires come from `hire_orders`.
- RULE-UI-007: which callers pass `force`.
- RULE-UI-008: whether timer slot 0 is a flag or a counter; what else is paced
  by it (Item Information rotation, the Comlink Send cursor).
- RULE-UI-009: the string resource IDs of the scenario names; whether the
  eliminated test reads `player_active`; the label for `controller` 3.
- RULE-UI-010: whose roster Gangs in Sector scans; more than six matches.
- RULE-UI-011: whether Income and Tolerance are hidden from non-owners; which
  number helper draws the values.
- SCR-UI-005: the close face rectangle and the keys the panel takes; which
  gang field each statistic row reads and which rows Base Statistics switches.
- SCR-UI-006: the screen row of Cost and Tech Level (backing y 236); the timer
  behind the item rotation; how Equip and Research open the panel.
- SCR-UI-007, SCR-UI-008: the keys each panel takes; whether Site Information
  shows remaining or base Resistance.
- SCR-UI-008: the code that opens Game Information at the start of a
  multi-player game and after loading (RULE-SETUP-008 calls `fn_0045519D` for a
  new local game).
- SCR-OPTIONS-001: the sounds Cancel and OK play; a click outside both faces.
- FND-UI-031: whether the scaled copy path ignores the requested copy mode
  (possible GFX bug).

## Found while integrating the spec

- FMT-STATE-002 `factory`, RULE-SITE-001, RULE-EQUIP-003: find the instruction that writes sector
  byte `0x0E`. No finding shows a writer, so RULE-SITE-001 cannot yet say which completed site
  sets the Factory flag.
- RULE-SEARCH-001: whether the original's save writes the Search filter table. If it does not,
  the rebuild matches it and nothing is needed; if it does, the rebuild's clearing of the filters
  on a load becomes a deviation.
- RULE-HIDE-001, FND-AWARDS-002: the award count reads every resolved Hide. Cite FND-AWARDS-002 in
  the HIDE rule once the Hide case of `fn_00472775` is read again, and check that the counter is
  incremented in the same place.
- RULE-AI-016, RULE-AI-017, RULE-ATTACK-001, RULE-CONTROL-001, FND-AI-006: record the instructions
  in `fn_00472775` that lower the attitude after an attack and after an owner change. The rules
  place the calls after the opening damage and at the owner write by interpretation; whether an
  evaded attack or a retaliation lowers the attitude too is not known.
