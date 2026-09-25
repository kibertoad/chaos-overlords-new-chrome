---
id: FND-STATE-007
title: Map of the match and computer-player state in .data, with each region's element, writers, readers and identity
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482108..0x00482177
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00489950..0x0048FB6F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494818..0x00494831
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004988F8..0x00498DA5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498DA8..0x004ABDBF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405404..0x00405525
tool: Ghidra 12.1.3
environment: null
---

## Observation

Every data reference that the 464 game functions make (FND-EXE-004) was listed
and grouped by address. This finding covers the regions that hold the state of
a match and of the computer players; FND-STATE-008 covers the interface,
platform and network globals, and FND-STATE-009 the initialized data and the
constants. Each row gives the region, its element, the functions that write
it, the functions that only read it, and what it is, with the finding that
identifies it. The load and save functions `fn_0046381A` and `fn_00463CC5`,
which push the address of every save block, are left out of the two function
columns; "save block n" is the block number of FND-SAVE-001. A function that
both reads and writes a region is listed as a writer. Where more than three
functions appear only the first three by address are named.

| Region | Element | Writers | Only readers | What it is |
|---|---|---|---|---|
| `0x00482108..0x0048210D` | UINT8[6] | `fn_0040AA65`, `fn_0040AB20`, `fn_00458FA0` | none | `ai_started`, save block 15 (FND-AI-003) |
| `0x00482110..0x00482127` | INT32[6] | `fn_0040AA65`, `fn_00458FA0` | none | save block 16: the last ``hire_limit`` of each computer player, capped at 80 (FND-STATE-003, FND-AI-012) |
| `0x00482128..0x0048213F` | INT32[6] | `fn_00432DA0`, `fn_00458FA0` | `fn_00402D70` | the hire role the planner chose, save block 20 (FND-AI-002) |
| `0x00482140..0x00482157` | INT32[6] | `fn_00458FA0` | none | save block 21: a flag stored with each hire-role choice and never read (FND-STATE-003) |
| `0x00482158..0x0048215D` | UINT8[6] | `fn_0040AA65`, `fn_0040AB20`, `fn_00458FA0` | none | save block 18: the takeover flag (FND-STATE-003) |
| `0x00482160..0x00482177` | INT32[6] | `fn_00458FA0` | `fn_00402D70` | the previous hire role (FND-AI-014) |
| `0x00489950..0x00489F4F` | INT32[6 * 64], element player * 64 + sector | `fn_0040A1A7` | `fn_00402D70`, `fn_00408642` | `sector_gang_count` (FND-AI-040); `fn_00458FA0` reads it at `0x00489850` + anchor*4, with the anchor stored as sector + 64 |
| `0x00489F50..0x0048A14F` | 64 records of two INT32 | `fn_00408553`, `fn_00408642` | none | scratch list of the sector selector: sector and score pairs that `fn_00408553` fills and `fn_00408642` draws from |
| `0x0048A150..0x0048A20F` | INT32[6 * 8], element player * 8 + k | `fn_00408642` | none | eight counters per player kept by the sector selector `fn_00408642` |
| `0x0048A250..0x0048C0AF` | FMT-STATE-007, player * 0x510 + slot * 0x10 | `fn_00401000`, `fn_00409DE1`, `fn_0040AB20`, 15 more | `fn_00402D70`, `fn_00408642` | `ai_plans`, save block 14 (FND-AI-001, FND-STATE-006) |
| `0x0048C0B0..0x0048DB43` | 14-byte records, player * 1134 + slot * 14 | `fn_00401000`, `fn_0040A1A7`, `fn_0040ABC0`, 13 more | `fn_00402D70` | `aux_records`: `fn_0040A1A7` writes the words at +0 to +8 from selector values; +0x0A and +0x0C are the two values the glossary names |
| `0x0048DB48..0x0048E2DF` | INT32[486], element player * 81 + slot | `fn_00458FA0` | `fn_00402D70` | save block 17: a per-gang divisor read only by selector 0x5D; the only store writes 0 (FND-STATE-003) |
| `0x0048E2E0..0x0048E2F7` | INT32[6] | `fn_0040A1A7` | `fn_00458FA0` | the number of living gangs each computer player has, counted by `fn_0040A1A7` at the start of its planning pass |
| `0x0048E2F8..0x0048E30F` | INT32[6] | `fn_0040AA65`, `fn_00458FA0` | `fn_00402D70` | `ai_hire_anchors`, save block 19 (FND-AI-010) |
| `0x0048E310..0x0048F80F` | 14-byte records, player * 0x380 + sector * 14 | `fn_00409F47`, `fn_0040A1A7` | `fn_00402D70` | per computer player and sector: the owner (+0), selectors 0x90 and 0x5E (+2, +4), and four words `fn_00409F47` keeps (+6 to +0x0C) |
| `0x0048F810..0x0048FB6F` | 24-byte records, player * 0x90 + other * 0x18 | `fn_0040A1A7` | `fn_0041FEF0` | per computer player and opposing player: sector counts (+0, +2), four sums of gang bytes 18 and 19 (+4 to +0x10) and a flag (+0x14), rebuilt by `fn_0040A1A7` |
| `0x00494818..0x0049482F` | INT32[6] | `fn_00439563` | `fn_004123CC`, `fn_00476726`, `fn_00476857`, 1 more | the six headquarters sectors 9, 12, 30, 33, 51 and 54 that `fn_00439563` writes (FND-CITY-003) |
| `0x00494830..0x00494831` | INT16 | `fn_004384C0`, `fn_0046A115` | `fn_00439563`, `fn_0046981D` | `current_player`, save block 27 (FND-SAVE-001) |
| `0x00498990..0x00498B75` | UINT8[486], element player * 81 + slot | `fn_00472775`, `fn_00476F3B` | `fn_0046A7CB` | set by the resolver `fn_00472775` and `fn_00476F3B` for each gang record the turn changed; read by the network result sender `fn_0046A7CB` |
| `0x00498B78..0x00498BB7` | UINT8[64] | `fn_00472775`, `fn_00476F3B` | `fn_0046A7CB` | set by the resolver for each sector record the turn changed; read by `fn_0046A7CB` |
| `0x00498BC0..0x00498DA5` | UINT8[486] | `fn_00472775` | `fn_0046A7CB` | set by the resolver for each gang that fought this turn; read by `fn_0046A7CB` |
| `0x004988F8..0x00498937` | UINT8[64] | `fn_00472775` | `fn_0046A7CB` | set by the resolver for each sector with a fight this turn; read by `fn_0046A7CB` |
| `0x00498DA8..0x0049CA67` | FMT-STATE-001, player * 0xA20 + slot * 0x20 | `fn_00401000`, `fn_0040ABC0`, `fn_0041462F`, 28 more | `fn_00402D70`, `fn_00408214`, `fn_00408642`, 19 more | gangs, save block 1; `fn_0041462F` writes roster slot 80 of a player, which starts at `0x004997A8` for player 0 |
| `0x0049CA68..0x0049CA6B` | INT32 | `fn_0046A115`, `fn_0046E766` | `fn_00402D70`, `fn_0044FD6C`, `fn_0045EAB1`, 4 more | `elapsed_turns`, save block 6 (FND-AI-009) |
| `0x0049CA70..0x0049CA75` | UINT8[6] | `fn_0046DC10`, `fn_0046E766` | none | the cash name-modifier flag: a player whose flag is set starts with $1,500 (FND-SETUP-015) |
| `0x0049CA78..0x0049CA8F` | INT32[6] | `fn_0046BA84`, `fn_0046E766`, `fn_00472775`, 1 more | `fn_0042B9E0`, `fn_0042CE61`, `fn_0046A7CB` | `cash_spent`, save block 32 |
| `0x0049CA90..0x004A08CF` | FMT-STATE-005, player * 0xA60 + message * 0xA6 | `fn_0045E04D`, `fn_00460391`, `fn_0046E766` | `fn_0045D2F0`, `fn_0045D61A`, `fn_0046FD80`, 1 more | `comlink_messages` |
| `0x004A08D0..0x004A08E7` | INT32[6] | `fn_00456F80`, `fn_00462579`, `fn_004677F0`, 1 more | `fn_00458155`, `fn_0045D2F0`, `fn_0046981D`, 3 more | the network connection that plays each seat, -1 for a seat played at this computer |
| `0x004A08E8..0x004A11E7` | FMT-STATE-002, sector * 0x24 | `fn_004123CC`, `fn_00439563`, `fn_0046DC10`, 4 more | `fn_00402D70`, `fn_00408642`, `fn_0040A1A7`, 28 more | sectors, save block 2; the fixed addresses `0x004A0CB4`, `0x004A0CD8`, `0x004A0DD4` and `0x004A0DF8` are the owner bytes of the centre sectors 27, 28, 35 and 36 |
| `0x004A11E8..0x004A24E3` | FMT-STATE-003, (player * 81 + slot) * 10 | `fn_0042E040`, `fn_00472775` | `fn_00408642`, `fn_0043087E`, `fn_00453A8D`, 4 more | `combat_records`, save block 26; the network code sends it in 810-byte slices per player (`0x004A1512` and on) |
| `0x004A24E8..0x004A256B` | UINT8[6 * 22], element player * 22 + site | `fn_00448E32`, `fn_0046E766` | `fn_004123CC`, `fn_00449925` | the Search panel's site selection (FND-SEARCH-001) |
| `0x004A2570..0x004A2587` | INT32[6] | `fn_0046DC10` | `fn_00472775` | `difficulty_band`, save block 38 (FND-AI-007) |
| `0x004A2588..0x004A25CF` | 12-byte records | `fn_0040F63D` | `fn_0040B9C0`, `fn_0040C4C5`, `fn_0040E0A0`, 19 more | `player_names`, save block 28 (FND-STATE-004) |
| `0x004A25D0..0x004A25E7` | INT32[6] | `fn_0046BA84`, `fn_00472775`, `fn_00475FE1` | `fn_0042B9E0`, `fn_0046A7CB` | `hide_count`, save block 31 |
| `0x004A25E8..0x004A25FF` | INT32[6] | `fn_0046A115`, `fn_0046BA84`, `fn_0046E766`, 1 more | `fn_00402D70`, `fn_004078D9`, `fn_00458FA0`, 4 more | cash, save block 9 |
| `0x004A2600..0x004A2605` | UINT8[6] | `fn_0046DC10` | none | save block 37: set to 1 for every player when the AI Mentality is 3 and to 0 otherwise; nothing reads it (FND-STATE-003) |
| `0x004A2608..0x004A2787` | INT8[384], element item * 6 + player | `fn_0046DC10`, `fn_00472775` | `fn_00402D70`, `fn_0042A6E0`, `fn_00436C70`, 7 more | `research_remaining`, save block 12 (FND-STATE-004); the addresses `0x004A26FE` to `0x004A2740` that reference searches report are the elements of items 41, 42, 43, 44, 46, 49, 50 and 52, which selector 0x73 reads (see below) |
| `0x004A2788..0x004A278D` | UINT8[6] | `fn_0046DC10`, `fn_0046E766` | none | the elite name-modifier flag: five extra gangs of definition 59 with three items each (FND-SETUP-004, FND-SETUP-015) |
| `0x004A2790..0x004A27A7` | INT32[6] | `fn_0046A115`, `fn_0046BA84`, `fn_0046E766`, 2 more | `fn_00402D70`, `fn_0042CE61`, `fn_004518D9`, 4 more | `scenario_score`, save block 22 (FND-AI-005) |
| `0x004A27A8..0x004A27BF` | INT32[6] | `fn_0046BA84`, `fn_00472775`, `fn_00475FE1` | `fn_0042B9E0`, `fn_0042CE61`, `fn_0046A7CB` | `overthrow_count`, save block 29; the Control pass increments it at `0x00475753` |
| `0x004A27C0..0x004A27C5` | UINT8[6] | `fn_00456F80`, `fn_00462579`, `fn_004677F0`, 2 more | `fn_0040C4C5`, `fn_00457AF3`, `fn_00457B7B`, 2 more | network lobby: whether each seat is open |
| `0x004A27C8..0x004A27D9` | INT8[18] | `fn_004078B8`, `fn_00408214`, `fn_00416C75`, 3 more | `fn_00412BF7`, `fn_00417CBA`, `fn_0044D1BB`, 3 more | `hire_orders`, save block 11 |
| `0x004A27E0..0x004A27F7` | INT32[6] | `fn_0046BA84`, `fn_0046E766`, `fn_00472775`, 1 more | `fn_0042CE61`, `fn_0046A7CB` | `cash_earned`, save block 34 |
| `0x004A2800..0x004A5ED7` | FMT-DATA-002[90], def * 0x9C | `fn_0044FD6C` | `fn_00402D70`, `fn_004078D9`, `fn_00410130`, 26 more | `gang_definitions`, read whole from DATA/Gangs (FND-DATA-007) |
| `0x004A5ED8..0x004A5EEF` | INT32[6] | `fn_0046BA84`, `fn_00472775`, `fn_00475FE1` | `fn_0042B9E0`, `fn_0042CE61`, `fn_0046A7CB` | `damage_inflicted`, save block 30 |
| `0x004A5EF0..0x004A5EF5` | UINT8[6] | `fn_0046DC10`, `fn_0046E766` | `fn_00472775` | `modifier_force_hire`, save block 44 (FND-HIRE-005) |
| `0x004A5EF8..0x004A5EFB` | INT32 | `fn_004384C0`, `fn_00438DA5`, `fn_00439563`, 1 more | `fn_00402D70`, `fn_004384F4`, `fn_0045519D`, 4 more | `turn_limit`, save block 8 |
| `0x004A5F00..0x004A5F05` | UINT8[6] | `fn_0040B9C0`, `fn_0040E0A0`, `fn_0040F72E`, 4 more | `fn_0040C4C5`, `fn_0040EE8A`, `fn_00413858`, 22 more | portraits, save block 4 |
| `0x004A5F08..0x004A8887` | FMT-DATA-003[64], item * 0xA6 | none | `fn_00401000`, `fn_00402D70`, `fn_0040ABC0`, 34 more | `item_definitions`, read whole from DATA/ITEMS (FND-DATA-007) |
| `0x004A8888..0x004AAE07` | FMT-STATE-008, sector * 0x96 | `fn_0046A115`, `fn_0046BA84`, `fn_0046DC10`, 1 more | `fn_0042E040`, `fn_0043087E`, `fn_00451F80`, 4 more | `combat_results`, save block 25 (FND-COMBAT-004) |
| `0x004AAE08..0x004AB587` | FMT-STATE-006, player * 0x140 + n * 10 | `fn_0046A115`, `fn_0046BA84`, `fn_0046E766`, 2 more | `fn_0044F2FC`, `fn_0044FD6C`, `fn_0046981D`, 3 more | reports, save block 24 |
| `0x004AB588..0x004AB58D` | UINT8[6] | `fn_0046DC10`, `fn_0046E766` | `fn_0046FA11` | `modifier_visibility`, save block 43 (FND-SETUP-011) |
| `0x004AB590..0x004AB61F` | INT32[36] | `fn_0040A1A7`, `fn_0046DC10`, `fn_00472775` | `fn_00401000`, `fn_00402D70`, `fn_00408642`, 12 more | attitude, save block 35 (FND-AI-006) |
| `0x004AB620..0x004AB637` | INT32[6] | `fn_0040B9C0`, `fn_0046BA84`, `fn_00472775`, 1 more | `fn_0042CE61`, `fn_0046A7CB` | casualties, save block 33 |
| `0x004AB638..0x004AB64F` | INT32[6] | `fn_0040B9C0`, `fn_0040E0A0`, `fn_0040F72E`, 4 more | `fn_00402D70`, `fn_0040A1A7`, `fn_0040EE8A`, 9 more | controller, save block 5 |
| `0x004AB650..0x004AB667` | INT32[6] | `fn_0046DC10` | `fn_00472775` | reaction, save block 36 (FND-STATE-003) |
| `0x004AB668..0x004ABBBB` | FMT-DATA-001[22], site * 0x3E | none | `fn_00402D70`, `fn_00410770`, `fn_004123CC`, 12 more | `site_definitions`, read whole from DATA/SITES (FND-DATA-007) |
| `0x004ABBC0..0x004ABBD1` | INT8[18] | `fn_0046BA84`, `fn_0046E766`, `fn_004716EB`, 1 more | `fn_00402D70`, `fn_004078D9`, `fn_00416C75`, 7 more | `hire_offers`, save block 10 |
| `0x004ABBD4` | UINT8 | `fn_0046E766`, `fn_00476857` | `fn_0046981D`, `fn_0046A115`, `fn_0046A7CB`, 1 more | `match_over`: cleared by `fn_0046E766`, set by the end evaluator `fn_00476857`, sent over the network |
| `0x004ABBD8..0x004ABBDD` | UINT8[6] | `fn_0046DC10`, `fn_0046E766` | none | the right-hands name-modifier flag: five extra unequipped gangs of definition 0 (FND-SETUP-004, FND-SETUP-015) |
| `0x004ABBE0..0x004ABBE5` | UINT8[6] | `fn_0046E766`, `fn_00476F3B` | `fn_004123CC`, `fn_00413858`, `fn_0041B4EA`, 12 more | `player_active`, save block 13 (FND-STATE-004) |
| `0x004ABBE8..0x004ABBEB` | INT32 | `fn_004384C0`, `fn_00438DA5`, `fn_0046A115` | `fn_00402D70`, `fn_0040ABC0`, `fn_004123CC`, 18 more | scenario, save block 7 |
| `0x004ABBF0..0x004ABC07` | INT32[6] | `fn_0046A115`, `fn_0046DC10`, `fn_0046FD80`, 1 more | `fn_0046981D` | each player's last selected sector, save block 3: the Right Hands' sector at a new match (FND-SETUP-015), then rewritten by the planning code `fn_0046FD80` and `fn_00471F06` |
| `0x004ABC08..0x004ABC0D` | UINT8[6] | `fn_0047712A` | `fn_00402D70`, `fn_00408642`, `fn_0042CE61`, 6 more | standings (FND-AI-005, FND-STATE-004) |
| `0x004ABC10..0x004ABC15` | UINT8[6] | `fn_0046DC10`, `fn_0046E766` | none | the islands name-modifier flag: every unowned sector gets Crackdown 100 (FND-SETUP-003, FND-SETUP-015) |
| `0x004ABC18..0x004ABC3B` | 6-byte records, player * 6 | `fn_00460CCF` | `fn_0040C4C5`, `fn_0040EE8A`, `fn_00410770`, 8 more | each player's colour: a 32-bit colour value and a 16-bit value, written by `fn_00460CCF` and passed to the drawing helpers |
| `0x004ABC58..0x004ABC5D` | UINT8[6] | `fn_0040E0A0`, `fn_00456F80`, `fn_00462579`, 1 more | `fn_0041B4EA`, `fn_0045519D`, `fn_0045EAB1`, 5 more | `players_human`, save block 39 |
| `0x004ABCA8..0x004ABCBF` | INT32[6] | `fn_00472775`, `fn_00477748` | none | `last_turn_report_count` |
| `0x004ABCC0..0x004ABDBF` | INT16[128] | `fn_00472775`, `fn_00475FE1` | none | `crackdown_history`, save block 23 (FND-POLICE-001) |

