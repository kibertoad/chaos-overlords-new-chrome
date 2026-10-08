# Experiments on the original

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

<!-- doc-index:begin toc depth=2 -->
- [The probe](#the-probe)
<!-- doc-index:end -->

An experiment is a controlled run of the original, recorded as an `EXP-` entry
in `spec/experiments/` with a JSON fixture next to it, in the form the
[documentation standard](https://dinorefurb.com/documentation-standard/#experiments)
sets out. It starts from a saved state, usually a save patch, changes one
input, and records what follows. It is repeated from the same state with the
random number generator's state varied between runs, and anything random gets
enough repetitions for a recorded distribution: a formula inferred from one roll
is a guess. Run the original offline. The experiments still to run are listed in
[manual_validation_plan.md](../../manual_validation_plan.md).

For a manually operated Windows session, use
[`Capture-OriginalWindow.ps1`](../../tools/Capture-OriginalWindow.ps1) and follow
the raw-burst evidence rules in [REFERENCE-CAPTURE.md](../REFERENCE-CAPTURE.md).
The helper captures the visible desktop client area because the legacy
DirectDraw window may not produce reliable window-only captures on modern
systems. A capture that a test compares with the rebuild pixel for pixel has to
be taken at the screen entry's `resolution` (640x480), with no scaling,
filtering or aspect correction, and in the colours the game set in its palette.
The finding or experiment that cites a capture says which tool took it and with
what settings, and gives its xxh3. Captures, saves and recordings that hold any
of the game's content are never committed.

## The probe

`tools/Rechaos.OriginalProbe` runs the installed original under the Windows
debugging interface and records a new local game without anyone at the
keyboard. It checks the executable's SHA-256 against BLD-GOG-EN-1.1 first.

The GOG install registers compatibility layers for the installed executable's
path, among them RUNASADMIN, so starting that file needs an elevated prompt.
A copy at another path escapes them. Put `Chaos Overlords.exe` and
`SMACKW32.DLL` in a directory outside the repository, add directory junctions
named `DATA`, `MUSIC` and `HELP` that point into the install (the game finds
its data next to its own executable), set the other layers for the child
process, and pass the copy with `--executable`:

```powershell
$env:__COMPAT_LAYER = 'DWM8And16BitMitigation WINXPSP2 DISABLEDWM 640X480 DISABLEDXMAXIMIZEDWINDOWEDMODE'
dotnet run --project tools/Rechaos.OriginalProbe -- new-game --executable <copy> --out <run directory> [--game <install directory>] [--timeout <seconds>] [--scenario <0-9>] [--mentality <0-3>] [--turns <26|52|104|208>] [--humans <slot[:modifier]>,...] [--end-turns <n>] [--seed <n>] [--dump-at-roll <n>] [--trace-calls <hex address>] [--orders <turn[:player]:slot:action:target:target_2:repeat>,...] [--hires <turn[:player]:offer slot:sector>,...] [--families <turn:player:slot:family>,...] [--raiders <turn:player>,...] [--retire <turn:player>,...] [--cash <turn[-turn]:player:value>,...] [--force <turn:player:slot:force>,...] [--tolerance <turn:sector:value>,...] [--finance <turn:sector>,...] [--search <turn[:player]:definition+definition...>,...] [--time-limit <0-3>] [--expire-turns <turn>,...] [--deactivate <turn:player:slot>,...] [--pass-cards] [--delays <turn:ms>,...] [--menu <turn:after_ms:hold_ms>,...] [--clock-captures] [--comlink <script file>] [--sound] [--capture] [--white-key] [--equip-lists] [--attack-lists] [--draw-values <hex address>=<int32>[/<int32>...],...] [--search-clicks <x:y>,...] [--hire-steps <drag:slot:sector|reject:slot|exit>,...] [--order-steps <open:sector|card:n:x:y:command|strip:x:y:command|back|exit>,...] [--gang-markers] [--pointer] [--sound-calls] [--watch-intro] [--waits] [--slides] [--saved <turn:value>,...] [--closes <saved:answer>,...]
dotnet run --project tools/Rechaos.OriginalProbe -- extract --experiment <EXP ID> --out spec/experiments/<EXP ID>.json <run directory>... [--screens <SCR ID>,...]
dotnet run --project tools/Rechaos.OriginalProbe -- extract-comlink --experiment <EXP ID> --out spec/experiments/<EXP ID>.json <run directory>...
```

`new-game` switches full screen off in memory, sets both volumes of the
Options dialog, `effects_level` and `music_level`, in memory (without `--sound`
to 0 with the flags RULE-AUDIO-003 derives from them, so no effect, movie sound
or music plays; with `--sound` to their initialized values, 6 and 5
(FND-OPTIONS-001), whatever the registry holds; nothing the rolls or the state
depend on reads them), ends the logos and intro movies
by holding `left_button_down` in memory (RULE-VIDEO-001 ends a movie only when
the button is held at one of its ticks, so a posted click is missed), presses
Begin, records the seed and every `roll` with its call site and result, and copies
the writable sections once the first planning phase waits for input.
EXP-SETUP-001 gives the breakpoints and the procedure. Without options, Begin
takes the settings the setup screen opens with (the registry's preferences).
The options write what the setup screen's controls would commit before Begin
is pressed: the scenario in the original's numbering, the Mentality, the time
limit, and the slots that hold humans, each optionally named with one of the
six name modifiers (`right_hands`, `visibility`, `hire_force`, `elite`,
`islands`, `cash`), which the probe reads from the running executable. With
several humans the recording stops at the first human's Ready card, before
its hire offers are drawn. `--end-turns` presses Done that many times with no
orders, each once the next planning phase waits for input, which the first
call of the planning time-limit test `0x0041BDD5` (FND-TIMER-003) after
`elapsed_turns` has moved on shows, with Warn if Idle Gangs and Detailed Combat switched off in memory so
nothing waits for input, and dumps the state at the planning phase that
follows the last one. The human's planning phase opens the Combat Results
panel (SCR-COMBAT-001) after a fight that involved its gangs, and the Last
Turn Events panel (SCR-EVENT-001) when it has reports, and waits in each; the
probe breaks on both handlers and presses Exit before the next Done; once a
press has closed a panel it waits up to 3 seconds for another panel to open
or the planning loop to run again, since Last Turn Events is a call of its own
after Combat Results has returned (FND-UI-061). It presses Done again if a
press left the turn unmoved for 20 seconds. A match that ends,
or a human eliminated, before `--end-turns` runs out stops the presses there,
and the fixture's inputs list only the turns the run played: one Done press, or
one turn left to the planning time limit, per entry of `done_at_roll`, with
that turn's writes before it, which the replay tests check. A press repeated
after 20 seconds and the press at the final view of a match that ends
(FND-OBJECTIVE-004) are not listed. Each call of either handler is kept
with the roll count at the call and whether it showed its panel: a call shows
it when it reaches its call of the panel-open helper, `0x00452146` in Combat
Results and `0x0044F3D1` in Last Turn Events, which comes before it waits for
input, and a call with nothing to show, as Combat Results when no fight
qualifies, returns without reaching it (FND-UI-061). The fixture holds the
calls as `panels`. `--orders` writes
an order into a gang record of a human before the Done press of the
given turn, counted from 1: the `action`, `target` and `target_2` bytes of
FMT-STATE-001, and for a recurring order `repeat_action` and `repeat_target`,
as the order screens write them (RULE-TURN-005). The player after the turn
names the human; an entry without one writes for the first `--humans` slot, or
slot 0 when the option is left out. The probe presses Done only in that human's
planning, and another human's begins behind a Ready card that refills its offers
(RULE-SETUP-008), so the probe refuses an order or hire for any other player, and
the replay requires each one to name the lowest human, whose planning it plays.
A Search write may name any human of the run. The fixture lists each order as an `order` input before
its Done press, with the player it was written for (`turn 3: player 0 gang
slot 0 action 13 target 0 target_2 0 repeat 0`), and the replay submits the
same order as that player's command. `--hires` writes the sector byte of a
human's hire order for an offer slot into `hire_orders` before the Done press
of the given turn, as the hire screen does (RULE-HIRE-003), with the player
given as for `--orders`; the fixture lists it as a `hire` input naming the
player, and the replay hires the gang the rebuild offers that player in that
slot.
`--families` writes the `family` byte of a computer player's planning record
(FMT-STATE-007) and `--raiders` sets the player's byte of `raider_mode`
(RULE-AI-027), and `--retire` clears the player's byte of `player_active`
(FND-STATE-004), all before the Done press of the given turn, to reach families
no local match assigns or a match that ends with one player active. `--cash`
sets the player's `cash` before the Done press of each turn it names, so a
human can pay for a hire every turn. `--force` sets the `force` of a player's
gang in a roster slot (FMT-STATE-001), so a gang ordered to Heal can be at
Force 10 when it acts, and `--tolerance` sets a sector's `base_tolerance`
(FMT-STATE-002), so one Bribe or Snitch can wrap the signed byte. The fixture
lists each as a `planning` input, and the replay makes the same change to the
rebuild's state, a retired player becoming eliminated with its gangs and
sectors left in place; since that change bypasses the replay recorder, such a
run's journal is not verified.
`--search` sets a human's `search_filters` entries for the given site
definitions before the Done press of the given turn, as the Search panel's
rows do (RULE-SEARCH-001), with the player given as for `--orders`, and keeps
the site markers of each city redraw (FND-SEARCH-006). The fixture lists each
write as a `search` input naming the player and holds the markers of the last
redraw before the dump as `city_markers`, with the human it was drawn for; the
replay compares them with the rebuild's markers for that human and the filter
the probe set for that human.
A run that ends the match keeps the endgame's first drawing: the renderer's
arguments and the player of each row it lists, ranked, eliminated or the
victory splash (FND-AWARDS-005), which the fixture holds as `endgame_rows`.
`--finance` opens the Financial panel before the Done press of the given turn,
once that turn's orders and hires are written: the City variant for sector -1,
otherwise the Sector variant, after writing the sector into the map selection.
It presses the part of the console's Financial control that opens the variant
(SCR-UI-003), keeps the nine numbers the panel draws (FND-FINANCE-003) and the
sector the panel function was passed, and presses the panel's close control.
The fixture lists each opening as a `left_click` input before the Done press
and the run's panels under `finance`; the replay compares them with the
rebuild's projection of the same panel.
`--time-limit` writes `planning_limit_choice` before Begin, and
`--expire-turns` leaves out the Done press of the listed turns so their
planning time runs out; the fixture lists each as a `wait` input and records
the planning clock of each such turn as `timers` (RULE-TIMER-002,
RULE-TIMER-003).
With several `--humans` and `--end-turns` the run plays hot seat
(RULE-SETUP-008). The first listed slot, which has to be the lowest, presses
Ready on its hand-off card and takes the turn's inputs, and every later human
presses Ready and Done with no orders. A turn ends at the first human's next
hand-off card, before its offers are drawn, or at the end of the match, where
the probe passes each human's final view with Done. The replay draws the offers
of each human at its planning entry and finishes the command of every later
human. `--deactivate` writes sector 100, `GANG_INACTIVE` (FMT-STATE-001), into
the sector byte of the player's roster slot before the Done press of the given
turn, which takes the gang out of the match as a fight does; in Eliminate a
player whose slot 0 is gone loses everything at the end of the turn
(RULE-TURN-006). The fixture lists it as a `planning` input, and the replay
retires the gang with its Force. The probe breaks at the elimination card
`0x0042C3F5` (FND-OBJECTIVE-002) and ends the run there; with `--pass-cards` it
presses the card's Done and goes on, and the fixture holds the `active_player`
of each card it passed as `elimination_cards`.
`--delays` waits the given milliseconds in the given turn before its Done
press, listed as a `wait` input. `--menu` needs a time limit of 1 to 3. When
`after_ms` of the turn's planning clock have passed, `timeGetTime` less
`planning_start_ms` (FND-TIMER-003), it posts `WM_SYSCOMMAND` with
`SC_KEYMENU` to the game window, which opens the menu bar as the Alt key does,
polls `GetGUIThreadInfo` until the game's thread is in menu mode, holds it until
`hold_ms` after the posting and posts Escape until menu mode ends. The run fails
when menu mode ends before that. The fixture lists each holding as a `key`
input and holds it as `menus`: the times of the posting, of menu mode seen, of
Escape and of menu mode left, the `GUITHREADINFO` flags, and as `ticks` the
elapsed time of every call of the presentation timer's callback `fn_004327C0`
for slot 0 (FND-TIMER-002) from the clock's start to its expiry or the turn's Done press
(EXP-TURN-102). `--clock-captures` breaks at `0x0041B8C8` in the clock's start
helper, after it stores the start time and before it draws the bar, copies the
drawing area there, and keeps the player, `elapsed_turns` and the width and
elapsed time of the last bar drawn before it, which the fixture holds as
`clock_captures` with the digest of the bar's rectangle as an SCR-UI-003
element (EXP-UI-035). The copy holds the game for a few milliseconds, which the
first bar of each turn shows as elapsed time.
`--equip-lists` reads the item lists of the Equip panel after the dump: at
the next `PeekMessageA` call of the message pump (FND-UI-020) the probe saves
the thread context and calls the list builder `fn_0043F136` (FND-EQUIP-012)
for each category of each living gang of the first human, with the Tech Level
of the gang's definition, as the panel does. The builder's research test reads
`active_player`, so the probe sets it to that human for the calls and puts it
back with the context afterwards. The
fixture holds the items of each list, in entry order, as `equip_lists`; the
replay compares them with the rebuild's legal Equip orders of the gang in that
category (RULE-EQUIP-004).
`--attack-lists` does the same with the Attack picker's roster builder
`fn_0043D132` (FND-ATTACK-006), for each other player and each living gang of
the first human, with the gang's sector. The builder tests what `active_player`
sees, so the probe sets it in the same way. The fixture holds the roster slots
of each list as `attack_lists`; the replay compares them with the gangs the
rebuild's Attack picker shows for that opponent (RULE-ATTACK-002). Neither
fixture names the player, and the replay takes the lowest human slot, so the
probe refuses `--equip-lists` and `--attack-lists` when the first `--humans`
slot is not the lowest.
`--search-clicks` posts a left-button press and release at each client point
after the dump, lets the original run for half a second after each, and keeps
the whole `search_filters` table, the active player and whether the Search
handler `fn_00448E32` is running (FND-SEARCH-001, FND-SEARCH-002). The fixture
holds them as `search_clicks`; the replay passes each point to the rebuild's
console and Search panel hit tests and compares the tables (RULE-SEARCH-001).
`--hire-steps` works the Hire dock after the dump: `reject:s` clicks offer
slot `s`'s Reject cross, `drag:s:sector` presses on the offer's portrait and
releases over the sector's city map cell, and `exit` presses a result panel's
Exit, skipped when no panel is open. A step that makes the original roll ends
the run as not dumped. The Hire handler follows the two pointer points the
window procedure keeps (FND-UI-020), one of them taken from the desktop
cursor, so a drag writes both points itself, again just before the release,
and posts only the button messages. The probe keeps `hire_orders` after each
step as `hire_steps`, and keeps the panel calls as they stood at the dump; the
replay passes each press and release point to the rebuild's dock and city map
hit tests, takes each step through the rebuild's dock and compares the orders
(RULE-HIRE-003).
`--order-steps` works the detailed sector screen after the dump: `open:n`
double-clicks city sector `n`, `card:n:x:y:command` presses card `n` at
`(x, y)` within the card, `strip:x:y:command` presses the window at `(x, y)`,
and `back` and `exit` press the back control and a result panel's Exit; an
`exit` with no result panel open is skipped. A
press that opens an order popup reaches the popup helper's `TrackPopupMenu`
call (FND-UI-021); the probe keeps the menu and the command and greyed state of
each item, then skips the call and hands the helper `command`, 0 for no choice,
so no menu is shown (EXP-TURN-095). The probe keeps the menu, the view, the
player whose gangs the cards list (EXP-TURN-096), the card slots and the
active player's order bytes after each step as
`order_steps`; the replay takes each step through the rebuild's strip hit
tests, its order panel and its orders and compares them (RULE-TURN-005), and
takes each press on an Overlord portrait through the rebuild's portrait
handling and compares the cards (RULE-UI-010).
`--gang-markers` logs every gang-status marker the original draws: each full
city redraw, each frame drawn for a sector holding the player's gang, each copy
of the saved cell back and each incoming-only mark (FND-UI-024, EXP-UI-004),
from the last full redraw before the dump on, tagged with the hire or order
step it came in. The fixture holds them as `gang_markers`; the replay takes
the steps through the rebuild's marker map and compares the map after each
(RULE-UI-006).
`--pointer` logs every call of the cursor helper `fn_00465BC8` (FND-UI-034)
with the rolls and Done presses before it, its shape and force and the address
of the call. The fixture holds them as `pointer_calls`; the replay checks that
every roll from the setup's hourglass on is made under the hourglass and
compares the rebuild's pointer at each planning entry and after each Done press
(RULE-UI-007, EXP-UI-022).
`--sound-calls` logs every call of the play helper `fn_0045851A`
(FND-AUDIO-006) with the rolls and Done presses before it, its slot and the
address of the call; with `--sound` the effects wrapper's calls are logged too.
It also reads `effects_enabled` (FND-AUDIO-002) at each call, at each Done press
and at the end of the run. The fixture holds the calls as `sound_calls` with
`effects_enabled` beside them, and `extract` refuses a run whose value is
unknown or differed between those reads. The replay expects the push cue of Begin and of each Done press
when effects were enabled, and no push cue when they were not
(RULE-AUDIO-006, EXP-AUDIO-001).
`--watch-intro` lets both intro movies play out before the button is held and
logs each frame the frame helper shows, with the movie's name, its header's
frame count, the slot's frame counter and the time from the first movie's first
frame, and the counter when the movie is closed. The fixture holds them as
`intro_movies` (RULE-VIDEO-001, EXP-VIDEO-001).
`--waits` logs, from the dump on, each tick of the presentation clock, timer
slot 0, and each call of the wait `fn_00464CD9` with its argument, the address
of the call and the times it starts and returns (FND-TIMER-002). The fixture
holds them as `ticks` and `waits` (RULE-TIMER-004, EXP-UI-024).
`--slides` logs, from the dump on, each slide-in of the panel-open helper
`fn_0041953E` with the return address of its call, the startup benchmark
count, the travel and the offset of each copy (FND-UI-011). The fixture holds
them as `slides`, the return address as `caller`, which FND-UI-066 maps to the
panel's handler (RULE-UI-003, EXP-UI-025).
`--saved turn:value,...` writes 0 or 1 to the saved byte `0x00498350`
(FND-UI-058) before the Done press of that turn, or at the dump when the turn
is one past the last, and records the value it replaced. `--closes
saved:answer,...` posts `WM_CLOSE` to the window after the dump and the other
steps, once per entry: `saved` (`-`, 0 or 1) is written to the byte first, and
each dialog the close opens through `fn_00465CEC` (FND-UI-022) is answered with
`answer`, without being shown: `1w` saves first and the save, which the probe
returns from without running, reports it written, `1c` reports it cancelled, 2
cancels and 3 goes on without saving. The stores of the quit byte `0x00487828`
in File, Exit are skipped and recorded, so every close of a run is played. The fixture
holds them as `saved_writes` and `closes` (RULE-UI-015, EXP-UI-026).
`--draw-values` writes 32-bit values into memory each time the planning-entry
function `fn_0046FD80` starts to draw the console (FND-UI-040): the nth value
at its nth call and the last at every later one. It makes the console draw a
number the match would not reach over what an earlier entry drew, as
EXP-UI-002 does with the score and cash. The calls are counted over every
human's planning entries and each human's console draws its own slot, so the
probe refuses `--draw-values` with more than one `--humans` slot, and it
refuses an address outside the executable's writable sections. The fixture
lists each as a `setup` input; the values change the match, so such a run is
not replayed.
Each run records the roll count at every press as `done_at_roll`, and as
`rolls_at_dump` the count the state dump follows when steps after the dump made
more, as a Ready press that refills the offers does (RULE-SETUP-008); the
replay counts draws up to it. `--seed` writes the given value over the argument of `srand`, so
a recorded run can be played again, and `--dump-at-roll` copies the writable
sections and the top of the stack at the entry of that call of `roll`, counted
from 0, into
`at-roll-<n>` in the run directory, to look at the state that led to a
divergence. `--trace-calls` sets a breakpoint on a function of the original and
adds a note to `trace.json` for each call: the roll count so far, the calling
instruction, the first four stack arguments and the returned value. Comparing
those notes with the same calls in the rebuild shows which call first gave a
different answer. `extract` refuses runs recorded with different
settings, since the runs of one experiment differ only in the seed. The run
directory holds
the original's memory and never goes into the repository. `extract` reads the
numbers of the spec's state layouts and glossary terms out of one or more run
directories and writes them as the runs of an experiment fixture, with no
names or texts. Among them are each player's Last Turn reports of the last
resolution (FMT-STATE-006), which the first run of EXP-TURN-001 and every run
from EXP-TURN-010 on hold, apart from the traced second run of EXP-TURN-021.
The same runs also hold each player's running totals (`cash_earned`,
`cash_spent`, `damage_inflicted`, `casualties`, `overthrow_count`,
`hide_count`) and their `hire_role` and `previous_hire_role`; the replay
compares them only in the runs that hold them, and reads the -1 the original
keeps in a human player's `hire_role` as the rebuild's 0. The fixtures also hold
the `scenario_score` and `scenario_standing` the last evaluation stored. A run
that ends the match stops when the endgame draws the awards, and its fixture
holds `match_over` and each player's first three `player_awards` entries.
From EXP-TURN-048 on, the fixtures also hold the computer players' planning
state: the planning record fields (FMT-STATE-007), `ai_started`,
`raider_mode`, `placement_anchor`, `sector_weight` and the `focus` and
`coverage_sector` values of the auxiliary records (FND-AI-081), each left out
when it holds 0; every combat record a resolution has written
(FMT-STATE-003); and the entries of the combat result rows that hold a gang,
with the `police_hit` values that are not 0 (FMT-STATE-008). The replay
compares the planning records byte for byte, and the auxiliary values only
for computer gangs whose family is assigned, since the original leaves the
others 0 from the start of a match where the rebuild holds -1. It rebuilds
the combat records and result rows of the last resolution from its attack and
police events, and compares the records of the gangs that fought,
`police_damage` in all 486 records and every result row.
`OriginalNewGameExperimentTests` replays every run of the EXP-SETUP and
EXP-TURN fixtures against the rebuild and names the first roll whose bound or
result differs, with the original's call instruction, then compares the state
and, where the fixture has them, the reports.

`--comlink` replaces the Done presses with a script of Comlink steps, one per
line, for a match with several humans (EXP-COMLINK-001). `visit p` waits for
player p's handoff card, presses Ready and closes the planning entry panels;
`view` and `send` press the two parts of the console's Comlink control;
`next`, `prev` and `dismiss` press the View panel's controls; `card p`,
`press send` and `press cancel` press the Send panel's; `type` posts a
`WM_KEYDOWN` for each character, with `{BACK}`, `{ENTER}`, `{LEFT}`, `{UP}`,
`{RIGHT}`, `{DOWN}` and `{EXEC}` for those keys; `dump` keeps the state; and
`done` presses Done. The probe also switches Slide Panels off in memory, so
a panel takes presses as soon as its handler runs. After each step it keeps
the panels open, the effect slots played, each message View showed with the
cursor and the numbers drawn, each drop of read messages at the end of a
player's planning, the Send panel's selection and draft, and every player's
Comlink counts, cursors, pending flag and message records (FMT-STATE-005).
`extract-comlink` writes those as the steps of a fixture, and
`OriginalComlinkExperimentTests` plays the same steps in the rebuild and
compares them after each one.
