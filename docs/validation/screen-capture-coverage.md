# Screen capture coverage

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

Every screen entry the rebuild draws has at least one capture of the original
that `ScreenCaptureTests` compares, and in every compared capture no element
differs. The table lists, for each entry, the experiments whose captures are
compared and the states of the screen that no capture shows yet. A state in
the last column rests on static readings and the layout tests alone. The
`PARITY.md` row of each entry says the same in its notes; this table gathers
them so the open captures can be planned together.
`ScreenCaptureTests.CoverageTableNamesEveryComparedCapture` fails when the
second column names other experiments than the fixtures' compared captures,
so a new capture experiment has to be added here. SCR-UI-009 and SCR-NET-001
to SCR-NET-005 are not drawn (DEV-UI-019, DEV-NET-001) and have nothing to
capture.

A shot taken after presses also checks some of the presses. The reference
frame makes a `strip` or `dbl` step and each setup step at the window point the
probe pressed in the original (`--reference-clicks`), so a shot whose elements
match shows that each of those presses changed the screen as the original's
did. A press that lands in the wrong region but leaves the same picture is not
caught. The other presses are not made at the original's points: a `card`
step's menu choice becomes a press on the rebuild's order panel (DEV-UI-021),
`back` is pressed at `(20, 425)` and `exit` is left out, as
[Comparing](screen-comparison.md#comparing) describes. Regions that no shot presses at the
original's point rest on the static reading of the handler, checked
against the entry by layout tests such as `HireDockLayoutTests`,
`UiNavigationTests` and `SetupPanelLayoutTests`, and on EXP-TURN-095 for the
detailed sector screen's card and group strips.

| Screen | Captures compared | States no capture shows |
|---|---|---|
| SCR-UI-001 | EXP-UI-015 | None; the rebuild's buttons and version are masked (DEV-UI-012, DEV-UI-019, DEV-VIDEO-003) |
| SCR-UI-002 | EXP-UI-015 | None |
| SCR-SETUP-001 | EXP-UI-015, the screen as New Game opens it and after presses and a drag from Add | A card-face drag with the pointer points written (#413); a computer slot's background |
| SCR-SETUP-002 | EXP-UI-016, EXP-UI-021 | The card held pressed, an eliminated player's card, a later turn |
| SCR-UI-003 | EXP-UI-001, EXP-UI-003, EXP-UI-006, EXP-UI-008, EXP-UI-012 to EXP-UI-014, EXP-UI-016, EXP-UI-021, EXP-UI-041, EXP-UI-044 | The Events or Comlink light lit; a timed scenario's countdown after the first planning entry; the final view (FND-UI-041); the city behind an elimination card of an earlier human (#421) |
| SCR-HIRE-002 | EXP-UI-001, EXP-UI-003, EXP-UI-006, EXP-UI-041 | The hire and snub marks; a dragged portrait; the mouse input |
| SCR-UI-004 | EXP-UI-006, EXP-UI-007, EXP-UI-009 to EXP-UI-011, EXP-UI-041, EXP-UI-042 | The group order strip's menus; a card drag |
| SCR-UI-005 | EXP-UI-006, EXP-UI-007 | A seventh gang in one sector |
| SCR-UI-006 | EXP-UI-009, opened from an Equip row; EXP-UI-041, a button held | The panel opened from Gang Information; how often the rotation advances, which a still capture cannot show |
| SCR-UI-007 | EXP-UI-007, one site | Another site; Site Information opened from Influence or Search |
| SCR-UI-008 | EXP-UI-006 | None; Advanced AI's field (DEV-AI-003) is off in the comparison |
| SCR-FINANCE-001 | EXP-UI-006 (City), EXP-UI-007 (Sector) | A queued Sell of several items (DEV-FINANCE-001); the cash row is masked (DEV-UI-006) |
| SCR-HIRE-001 | EXP-UI-008, EXP-UI-041 | An offer with a two-digit negative value |
| SCR-GANG-002 | EXP-UI-008, a hire offer; EXP-UI-041, a button held | A hired gang's panel; its rotating items |
| SCR-COMBAT-001 | EXP-UI-008, one page; EXP-UI-041, a button held | A second page; the pressed arrows |
| SCR-EVENT-001 | EXP-UI-008, one Crackdown report; EXP-UI-041, a button held | Other report types, compared through the replays' records only; the pressed arrows |
| SCR-OBJECTIVE-001 | EXP-UI-008, EXP-UI-041 | Other scores; tied players |
| SCR-SEARCH-001 | EXP-UI-008, one selection state; EXP-UI-041, a button held | Other selection states; Site Information opened from a row |
| SCR-MOVE-001 | EXP-UI-009, no destination chosen; EXP-UI-041, a button held | The arrow of a chosen destination; an edge sector's bands |
| SCR-EQUIP-001 | EXP-UI-009, category 0 with no item chosen or carried; EXP-UI-041, a button held | The row mark; a carried item; other categories |
| SCR-RESEARCH-001 | EXP-UI-009, one category with no row chosen; EXP-UI-041, a button held | A chosen row; other categories |
| SCR-GANG-001 | EXP-UI-009, a gang carrying no items | A gang carrying items |
| SCR-GIVE-001 | EXP-UI-010, one recipient with no item chosen; EXP-UI-042, a choice made | A chosen item or recipient; a dimmed card |
| SCR-SELL-001 | EXP-UI-010, no item chosen; EXP-UI-042, a choice made | The highlight of a chosen item |
| SCR-INFLUENCE-001 | EXP-UI-010, no site chosen; EXP-UI-042, a choice made | The frame of a chosen site |
| SCR-ATTACK-001 | EXP-UI-011, before and after a target is chosen | When the Confirm face is first drawn |
| SCR-OPTIONS-001 | EXP-UI-012, the line shown | The line in its black ticks |
| SCR-COMLINK-002 | EXP-UI-016, opened and after a recipient press; EXP-UI-044, Send held | Typed text; the caret plain; a pressed face (#417); an empty slot |
| SCR-COMLINK-001 | EXP-UI-021, one message | Several messages; a step between them; a pressed face |
| SCR-AWARDS-001 | EXP-UI-017, both tabs | An eliminated player's row; the endgame after an elimination card (#419) |
| SCR-OBJECTIVE-002 | EXP-UI-018 | The card behind a Ready card (#421); the press of its Done |
| SCR-COMBAT-002 | EXP-UI-019, a bare-handed attack without Martial Arts; EXP-UI-020, a police clip; EXP-UI-029, a second police clip; EXP-UI-046, an armed and an unarmed attack on the viewer's gang; EXP-UI-047, a bare-handed Martial Arts attack; EXP-UI-048, an evaded attack by the viewer's gang; EXP-UI-049 and EXP-UI-054, evaded attacks on the viewer's gang | The pressed Exit face; a no-damage hit strip; a clip whose hold flag is cleared; a paint before tick 3, which EXP-UI-049 shows once and the rebuild does not draw (FND-COMBAT-032) |
| SCR-AWARDS-002 | EXP-UI-023, a human survivor | A computer survivor; a tab pressed |