The three definition tables (`0x004A2800`, `0x004A5F08`, `0x004AB668`) are the
only regions no instruction writes, apart from the name truncation of the
report panel `fn_0044FD6C`, which writes a NUL into a gang definition's name
and restores the byte after drawing (`0x00451156..0x0045121A`).

Fixed item numbers. Reference searches report reads of `0x004A26FE`,
`0x004A2704`, `0x004A270A`, `0x004A2710`, `0x004A271C`, `0x004A272E`,
`0x004A2734` and `0x004A2740`. They are all one instruction, `0x004054FB` in
selector `0x73` of `fn_00402D70` (`0x00405404..0x00405525`), whose index is one
of the eight constant item numbers 44, 41, 42, 43, 46, 50, 49 and 52; each
address is `0x004A2608 + item * 6`, the item's `research_remaining` entry of
player 0. The selector walks the eight items in that order and returns the
first whose `tech_level` (`0x004A5F88 + item * 0xA6`) is at most selector
`0x62`'s value and whose `research_remaining` for the given player is above 0,
or -1 when none is. The same list is the fixed miscellaneous list of FND-AI-035.
`fn_0042A6E0` reads `0x004A2710 + player` at `0x0042A85A`, item 44's entry
(FND-AI-037).

Addresses inside a region that reference searches report on their own
(`0x00489850`, `0x004899BC`, `0x00489F4C`, `0x004A0CB4` and the like) are
constant-index reads or accesses with an offset folded into the base, and are
described in the row of the region they fall in.

## Interpretation

The match state sits in one block from `0x00498DA8` to `0x004ABDBF`, with the
save blocks, the three definition tables and a few network and setup values
interleaved. The computer players' working state sits apart, from
`0x00489950` to `0x0048FB6F`; of it only the planning records (block 14),
block 17 and the hire anchors (block 19) are saved, and every planning pass
rebuilds the rest. The four change-flag arrays at `0x004988F8..0x00498DA5`
are read only by the network result sender, so they look like the list of
records a network game sends after a turn.

## Alternatives

A region read only through a pointer held in a register, such as a record
passed to a panel, can have more readers than the table shows. The element
sizes come from the index arithmetic of the accesses; where every access uses
the same scaled index the element size is certain, and the region's extent is
the element size times the count the loops use.

## How to reproduce

For each function of FND-EXE-004, list its data references, keep those into
`.data`, and sort them by address. Group runs of references whose index
arithmetic shares a stride, and read the writers of each group. For the fixed
item numbers, open selector `0x73` of `fn_00402D70` and read its eight-case
switch and the test at `0x004054E1..0x004054FB`.
