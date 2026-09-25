# Glossary

Add one `##` heading per term, spelled the way the pseudocode spells it. Each
entry says what the term means, the name the game shows the player where there
is one, and what the
[documentation standard](https://dinorefurb.com/documentation-standard/#where-it-lives)
asks of that kind of term. Every claim about the original (an address, the
order of a list, the order of handlers or of a queue, what an outside value is
read from) is followed by the IDs of its findings or experiments in brackets,
or by `(unknown)`.

## active_gangs

`active_gangs(player)` counts a player's active gangs. A function, defined by
RULE-AI-011.

## active_player

The player slot whose turn it is at this computer, whose view the city,
panels and reports show. Any other value the game keeps: a player slot, at
`0x004ABC84` [FND-UI-036]. Its width is not recorded (unknown).

## ai_started

Whether a computer player's planning records have been reset for this match.
Any other value the game keeps: one byte per player slot at `0x00482108`
[FND-AI-003].

## armor_defense_upgrade

`armor_defense_upgrade(player, idx)` gives the armor with the greatest Defense
improvement, or -1. A function, defined by RULE-AI-005.

## armor_upgrade

`armor_upgrade(player, idx)` gives the armor a computer gang would buy, or -1.
A function, defined by RULE-AI-005.

## attitude

How each player regards each other player, from -10 to +10; a negative value
makes the observer treat the other player as hostile. Any other value the game
keeps: `INT32LE[36]` at `0x004AB590`, element `observer * 6 + other`
[FND-AI-006]. The resolver `fn_00472775` writes it in three places: the
recovery at `0x0047280B`, the decrement after an attack at `0x00473F7D` and the
decrement at a Control takeover at `0x00475781` [FND-AI-047].

## aux_records

A second per-gang record of the computer players. A list the game keeps, of
14-byte records, element `player * 81 + roster_slot` (assumed), referenced at
`0x0048C0BA` [FND-AI-015]. Fields the rules use: `focus`, the first 16-bit
value, whose meaning depends on the family, and `coverage_sector`, the second
16-bit value, the sector a family-6 gang heads for or covers [FND-AI-015,
FND-AI-013].

## award_least

`award_least(stat, start, category)` appends `category` to `player_awards` of
every player tied for the lowest value of `stat` below `start`, or equal to
it. A function, defined by RULE-AWARDS-001.

## award_most

`award_most(stat, start, category)` appends `category` to `player_awards` of
every player tied for the highest value of `stat` above `start`, or equal to
it. A function, defined by RULE-AWARDS-001.

## best_site

`best_site(s, kind)` gives the slot of the unfinished site with the greatest
positive Cash or Support in sector `s`, or -1. A function, defined by
RULE-AI-022.

## blit_benchmark_count

How many identical screen copies the startup benchmark managed in just over one
second. A value from outside the game: an integer measured once at startup by
`fn_00432954` [FND-UI-011]; it depends on the speed of the machine and sets the
panel slide step. Its address is not recorded (unknown).

## block_leader_sector

`block_leader_sector(player, slot)` gives the focus sector of the leader of a
family-11 gang's block. A function, defined by RULE-AI-006.

## BribeCashShort

An event: a Bribe failed because its player could not pay. It carries
`player` and `sector`, in that order. Its handler is RULE-EVENT-008, run at
once [FND-EVENT-001].

## cash

A player's money, shown as Cash. Any other value the game keeps:
`INT32LE[6]`, indexed by player slot, at `0x004A25E8` [FND-EQUIP-006,
FND-UPKEEP-001, FND-PLATFORM-003]. Rules write `cash[player]`.

## cash_earned

The running total of cash a player has taken in, shown in the financial panel.
Any other value the game keeps: `INT32LE[6]`, indexed by player slot, at
`0x004A27E0` [FND-UPKEEP-001, FND-PLATFORM-003].

## cash_spent

The running total of cash a player has paid out, shown in the financial panel
and used by the endgame awards. Any other value the game keeps: `INT32LE[6]`,
indexed by player slot, at `0x0049CA78` [FND-UPKEEP-001, FND-AWARDS-001,
FND-PLATFORM-003].

## casualties

The number of each player's gangs that have died from damage, shown on the
endgame Stats panel as Casualties. It is raised by one for each gang whose
Force falls below 1 when the combat damage is applied, whether the damage came
from attacks or from the police; Terminate and the Eliminate clean-up do not
raise it. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004AB620`, raised only at `0x00474889` and set to 0 at
`0x00476045` [FND-GANG-003, FND-GANG-005].

## chaos_payout_phase

The step of `resolution` that pays out the Chaos successes rolled in
`chaos_phase`. It runs after `transaction_phase` and before `terminate_phase`
[FND-CHAOS-001].

## chaos_phase

The step of `resolution` that rolls each Chaos gang's successes and then,
sector by sector, creates Crackdowns. It runs after `instant_phase` and before
`combat_phase` [FND-CHAOS-001, FND-POLICE-001].

## chaos_successes

The Chaos successes each gang rolled this turn, kept from the Chaos rolls
until the Chaos payout. Any other value the game keeps: one integer per gang,
element `player * 81 + roster_slot`, width not recorded, kept as a local of the
whole-turn resolver with no fixed address [FND-CHAOS-001].

## choose_anchor

`choose_anchor(player, anchor)` picks a new placement sector, or -1. A
function, defined by RULE-AI-013.

## combat_advantage

Set when a computer player's gangs out-fight the visible defenders in more than
three quarters of another player's sectors. Any other value the game keeps:
the byte at +20 of a player-pair record, element `observer * 6 + other`; the
records' address is not recorded (unknown) [FND-AI-018, FND-AI-032].

## combat_phase

The step of `resolution` in which each attacking gang makes its attack and
takes any retaliation, and at whose end the damage from attacks and police is
applied to Force. It runs after `chaos_phase` and before `transaction_phase`.
`police_phase` runs inside it, after the attacks and before the damage is
applied [FND-CHAOS-001, FND-COMBAT-001, FND-COMBAT-004, FND-GANG-003].

## combat_rating

A gang's Combat plus the skills that match its weapon. A function, defined by
RULE-COMBAT-001.

## combat_records

What each gang did and suffered in the last combat phase. A list the game
keeps, of FMT-STATE-003, one element per player and roster slot, element
`player * 81 + roster_slot`, in player slot order and then roster slot order,
at `0x004A11E8` [FND-COMBAT-004, FND-AI-010, FND-PLATFORM-003].

## combat_results

For each sector, which gangs of each player fought there in the last combat
phase, and whether the police attacked each player there. A list the game
keeps, of the combat result row (six players' rows of six four-byte entries,
then six police flag bytes, 150 bytes in all), 64 elements in ascending sector
order, at `0x004A8888 + sector * 0x96` [FND-AUDIO-002, FND-COMBAT-004]. An
entry is two `INT16LE` gang indices, `player * 81 + roster_slot`: the gang,
or -1 for an empty entry, and the gang's Attack target, or -1 when its action
was not Attack; only the first is cleared each phase. A gang is listed in its
own player's row of its own sector. The police flag of a player is set when
the police find one of its gangs there [FND-COMBAT-008]. The rows are
written in `turn_order` and roster slot order [FND-COMBAT-004].

## CombatClip

An event: Detailed Combat plays one clip. It carries `focal`, the viewer's
gang as its element number in `gangs`; `other`, the other gang's element
number, or -2 for the police; `mirrored`, true when `other` attacks `focal`;
and `hold`, false when the next clip follows at tick 16 without the result
hold. It has no handlers; SCR-COMBAT-002 draws it [FND-COMBAT-005].

## comlink_alert_repeat

The counter that times the repeats of the incoming-message alert. Any other
value the game keeps: at `0x00487808` [FND-AUDIO-002]; its type is not recorded
(unknown).

## comlink_blink_step

The eight-step animation counter the event pump advances once per
`presentation_tick`; it blinks the Comlink button and times the alert repeat.
Any other value the game keeps [FND-AUDIO-012], at an address not recorded
(unknown).

## comlink_count

How many messages each player holds in `comlink_messages`, 0 to 16. Any other
value the game keeps: `INT32LE[6]`, at `0x004981E0 + player * 4`
[FND-COMLINK-001, FND-COMLINK-002]. Rules write `comlink_count[player]`.

## comlink_cursor

The index, within the player's 16 elements of `comlink_messages`, of the
message Comlink View shows. Any other value the game keeps: `INT32LE[6]`, at
`0x004981C8 + player * 4` [FND-COMLINK-001, FND-COMLINK-004].

## comlink_draft

The message being composed in the Comlink Send panel, in the layout of a
stored message. A structure the game keeps: FMT-STATE-005, one global buffer
that the Send panel edits and the recorder copies [FND-COMLINK-001], at
`0x00498120` [FND-COMLINK-006].

## comlink_draft_column

The column, 0 to 39, of the text cursor in `comlink_draft`. Any other value
the game keeps: an integer [FND-COMLINK-005], a local of the Send handler with
no fixed address, 0 each time the panel opens [FND-COMLINK-007].

## comlink_draft_row

The row, 0 to 3, of the text cursor in `comlink_draft`. Any other value the
game keeps: an integer [FND-COMLINK-005], a local of the Send handler with no
fixed address, 0 each time the panel opens [FND-COMLINK-007].

## comlink_eligible

For each player slot, whether the Send panel lets the active player pick that
player as a recipient. Any other value the game keeps: `UINT8[6]`, rebuilt
each time the Send panel opens [FND-COMLINK-003], a local of the Send handler
with no fixed address [FND-COMLINK-007].

## comlink_messages

The messages in each player's Comlink inbox. A list the game keeps, of
FMT-STATE-005, 16 elements per player at `0x0049CA90 + player * 0xA60`, kept
in the order they arrived, oldest first. When all 16 are in use, a new message
drops the oldest and the rest move down [FND-COMLINK-001, FND-COMLINK-004].
Emptied when the match loop starts, and cut at the front of its read messages
when its player finishes planning (RULE-COMLINK-007) [FND-COMLINK-006].

## comlink_pending

Set while the active player has an unread Comlink message; it makes the
incoming-message alert repeat. Any other value the game keeps: `UINT8` at
`0x0048781C` [FND-AUDIO-002, FND-COMLINK-004].

## comlink_selected

For each player slot, whether the player is picked as a recipient in the Send
panel; shown as a green card frame. Any other value the game keeps:
`UINT8[6]` [FND-COMLINK-003] at `0x00498114`, set to 0 each time the Send
panel opens [FND-COMLINK-007].

## ComlinkAlert

An event: the Comlink alert sounds for an unread message. It carries no
arguments. Its handler is RULE-AUDIO-007, run at once, which plays
`DATA/Snd00205` from effect slot 6 [FND-AUDIO-012, FND-COMLINK-001].

## comm_type

The communication type the player last chose for network play. Any other value
the game keeps: a DWORD at `0x00487884`, initialized to 0 and read from the
registry value `commType` [FND-OPTIONS-001].

## control_phase

The step of `resolution` that pools each player's Control strength and settles
the owner of each sector, in ascending sector order. It runs after
`move_phase` [FND-CONTROL-001, FND-CHAOS-001].

## ControlArtDrawn

An event: the pressed or released art of a console control is drawn. It
carries 1 when the control is drawn pressed and 0 when it is drawn released,
and has no handlers [FND-UI-032, FND-AUDIO-010].

## ControlGainedReport

An event: a player's Control order has taken a sector. It carries `player`,
`sector` and `previous`, the sector's owner before or -1, in that order
[FND-EVENT-004]. RULE-CONTROL-001 emits it. Its handler is
RULE-EVENT-012, run at once [FND-EVENT-001].

## controller

Who plays each player slot. Any other value the game keeps: `INT32LE[6]`,
indexed by player slot, at `0x004AB638` [FND-SETUP-002, FND-PLATFORM-003].
The values are -1 for an empty setup slot, 0 for a human at this computer, 1
for a computer player and 3 for a human playing over the network
[FND-SETUP-002, FND-AI-004, FND-TURN-005].

## ControlLostReport

An event: a player has lost a sector, to a third Crackdown within five turns
(RULE-POLICE-002) or to another player's Control (RULE-CONTROL-001). It
carries `player`, `sector` and `taker`, in that order: `taker` is the player
whose Control took the sector, or 0 for a Crackdown [FND-EVENT-004]. Its
handler is RULE-EVENT-013, which records a type-3 Last Turn report, run at
once [FND-POLICE-002, FND-EVENT-001].

## covered_by

`covered_by(player, c)` gives a family-6 gang of the player covering sector
`c`, or -1. A function, defined by RULE-AI-025.

## crackdown_at

`crackdown_at(c)` gives the Crackdown a neighbourhood scan reads at index `c`,
0 to 64. A function, defined by RULE-AI-013.

## crackdown_history

The turns of the last two Crackdown occurrences in each sector. A list the
game keeps, one element per sector in ascending sector order, each element
`INT16LE[2]` holding a turn number or -100 for an empty slot, at
`0x004ABCC0 + sector * 4` [FND-POLICE-001, FND-PLATFORM-003]. The turn stored
and the window test both use `elapsed_turns` [FND-CHAOS-002].

## crackdown_in_force

`crackdown_in_force(s)` tells whether sector `s` has a Crackdown. A function,
defined by RULE-AI-004.

## CrackdownReport

An event: a Crackdown was created in a sector where the player had a gang when
`resolution` began. It carries `player` and `sector`, in that order. Its
handler is RULE-EVENT-004, run at once [FND-EVENT-001].

## damage_inflicted

The total damage each player's gangs have dealt with opening attacks, used by
the endgame awards. Any other value the game keeps: `INT32LE[6]`, indexed by
player slot, at `0x004A5ED8` [FND-COMBAT-003, FND-AWARDS-001,
FND-PLATFORM-003].

## danger_near

`danger_near(player, idx)` is the computer players' equipment gate. A function,
defined by RULE-AI-005.

## difficulty_band

The per-player band, 0, 1 or 2, that adjusts several dice pools during
`resolution` according to the AI Mentality and whether the player is a
computer. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004A2570` [FND-AI-007, FND-PLATFORM-003].

## dominance_points

`dominance_points(player)` gives the player's Dominance numerator: cash,
Support and owned sectors weighted by the match's time limit. A function,
defined by RULE-OBJECTIVE-004.

## draw_once

`draw_once(player, idx, kind)` makes one target draw and returns the target
when the strength test passes, -1 otherwise. A function, defined by
RULE-AI-004.

## draw_target

`draw_target(player, idx, kind, tries)` makes up to `tries` target draws and
returns the last target drawn. A function, defined by RULE-AI-004.

## effect_slots

The loaded general sound effects, one per slot. A list the game keeps: 48
slots, of which 0 to 9 are used; slot 5 is loaded and emptied around each
Detailed Combat attack sound, and an empty slot plays nothing. Its address is
not recorded (unknown) [FND-AUDIO-002, FND-AUDIO-013]. Rules write
`effect_slots[slot]`.

## EffectPlayed

An event: an effect slot's sound starts playing. It carries the slot number,
and has no handlers [FND-AUDIO-002, FND-AUDIO-003].

## effects_enabled

Whether sound effects play. Any other value the game keeps: a byte at
`0x0048783C`, set when `effects_level` is nonzero [FND-AUDIO-002].

## effects_level

The Sound Effects volume the Options dialog shows, 0 to 10. Any other value
the game keeps: a DWORD at `0x00487864`, initialized to 6 and read from the
registry value `prefsVolumeSFX` [FND-OPTIONS-001, FND-AUDIO-002].

## EffectsVolumeSet

An event: the wave output volume is set. It carries the volume as the
two-channel DWORD the level gives, and has no handlers [FND-AUDIO-002].

## elapsed_turns

The number of turns completed, counted from 0. Any other value the game keeps:
`INT32LE` at `0x0049CA68` [FND-AI-009, FND-PLATFORM-003]. A new match sets it
to 0; it goes up by 1 after each `resolution`, before the next `turn_start`,
so it is 0 throughout the first turn. The Crackdown window and history read
it [FND-TURN-006].

## endgame_rows

The order in which the endgame screen lists the player slots: active players
by standing, then eliminated players. A list the game builds when the match
ends [FND-AI-005, FND-AWARDS-003]; where it is kept is not recorded (unknown).

## EquipCashShort

An event: an Equip failed because its player could not pay. It carries
`player`, `sector` and `definition`, in that order: the gang's sector and its
`definition` byte, which the Last Turn report stores in place of the item
[FND-EVENT-004]. RULE-EQUIP-001 emits it. Its handler is
RULE-EVENT-014, run at once [FND-EVENT-001].

## events_page

The index of the report the Last Turn Events panel shows, which is also the
index of its record in `last_turn_reports`. Any other value the game keeps: an
integer at `0x004948EC`, set to 0 at the start of each human planning visit
and changed only by the panel's Previous and Next [FND-EVENT-005].

## events_page_drawn

`events_page_drawn()` marks the Last Turn report on show as seen and updates
`events_unviewed`. A function, defined by RULE-EVENT-005.

## events_planning_start

`events_planning_start()` resets `events_page` and opens the Last Turn Events
panel when the active player has a report. A function, defined by
RULE-EVENT-005.

## events_seen

For each of the active player's 32 Last Turn records, whether the panel has
shown it since the planning visit began; unoccupied records start as seen.
Any other value the game keeps: `UINT8[32]` at `0x00494870` [FND-EVENT-005].

## events_show

`events_show()` opens the Last Turn Events panel when the active player has at
least one report and returns 1, or returns 0. A function, defined by
RULE-EVENT-005.

## events_unviewed

Set while some Last Turn report of the active player has not been shown; it
lights the Events control. Any other value the game keeps: `UINT8` at
`0x00487814` [FND-EVENT-005].

## fight_marks

Whether each gang took part in a gang fight in the current combat phase. Any
other value the game keeps: one value per gang, element
`player * 81 + roster_slot`, written by the attack block and never read as a
condition for an attack [FND-COMBAT-006]: a local byte of the resolver, set
for every attacker, every attack's target and every gang the police find, and
copied at the record fill into the `UINT8[486]` at `0x00498BC0`
[FND-COMBAT-008].

## fight_or_hold

`fight_or_hold(player, idx, kind, tries)` writes an Attack, Heal or Control
for a gang on an objective. A function, defined by RULE-AI-031.

## finance_rows

A function, defined by RULE-FINANCE-001: the eight amounts of the Financial
panel and its gang count, for the whole city or one sector [FND-FINANCE-002].

## first_visible_definition_zero

`first_visible_definition_zero(player, s)` gives the first visible gang of
another player in sector `s` whose definition is 0, or -1. A function, defined
by RULE-AI-029.

## fn_0040F63D

The unidentified function of build BLD-GOG-EN-1.1 at `0x0040F63D` that opens
the local setup's name editor for a player card [FND-SETUP-005].

## fn_00451F80

The unidentified function of build BLD-GOG-EN-1.1 at `0x00451F80` that shows
the Combat Results panel at the start of a player's planning when Detailed
Combat is off [FND-SETUP-010].

## fn_0045519D

The unidentified function of build BLD-GOG-EN-1.1 at `0x0045519D` that shows
the Game Information panel at the start of a player's first planning in a new
local game [FND-SETUP-010].

## fn_00468CFC

The unidentified function of build BLD-GOG-EN-1.1 at `0x00468CFC` that steps
a setup card's portrait up to the next unused one [FND-SETUP-005].

## fn_00468D87

The unidentified function of build BLD-GOG-EN-1.1 at `0x00468D87` that steps
a setup card's portrait down to the previous unused one [FND-SETUP-005].

## force_meter_length

`force_meter_length(force)` gives the length in pixels of a gang's Force meter
on the detailed-sector screen. A function, defined by RULE-UI-005.

## foreign_neighbours

`foreign_neighbours(player, center)` counts the cells around a centre not owned
by the player. A function, defined by RULE-AI-013.

## free_neighbours

`free_neighbours(player, center)` counts neutral cells without a Crackdown
around an owned centre. A function, defined by RULE-AI-013.

## g_004A08C4

The unidentified byte of build BLD-GOG-EN-1.1 at `0x004A08C4`, 36 bytes before
the sector list, which the placement-anchor scan reads as the owner of sector
-1 [FND-AI-010].

## game_info_limit_text

`game_info_limit_text()` gives the planning time limit text Game Information
shows. A function, defined by RULE-UI-009.

## game_info_mentality_text

`game_info_mentality_text()` gives the Mentality text Game Information shows.
A function, defined by RULE-UI-009.

## game_info_scenario_text

`game_info_scenario_text(scenario_name)` gives the scenario text Game
Information shows, with the game length after it for the four scenarios that
have one. A function, defined by RULE-UI-009.

## gang

A gang a player has hired. A structure the game keeps: FMT-STATE-001, kept as
one element of `gangs` [FND-HIRE-002, FND-UI-036]. A roster slot holds a gang
while the record's `sector` is not 100 (`GANG_INACTIVE`). The statistics the
game shows as Force, Combat, Defense, Stealth, Detect, Chaos, Control, Heal,
Influence, Research, Strength, Blade, Ranged, Fighting and Martial Arts are
the fields `force`, `combat`, `defense`, `stealth`, `detect`, `chaos`,
`control`, `heal`, `influence`, `research`, `strength`, `blade`, `ranged`,
`fighting` and `martial_arts`. All but `force` hold the effective value, with
equipment and completed sites added.

## gang_definitions

The gang types, read from `DATA/Gangs`. A list the game keeps, of
FMT-DATA-002, 90 elements in file order, indexed by a gang's `definition`,
at `0x004A2800` [FND-HIRE-006].

## gangs

Every roster slot of every player. A list the game keeps, of FMT-STATE-001,
486 elements: element `player * 81 + roster_slot`, in player slot order and
then roster slot order, at `0x00498DA8` [FND-HIRE-002, FND-UI-036,
FND-PLATFORM-003].

## grudge_after_attack

`grudge_after_attack(attacker, defender, damage)` lowers the defender's
attitude toward the attacker. A function, defined by RULE-AI-016.

## grudge_after_takeover

`grudge_after_takeover(previous_owner, new_owner)` lowers the previous owner's
attitude toward the new owner of a sector. A function, defined by RULE-AI-017.

## hide_count

The number of Hide actions each player's gangs have carried out, used by the
endgame awards. Any other value the game keeps: `INT32LE[6]`, indexed by
player slot, at `0x004A25D0` [FND-AWARDS-001, FND-PLATFORM-003].

## hire_allowed

`hire_allowed(player)` tells whether a computer player tries to hire this
turn. A function, defined by RULE-AI-011.

## hire_destination

`hire_destination(player, mode, offer)` gives the encoded sector a computer
player's hire is placed in. A function, defined by RULE-AI-012.

## hire_force_modifier

Whether a player's name matched the name modifier that makes hires start at
Force 10. Any other value the game keeps: `UINT8[6]`, indexed by player slot,
at `0x004A5EF0`; nonzero when set. Set at new-game setup and kept in the save
[FND-HIRE-005].

## hire_limit

`hire_limit(player)` is the gang count below which a computer player tries to
hire. A function, defined by RULE-AI-011. Each planning pass stores its result
in `INT32LE[6]` at `0x00482110`, save block 16 [FND-STATE-003].

## hire_offers

The three gangs each player is offered for hire. Any other value the game
keeps: `INT8[18]`, element `player * 3 + offer slot`, at `0x004ABBC0`
[FND-HIRE-001, FND-HIRE-002, FND-AI-008]. An element holds a gang definition
number, -100 before the first offer, or the negated number of a gang just
hired or snubbed, which marks the slot to be refilled [FND-HIRE-001].

## hire_orders

What each player has chosen to do with each offer this turn. Any other value
the game keeps: `INT8[18]`, element `player * 3 + offer slot`, at
`0x004A27C8` [FND-HIRE-001, FND-HIRE-002]. An element holds -1 for no order,
-2 to snub the offer, or the sector to place the hired gang in
[FND-HIRE-001].

## hire_phase

The step of `resolution` that carries out hires and snubs, player by player
and offer slot by offer slot. It runs after `control_phase` and before
`turn_end`: the Control loop's exit jumps to its first instruction
(`0x00475862`), and its own exit to the presence countdown (`0x00475E16`)
[FND-EQUIP-006, FND-TURN-008].

## hire_role

The hire role a computer player's schedule chose for its latest hire, 0 to 6.
Any other value the game keeps: `INT32LE[6]`, indexed by player slot, at
`0x00482128` [FND-AI-009, FND-AI-014].

## HireCashShort

An event: a hire failed because its player could not pay. It carries `player`
and `definition`, in that order. Its handler is RULE-EVENT-009, run at once
[FND-EVENT-001].

## HireRosterFull

An event: a hire failed because the player had no free roster slot. It
carries `player` and `definition`, in that order. Its handler is
RULE-EVENT-011, run at once [FND-EVENT-001].

## HireSectorFull

An event: a hire failed because the chosen sector was full. It carries
`player` and `sector`, in that order. Its handler is RULE-EVENT-010, run at
once [FND-EVENT-001].

## hostile_human_owner

`hostile_human_owner(player, s)` is `hostile_owner` for a human owner. A
function, defined by RULE-AI-004.

## hostile_owner

`hostile_owner(player, s)` tells whether sector `s` belongs to another player
the player is hostile to. A function, defined by RULE-AI-004.

## hq_sectors

The six sectors the city generator can place headquarters in, which are also
Siege's important sectors and Eliminate's scored sectors. Any other value the
game keeps: six `INT32LE` sector indexes at `0x00494818` [FND-CITY-003,
FND-UI-033].

## human_count

`human_count()` counts the human players. A function, defined by RULE-AI-006.

## idle_warning_choice

The button the player used to leave the idle-gang warning: 1 for OK, 0 for
Cancel. A value from outside the game: the player's input on SCR-OPTIONS-001
[FND-OPTIONS-002].

## instant_phase

The first step of `resolution`: the Bribe, Heal, Hide, Influence, Research and
Snitch actions, carried out gang by gang in `turn_order` and roster slot
order, followed by raising every sector's Tolerance below 1 to 1. It runs
before `chaos_phase` [FND-TURN-001, FND-SNITCH-001, FND-CHAOS-001].

## is_block_leader

`is_block_leader(player, slot)` tells whether a family-11 gang leads its block
of six. A function, defined by RULE-AI-006.

## is_hidden

Whether a gang is hiding, which is true exactly while its `action` is Hide. A
function, defined by RULE-HIDE-001.

## is_human

`is_human(p)` tells whether a person plays player `p`, at this computer or over
the network. A function, defined by RULE-AI-004.

## item_definitions

The item types, read from `DATA/ITEMS`. A list the game keeps, of
FMT-DATA-003, 64 elements of 166 bytes in file order, at `0x004A5F08`, which
puts record 0's `research_difficulty` (offset `0x7C`) at `0x004A5F84`
[FND-RESEARCH-002].

## item_ok

`item_ok(player, item, tech_cap)` tells whether an item is researched, within a
Tech cap and affordable. A function, defined by RULE-AI-005.

## item_price

A function, defined by RULE-EQUIP-003: what a player pays for an item bought
by a gang in a given sector, with the Factory discount.

## last_turn_report_count

How many reports each player has in `last_turn_reports`. Any other value the
game keeps: `INT32LE[6]` at `0x004ABCA8 + player * 4` [FND-EVENT-001,
FND-EVENT-004]. Rules write
`last_turn_report_count[player]`.

## last_turn_reports

The reports of the last resolution, shown in the Last Turn Events panel. A
list the game keeps, 32 records of 10 bytes per player: element
`player * 32 + index`, at `0x004AAE08 + player * 0x140 + index * 10`, each
player's records in the order they were recorded [FND-EVENT-001]. Its
elements are FMT-STATE-006 [FND-EVENT-004].

## left_button_down

Whether the left mouse button is held. A value from outside the game: taken
from the Windows mouse messages the event pump receives; it changes whenever
the player presses or releases the button [FND-UI-032].

## local_game

Set when the game is a local game. Any other value the game keeps: the flag at
`0x00482178` [FND-AUDIO-003]. The reading of this flag is an interpretation;
its width is not recorded (unknown).

## local_tech_cap

The Tech ceiling the weapon choice applies to a gang's first pass over the
weapon classes, computed from the gang and the sites near it (selector
`0x62`). Any other value the game computes, element `player * 81 +
roster_slot`; how it is computed is not recorded (unknown) [FND-AI-024].

## match_over

Set when the end-of-turn evaluation finds the match finished. Any other value
the game keeps: `UINT8` at `0x004ABBD4`, cleared when a match starts
[FND-AI-005, FND-OBJECTIVE-003, FND-OBJECTIVE-004].

## mentality

The AI Mentality chosen at setup, which sets how the computer players play.
Any other value the game keeps: `INT8` at `0x00487850`, 0 for Goon, 1 for
Criminal, 2 for Crime Lord and 3 for Homicidal Maniac [FND-AI-004].

## misc_chaos_upgrade

`misc_chaos_upgrade(player, idx)` gives the miscellaneous item with the
greatest Chaos improvement, or -1. A function, defined by RULE-AI-005.

## mode_score

`mode_score(player, mode, idx, c)` is the score the sector selector gives
sector `c` in a mode. A function, defined by RULE-AI-006.

## modifier_cells

`modifier_cells(value, width)` gives the glyph codes a signed statistic
modifier is drawn with, a zero drawn as the dim zero. A function, defined by
RULE-UI-004.

## modifier_name_cash

The fixed upper-case string in the executable that a player's name must equal
to start with $1,500. A string the game keeps, at `0x00487BD8`; a match sets
the flag at `0x0049CA70 + player` [FND-SETUP-001, FND-SETUP-015].

## modifier_name_elite

The fixed upper-case string in the executable that a player's name must equal
to start with five extra equipped gangs. A string the game keeps, at
`0x00487BC0`; a match sets the flag at `0x004A2788 + player` [FND-SETUP-004,
FND-SETUP-015].

## modifier_name_islands

The fixed upper-case string in the executable that a player's name must equal
to give every unowned sector a permanent Crackdown. A string the game keeps,
at `0x00487BCC`; a match sets the flag at `0x004ABC10 + player`
[FND-SETUP-003, FND-SETUP-015].

## modifier_name_right_hands

The fixed upper-case string in the executable that a player's name must equal
to start with five extra unequipped gangs. A string the game keeps, at
`0x00487B9C`; a match sets the flag at `0x004ABBD8 + player` [FND-SETUP-004,
FND-SETUP-015].

## modifier_name_visibility

The fixed upper-case string in the executable that a player's name must equal
to see every opposing gang. A string the game keeps, at `0x00487BA8`
[FND-SETUP-011, FND-SETUP-015].

## modifier_visibility

Whether a player's name set the visibility name modifier, which lets the player
see every opposing gang. Any other value the game keeps: `UINT8[6]`, indexed
by player slot, at `0x004AB588` [FND-SETUP-011, FND-DETECT-001].

## move_phase

The step of `resolution` that moves gangs to their destinations. It runs after
`terminate_phase` and before `control_phase` [FND-MOVE-001, FND-CHAOS-001].

## music_enabled

Whether music plays. Any other value the game keeps: a flag cleared when
`music_level` is set to 0 [FND-AUDIO-001]; its address is not recorded
(unknown).

## music_level

The Music volume the Options dialog shows, 0 to 10. Any other value the game
keeps: a DWORD at `0x00487868`, initialized to 5 and read from the registry
value `prefsVolumeCD` [FND-OPTIONS-001, FND-AUDIO-001].

## music_mode

Which music program plays: 0 the title program, 1 the endgame program, 2 the
game program. Any other value the game keeps: a DWORD at `0x00487878`
[FND-AUDIO-001].

## MusicPaused

An event: CD playback pauses because the game window lost focus. It carries
no arguments and has no handlers [FND-AUDIO-001].

## MusicPlayRequested

An event: CD playback of a range of audio tracks starts. It carries the first
and last track numbers, counted from 1 on the game's disc, and has no handlers
[FND-AUDIO-001].

## MusicResumed

An event: paused CD playback resumes because the game window got focus back.
It carries no arguments and has no handlers [FND-AUDIO-001].

## MusicStopped

An event: CD playback stops. It carries no arguments and has no handlers
[FND-AUDIO-001].

## MusicVolumeSet

An event: the CD audio volume is set. It carries the two-channel DWORD the
level gives, and has no handlers [FND-AUDIO-001].

## name_matches

`name_matches(player, modifier)` gives 1 when the player's name equals the
string `modifier` in length and in every byte, case included, and 0
otherwise. A function, defined by RULE-SETUP-001.

## network_game

Set when the game is a network game. Any other value the game keeps: the flag
at `0x00487B58` [FND-AUDIO-003]. The reading of this flag is an
interpretation; its width is not recorded (unknown).

## new_local_game

Set while a new local game has not yet shown its first player the Game
Information panel. Any other value the game keeps [FND-SETUP-010]; its type
and address are not recorded (unknown).

## next_option

`next_option(index, buffer)` gives what the registry loader stores for its
`index`th value: the registry value when the query succeeds, the unchanged
shared buffer otherwise. A function, defined by RULE-OPTIONS-001.

## number_cells

`number_cells(value, width, leading_zeros)` gives the glyph codes an unsigned
value is drawn with. A function, defined by RULE-UI-004.

## objective_choice

The objective last chosen on the setup screen. Any other value the game keeps:
a DWORD at `0x00487858`, initialized to 0 and read from the registry value
`prefsObjective` [FND-OPTIONS-001].

## offer_to_snub

`offer_to_snub(player)` chooses the hire offer a computer player snubs. A function,
defined by RULE-AI-009.

## on_objective

`on_objective(s)` tells whether sector `s` is an objective sector of scenario
6 or 8. A function, defined by RULE-AI-031.

## overthrow_count

The number of sectors each player has taken from another player, used by the
endgame awards. Any other value the game keeps: `INT32LE[6]`, indexed by
player slot, at `0x004A27A8` [FND-AWARDS-001, FND-PLATFORM-003].

## opening_damage

The damage each gang's own attack did in the current combat phase, or -1 when
the target evaded, copied into `damage_dealt` of the gang's combat record. Any
other value the game keeps: a local 32-bit value per gang of the resolver,
element `player * 81 + roster_slot`, set only for gangs whose action is Attack
[FND-COMBAT-008].

## owned_sector_count

`owned_sector_count(player)` gives the number of the 64 sectors the player
owns. A function, defined by RULE-OBJECTIVE-002.

## owner_at

`owner_at(c)` gives the owner a neighbourhood scan reads at index `c`, 0 to 64.
A function, defined by RULE-AI-005.

## PanelSlideDrawn

An event: one step of a panel sliding in or out is drawn. It carries the
panel's horizontal offset from its resting place, and has no handlers
[FND-UI-011].

## phase_damage

The damage each gang has taken so far in the current combat phase, from
attacks, retaliations and the police, applied to Force at the end of the phase.
Any other value the game keeps: one integer per gang, element
`player * 81 + roster_slot` [FND-COMBAT-003]: a local 32-bit value of the
resolver, cleared at the start of `resolution` and capped at 10 before it is
applied [FND-COMBAT-008].

## placement_anchor

The sector a computer player places its new gangs in, stored as the sector
plus `0x40`. Any other value the game keeps: one per player slot at
`0x0048E2F8`; its element type is not recorded [FND-AI-010].

## plan

`plan(idx, action, t1, t2)` writes a computer gang's planned action and targets
into its planning record and gang record. A function, defined by RULE-AI-004.

## planning_limit_choice

The planning time limit chosen on the setup screen: 0 none, 1 thirty seconds,
2 two minutes, 3 five minutes. Any other value the game keeps: a DWORD at
`0x00487854`, initialized to 0 and read from the registry value
`prefsTimeLimit` [FND-OPTIONS-001, FND-TIMER-001].

## planning_limit_ms

The planning time limit in milliseconds, -1 for none. Any other value the game
keeps [FND-TIMER-001]; its address is not recorded (unknown).

## planning_phase

The part of a turn in which each player gives orders. Players plan one after
another in `turn_order`: a computer player's slot runs the computer planner, a
human's slot the human handler, and an eliminated slot is skipped
[FND-TURN-005]. Visibility is rebuilt once, before the first player plans
[FND-DETECT-001, FND-TURN-006], and each planning entry refills vacant
`hire_offers` [FND-HIRE-001]. It runs after `turn_start` and before
`resolution` [FND-TURN-005].

## planning_records

The computer players' per-gang planning state. A list the game keeps, one
16-byte record per player and roster slot, element `player * 81 +
roster_slot`, at `0x0048A250` [FND-AI-019, FND-AI-001]; its element format is
FMT-STATE-007. Fields: `family` (+0), `unk_01` (+1), `older_action`,
`older_target`, `older_target_2` (+2..+4), `previous_action`,
`previous_target`, `previous_target_2` (+5..+7), `planned_action`,
`planned_target`, `planned_target_2` (+8..+10), `unk_0B` (+11, never
addressed), `weapon_cooldown` (`INT16`, +12) and `armor_cooldown` (`INT16`,
+14) [FND-AI-019, FND-AI-021, FND-STATE-006].

## planning_start_ms

The value of `timer_ms` when the current player's planning began. Any other
value the game keeps [FND-TIMER-001]; its address is not recorded (unknown).

## planning_time_expired

`planning_time_expired()` tells whether the current player's planning time has
run out. A function, defined by RULE-TIMER-002.

## planning_timer_start

`planning_timer_start()` starts the planning clock for the current player. A
function, defined by RULE-TIMER-002.

## play_effect

`play_effect(slot)` plays an effect slot when sound effects are on. A
function, defined by RULE-AUDIO-005.

## play_sound

`play_sound(slot)` plays an effect slot without testing whether sound effects
are on. A function, defined by RULE-AUDIO-005.

## player

A player slot, 0 to 5. The game keeps no player structure: each per-player
value is an array of its own indexed by the slot, such as `cash`,
`controller` and `hire_offers`, and each player's gangs are the 81 elements of
`gangs` from `player * 81` [FND-HIRE-002, FND-PLATFORM-003].

## player_active

Whether a player is still in the match. Any other value the game keeps:
`UINT8[6]`, indexed by player slot, at `0x004ABBE0` [FND-TURN-003,
FND-OBJECTIVE-003, FND-STATE-004]. It is set for all six slots when a new
match starts and saved with the match [FND-STATE-004]. It is cleared only near
the end of `resolution`, for a player who owns no sector and has no gang
[FND-TURN-003, FND-STATE-004].

## player_awards

The endgame awards each player has earned, as category numbers in the order
they were given: 0 Fist, 1 Skull, 2 Big Fat Chicken, 3 Dollar Sign, 4 Safe.
A table the game keeps, up to five entries per player slot [FND-AWARDS-001];
its address and the codes it stores are not recorded (unknown).

## player_names

The players' names. Any other value the game keeps: six 12-byte records at
`0x004A2588 + player * 12` [FND-UI-003, FND-PLATFORM-003]. Byte 0 holds the
name's length, 1 to 10; the characters follow from byte 1, each from `0x20`
(space) to `0x5A` (`Z`), with a NUL after the last [FND-STATE-004].


## player_retired

Set for a local human slot once its elimination card has been shown. Any
other value the game keeps: one flag per player slot [FND-OBJECTIVE-002]; its
type and address are not recorded (unknown).

## player_status_text

`player_status_text(slot)` gives the status Game Information shows for a
player slot. A function, defined by RULE-UI-009.

## players_human

For each player slot, whether a human plays it. Any other value the game
keeps: `UINT8[6]` at `0x004ABC58`, set at setup to 1 where `controller` is 0
or 3 and to 0 otherwise, and set to 0 when a network player's slot is handed
to the computer [FND-COMLINK-007]. A save file keeps it as block 39
[FND-SAVE-001].
## pointer_in_rect

`pointer_in_rect(x, y, w, h)` tells whether the pointer is inside a rectangle.
A function, defined by RULE-UI-001.

## pointer_shape

The stock cursor the game last set, 0 to 4. Any other value the game keeps,
remembered by the cursor helper `fn_00465BC8` [FND-UI-034]; its address is not
recorded (unknown).

## pointer_x

The pointer's horizontal position on the 640-by-480 screen. A value from
outside the game: taken from the Windows mouse messages; it changes whenever
the mouse moves [FND-UI-032].

## pointer_y

The pointer's vertical position on the 640-by-480 screen. A value from outside
the game: taken from the Windows mouse messages; it changes whenever the mouse
moves [FND-UI-032].

## PointerShapeSet

An event: the Windows cursor changes to a stock shape. It carries the stock
cursor's Windows ID (`IDC_ARROW` and the like), and has no handlers [FND-UI-034].

## police_phase

The part of `combat_phase` in which the police look for and attack gangs in
Crackdown sectors, gang by gang in `turn_order` and roster slot order. It runs
after the attacks and before the damage is applied [FND-COMBAT-001,
FND-GANG-003].

## portrait

The Overlord portrait a player slot shows, 0 to 14, which also chooses the
player's default name. Any other value the game keeps: one per player slot
[FND-SETUP-002, FND-SETUP-005]; its type and address are not recorded
(unknown).

## pref_base_stats

The Base Statistics option: when set, gang panels show base values. Any other
value the game keeps: a DWORD at `0x0048784C`, initialized to 0 and read from
the registry value `prefsBaseStats` [FND-OPTIONS-001].

## pref_detailed_combat

The Detailed Combat option: when set, combat plays as the detailed
presentation. Any other value the game keeps: a DWORD at `0x0048785C`,
initialized to 1 and read from the registry value `prefsCombat`
[FND-OPTIONS-001].

## pref_full_screen

The Full Screen option. Any other value the game keeps: a DWORD at
`0x0048786C`, initialized to 1 and read from the registry value
`prefsFullScreen` [FND-OPTIONS-001].

## pref_slide_panels

The Slide Panels option: when set, panels slide in and out. Any other value
the game keeps: a DWORD at `0x00487840`, initialized to 1 and read from the
registry value `prefsSlide` [FND-OPTIONS-001, FND-UI-011].

## pref_thousands_colors

The Thousands of Colors option. Any other value the game keeps: a DWORD at
`0x00487844`, initialized to 1 and read from the registry value `prefsVidDeep`
[FND-OPTIONS-001].

## pref_warn_idle

The Warn if Idle Gangs option. Any other value the game keeps: a DWORD at
`0x00487860`, initialized to 1 and read from the registry value
`prefsFreeGang` [FND-OPTIONS-001, FND-OPTIONS-002].

## PreferencesWriteFailed

An event: the preferences writer tried to store the options and every write
failed, because the key was opened read-only. It carries no arguments and has
no handlers [FND-OPTIONS-001].

## preferred_scenario

The scenario a fresh local setup starts on: 0 (Greed) in the executable's
data, replaced at startup by the registry value `prefsObjective`, and set to
the scenario chosen on the setup screen. Any other value the game keeps:
`INT8` at `0x00487858` [FND-SETUP-009, FND-SETUP-012, FND-SETUP-013].

## presentation_tick

The timer that paces presentation: combat animation phases, the Comlink blink
and the planning timer bar. A clock, defined by RULE-UI-008.

## presentation_tick_pending

Set by each `presentation_tick` and cleared by the loop that consumes it. Any
other value the game keeps: timer slot 0 [FND-UI-001]; whether it is a flag or
a counter, and its address, are not recorded (unknown).

## previous_action_count

`previous_action_count(player, s, action)` counts the player's gangs in sector
`s` whose previous action is `action`. A function, defined by RULE-AI-004.

## previous_hire_role

The hire role a computer player held before its latest schedule step. Any
other value the game keeps: `INT32LE[6]`, indexed by player slot, at
`0x00482160` [FND-AI-014].

## propose_site

`propose_site()` draws a site definition for the city generator, redrawing
the two kinds Armageddon excludes. A function, defined by RULE-CITY-002.

## random_neighbour

`random_neighbour(player, idx)` is sector selector mode 0. A function, defined
by RULE-AI-007.

## rank_offer

`rank_offer(player, mode)` scores a hire offer for a computer player's hire role. A
function, defined by RULE-AI-008.

## reaction

A player's reaction value, drawn at new-game setup and not written again,
which scales how the computer players respond to attacks. Any other value the
game keeps: `INT32LE[6]`, indexed by player slot, at `0x004AB650`; the setup
draw stores 3 to 6 there, and save block 36 copies it [FND-AI-006,
FND-RNG-005, FND-RNG-006, FND-STATE-003].

## refresh_anchor

`refresh_anchor(player)` keeps or replaces a player's placement anchor. A
function, defined by RULE-AI-013.

## registry_dword

The DWORD a registry value holds. A value from outside the game: read with
`RegQueryValueExA` from `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0`,
once at startup; `registry_dword[index]` is the value the loader queries
`index`th [FND-OPTIONS-001].

## registry_present

Whether a registry query succeeds. A value from outside the game: the result of
`RegQueryValueExA` for the loader's `index`th value, once at startup
[FND-OPTIONS-001].

## research_first

`research_first(player, idx, want)` gives the first item of a type, or of the
fixed miscellaneous list, still to research, or -1. A function, defined by
RULE-AI-026.

## research_remaining

How much research each player still needs for each item; 0 means the item is
researched. Any other value the game keeps: `INT8[384]`, element
`item * 6 + player`, at `0x004A2608`; every read loads it signed
[FND-RESEARCH-001, FND-RESEARCH-002, FND-PLATFORM-003, FND-STATE-004].

## research_score

`research_score(c)` sums the Research modifiers of sector `c`'s sites. A
function, defined by RULE-AI-026.

## ResearchCompleted

An event: a player's Research has completed an item. It carries `player` and
`item`, in that order. Its handler is RULE-EVENT-007, run at once
[FND-EVENT-001].

## reset_planning

`reset_planning(idx)` clears one planning record at a computer player's first
planning pass. A function, defined by RULE-AI-001.

## resolution

The part of a turn that carries out every player's orders, after
`planning_phase`. Its steps run in this order: `instant_phase`,
`chaos_phase`, `combat_phase` (with `police_phase` inside it),
`transaction_phase`, `chaos_payout_phase`, `terminate_phase`, `move_phase`,
`control_phase`, `hire_phase` and `turn_end` [FND-CHAOS-001,
FND-COMBAT-001, FND-MOVE-001, FND-EQUIP-006, FND-TURN-003, FND-TURN-008].
Before `instant_phase` it clears each player's report count and notes where
each player has gangs, player by player, and then moves each sector's
Tolerance one step toward normal [FND-TURN-008]. Just before resolution
starts, the previous turn's Last Turn reports are cleared [FND-EVENT-001].

## retaliation_damage

The retaliation damage each attacking gang took in the current combat phase,
copied into `retaliation_taken` of its combat record. Any other value the game
keeps: a local 32-bit value per gang of the resolver, element
`player * 81 + roster_slot`, set to 0 and then written only for gangs whose
action is Attack [FND-COMBAT-008].

## rng

The game's one random number generator, the C runtime's `rand`. A random
number generator, specified by RULE-RNG-001; its state is `rng_state`. Rules
draw from it with the built-in `draw()`, which gives 0 to 32767, or through
`roll`.

## rng_state

The generator's 32-bit state. Any other value the game keeps: `UINT32LE`,
kept in the C runtime's per-thread data, which the runtime allocates, so it
has no fixed address [FND-RNG-001, FND-RNG-002].

## rng_step

The step `rng` makes for each `draw()`: it advances `rng_state` and returns
bits 16 to 30 of the new state. A function, defined by RULE-RNG-001.

## roll

`roll(n)` gives a whole number from 1 to `n`, counting any `n` below 1 as 1,
and makes three draws from `rng`. A function, defined by RULE-RNG-002.

## roster_slot

A gang's position, 0 to 80, among its player's 81 elements of `gangs`. It
does not change while the gang lives. Slot 0 holds the player's Right Hands
[FND-TURN-003]. A hire copies the new gang into the first free slot of the
hiring player from 0 to 79, so slot 0 is reused once the Right Hands are dead
[FND-TURN-005, FND-HIRE-001, FND-TURN-008]. Slot 80 never holds a gang: the
command bar uses it as scratch space while a sector-wide order is chosen
[FND-TURN-009].

## scenario

The objective of the match. Any other value the game keeps: `INT32LE` at
`0x004ABBE8` [FND-RESEARCH-002, FND-SETUP-009, FND-PLATFORM-003]. 0 is Greed,
1 Power, 2 Acceptance, 3 Dominance, 4 Kill 'Em All, 5 Big 40, 6 Siege,
7 Eliminate, 8 Big Man and 9 Armageddon [FND-OBJECTIVE-003, FND-UI-033,
FND-TURN-003, FND-RESEARCH-002].

## scenario_acceptance

The value of `scenario` for Acceptance, the timed scenario scored by Support.
A constant, 2 [FND-OBJECTIVE-003, SRC-MANUAL-GOG].

## scenario_big_40

The value of `scenario` for Big 40, won by the first player to own 40
sectors. A constant, 5 [FND-OBJECTIVE-003, SRC-MANUAL-GOG].

## scenario_dominance

The value of `scenario` for Dominance, the timed scenario scored by cash,
Support and sectors together. A constant, 3 [FND-OBJECTIVE-003,
SRC-MANUAL-GOG].

## scenario_greed

The value of `scenario` for Greed, the timed scenario scored by cash. A
constant, 0 [FND-OBJECTIVE-003, SRC-MANUAL-GOG].

## scenario_power

The value of `scenario` for Power, the timed scenario scored by sectors
owned. A constant, 1 [FND-OBJECTIVE-003, SRC-MANUAL-GOG].

## scenario_score

Each player's score toward the scenario's objective, rebuilt by the end
evaluation. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004A2790` [FND-AI-005, FND-TURN-003, FND-PLATFORM-003].

## scenario_standing

For each active player, the number of players with a strictly greater
`scenario_score`; 0xFF for an inactive player. Any other value the game
keeps: `UINT8[6]`, indexed by player slot, at `0x004ABC08` [FND-AI-005,
FND-AI-009]. Selector `0x2D` of the computer players searches these bytes for
player slot numbers, as if the table listed players in ranking order
[FND-STATE-004].

## search_filters

The Search panel's site selection: for each player and each of the 22 site
definitions, whether the city shows the uncontrolled sites of that definition.
Any other value the game keeps: `UINT8[132]`, element
`player * 22 + definition`, at `0x004A24E8` [FND-SEARCH-001, FND-SEARCH-003].
Each element is 0 or 1; the table is emptied when the match loop starts and is
not saved [FND-SEARCH-004, FND-COMLINK-006].

## search_set_all

`search_set_all(value)` sets all 22 of the active player's `search_filters`
to `value`. A function, defined by RULE-SEARCH-001.

## search_toggle

`search_toggle(definition)` flips one of the active player's
`search_filters`. A function, defined by RULE-SEARCH-001.

## sector

One of the 64 squares of the city, numbered 0 to 63 row by row, eight to a
row: column `sector % 8`, row `sector / 8` [FND-AI-005, FND-UI-033]. The
sectors adjacent to a sector are the up to eight whose row and column each
differ from its own by at most one, at offsets -9, -8, -7, -1, +1, +7, +8 and
+9, leaving out any that would wrap past the edge of a row or leave the city
[FND-AI-005, FND-MOVE-002]. A structure the game keeps: FMT-STATE-002, kept
as one element of `sectors` [FND-CONTROL-001, FND-UI-035].

## sector_card_slots

`sector_card_slots(sector_number)` gives the gang slots whose cards the
detailed-sector screen draws. A function, defined by RULE-UI-010.

## sector_gang_count

The number of a computer player's active gangs in each sector, rebuilt at each
planning pass. Any other value the game keeps: `INT32`, element `player * 64 +
sector`, from `0x00489950` [FND-AI-040, FND-AI-018].

## sector_presence

Which players had a gang in each sector when the current resolution began.
Any other value the game keeps: `UINT8[384]`, element `sector * 6 + player`,
kept as a local of the whole-turn resolver with no fixed address, element
`sector * 6 + player` at byte offset `sector * 6 + player` of the local
[FND-POLICE-002, FND-EVENT-004].

## sector_roster_slots

`sector_roster_slots(sector_number)` gives the gang slots the Gangs in Sector
panel lists. A function, defined by RULE-UI-010.

## sector_weight

A computer player's cached `visible_weight` of each sector, rebuilt at each
planning pass. Any other value the game keeps: element `player * 64 +
sector`; its type and address are not recorded (unknown) [FND-AI-039,
FND-AI-013].

## sectors

The city's sectors. A list the game keeps, of FMT-STATE-002, 64 elements in
ascending sector number, at `0x004A08E8` [FND-CONTROL-001, FND-UI-035,
FND-PLATFORM-003].

## seed_anchor

`seed_anchor(player)` sets a player's first placement anchor. A function,
defined by RULE-AI-013.

## select_sector

`select_sector(player, mode, idx)` is the computer players' sector selector: it
returns the one-step destination for a gang. A function, defined by
RULE-AI-006.

## selected_card

The local setup's selected player card, 0 to 5, the only card whose portrait
and name can be changed. Any other value the game keeps: at `0x004854C4`
[FND-SETUP-005]; its width is not recorded (unknown).

## serial_number

The number the game keeps as its serial number. Any other value the game
keeps: a DWORD at `0x00487870`, initialized to 0 and read from the registry
value `serialNum` [FND-OPTIONS-001].

## set_pointer

`set_pointer(shape, force)` changes the Windows cursor to a stock shape. A
function, defined by RULE-UI-007.

## site

One of the three sites in a sector. A structure the game keeps:
FMT-STATE-004, kept in the field `sites` of FMT-STATE-002 [FND-TURN-001]. A
site is complete when its `progress` equals its definition's Resistance, and
a complete site benefits the sector's owner [FND-GANG-001].

## site_builder

`site_builder(player, slot, kind)` is the shared handler of families 3 and 5.
A function, defined by RULE-AI-022.

## site_controlled

`site_controlled(s, slot)` tells whether the site in slot `slot` of sector `s`
counts as controlled by `active_player` for the city's site markers. A
function, defined by RULE-SEARCH-002.

## site_definitions

The site types, read from `DATA/SITES`. A list the game keeps, of
FMT-DATA-001, in file order, indexed by a site slot's `definition`. The
list is at `0x004AB668`, 22 entries of 62 bytes read whole from `data\Sites`,
so the `resistance` field of entry `d` is at `0x004AB67E + d × 0x3E` and the
`tolerance` field at `0x004AB684 + d × 0x3E` [FND-TURN-001, FND-TURN-006].

## site_meter_length

`site_meter_length(progress, resistance)` gives the length in pixels of a site
meter on the detailed-sector screen. A function, defined by RULE-UI-005.

## site_unfinished

`site_unfinished(s, k)` tells whether the site in slot `k` of sector `s` still
has Resistance left. A function, defined by RULE-AI-004.

## SiteCooperationAchieved

An event: a player's Influence has completed a site. It carries `player`,
`sector` and `slot`, in that order. Its handler is RULE-EVENT-006, run at once
[FND-EVENT-001].

## SiteMarkerDrawn

An event: the city draws one site marker from `PX00150`. It carries
`definition`, `controlled`, `source_x`, `source_y`, `x` and `y`, in that
order, the last two in the 432-by-416 city buffer. It has no handlers
[FND-SEARCH-003].

## sites_balanced

`sites_balanced(ids)` gives 1 when a sector's proposed site set passes the
generator's balance test and 0 otherwise. A function, defined by
RULE-CITY-002.

## slide_step

`slide_step(travel)` gives the number of pixels a panel moves per step. A
function, defined by RULE-UI-003.

## solo_control_ok

`solo_control_ok(player, idx, s)` tells whether a gang could take sector `s` by
Control on its own. A function, defined by RULE-AI-004.

## sound_output_available

Whether the machine has a wave output device the game can play through. A
value from outside the game: what the sound setup reports at startup
[FND-AUDIO-003]; the detection call is not recorded (unknown).

## stealth_sum

`stealth_sum(c)` sums the positive Stealth of sector `c`'s finished sites. A
function, defined by RULE-AI-028.

## strength_check

`strength_check(a, t)` is the computer players' test before an attack. A
function, defined by RULE-AI-004.

## terminate_phase

The step of `resolution` that carries out every Terminate action. It runs
after `chaos_payout_phase` and before `move_phase` [FND-MOVE-001,
FND-CHAOS-001].

## timer_ms

The Windows system time in milliseconds. A value from outside the game:
`UINT32`, read from the multimedia function `timeGetTime`; it changes every
millisecond. The game reads it when the process starts, to seed `rng`
[FND-RNG-001], and the planning timer reads it at the start of planning and at
every check (RULE-TIMER-002, RULE-TIMER-003).

## timer_redraw_countdown

Counts presentation ticks down to the next redraw of the planning timer bar,
reloaded with 6. Any other value the game keeps: a DWORD at `0x00487898`
[FND-TIMER-001].

## TimerBarDrawn

An event: the planning timer bar is redrawn. It carries the bar's width in
pixels, 0 to 60, and has no handlers [FND-TIMER-001].

## transaction_phase

The step of `resolution` that carries out Equip, Give and Sell, gang by gang
in `turn_order` and roster slot order. It runs after `combat_phase` and
before `chaos_payout_phase` [FND-CHAOS-001, FND-EQUIP-002].

## turn_end

The last step of `resolution`. Police presence counts down in every sector
(the decrement at `0x00475E74`) [FND-POLICE-001]; then eliminated players are
found (the call of `fn_00476F3B` at `0x00475ECD`) and reported, and then the
end of the match is evaluated (the call of `fn_00476857` at `0x00475F61`)
[FND-TURN-003, FND-TURN-008].

## turn_limit

The match length of a timed scenario in turns: 26, 52, 104 or 208, and 52
when the setup screen opens. Any other value the game keeps: `INT32LE` at
`0x004A5EF8` [SRC-MANUAL-GOG, FND-AI-005, FND-OBJECTIVE-003, FND-SETUP-013].

## turn_order

The player slots in the order every per-player pass visits them: 0, 1, 2, 3,
4 and 5, whatever the mix of human and computer players. A list the game
keeps only in the sense that each pass counts the slots up from 0: its
elements are player slots, and no global holds it [FND-TURN-005,
FND-TURN-001, FND-COMBAT-001].

## turn_start

The work the game does at the start of every turn, before `planning_phase`,
in this order. It clears recurring actions that can no longer apply and
copies each gang's `repeat_action` and `repeat_target` into `action` and
`target` [FND-TURN-004, FND-HIDE-001], and runs `upkeep_phase`
[FND-UPKEEP-001]; the first pass of the turn loop, after a new game or a load,
skips both [FND-TURN-006]. It rebuilds every sector record from its
completed sites [FND-UPKEEP-001, FND-UI-035], and then every active gang's
effective statistics [FND-GANG-001].

## turns_remaining

`turns_remaining()` gives the turns left in the match. A function, defined by
RULE-AI-004.

## unique_leader

`unique_leader()` gives the one player with scenario standing 0, or -1. A
function, defined by RULE-AI-006.

## upkeep_phase

The part of `turn_start` in which each player, in `turn_order`, pays the
Upkeep of each active gang and collects `cash_yield` from each sector it
owns. The first turn of a match skips it [FND-UPKEEP-001, FND-TURN-005].

## visible_opponents

`visible_opponents(player, s, kind)` lists the gangs of other players the
player can see in sector `s`, narrowed by `kind`. A function, defined by
RULE-AI-004.

## visible_weight

`visible_weight(observer, s)` rates the first gang the observer can see in
sector `s`: 10, 1 or 0. A function, defined by RULE-AI-004.

## weapon_upgrade

`weapon_upgrade(player, idx)` gives the weapon a computer gang would buy, or
-1. A function, defined by RULE-AI-005.

## weight_at

`weight_at(player, c)` gives the cached weight a neighbourhood scan reads at
index `c`, 0 to 64. A function, defined by RULE-AI-005.
