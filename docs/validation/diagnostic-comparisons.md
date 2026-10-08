# Diagnostic screen comparisons

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

<!-- doc-index:begin toc depth=2 -->
- [First-planning map comparison without the keyboard footer](#first-planning-map-comparison-without-the-keyboard-footer)
- [All active-player marker frames in the first planning view](#all-active-player-marker-frames-in-the-first-planning-view)
- [Both selected-sector outline states](#both-selected-sector-outline-states)
- [Completed-state final calendar and report capture](#completed-state-final-calendar-and-report-capture)
- [Final-entry hire dock comparison](#final-entry-hire-dock-comparison)
- [Completed-state local waiting-light comparison](#completed-state-local-waiting-light-comparison)
<!-- doc-index:end -->

## First-planning map comparison without the keyboard footer

On 2026-10-02, the deterministic first-planning-entry capture of the
EXP-SETUP-001 state (seed 52421) at selection-marker frame 6 was compared over
map rectangle `(2,42,432,416)`. The original window capture and rebuild
differed in 5,763 RGB pixels. Of these, 5,088 had exact-white original pixels
and were classified as the Windows 11 white-block artifacts that FND-UI-041
notes in the city. All 675 remaining differences occurred at y 439 through
445, where the rebuild draws the mandatory keyboard footer (DEV-UI-023).

An external diagnostic build using the corrected console renderer omitted
only that footer draw. It produced zero differing nonwhite pixels over the
same map rectangle; the 5,088 original-white differences remained. The game
implementation retains its documented footer. Captures, diagnostic projects
and original memory remain outside Git.

This comparison supports the unaffected map pixels of one fixed state and
frame. Pixels that are exact white in the original capture were set aside as
capture artifacts, so the rebuild's sprites under them remain unchecked. It
does not establish all marker frames, every selected sector, search overlays,
pointer states or whole-screen parity.

## All active-player marker frames in the first planning view

On 2026-10-02, an external diagnostic copy of the original probe collected
all twelve active-player marker frames from the first planning view of the
EXP-SETUP-001 state (seed 52421), the state of the previous section.
FND-UI-038 steps the counter `0x00487B90` after drawing frame `k`, so a
capture taken while the counter holds `c` shows frame `(c + 11) mod 12`.
Each accepted frame had the same counter value before and after two
byte-identical non-repainting window captures. The probe pumped the original
between captures until its counter changed; it did not patch the executable
or synthesize marker artwork.

Twelve rebuild captures came from the external diagnostic harness of the
previous section, which selected frames 0 through 11 explicitly instead of
running the game's timer, and omitted the DEV-UI-023 footer below the compared
rectangle. Every capture matched the original over Overlord-bar rectangle
`(2,0,432,42)` with zero differing RGB pixels. This includes the occupied
portraits and the marker position for the viewed seat in this state.

This verifies frame artwork and placement for one viewer and state. It does
not measure timing, dropped ticks, focus behavior, other viewed seats, empty
seat animation or the two-frame selected-sector outline.
Captures and diagnostic code remain outside Git.

## Both selected-sector outline states

The twelve synchronized original captures above contain both selected-sector
outline states, the selection frames `f = 0` and `f = 1` of FND-UI-017. They
were compared over the map with DEV-UI-023's footer omitted in the external
diagnostic harness. Against the rebuild's frame 0, the original captures
labelled with active-player marker frames 2 through 7 have zero differing
nonwhite map pixels. The other six captures differ in exactly 200
nonwhite pixels, all inside border bounds x 219 through 270, y 97 through 146
of the selected cell at column 4, row 1.

A second external build selected frame 1 explicitly. It matches those six
captures with zero differing nonwhite map pixels, and differs from the first
six by the same 200 border pixels. Thus every original map capture matches
one of the two outline states outside exact-white artifact pixels. The
independent player-marker labels identify these particular captures; they do
not imply that the two animation counters share a phase.

This verifies both outline artworks and placement for the selected cell in
this state. The comparison does not measure their period, reset behavior,
other cells, dropped ticks or the pixels covered by the expected Windows 11
artifacts. The gang-status markers need no timing comparison: RULE-UI-006
picks each one from the sector's state, and no timer cycles them.

## Completed-state final calendar and report capture

An isolated capture from the actual completed EXP-TURN-042 replay exposed a
presentation error: its coordinator had moved to turn 27, while the original
final visit still held elapsed turns 25. Reading the coordinator directly drew
console week 27 and report week 26. A diagnostic that sets the turn to 26
directly does not exercise this completed state, so it cannot catch the error.

The city and Events renderers now derive presentation elapsed turns from the
outcome turn after completion, and from the coordinator during an active match.
The completed replay retains coordinator turn 27 and outcome turn 26; it draws
console week 26 and report week 25, matching FND-UI-041 and EXP-TURN-042.
Report projection continues to select resolution-turn 26 records.

The external renderer loaded the actual completed replay save without changing
its turn or outcome, selected player 0 and the Events panel, and froze updates.
With marker frame 8, compared with the paired original EXP-TURN-042 capture,
the console calendar rectangle `(481,15,101,7)` has zero differing RGB pixels.
The entire Events panel `(104,124,344,209)` also has zero differing RGB pixels
across all 71896 pixels, including date, subject, caption, controls and artwork.
This region has no expected Windows 11 white-block differences to exclude.

The diagnostic selected the viewer and panel directly; handler entry and closing
are tested separately. This comparison covers one cash-short report and final
state, not all reports, combat results, multiple viewers or input timing.
Screenshots, saves and the diagnostic harness remain outside Git.

## Final-entry hire dock comparison

In the EXP-TURN-042 original final-entry capture, all three 64-by-64 hire
portraits match the corresponding completed replay render exactly: zero
RGB differences in each of the 4096-pixel cells. The three retained offer
IDs also agree with the `hire_offers` values of the numeric original fixture,
so the rebuild needs no extra offer draw for this final visit.

The earlier layout's price strip `(438,436,198,24)` differed in 158 pixels,
confined to x 449 through 592 and y 440 through 446. Applying the existing
hire-price-origin correction from commit `f737f302` places the three prices
at x 450, 516 and 582, as FND-HIRE-007 records. Repeating the isolated render
then matches all 4752 pixels of that strip exactly. The calendar and entire
Events panel remain exact after this correction.

This validates the existing correction against a later completed-match state,
in addition to the first-planning layout tests. It does not establish every
hire/snub mark, other offer combinations or drag and release behavior. The
reference screenshots and diagnostic renderer remain outside Git.

## Completed-state local waiting-light comparison

FND-UI-043 establishes that completing a local human planning visit marks
that seat's orders as submitted and redraws its waiting light black. The
next round resets the flags; the final city visits retain the completed
round's flags. The renderer previously assumed only online play submitted
orders, leaving the local final-view light lit.

The external EXP-TURN-042 renderer was rebuilt with the committed city-map
renderer, retaining only its diagnostic marker-frame override. It loaded
the same actual completed replay save, selected player 0 and Events, froze
updates, and captured marker frame 8. Compared with the paired original,
the entire Overlord bar `(18,5,404,32)` matches in all 12928 RGB pixels.
The human waiting light `(51,30,20,6)` matches in all 120 pixels. No artifact
exclusion is needed for these regions. The console calendar, entire Events
panel, three hire portraits and hire-price strip remain exact.

The focused fast gate passed 39 tests, with no skips or build warnings or
errors, including actual rendering-predicate checks for initial planning,
an earlier completed local seat, a later waiting human, the next turn's
upkeep and reset, and the completed EXP-TURN-041 and EXP-TURN-042 states.
The hot-seat transition is a synthetic state checked against static
evidence; no original multi-human capture covers it.
The pixel comparison covers one final state and marker frame; later local
rounds, multiple original human viewers and input timing remain unverified.
Screenshots, saves and the isolated harness remain outside Git.
