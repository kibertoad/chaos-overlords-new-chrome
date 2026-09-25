# Parity matrix

How much of the [spec](spec/README.md) the rebuild does, one row per rule, format and screen entry
that is not superseded, as version 1 of the
[documentation standard](https://dinorefurb.com/documentation-standard/#parity-matrix) defines it.
Spec ID, Title and Spec status are copied from the entry. Code says how much of the entry the
rebuild does. Tests lists only test files that compare the rebuild with evidence from the original;
manual play and tests that compare the rebuild with itself or with the spec do not count.
Deviations lists the entries of [DEVIATIONS.md](DEVIATIONS.md) that depart from the row. Status is
worked out from the other columns, and `node tools/check-spec.mjs` checks all of it.

| Status | Rows |
|---|---|
| `unknown` | 1 |
| `sourced` | 0 |
| `supported` | 102 |
| `established` | 0 |
| `disputed` | 0 |
| `implemented` | 119 |
| `validated` | 0 |

| Code | Rows |
|---|---|
| `missing` | 20 |
| `partial` | 83 |
| `complete` | 119 |

## DATA

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-DATA-001` | Site definition records in DATA/SITES | supported | complete | None | None | implemented | None |
| `FMT-DATA-002` | Gang definition records in DATA/Gangs | supported | complete | None | None | implemented | None |
| `FMT-DATA-003` | Item definition records in DATA/ITEMS | supported | complete | None | None | implemented | None |
| `FMT-DATA-004` | Colour list in DATA/CLT00002 | supported | missing | None | None | supported | The rebuild copies CLT00002 unread into its asset pack. |
| `FMT-DATA-005` | Compressed archive DATA/DATA.Z | unknown | missing | None | None | unknown | The rebuild copies DATA.Z unread into its asset pack. |

## GFX

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-GFX-001` | 16-bit image files in DATA/PX16 | supported | complete | None | None | implemented | The rebuild supplies the missing header fields itself; the widths it uses for PX00202, PX00203 and PX06008 should be checked against the 311 and 241 the files give. |
| `FMT-GFX-002` | 8-bit image files in DATA/PX08 | supported | complete | None | None | implemented | None |
| `FMT-GFX-003` | Palette entry in a PX08 image file | supported | complete | None | None | implemented | None |
| `RULE-GFX-001` | Decoding the RLE8 pixel data of a PX08 image | supported | complete | None | None | implemented | None |
| `RULE-GFX-002` | The display is a 640-by-480 window or screen whose drawing area of 640 by 460 sits directly under the menu bar and is copied from an off-screen surface | supported | missing | None | None | supported | Not yet compared with the rebuild. |

## AUDIO

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-AUDIO-001` | Sound effect files DATA/SNDnnnnn | supported | complete | None | None | implemented | The files are passed whole to the sound library. |
| `FMT-AUDIO-002` | Ogg pages of the music tracks MUSIC/TrackNN.ogg | supported | complete | None | None | implemented | The files are passed whole to the music player. |
| `RULE-AUDIO-001` | Starting a music program | supported | complete | None | None | implemented | Title, game and endgame programs are wired to the matching Ogg tracks; the rebuild plays files in place of CD audio. |
| `RULE-AUDIO-002` | Music repeats its program when it ends and pauses while the window is inactive | supported | complete | None | None | implemented | Focus loss pauses and focus gain resumes music; the end-of-playback restart was not checked separately. |
| `RULE-AUDIO-003` | Applying the music and effects levels | supported | complete | None | None | implemented | The 0 to 10 level conversion and the level-5 music default are implemented. |
| `RULE-AUDIO-004` | Loading the general sound effects | supported | complete | None | None | implemented | None |
| `RULE-AUDIO-005` | Playing a sound effect, which cuts off the one playing | supported | complete | None | None | implemented | One effect voice, each new cue stopping the one before it. |
| `RULE-AUDIO-006` | The turn-start sound | supported | partial | None | None | supported | The original plays the cue only in network games of its own protocol, on the host and on joined computers; whether the rebuild plays it in other games, or with effects off (BUG-AUDIO-001), was not checked. |
| `RULE-AUDIO-007` | The Comlink alert plays slot 6 through the effects gate | supported | complete | None | None | implemented | None |
| `RULE-AUDIO-008` | The Comlink alert repeats every 24 presentation ticks | supported | complete | None | None | implemented | The repeat is paced by the rebuild's presentation clock at four seconds. |
| `RULE-AUDIO-009` | The sound of an attack in Detailed Combat | supported | complete | None | None | implemented | None |
| `RULE-AUDIO-010` | The startup drive check always passes and the game never looks for its disc | supported | missing | None | `DEV-AUDIO-001` | supported | The rebuild plays the music files and never checks a drive (deviation). |

## VIDEO

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-VIDEO-001` | Smacker movies DATA/MVINTRO and DATA/MVLOGOS | supported | complete | None | `DEV-VIDEO-001` | implemented | The rebuild decodes only the subset the two shipped movies use. |
| `RULE-VIDEO-001` | The intro plays the logos movie and then the intro movie, each ended by the left button | supported | partial | None | `DEV-VIDEO-001`, `DEV-VIDEO-002`, `DEV-VIDEO-003` | supported | Both movies play in order, centred, at 100 ms a frame, and a missing movie is skipped. Skipping on keys and either button is a mandatory deviation; with Intro only once off, its default, the movies play at every start; switching it on gives DEV-VIDEO-003. The movie sound follows the Sound Effects level; the level-to-volume curve was not compared. |

## HELP

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-HELP-001` | WinHelp container HELP/Chaos.hlp | supported | complete | None | `DEV-HELP-001`, `DEV-HELP-002` | implemented | None |
| `FMT-HELP-002` | Help contents file HELP/CHAOS.CNT | supported | complete | None | None | implemented | None |
| `RULE-HELP-001` | Help Topics does nothing, and no key opens the help file | supported | missing | None | `DEV-HELP-001` | supported | The rebuild opens its own help viewer where the original does nothing (deviation). |

## SAVE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-SAVE-001` | Full save file | supported | missing | None | `DEV-NET-001`, `DEV-SAVE-001` | supported | Reading or writing original saves is a declared non-goal; the rebuild has its own save format. |
| `FMT-SAVE-002` | Short M10W save file | supported | missing | None | `DEV-SAVE-001` | supported | Reading or writing original saves is a declared non-goal. |

## STATE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-STATE-001` | Gang record, one per player and roster slot | supported | partial | None | None | supported | The rebuild keeps each gang's fields as named state rather than this 32-byte layout, and marks an empty roster slot with Force 0 rather than sector 100. |
| `FMT-STATE-002` | Sector record, one per city sector | supported | partial | None | None | supported | The rebuild keeps sector owner, Income, Tolerance, sites and site benefits as named state rather than this 36-byte layout; whether it keeps a base Tolerance apart from the shown one, the research level and the seen-gang bytes was not checked. |
| `FMT-STATE-003` | Per-gang combat record of the last resolution | supported | partial | None | None | supported | The rebuild derives combat presentation from combat events and clip forces rather than keeping these 10-byte records. |
| `FMT-STATE-004` | Site slot in a sector record | supported | partial | None | None | supported | The rebuild keeps remaining Resistance per site plus an explicit influencer identity that the original does not store. |
| `FMT-STATE-005` | Comlink message record | supported | partial | None | None | supported | The rebuild keeps the Comlink inbox with the same occupied, read, turn, sender and text content, not this 166-byte layout. |
| `FMT-STATE-006` | Last Turn report record | supported | partial | None | None | supported | The rebuild keeps its own notification history and derives the Last Turn reports from it, not this 10-byte layout; whether its arguments match the report types' arguments was not checked. |
| `FMT-STATE-007` | Computer player planning record, one per player and roster slot | supported | missing | None | None | supported | The rebuild's computer players keep their own planning state; it was not compared with this 16-byte layout. |
| `FMT-STATE-008` | Combat result row of one sector | supported | missing | None | None | supported | The rebuild keeps combat events with combatant details instead of these per-sector rows of gang and target indices. |
| `FMT-STATE-009` | Input event record | supported | missing | None | None | supported | Not yet compared with the rebuild. |

## RNG

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-RNG-001` | The generator, its step, and its seed at process start | supported | partial | None | `DEV-RNG-001` | supported | The rebuild seeds from the low 16 bits of a process-uptime clock when the game object is created, not timeGetTime at process start, and takes an explicit full-width seed for replays, tests and online matches. |
| `RULE-RNG-002` | roll(n) gives a whole number from 1 to n from three draws | supported | complete | None | None | implemented | None |

## TURN

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-TURN-001` | A turn is turn start, planning by each active player in slot order, then resolution | supported | complete | None | None | implemented | None |
| `RULE-TURN-002` | Resolution carries out the orders in a fixed order of steps, each visiting players and roster slots in ascending order | supported | complete | None | None | implemented | None |
| `RULE-TURN-003` | The instant phase carries out Bribe, Heal, Hide, Influence, Research and Snitch gang by gang, then clamps every base Tolerance to 1..40 | supported | partial | None | None | supported | The rebuild's clamp has no ceiling of 40 (RULE-TOLERANCE-002). |
| `RULE-TURN-004` | At turn start, recurring actions that can no longer apply are cleared and the rest become the gangs' actions | supported | complete | None | `DEV-TURN-001` | implemented | None |
| `RULE-TURN-005` | Giving a gang an order replaces its whole previous order, one-off or recurring | supported | partial | None | `DEV-TURN-001` | supported | The rebuild rejects recurring Bribe and Snitch from any path, and it is not recorded whether its sector-wide order leaves Research out of the recurring choices. |
| `RULE-TURN-006` | The end of a turn removes eliminated players, reports each elimination to every player, then evaluates the objective | supported | complete | None | None | implemented | The rebuild marks retired gangs with Force 0 and cleared orders instead of sector 100, which changes no playable state. |

## SETUP

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-SETUP-001` | A new match gives every player $20, or $500 in Armageddon, and $1,500 to a player with the cash modifier name | supported | complete | None | `DEV-SETUP-001` | implemented | None |
| `RULE-SETUP-002` | A fresh local setup selects the stored scenario preference, which is Greed when nothing is stored, and a one-year time limit | supported | partial | None | None | supported | The rebuild starts every setup on Kill 'Em All and keeps no scenario preference; value 0 is Greed (FND-SETUP-013, FND-OBJECTIVE-003). The rebuild numbers Eliminate 6 and Siege 7, the reverse of the original, but the number leaves the rebuild only in its own save files, state fingerprint and multiplayer settings, which only the rebuild reads, so nothing has to match the original's numbering. |
| `RULE-SETUP-003` | Begin turns every empty setup slot into a computer player with an unused random portrait and that portrait's name | supported | complete | None | None | implemented | None |
| `RULE-SETUP-004` | A new match draws every slot's reaction, sets the research, generates the city, the headquarters and the Right Hands, then applies the name modifiers | supported | complete | None | None | implemented | Reactions for all six slots, then the city, headquarters and Right Hands. Research is set after the city, which changes nothing because it makes no draw and neither step reads what the other writes. The initial-state fixture from the original that would confirm the draw order is still pending. |
| `RULE-SETUP-005` | A player named with the island modifier puts every neutral sector under a Crackdown that never ends | supported | complete | None | `DEV-SETUP-001` | implemented | None |
| `RULE-SETUP-006` | A player named with either extra-gang modifier starts with five more Force-10 gangs in its headquarters | supported | complete | None | `DEV-SETUP-001` | implemented | None |
| `RULE-SETUP-007` | A player named with the visibility modifier sees every opposing gang for the whole match | supported | complete | None | `DEV-SETUP-001` | implemented | None |
| `RULE-SETUP-008` | A local human's planning opens with the Ready card when several humans share the computer, then Game Information, combat results and Last Turn Events | supported | partial | None | None | supported | The handoff order is Combat, Events, planning; whether the once-only Game Information panel and the Comlink scan follow the entry's order was not checked. |
| `RULE-SETUP-009` | A press on a setup player card selects it first, then works its portrait arrows or name, and a drag moves or swaps whole players | supported | partial | None | None | supported | The rebuild's portrait arrows step through every portrait instead of skipping the ones other slots hold (FND-SETUP-013). |
| `RULE-SETUP-010` | The first local setup of a session starts with one human, later ones with the last roster begun, and Add and Remove change the number of local humans from one to six | supported | partial | None | None | supported | Add does not give the new human the lowest free portrait, and whether the rebuild reopens setup with the roster of the last Begin was not checked (FND-SETUP-013). |
| `SCR-SETUP-001` | Full local game setup screen with scenario, settings and six player cards | supported | complete | None | `DEV-SETUP-002` | implemented | The left panel follows FND-SETUP-013: its rectangles, the refusal area of the time limit, the pressed images and push cue, the commit on release inside, and the light sprite. The stored scenario preference belongs to RULE-SETUP-002. |
| `SCR-SETUP-002` | Hot-seat handoff card that waits for the next local player to press Ready | supported | complete | None | `DEV-SETUP-002` | implemented | None |

## CITY

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-CITY-001` | A new city's sector Income comes from a random density field, and its starting Tolerance is 17 minus the Income | supported | complete | None | None | implemented | No original initial-city fixture has been captured. |
| `RULE-CITY-002` | Each sector's three sites are drawn uniformly and redrawn until they differ and their modifiers stay within six either way | supported | complete | None | None | implemented | No original initial-city fixture has been captured. |
| `RULE-CITY-003` | The six players get the six fixed headquarters sectors in a random order, and each headquarters' first site becomes the headquarters site | supported | complete | None | None | implemented | None |
| `RULE-CITY-004` | Each player's Right Hands starts in roster slot 0 in its headquarters at Force 10 with no equipment | supported | complete | None | None | implemented | None |

## HIRE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-HIRE-001` | Hires and snubs are carried out player by player and offer slot by offer slot | supported | complete | None | None | implemented | None |
| `RULE-HIRE-002` | Vacant hire offers are refilled in place at the player's planning entry | supported | complete | None | None | implemented | None |
| `RULE-HIRE-003` | A human player holds at most one hire or snub order, set by dragging an offer or pressing Reject | supported | complete | None | `DEV-HIRE-001` | implemented | The rebuild refuses a drop on a sector already holding six friendly gangs, which the original accepts (see deviations). |
| `RULE-HIRE-004` | A new match starts with every hire offer vacant and no hire order | supported | complete | None | None | implemented | None |
| `SCR-HIRE-001` | Hire comparison panel showing the three offers side by side | supported | complete | None | None | implemented | The positions the rebuild took from captures match the ones the spec now records. |
| `SCR-HIRE-002` | Hire offers on the main console, with drag-to-hire and Reject | supported | partial | None | `DEV-HIRE-001`, `DEV-HIRE-002` | supported | Adds a hire-shortfall warning and refuses drops on full sectors (see deviations). The rebuild draws the portraits one pixel left of and two pixels above the recorded cells, and crops the hire and snub marks from other rectangles of the image. |

## HIDE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-HIDE-001` | A gang hides while its action is Hide, and each Hide carried out is counted for its player | supported | complete | None | None | implemented | None |

## INFLUENCE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-INFLUENCE-001` | Each Influence gang rolls on its own and adds its successes to the site's progress at once | supported | complete | None | None | implemented | None |
| `SCR-INFLUENCE-001` | Influence picker for choosing one of the sector's three sites | supported | complete | None | None | implemented | None |

## HEAL

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-HEAL-001` | Heal rolls four dice plus the gang's Heal and adds each success to Force, up to 10 | supported | complete | None | None | implemented | None |

## RESEARCH

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-RESEARCH-001` | Each Research gang rolls Force plus Research and takes its successes off the item's remaining research at once | supported | complete | None | None | implemented | None |
| `RULE-RESEARCH-002` | A new match starts each player with each item's research difficulty, or with every item researched in Armageddon | supported | complete | None | None | implemented | The rebuild leaves the item table's padding records out of the researched set (see deviations). |
| `SCR-RESEARCH-001` | Research panel with item categories and a fixed sixteen-row item list | supported | partial | None | `DEV-RESEARCH-001` | supported | The list filter follows FND-RESEARCH-003: category from item type (types 0 and 1 together), item order, only unfinished items, Tech Level at most the gang type's and at most 5 or 8 by the research-site level of a sector the player owns. The category cells match. A press on the list is taken from panel y 19 where the original's press region starts at y 26. |

## BRIBE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-BRIBE-001` | Bribe pays 3 cash to raise the gang's sector base Tolerance by 3 | supported | partial | None | None | supported | The rebuild keeps one Tolerance per sector, so a Bribe already protects the sector in the same turn's Chaos test, and it does not wrap the byte. |

## SNITCH

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-SNITCH-001` | Snitch lowers the gang's sector base Tolerance by 3, free and whatever the player's cash | supported | partial | None | None | supported | The rebuild keeps one Tolerance per sector, so a Snitch already reaches the same turn's Chaos test, and it does not wrap the byte. |

## TOLERANCE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-TOLERANCE-001` | At the start of each resolution a sector's base Tolerance moves one point toward 17 minus its base Income | supported | partial | None | None | supported | The rebuild moves its single combined Tolerance during Upkeep toward a normal value that includes the sites' Tolerance. |
| `RULE-TOLERANCE-002` | After the instant phase every sector's base Tolerance is clamped to 1..40 | supported | partial | None | None | supported | The rebuild raises Tolerance below 1 to 1 and has no ceiling of 40. |

## SITE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-SITE-001` | Before planning, each sector record is rebuilt from its completed sites, whose bonuses go to the owner's gangs there | supported | complete | None | None | implemented | The rebuild also keeps an explicit influencer per site, checked against the sector owner, which the original does not store. |

## MOVE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-MOVE-001` | Move pass carries out every Move, player by player, after normalizing each player's destinations | supported | complete | None | `DEV-MOVE-001` | implemented | None |
| `RULE-MOVE-002` | Move destinations are rewritten until no sector would hold more than six of the player's gangs | supported | complete | None | `DEV-AI-002`, `DEV-MOVE-002` | implemented | The fallback for a mover already sent back draws a random neighbour (RULE-AI-007); after 256 of them DEV-MOVE-002 applies. |
| `SCR-MOVE-001` | Move panel | supported | partial | None | `DEV-MOVE-001`, `DEV-UI-003`, `DEV-UI-008` | supported | Panel resource and neighborhood cells are the original's; the controls, keys and destination marker are not pinned by findings. |

## CONTROL

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-CONTROL-001` | Control pools each player's strength per sector and settles contested sectors in ascending order, with the owner's defense added to its own pool and a neutral candidate at a zero margin | supported | partial | None | `DEV-CONTROL-001`, `DEV-CONTROL-002` | supported | Candidates are limited to players with a Control order and a Crackdown sector gets an explicit failed result (see deviations). FND-CONTROL-003 changed the procedure (only contested sectors without police are settled; the defense joins the owner's pool); the rebuild has not been checked against it. |

## GANG

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-GANG-001` | Each active gang's fourteen statistics are its definition's, plus its items', plus its owned sector's completed sites', and Combat also takes the skills that go with its weapon | supported | partial | None | None | supported | The rebuild adds the weapon skills when an attack is computed instead of storing them in Combat (FND-GANG-007), so other readers of the stored Combat can differ. |
| `RULE-GANG-002` | A gang that dies or is terminated has only its sector byte set to inactive | supported | partial | None | None | supported | Force 0 and cleared orders stand in for the sector byte 100. |
| `SCR-GANG-001` | Compact gang information panel opened from the Attack, Equip, Research, Sell and Give panels | supported | partial | None | None | supported | Field columns and rows follow the original; which statistic sits on which row is not recorded. |
| `SCR-GANG-002` | Gang information panel for a hired gang | supported | partial | None | `DEV-GANG-001` | supported | Value columns follow the original; the rows, equipment and portrait positions are not recorded, and the rebuild adds breakdown tooltips. |

## EQUIP

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-EQUIP-001` | Equip pays the item's price from the cash the player has at that point, and replaces the item in the matching slot | supported | complete | None | `DEV-EQUIP-001` | implemented | Cash is tested at the gang's place in the roster scan; the Revised rules setting tests it in submission order instead. The failed-Equip report is compared under RULE-EVENT-014. |
| `RULE-EQUIP-002` | The transaction pass carries out Equip, Give and Sell by player and roster slot, and delivers gifts after each player's scan | supported | complete | None | `DEV-EQUIP-001` | implemented | The rebuild empties every giver's slots before the scan and delivers after all players' scans; no Equip or Sell reads a giver's or recipient's slot in between, so the result is the same. The Revised rules setting resolves Equip and Sell in submission order. |
| `RULE-EQUIP-003` | An item's price is its Cost, less a third of it rounded down when the buyer owns the sector and its Factory is complete | supported | complete | None | None | implemented | None |
| `RULE-EQUIP-004` | The Equip list offers researched items of the chosen category within the gang's Tech Level that the gang does not already carry | supported | complete | None | None | implemented | None |
| `SCR-EQUIP-001` | Equip panel | supported | partial | None | `DEV-EQUIP-002` | supported | Category cells and list area follow the original; the controls, fonts and keys are not pinned, and the rebuild adds a held-items row. |

## GIVE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-GIVE-001` | Give empties the giver's selected slots and holds the items for delivery to the recipient after the player's scan | supported | complete | None | None | implemented | None |
| `SCR-GIVE-001` | Give panel | supported | partial | None | `DEV-GIVE-001` | supported | Item cells, recipients and keys follow the original; the Cancel control and markers are not pinned, and the rebuild adds Up/Down cycling. |

## SELL

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-SELL-001` | Sell removes every selected item but pays half the Cost of only the last selected slot | supported | complete | None | `DEV-EQUIP-001` | implemented | Keeps BUG-SELL-001: a multi-item Sell pays only the last selected slot, so no deviation covers it. |
| `SCR-SELL-001` | Sell panel | supported | partial | None | None | supported | Row targets follow the original; the OK and Cancel controls and the drawn prices are not pinned. |

## TERMINATE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-TERMINATE-001` | Terminate pass retires every gang ordered to Terminate, before any Move | supported | partial | None | None | supported | The rebuild also sets Force to 0 and clears orders and Hidden (see deviations). |

## UPKEEP

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-UPKEEP-001` | Upkeep charges each active gang its Upkeep and pays each owned sector's Cash byte, player by player | supported | complete | None | None | implemented | None |

## FINANCE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-FINANCE-001` | The Financial panel projects next turn's cash flow for the whole city or one sector | supported | complete | None | `DEV-FINANCE-001` | implemented | Each row follows FND-FINANCE-002 except the Sell credit of DEV-FINANCE-001: terminating gangs give back their Upkeep, the Sector variant charges a moving gang to its destination, and the Chaos row is a third of Income + Chaos + Force, halved outside the player's sectors. |
| `SCR-FINANCE-001` | Financial panel, City and Sector | supported | partial | None | `DEV-FINANCE-001`, `DEV-UI-006` | supported | Panel, close control, field positions and the row assignment of FND-FINANCE-002 follow the original; the row labels in the template images were not read. |

## ATTACK

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-ATTACK-001` | One gang's attack and the retaliation it provokes | supported | partial | None | `DEV-HELP-002` | supported | The rebuild lowers the attitude only for an attack that was not evaded, where the original also lowers it for an evaded attack, and its Martial Arts test on the attacker is `> 0` where the original tests `== 0` (FND-COMBAT-008, FND-AI-047). |
| `RULE-ATTACK-002` | An Attack can target only an enemy gang the attacker's player sees in the attacker's sector | supported | complete | None | `DEV-ATTACK-002` | implemented | None |
| `SCR-ATTACK-001` | Attack picker (Target Acquisition) | supported | partial | None | `DEV-ATTACK-001`, `DEV-UI-008` | supported | Opponent and target hit maps are the original's. FND-ATTACK-003 now records the panel origin, the Confirm and Cancel faces, Enter, plus and Escape, the acting gang's portrait and equipment and the initial selection, and FND-ATTACK-004 the double-click information panels; the rebuild was not compared with them. |

## COMBAT

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-COMBAT-001` | A gang's Combat takes the skills that match its weapon when its statistics are rebuilt | supported | partial | None | None | supported | The rebuild adds the weapon skills when an attack is computed instead of storing them in Combat (FND-GANG-007), so other readers of the stored Combat can differ. |
| `RULE-COMBAT-002` | The combat phase runs every attack, then the police, then applies the damage and fills the combat records | supported | partial | None | None | supported | Damage, deaths and order follow the rule, but the rebuild keeps combat events with combatant details instead of the per-gang combat records and per-sector result rows. |
| `RULE-COMBAT-003` | Damage Inflicted counts the full damage of every opening attack and no retaliation | supported | complete | None | None | implemented | None |
| `RULE-COMBAT-004` | Detailed Combat plays the viewer's fights sector by sector, one clip per attack | supported | complete | None | `DEV-COMBAT-002` | implemented | None |
| `SCR-COMBAT-001` | Combat Results panel, paged by sector | supported | partial | None | `DEV-COMBAT-002` | supported | Paging, opponent strip and exit follow the original. FND-COMBAT-012 now records the arrow and Enter/plus keys (Escape is not handled), the force selector's focus and its outlines, and the grid cells' portraits and tracks; the rebuild was not compared with them. |
| `SCR-COMBAT-002` | Detailed Combat panel | supported | partial | None | `DEV-COMBAT-001` | supported | Layout, strips and 166 ms cadence are implemented. FND-COMBAT-010 now records the track positions, the portrait cells, the Exit face and Escape, which end the whole presentation; the rebuild was not compared with them. The portraits come from `PX03000` (FND-COMBAT-013). |

## DETECT

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-DETECT-001` | A player sees an enemy gang when its Stealth is at most the player's detection strength in that sector | supported | complete | None | None | implemented | None |

## CHAOS

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-CHAOS-001` | Chaos is rolled gang by gang, and a sector whose Chaos exceeds its Tolerance gets a Crackdown | supported | complete | None | None | implemented | None |
| `RULE-CHAOS-002` | Chaos pays one cash per success, halved once per player and sector outside the player's own sectors | supported | partial | None | None | supported | The rebuild pays nothing in a sector under police presence, where the original pays unless the sector cracked down this turn (FND-CHAOS-002); whether it skips gangs killed in this turn's combat was not checked. |

## POLICE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-POLICE-001` | In a Crackdown sector the police may find each gang and attack it with 25 minus its Defense in dice | supported | complete | None | None | implemented | None |
| `RULE-POLICE-002` | A Crackdown is recorded in the sector's history, and a third within five turns neutralizes the sector and adds 3 to 5 turns of police | supported | partial | None | None | supported | The rebuild adds police presence, with its draw, on every Crackdown, where the original does so only on the third (FND-POLICE-004). It also resets more on neutralization (Support, Tolerance modifiers, resistance) than the original, which clears only the three site progress bytes (FND-CHAOS-002). |
| `RULE-POLICE-003` | Police presence counts down by one at the end of every turn unless it is permanent | supported | complete | None | None | implemented | None |
| `RULE-POLICE-004` | Crackdown reports go to the players who had a gang in the sector when resolution began | supported | complete | None | None | implemented | None |

## AI

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-AI-001` | A computer player's planning pass rolls its gangs' action history, dispatches every gang, then hires | supported | partial | None | `DEV-AI-003` | supported | The rebuild submits commands in roster slot order and keeps the history rollover, but applies the family table at every pass without needs_family (FND-AI-042); the takeover's raider_mode is not checked. |
| `RULE-AI-002` | The per-gang AI dispatcher sets the gang's family from scenario and hire role, then runs that family's handler | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The rebuild applies the family table to every active gang at every pass, without the needs_family gate, the record reset, the family 99 of a blank cell or the Big Man first-turn hire role (FND-AI-041). |
| `RULE-AI-003` | Each planning pass refreshes a computer player's gang counts, sector danger and combat-advantage hostility | supported | complete | None | None | implemented | None |
| `RULE-AI-004` | Queries the computer players' handlers share | supported | complete | None | None | implemented | hostile_owner reads the attitude cell of owner_query, with the out-of-row reads of FND-AI-048 for a neutral sector and one under police presence. |
| `RULE-AI-005` | How a computer player picks a weapon, armor or miscellaneous upgrade, and when danger calls for one | supported | partial | None | None | supported | FND-AI-055 changes the reading: the weapon choice starts from the equipped weapon, family 10's armor is chosen by Stealth, and the miscellaneous upgrades compare Detect (families 11 and 12) or Control (13 and 14). The rebuild chooses family 12's item by Chaos; the other choices are not checked against it. |
| `RULE-AI-006` | The shared AI sector selector scores the nearest sectors by mode and routes one step toward the best | supported | partial | None | None | supported | Mode 4 is now written out (FND-AI-056); no call reaches it, and the rebuild's version is not checked against it. |
| `RULE-AI-007` | Sector selector mode 0 picks a random neighbouring sector | supported | complete | None | None | implemented | Mode 0 draws one of the eight neighbours with roll(8) and draws again off the map, with no capacity test. |
| `RULE-AI-008` | A computer player ranks its three hire offers by the mode of its hire role | supported | complete | None | None | implemented | None |
| `RULE-AI-009` | A computer player that hires nothing snubs one offer, the first in Greed and the least efficient elsewhere | supported | complete | None | None | implemented | None |
| `RULE-AI-010` | A computer player picks a hire role from its scenario's turn schedule, then hires, places or snubs | supported | partial | None | `DEV-AI-001` | supported | The hunter guards compare the previous hire role with the schedule slot number, as the original does (BUG-AI-001); the Revised rules setting compares it with role 4. The per-scenario slot adjustments and the hunter reversion of FND-AI-050 are not checked against the rebuild. |
| `RULE-AI-011` | A computer player tries to hire only below a gang limit and outside each scenario's closing turns | supported | complete | None | None | implemented | None |
| `RULE-AI-012` | The AI hire destination helper writes an encoded sector directly, and has two random modes nobody reaches | supported | complete | None | None | implemented | None |
| `RULE-AI-013` | A computer player keeps one hire placement sector and replaces it by fixed scans when it stops being a good base | supported | partial | None | None | supported | Placement is carried out in the Hire phase. The rebuild always replaces an anchor of 63, where the original keeps it for player 0 while sector 0, 6, 7 or 8 is free land (FND-AI-051). |
| `RULE-AI-014` | A new match starts every attitude at 0, or at Homicidal Maniac at -10 toward humans and +10 toward computers | supported | complete | None | None | implemented | None |
| `RULE-AI-015` | At the start of each turn's resolution every attitude below +10 rises by 1, except at Homicidal Maniac | supported | complete | None | None | implemented | None |
| `RULE-AI-016` | Every Attack order lowers the target player's attitude toward the attacker by the larger of its reaction and the opening damage | supported | partial | None | None | supported | The rebuild lowers the attitude only for attacks that are not evaded; the original also lowers it by the reaction after an evaded attack (FND-AI-047). |
| `RULE-AI-017` | A Control takeover lowers the previous owner's attitude toward the new owner by twice its reaction | supported | complete | None | None | implemented | None |
| `RULE-AI-018` | A new match gives computer players difficulty band 0 at Goon, 1 at Criminal and 2 at Crime Lord and Homicidal Maniac | supported | complete | None | None | implemented | None |
| `RULE-AI-019` | Family-0 computer gangs heal, raise Chaos, probe weak enemies or wander, by previous action, and turn aggressive after two moves | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The rebuild counts previous Hide where the original counts previous Chaos and writes Chaos (FND-AI-046), and groups the previous actions differently from the jump table (FND-AI-048). |
| `RULE-AI-020` | Family-1 computer gangs heal, raise Chaos, snitch, take sectors or wander, by previous action, cash and Mentality | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The branch after Attack, Hide or Move, the fall-through of the crime gate to the Goon test, the neutral-owner read of owner_is_human and the needs_family write with the Greed Terminate (FND-AI-057) are not checked in the rebuild. |
| `RULE-AI-021` | Family-2 computer gangs equip, heal, attack visible hostile gangs and take weak or hostile sectors | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The owner query in the owned-sector test, the owner_is_human late gate and the needs_family write with the Greed Terminate (FND-AI-058) are not checked in the rebuild. |
| `RULE-AI-022` | Family-3 computer gangs influence the best Cash site in owned land, take sectors or move toward Cash | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The needs_family write with the Greed Terminate (FND-AI-042) is not checked in the rebuild. |
| `RULE-AI-023` | Family-4 computer gangs raise Chaos in owned land, probe weak enemies and move through sector selector mode 2, and no match reaches them | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The rebuild counts previous Hide where the original counts previous Chaos and writes Chaos, and groups the previous actions differently from the jump table (FND-AI-049). No match writes family 4, so the handler matters only for a loaded planning record. |
| `RULE-AI-024` | Family-5 computer gangs influence the best Support site in owned land, take sectors or move toward Support | supported | complete | None | `DEV-AI-002`, `DEV-AI-003` | implemented | None |
| `RULE-AI-025` | Family-6 computer gangs hunt sectors with visible hostile human gangs and fight there | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The end marker 100 of the guard target list, which sends the gang toward a random sector when every weight-10 sector is covered, and the needs_family write with the Greed Terminate (FND-AI-059) are not checked in the rebuild. |
| `RULE-AI-026` | Family-7 computer gangs sit where sites add the most Research, influence Research sites and research items in a fixed cycle | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The needs_family write with the Greed Terminate (FND-AI-042) is not checked in the rebuild. |
| `RULE-AI-027` | Family-9 computer gangs equip without waiting, leave owned land, and fight or take other players' sectors | supported | complete | None | `DEV-AI-002`, `DEV-AI-003` | implemented | None |
| `RULE-AI-028` | Family-10 computer gangs improve armor, equip item 44, heal, seek Stealth sites, then raise Chaos or hide | supported | complete | None | `DEV-AI-002`, `DEV-AI-003` | implemented | None |
| `RULE-AI-029` | Family-11 computer gangs equip, heal, attack the first visible definition-0 gang, or move in blocks of six behind a leader | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The previous-action test on the miscellaneous Equip and the Heal, the owner query in the owned-sector test and the focus writes (FND-AI-061) are not checked in the rebuild. |
| `RULE-AI-030` | Family-12 computer gangs equip and heal when unopposed, wander at random, and attack when opposed | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The needs_family write with the Greed Terminate (FND-AI-042) is not checked in the rebuild. |
| `RULE-AI-031` | Family-13 and family-14 computer gangs move to the Big Man or Siege objectives, fight for them on alternate turns and hold them | supported | partial | None | `DEV-AI-002`, `DEV-AI-003` | supported | The five contested draws, the missing write after a failed attack with the Heal test failing, the hostile-owner pool test and the unset Support threshold (FND-AI-062, BUG-AI-006) are not checked in the rebuild. |

## EVENT

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-EVENT-001` | The Last Turn reports are cleared just before each resolution | supported | complete | None | None | implemented | None |
| `RULE-EVENT-002` | Recording a Last Turn report keeps the first 32 reports of a resolution | supported | complete | None | None | implemented | The rebuild keeps a fuller notification history and derives the first 32 reports of the completed turn from it. |
| `RULE-EVENT-003` | An elimination is reported to all six player slots | supported | partial | None | None | supported | Whether the rebuild records the elimination report for all six slots, empty and computer slots included, was not checked. |
| `RULE-EVENT-004` | A Crackdown is reported to each player who had a gang in its sector | supported | partial | None | None | supported | Whether the rebuild judges Crackdown recipients by gang presence at the start of resolution was not checked. |
| `RULE-EVENT-005` | The Last Turn Events panel shows the viewer's recorded reports in the order they were recorded | supported | complete | None | None | implemented | None |
| `RULE-EVENT-006` | A completed site is reported to the player whose Influence completed it | supported | complete | None | None | implemented | None |
| `RULE-EVENT-007` | A completed item is reported to the player whose Research completed it | supported | complete | None | None | implemented | None |
| `RULE-EVENT-008` | A Bribe that fails for lack of cash is reported to its player | supported | complete | None | None | implemented | None |
| `RULE-EVENT-009` | A Hire that fails for lack of cash is reported to its player | supported | complete | None | None | implemented | None |
| `RULE-EVENT-010` | A Hire refused because its sector is full is reported to its player | supported | complete | None | None | implemented | None |
| `RULE-EVENT-011` | A Hire refused because the player has the most gangs allowed is reported to its player | supported | complete | None | None | implemented | None |
| `RULE-EVENT-012` | Taking control of a sector is reported to the new owner | supported | partial | None | None | supported | The rebuild emits its own control notifications; the report type and its arguments are not checked against the original. |
| `RULE-EVENT-013` | Losing control of a sector is reported to the previous owner | supported | partial | None | None | supported | The rebuild emits its own control notifications; the report type and its arguments are not checked against the original. |
| `RULE-EVENT-014` | An Equip that fails for lack of cash is reported to its player | supported | partial | None | None | supported | The rebuild reports a failed Equip through its own notification; the report type and its arguments are not checked against the original. |
| `SCR-EVENT-001` | Last Turn Events panel | supported | complete | None | `DEV-EVENT-001`, `DEV-EVENT-002` | implemented | Positions of the status line, illustration and footer come from the rebuild's own measurements, not from findings. |

## COMLINK

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-COMLINK-001` | Storing a Comlink message keeps each player's newest 16 messages | supported | partial | None | `DEV-NET-001` | supported | Delivery to a recipient on another computer (packet type 10) is not implemented; the rebuild tracks read state per message rather than through a shifted cursor. |
| `RULE-COMLINK-002` | Comlink Send opens only when another human player can receive a message | supported | complete | None | None | implemented | None |
| `RULE-COMLINK-003` | Sending a Comlink message stores a copy for each selected recipient | supported | complete | None | None | implemented | None |
| `RULE-COMLINK-004` | Comlink View opens at the oldest unread message and refuses an empty inbox | supported | complete | None | None | implemented | None |
| `RULE-COMLINK-005` | Showing a Comlink message marks it read and dates it from its turn | supported | complete | None | None | implemented | None |
| `RULE-COMLINK-006` | Typing in Comlink Send overwrites a fixed grid of four rows of 40 upper-case characters | supported | complete | None | None | implemented | None |
| `RULE-COMLINK-007` | When a player finishes planning, the read messages at the front of the inbox are dropped | supported | missing | None | None | supported | The rebuild keeps read messages until the 16-message limit drops them. |
| `SCR-COMLINK-001` | Comlink View panel | supported | complete | None | `DEV-COMLINK-001` | implemented | None |
| `SCR-COMLINK-002` | Comlink Send panel | supported | complete | None | None | implemented | None |

## SEARCH

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-SEARCH-001` | Each player's Search filter starts empty and is changed by ALL, NONE and its rows | supported | complete | None | None | implemented | The rebuild also clears the filters when a save or replay is loaded; whether the original saves them is not known. |
| `RULE-SEARCH-002` | The city shows a marker for each site the viewer controls and for each other site of a type the viewer's Search filter selects | supported | complete | None | None | implemented | None |
| `SCR-SEARCH-001` | Search panel | supported | complete | None | `DEV-SEARCH-001` | implemented | None |

## OBJECTIVE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-OBJECTIVE-001` | At the end of each turn the scores are rebuilt, a lone surviving player ends the match, and then the scenario's own condition is tested | supported | complete | None | None | implemented | None |
| `RULE-OBJECTIVE-002` | Each player's scenario score is rebuilt from what the scenario counts, and a player's standing is the number of players with a higher score | supported | complete | None | None | implemented | None |
| `RULE-OBJECTIVE-003` | At the end of resolution, a player without the Right Hands in Eliminate loses everything, and any player with no sector and no gang leaves the match | supported | complete | None | None | implemented | None |
| `RULE-OBJECTIVE-004` | Each scenario's own end condition, and the Dominance weights | supported | partial | None | None | supported | The rebuild tests Big 40, Siege, Big Man and Armageddon for living players only where the original counts every slot, and gives Kill 'Em All and Eliminate tests of their own where the original has none (FND-OBJECTIVE-003). The timed test was not compared. The rebuild numbers Eliminate 6 and Siege 7, the reverse of the original, but the number leaves the rebuild only in its own save files, state fingerprint and multiplayer settings, which only the rebuild reads, so nothing has to match the original's numbering. |
| `RULE-OBJECTIVE-005` | An eliminated local human sees the elimination card at that player's place in the slot order, behind the Ready card when several humans play | supported | partial | None | None | supported | The card and its place in slot order match. The rebuild keeps the gameplay music over the card, where the original starts the endgame music and only a later local human's request brings the gameplay music back. With two or more local humans all eliminated, the rebuild plays the computers on to the end and shows the awards where the original returns to the title without them, and it shows the Ready card before the last human's card. A lone local human's elimination ends the match at resolution, which skips the awards the original shows when the match would have ended that turn anyway. |
| `SCR-OBJECTIVE-001` | Player Rankings panel with one vertical rail per player and portraits placed by score | supported | partial | None | None | supported | The rebuild places each portrait 28 pixels lower per standing where the original places it in proportion to the score's distance from the leader, over 140 pixels (FND-OBJECTIVE-005). |
| `SCR-OBJECTIVE-002` | Private elimination card shown to an eliminated local human over the city screen | supported | complete | None | `DEV-SETUP-002` | implemented | None |

## AWARDS

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-AWARDS-001` | The endgame awards go to every player tied at the extreme of each statistic, with activity thresholds for the first three | supported | complete | None | None | implemented | None |
| `RULE-AWARDS-002` | The endgame lists players by standing, ties in slot order, eliminated players last, and shows a victory splash first when one player is left | supported | partial | None | None | supported | The rebuild shows its notice when one human played, for that human, where the original shows the splash of the lone active player, human or computer, on the Awards tab (FND-AWARDS-004). |
| `SCR-AWARDS-001` | Endgame screen listing the players by place with their awards or their statistics | supported | complete | None | `DEV-SETUP-002` | implemented | Row typography and timing are unconfirmed against captures of the original. |
| `SCR-AWARDS-002` | Victory splash shown on the endgame's Awards tab when one player is left | supported | partial | None | None | supported | Shown under the rebuild's one-human test instead of the one-active-player test (FND-AWARDS-004); whether it takes the place of the Awards tab was not checked. |

## TIMER

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-TIMER-001` | Planning time limit chosen for a match | supported | complete | None | None | implemented | None |
| `RULE-TIMER-002` | A human planning turn ends when its time limit passes | supported | complete | None | None | implemented | On expiry the rebuild submits the finish-planning operation without the idle-gang warning. |
| `RULE-TIMER-003` | The planning clock bar and its warning sounds | supported | complete | None | None | implemented | Checks run every sixth fixed update rather than every sixth presentation tick; the two rates were not compared. |
| `RULE-TIMER-004` | Presentation waits last until the next tick of the six-per-second clock, and only the panel slide step depends on the machine's speed | supported | missing | None | None | supported | Not yet compared with the rebuild. |

## UI

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-UI-001` | A push-button control acts only when released inside | supported | complete | None | None | implemented | None |
| `RULE-UI-002` | Routing a press on the main console | supported | complete | None | None | implemented | None |
| `RULE-UI-003` | Panels slide in from the right and out to the right | supported | partial | None | `DEV-UI-001` | supported | The slide-in follows the benchmark step; the slide-out is not animated (deviation). |
| `RULE-UI-004` | Drawing numbers in fixed glyph cells | supported | complete | None | None | implemented | None |
| `RULE-UI-005` | Lengths of the site progress and Force meters | supported | partial | None | None | supported | Detailed-sector meters are drawn; whether their lengths use the same integer arithmetic was not checked. |
| `RULE-UI-006` | Choosing a sector's gang-status marker | supported | partial | None | None | supported | Status art is drawn for every active-gang sector; the frame precedence was not checked against the rule. |
| `RULE-UI-007` | The pointer shape | supported | partial | None | None | supported | The framework's standard pointer is used; the wait cursor during blocking work is not reproduced. |
| `RULE-UI-008` | The presentation timer | supported | partial | None | None | supported | The rebuild uses its own fixed update rate; the 166 ms tick is not a separate clock. |
| `RULE-UI-009` | The texts of the Game Information panel | supported | partial | None | `DEV-AI-003` | supported | Game Information appends the AI policy label after Mentality (deviation); the other fields follow the original. |
| `RULE-UI-010` | Which gangs the detailed sector cards and Gangs in Sector list | supported | partial | None | None | supported | Which gangs the roster and card lists include was not checked against the rule. |
| `RULE-UI-011` | The sector values on the main console | supported | complete | None | `DEV-UI-007` | implemented | Income for all and Support and Cash for the owner are shown. |
| `RULE-UI-012` | Objective sectors marked on the city map | supported | complete | None | `DEV-UI-002` | implemented | The rebuild also draws the pylons on the detailed-sector minimap (deviation). |
| `RULE-UI-013` | The program starts one instance, chooses the image set and display depth, runs the title loop, and undoes its setup on the way out | supported | missing | None | None | supported | Not yet compared with the rebuild. |
| `RULE-UI-014` | Input reaches the screen loops as one polled event at a time, and the event step handles the option commands and window activation for every loop | supported | missing | None | None | supported | Not yet compared with the rebuild. |
| `SCR-UI-001` | Title screen | supported | partial | None | `DEV-UI-004`, `DEV-UI-012` | supported | The title screen shows the build version and an intro button the original lacks. |
| `SCR-UI-002` | Credits screen | supported | partial | None | None | supported | Whether the credits sequence matches the original presenter's order and timing was not checked. |
| `SCR-UI-003` | City screen and main console | supported | partial | None | `DEV-UI-005`, `DEV-UI-006` | supported | Console routes and pressed art match; tooltips and projected cashflow are added. |
| `SCR-UI-004` | Detailed sector screen | supported | partial | None | `DEV-UI-002`, `DEV-UI-003`, `DEV-UI-005`, `DEV-UI-007`, `DEV-UI-008`, `DEV-UI-013`, `DEV-UI-014` | supported | Adds tooltips, target highlights, ctrl-picking and minimap pylons. |
| `SCR-UI-005` | Gangs in Sector panel | supported | partial | None | `DEV-UI-001`, `DEV-UI-005`, `DEV-UI-010` | supported | Whether the compact all-gangs roster matches the original rows was not checked. |
| `SCR-UI-006` | Item Information panel | supported | complete | None | `DEV-UI-001`, `DEV-UI-005`, `DEV-UI-009`, `DEV-UI-010` | implemented | Item Information uses PX05001 with the 15-frame rotation; it can also be opened from Gang Information. |
| `SCR-UI-007` | Site Information panel | supported | complete | None | `DEV-UI-001`, `DEV-UI-005`, `DEV-UI-010` | implemented | None |
| `SCR-UI-008` | Game Information panel | supported | partial | None | `DEV-UI-001`, `DEV-UI-005`, `DEV-UI-010` | supported | Appends the AI policy label. |
| `SCR-UI-009` | Application menu bar | supported | missing | None | `DEV-HELP-001`, `DEV-OPTIONS-003`, `DEV-UI-011` | supported | The rebuild has no Windows menu bar; its commands live in the Escape menu, Options and shortcuts. |

## OPTIONS

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-OPTIONS-001` | Reading the options from the registry at startup | supported | partial | None | `DEV-OPTIONS-001`, `DEV-OPTIONS-002`, `DEV-OPTIONS-003`, `DEV-RNG-001` | supported | The rebuild reads a per-user preferences file with the same defaults except Slide Panels, and makes no serial-number draws (deviation). |
| `RULE-OPTIONS-002` | Saving the options to the registry, which always fails | supported | partial | None | `DEV-OPTIONS-001` | supported | The rebuild saves options reliably instead of failing (deviation). |
| `RULE-OPTIONS-003` | Warn if Idle Gangs asks before Done ends a turn with a gang left idle | supported | complete | None | None | implemented | The warning is skipped when the planning time runs out. |
| `SCR-OPTIONS-001` | Idle gang warning panel | supported | complete | None | `DEV-UI-001`, `DEV-UI-010` | implemented | Adds Escape and right-click cancel beyond the original keys. |

## NET

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `SCR-NET-001` | Legacy network host lobby that edits up to four seats and waits for the participants | supported | missing | None | `DEV-NET-001` | supported | Legacy network screens are deliberately not reproduced. |
| `SCR-NET-002` | Legacy network client session editor with four seats | supported | missing | None | `DEV-NET-001` | supported | Legacy network screens are deliberately not reproduced. |
| `SCR-NET-003` | Legacy network screen that waits for every participant to be ready | supported | missing | None | `DEV-NET-001` | supported | Legacy network screens are deliberately not reproduced. |
| `SCR-NET-004` | Legacy network transfer progress frame with a status line and a spinner | supported | missing | None | `DEV-NET-001` | supported | Legacy network screens are deliberately not reproduced. |
| `SCR-NET-005` | Legacy network turn synchronization frame with one progress row per seat and a spinner | supported | missing | None | `DEV-NET-001` | supported | Legacy network screens are deliberately not reproduced. |
