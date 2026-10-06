# Project decisions

Status: active

This log records deliberate product and compatibility boundaries that affect the
implementation plan.

Entries are ordered newest first. Each records the decision, the evidence or
reasoning behind it, and what it rules in or out. A decision that departs from
the original is also recorded as an entry of [DEVIATIONS.md](../DEVIATIONS.md),
which names the spec entries it departs from.

## Decision index

Generated from the `##` headings of this file by `node tools/update-doc-indexes.mjs`.

<!-- doc-index:begin decision-index -->
| Date | Decision |
|---|---|
| 2026-10-06 | [Spectators watch an online match some turns behind](#2026-10-06--spectators-watch-an-online-match-some-turns-behind) |
| 2026-10-06 | [Chat in the online lobby, through the match's event log](#2026-10-06--chat-in-the-online-lobby-through-the-matchs-event-log) |
| 2026-10-06 | [Recover from a desync without waiting on the host](#2026-10-06--recover-from-a-desync-without-waiting-on-the-host) |
| 2026-10-06 | [Count a row its mandatory deviations replace as deviated](#2026-10-06--count-a-row-its-mandatory-deviations-replace-as-deviated) |
| 2026-10-05 | [Capture the original with the 32-bit white key](#2026-10-05--capture-the-original-with-the-32-bit-white-key) |
| 2026-10-05 | [Take captures of the original without a DirectDraw wrapper](#2026-10-05--take-captures-of-the-original-without-a-directdraw-wrapper) |
| 2026-10-05 | [Switch DEV-AI-008 off from the command line only](#2026-10-05--switch-dev-ai-008-off-from-the-command-line-only) |
| 2026-10-04 | [Switch DEV-AI-007 off from the command line only](#2026-10-04--switch-dev-ai-007-off-from-the-command-line-only) |
| 2026-09-26 | [Keep a replay load's random state and inboxes](#2026-09-26--keep-a-replay-loads-random-state-and-inboxes) |
| 2026-09-26 | [Keep the original hunter guard and drop DEV-AI-001](#2026-09-26--keep-the-original-hunter-guard-and-drop-dev-ai-001) |
| 2026-09-26 | [Keep the rule and AI corrections mandatory](#2026-09-26--keep-the-rule-and-ai-corrections-mandatory) |
| 2026-09-25 | [Play the intro at every start unless Intro only once is on](#2026-09-25--play-the-intro-at-every-start-unless-intro-only-once-is-on) |
| 2026-09-24 | [Record the gangs that fought in each combat event](#2026-09-24--record-the-gangs-that-fought-in-each-combat-event) |
| 2026-09-24 | [Refuse a hire drop on a sector already holding six friendly gangs](#2026-09-24--refuse-a-hire-drop-on-a-sector-already-holding-six-friendly-gangs) |
| 2026-09-24 | [Mark objective sectors on the detailed-sector minimap](#2026-09-24--mark-objective-sectors-on-the-detailed-sector-minimap) |
| 2026-09-24 | [Resolve cash transactions in player order](#2026-09-24--resolve-cash-transactions-in-player-order) |
| 2026-09-23 | [Do not animate the panel slide-out](#2026-09-23--do-not-animate-the-panel-slide-out) |
| 2026-09-22 | [Fold the definition set into a fingerprint as a digest](#2026-09-22--fold-the-definition-set-into-a-fingerprint-as-a-digest) |
| 2026-09-21 | [Fingerprint match state with XxHash128, not SHA-256](#2026-09-21--fingerprint-match-state-with-xxhash128-not-sha-256) |
| 2026-09-19 | [Order a ctrl-picked selection of gangs at once](#2026-09-19--order-a-ctrl-picked-selection-of-gangs-at-once) |
| 2026-09-18 | [Scope the Sector workspace's opponent gang view to detection](#2026-09-18--scope-the-sector-workspaces-opponent-gang-view-to-detection) |
| 2026-09-17 | [Do not substitute rejected recovered AI commands](#2026-09-17--do-not-substitute-rejected-recovered-ai-commands) |
| 2026-09-17 | [Correct the original registry persistence defects](#2026-09-17--correct-the-original-registry-persistence-defects) |
| 2026-09-17 | [Correct the original AI hire slot/role indexing defect](#2026-09-17--correct-the-original-ai-hire-slotrole-indexing-defect) |
| 2026-09-13 | [Decode the supported Smacker subset at runtime](#2026-09-13--decode-the-supported-smacker-subset-at-runtime) |
| 2026-09-13 | [Stream the intro once, then keep it on the title screen](#2026-09-13--stream-the-intro-once-then-keep-it-on-the-title-screen) |
| 2026-09-13 | [Bug reports carry a replayable journal, stored apart from matches](#2026-09-13--bug-reports-carry-a-replayable-journal-stored-apart-from-matches) |
| 2026-09-10 | [Save compatibility scope](#2026-09-10--save-compatibility-scope) |
| 2026-09-10 | [Networking scope](#2026-09-10--networking-scope) |
<!-- doc-index:end -->

## 2026-10-06 — Spectators watch an online match some turns behind

- Decision: a host can let people who hold no seat watch an online match. The
  host turns it on in the lobby by choosing a delay of 2 to 20 turns
  (`settings.spectatorDelayTurns`); leaving it unset, the default, means the
  match cannot be watched. The setting can change only while the match is in
  its lobby, so the players who sit down know whether they will be watched.
  It travels in the match settings, so every seated player's lobby shows it,
  and the public listing shows it too.
- Joining: `POST /spectate` with the join code, the password when the match
  has one, and a display name. The password check and its budgets are the
  ones the seat doors use. The answer is a spectator token, prefixed `cos_`
  where a player token is `cop_`. A match admits at most 64 spectators over
  its life, counting those who left or were removed, and refuses the 65th
  with `spectators_full`. The cap bounds both the table and the
  `spectator.joined` and `spectator.left` events a lobby log can collect.
- Authorization: spectator routes live under `/spectate/:matchId` and take
  only a spectator token; every `/matches/:matchId` route takes only a player
  token. A spectator holds no seat and has no player row, so nothing that
  counts players can count one: not capacity, readiness, the turn barrier,
  takeover votes, hash reports, the desync verdict or its tie-break. A
  spectator cannot write anything except leaving. Their requests are charged
  to the member rate limit, keyed by spectator id.
- The delay is enforced by the server. While the match runs, the released
  turn is the newest sealed turn minus the delay, and a spectator may read:
  - the spectator view: status, settings without the live `seatSummaries`,
    the roster, the open turn number, the delay and the released turn;
  - the seed, once at least turn 1 is released;
  - the sealed order set of any released turn;
  - the newest snapshot at or below the released turn. Snapshot pruning keeps
    that snapshot while the match runs, so a spectator can always start;
  - the events that change who controls a seat (start, turn opened and
    sealed, takeover, return, late join, status) logged before the seal of
    the first unreleased turn. A rebuild from sealed sets needs them; see
    the control handovers in the match session.
  Once the match has finished or been abandoned every sealed turn is
  released. A request for anything later answers `409 turn_not_released`.
  A spectator never reads the event stream, readiness, deadlines, hash
  reports, desync announcements, takeover votes or lobby chat. Chat is
  between the people at the table, and the players wrote it without an
  audience.
- What a spectator sees: the whole map, every seat's position and the orders
  every seat gave, as of the released turn. That is hidden information to the
  players, who see the city through fog of war and never see a rival's
  orders. If the Comlink is carried online in sealed sets, its messages are
  in that view too. The delay is the control against a player watching their
  own match to cheat: what a spectator can relay is at least the delay old,
  and positions and plans that old have mostly been overtaken. It narrows the
  advantage without removing it, which is why spectating is off unless the
  host turns it on and why the setting is shown to every player.
- Removal: the host can remove a spectator at any time
  (`POST /matches/:id/spectators/:spectatorId/kick`), which revokes the
  token. A spectator can leave (`POST /spectate/:id/leave`). Players can list
  the spectators (`GET /matches/:id/spectators`) and are told of arrivals and
  departures through `spectator.joined` and `spectator.left` in their event
  log.
- Runtimes and retention: both runtimes serve the same routes from the shared
  application and storage, the Worker over D1 with the SQLite migration. A
  spectator polls; nothing is added to the stream or the Durable Object.
  Spectators are rows of their own table, deleted with the match.
- Versions: protocol version 28. The session version is unchanged: the
  setting is a new optional field and the spectators a new table, and nothing
  a stored match already holds changes meaning. A match created before has no
  delay, which means it cannot be watched.
- Desktop screens: the lobby summary has a SPECTATORS row that every seat
  reads; for the host it is also the control, whose arrows step from off
  through 2 to 20 turns. The classic lobby puts the same choice on WATCH: NO
  and WATCH: YES faces with FEWER TURNS and MORE TURNS beside them. Every seat
  can open the list of spectators, from the lobby and from the online match's
  menu, and only the host's copy can remove one; arrivals and departures are
  lines in the lobby chat and on the city's message line. The connect form
  has a third role, WATCH A GAME, which asks for the join code, a name and
  the password, and the browser lists how far behind a watchable session is
  shown and offers WATCH beside JOIN. The spectator view is the city screen
  drawn from the released state for one seat at a time (Tab, the arrows on
  the panel or a click on a portrait changes the seat), with a panel over the
  command buttons that gives the turn shown, the turn the players are on and
  the delay; before there is a state it waits on the online frame. No input
  on it reaches the match. The spectator's token is kept in
  `multiplayer-spectating.json`, apart from the seats, so an older build never
  reads it as one, and the unfinished sessions list offers it as WATCHING.
- Status: the server on both runtimes, the contracts, the client session
  that follows a match and the desktop screens are implemented and tested.

## 2026-10-06 — Chat in the online lobby, through the match's event log

- Decision: players seated in an online lobby can send each other short text
  messages until the host starts the match. A message is posted with
  `POST /matches/:id/chat` and stored as a `lobby.chatMessage` event in the
  match's own event log. The game reads new events whenever its once-a-second
  lobby poll shows the log has grown, so a message reaches the others within
  about a second, and a player who joins later reads the conversation so far.
- Lobby only. Once the match starts, the server refuses chat
  (`match_not_in_lobby`) and the game hides the panel. Inside a match the
  original's Comlink is the channel between players, with its own rules about
  who may write to whom and when (RULE-COMLINK-002 and RULE-COMLINK-003). A
  second, unrestricted channel beside it would change how the diplomacy of a
  match is played. Players are free to talk elsewhere, but the game does not
  offer that channel.
- Limits:
  - A message is 1 to 160 characters after trimming and NFC normalisation,
    the length of a Comlink message. Control, format and private-use
    characters are refused, as in names.
  - Each player may post ten messages a minute.
  - Once a lobby's log holds 1,000 events, chat is refused with
    `lobby_log_full`. The log is the only store and lobby retention keeps it
    for days, so a cap on its length is what bounds what one lobby can cost
    the server, whichever process counted the rate.
- The game draws chat in the original font, which has upper-case letters,
  digits and punctuation. A character it has no glyph for is drawn blank, and
  the game's own input accepts only characters it can draw. The server accepts
  any safe text, because other clients may read it.
- Moderation is the host's kick: it revokes the member's token, which ends
  their chat along with their seat. Messages already sent stay in the log.
  There is no word filter.
- Transport: the event log rather than a WebSocket lane. The lobby already
  polls once a second, and the log gives ordering, history for latecomers,
  retention and deletion with the match, all without new infrastructure on
  either runtime.
- Status: implemented and tested; protocol version 25, session version
  unchanged.

## 2026-10-06 — Recover from a desync without waiting on the host

- Decision: a desync no longer depends on the host's snapshot in either of the
  two places it still did.
  - Every client first checks its own report. It rebuilds the disputed turn
    from server facts (the newest snapshot below the turn, then the sealed order
    sets) and compares the result with the hash it reported. When they differ,
    its live state went wrong somewhere outside the sealed sets, so it adopts
    the rebuilt state and reports again. A divergence of that kind then settles
    on unanimous reports, with no snapshot uploaded by anybody.
  - A tie is broken by a designated player rather than by the host alone. The
    designee is the host when the host's report is one of the tied hashes, and
    otherwise the lowest-numbered seat whose report is. `turn.desynced` names
    the designee, and the server takes a tie-breaking snapshot only from that
    player.
- Reason: with five or six players a tie can leave the host outside every tied
  hash (two reports each for two hashes, the host's alone on a third). The host
  could not claim a hash it did not hold, no peer was allowed to break the tie,
  and the match stayed paused until retention collected it. The self-check
  covers the other gap: a client that diverged through its own fault, which in
  a two-player match is a tie, had its state imposed on the other player
  whenever it was the host.
- The designee is recomputed when the roster changes. A departure, a kick, a
  takeover or a rejoin during the pause re-runs the verdict, and when the
  candidates or the designee differ from the turn's latest announcement the
  server announces `turn.desynced` again, keyed by the announcement it follows,
  so a verdict that returns to an earlier one (a seat that leaves and rejoins)
  is announced too. The sweep re-runs verdicts without announcing, so a paused
  match costs no extra writes while nothing changes.
- Unchanged: a snapshot may still only claim a hash the most players reported,
  and a sole most-reported hash may still be posted by anyone holding it. The
  self-check changes nothing but this client's own report: the server still
  confirms a turn only on unanimous reports or against a snapshot that claims
  a most-reported hash.
- Out of scope: resolving turns on the server, which would remove snapshots
  from recovery altogether, is tracked in #453.
- Status: implemented and tested; protocol version 24, session version
  unchanged.

## 2026-10-06 — Count a row its mandatory deviations replace as deviated

- A `mandatory` deviation may name, in a Replaces item, the entries of its
  Departs from that it replaces entirely. A complete parity row such an item
  names has no tests of its own, and it is `deviated` once every `mandatory`
  deviation it lists has a Tests item. Until then it stays `implemented`. A row
  a deviation changes only in part keeps the rest to compare with the
  original, so it needs parity tests as before.
- A deviation's Tests item lists the test files that check the rebuild does
  what the deviation's Reason says. Each file cites the deviation's ID. These
  tests compare the rebuild with the deviation, so they are not parity tests
  and do not go in a row's Tests column.
- Reason: a row such as SCR-NET-001, whose entry the rebuild replaces with no
  setting to bring the original back, has nothing of the original left to
  compare with, so under version 1 of the documentation standard it stays
  `implemented` however finished it is. `deviated` marks it as done, and the
  Tests item makes sure the replacement does what the deviation log claims.
- The rule is the documentation standard's, a minor version of version 1
  (kibertoad/refurbished-dinosaurs#58, with the checker in
  kibertoad/refurbished-dinosaurs-toolkit#291). `tools/check-spec.mjs` applies
  it the same way, and `docs/upstream/` holds the standard's text.

## 2026-10-05 — Capture the original with the 32-bit white key

- Decision: the probe takes captures of the original for screen comparisons with `--white-key`,
  which hands the keyed mask compositor the white a 32-bit surface holds in place of the 16-bit
  key. A capture taken without it keeps its white areas, and `ScreenCaptureTests` goes on
  reporting them as unverified.
- Reason: the solid white areas the original leaves on Windows 11 are its keyed copies drawn
  opaque. Its surfaces follow the 32-bit desktop, where the 16-bit key `RGB(255,252,255)`
  matches no pixel (FND-PLATFORM-014). With the key replaced, EXP-SETUP-001's setup (seed 52421)
  drew 1 exact-white pixel where it drew 5089, and showed the selected sector's interior and the
  edge tabs' labels in place of white. That run and EXP-TURN-041's configuration made the same
  310 and 10647 rolls as their fixtures and reached the same 2760 and 4331 end-state values: the
  key reaches the drawing and nothing else. A DirectDraw wrapper cannot help, since the windowed
  original draws with GDI only (FND-GFX-004): DDrawCompat left the same 5089 white pixels (the
  next decision, which still holds for the wrapper). No compatibility layer changed the result.
- Boundary: the write changes one argument of one `SetBkColor` call and only when it is the
  16-bit key. A capture shows what the original draws on a 16-bit display only where the key is
  the difference; FND-PLATFORM-014 records the capture of the first planning entry only.

## 2026-10-05 — Take captures of the original without a DirectDraw wrapper

- Decision: the probe takes captures of the original without DDrawCompat or another DirectDraw
  wrapper beside the staged executable. The solid white areas the original leaves on Windows 11
  stay in the captures, and `ScreenCaptureTests` reports them as unverified.
- Reason: DDrawCompat v0.7.1 (the release asset `DDrawCompat-v0.7.1.zip` of
  narzoul/DDrawCompat, SHA-256
  `0c33ecb1c01c1c779063b490a2e818f6d9227b3b4ee827c51790fb0fd59b17c5`, matching the digest
  GitHub lists for it) changed no game result but did not remove the white areas. Beside a
  staged copy, EXP-TURN-041's configuration (scenario 0, 26 turns, seed 52421) recorded again
  made the same 10647 rolls with the same bounds and results, and every one of the 4331 values
  of the fixture's end state was the same. EXP-UI-001's first configuration (seed 52421, first
  planning entry) recorded with the wrapper, with and without the compatibility layers, made
  the same 310 rolls and drew the same frame as without it, apart from the Overlord bar's
  marker, which was at another frame: the same 5089 exact-white pixels, among them the solid
  white selected sector. Windowed, the original draws everything with GDI and does not use
  DirectDraw at all (FND-GFX-004), so a DirectDraw wrapper has nothing to change there. Full
  screen under the wrapper, the window's device context gave an all-black copy.
- Boundary: a capture records the white areas as the original drew them. Another way to take
  captures, such as a different compatibility layer or an older Windows in a virtual machine,
  needs its own comparison of a recorded run before it is used.

## 2026-10-05 — Switch DEV-AI-008 off from the command line only

- Decision: DEV-AI-008 (a computer player hires only where a human could) is a setting that starts
  on, switched off by `--original-computer-hires` on the game's command line. No screen offers
  it. The match setup carries it, so saves, replay journals and state fingerprints record it;
  online matches keep it on.
- Reason: the default follows a simulation of the computer players' win rates against a
  planner-played human seat that keeps the human rule, run with the setting on and off. The
  original's behaviour would have stayed the default had the win rates differed; they did not,
  and no computer hire in the simulation used the freedom, so the fair rule is the default and
  an Options entry would add clutter for nothing.
- Boundary: the flag reaches local matches started in the session it is given to. A loaded save
  or journal keeps the value it was started with.

## 2026-10-04 — Switch DEV-AI-007 off from the command line only

- Decision: DEV-AI-007 (a computer player's Move goes to a neighbour) becomes a setting that
  starts on, switched off by `--original-computer-moves` on the game's command line. No screen
  offers it. The match setup carries it, so saves, replay journals and state fingerprints record
  it; online matches keep it on.
- Reason: with the deviation mandatory, every recorded run of the original in which a computer
  player jumps several sectors stopped matching at that Move, and those runs had to be recorded
  shorter or kept as known divergences. Run with the setting off, they replay to the end. The
  deviation's Justification shows that no player can notice the difference, so an Options entry
  would add clutter for nothing; the flag still lets a player who wants the original's Moves
  have them.
- Boundary: the flag reaches local matches started in the session it is given to. A loaded save
  or journal keeps the value it was started with.

## 2026-10-01 ? Use the original executable as the parity target

- Decision: Parity requires the original executable's behavior, not feature parity with the GOG CD compatibility wrapper.
- Reason: The wrapper is a separate attempt to fix known issues rather than evidence that the original executable implemented those fixes.
- Scope: Keep wrapper findings as context and consider its approaches when addressing known issues. There is no requirement to implement the same fixes or reproduce their exact behavior. Any intentional departure from the original still follows the deviation ledger.

## 2026-09-26 — Keep a replay load's random state and inboxes

- Decision: F10 rebuilds the match from the F6 journal and plays on with the
  random state and Comlink inboxes the journal reached. It does not do what a
  save load does: draw on from the run's sequence (RULE-RNG-001) and empty every
  inbox (RULE-COMLINK-004).
- Reason: a replay exists to reproduce a session exactly. Reseeding would make
  the turn after the load differ from the recorded one, and emptying the inboxes
  would throw away part of the state being reproduced, so a bug seen after the
  load could no longer be followed. RULE-RNG-001 and RULE-COMLINK-004 describe
  how the original enters a loaded game; the original has no replay load, so
  they do not govern this one.
- Open: F6 and F10 are available to players in every local match. Together they
  act as a quick save and quick load that keeps the luck, which is exactly what
  RULE-RNG-001's reload behaviour prevents for F5 and F9. The keys should later
  move behind a debug setting. Players lose nothing by that, since F5 and F9
  already save and load, and the replay stays a reproduction tool.

## 2026-09-26 — Keep the original hunter guard and drop DEV-AI-001

- Decision: the computer players' hunter guards compare the previous hire role
  with the scenario's hunter slot number, as the original does (BUG-AI-001,
  RULE-AI-010). DEV-AI-001, which compared with role 4 in every scenario, is
  dropped. The rule change moves the multiplayer session version to 14; the
  protocol version and the state fingerprint encoding do not change.
- Reason: DEV-AI-001 was mandatory on the argument that the original compares
  with a number that never matches, so the correction restores the author's
  intent. That argument shows the original is a defect. It does not show that
  the correction is better for the player, which a mandatory deviation must.
  The question that decides it is whether the correction changes how strong the
  computer players are, and headless simulations of the rebuild answer that it
  does not.
- Method: a throwaway harness, not kept in the repository, switched the guard
  per player between the two comparisons. Seat 0 was a simulated human (see
  [VALIDATION.md](VALIDATION.md#simulated-human-seats)) that always used the
  corrected comparison; the five computer players used the original comparison
  in one arm and the corrected one in the other, on the same seeds. It ran the
  seven scenarios that have a hunter guard (Greed, Power, Acceptance,
  Dominance, Kill 'Em All, Big 40 and Armageddon), 24 seeds each, four-year
  matches, at each of the four Mentality levels and under both AI policies:
  1,344 pairs, 2,688 matches.
- Findings: every pair finished identical: same end turn, standings, scores,
  hires, attacks and eliminations. Mentality changed the matches a great deal
  (the simulated human was eliminated in 1 of 168 matches against Goon under
  the Original policy and in 131 of 168 against Homicidal Maniac under the
  Advanced policy), so the sample covers passive and aggressive computer
  players. The hunter force reached its guard only in Armageddon, 1,052 times
  in all, and the two comparisons disagreed every time; the scenario's later
  adjustments, which set the slot to 0 while the player has fewer than four
  family-0 or family-4 gangs or enough family-6 and family-12 gangs, overwrote
  each of those slots before a hire. In the other six scenarios the guard was
  never reached, because a visible hostile sector, no covering hunter and the
  family-3, family-5 or family-7 gangs the test needs never came together.
  None of the roughly 175,000 computer hires across all matches was a hunter
  (role 4).
- Limits: the simulations measure the rebuild's hire logic, and RULE-AI-010 is
  `partial` in [PARITY.md](../PARITY.md). A simulated human plays like a
  computer player and provokes less than a person would. The absence of hunter
  hires is itself suspect and is recorded as an open claim on RULE-AI-010,
  with its follow-up in step 8 of
  [parity-achievement-plan.md](../parity-achievement-plan.md).

## 2026-09-26 — Keep the rule and AI corrections mandatory

- Decision: DEV-EQUIP-001 (Equip and Sell change cash in the order the player scheduled them)
  and DEV-AI-002 (a planned AI action with no legal command is dropped) stay `mandatory`, with no
  setting that restores the original. The rebuild keeps one code path for each. This upholds the
  decisions of 2026-09-17 and 2026-09-24 against a proposal to put them behind a Revised rules
  setting that starts off. The same proposal covered DEV-AI-001 (the corrected hunter guard),
  which was dropped instead (the entry above).
- Reason for DEV-EQUIP-001: the original scans roster slots, an order with no meaning in play.
  The player never sees a gang's roster slot while giving orders, so whether a Sell pays for an
  Equip turns on a number the player cannot read. Resolving in the order the player scheduled is
  strictly better: the player controls it and the cash row of the console shows it.
- Reason for DEV-AI-002: the 2026-09-17 decision stands. A computer gang is held to the same
  legal orders as a human's.
- DEV-CONTROL-001 (only players who ordered Control compete for the sector) is also mandatory.
  The original's behaviour is a bug (BUG-CONTROL-001): every player slot enters the Control pass,
  so a player with no order starts at 0 and has a positive margin when the sector's Income plus
  Support is negative, which hands the sector to a bystander or lets one tie with a real
  challenger. The manual describes the comparison only among players who try to control the
  sector, and no player source relies on the case. Step 3 of the parity achievement plan keeps
  the correction when it brings Control in line with FND-CONTROL-003.
- Interface additions stay `mandatory` with no setting to hide them: the cash row of the console
  (DEV-UI-006), the Tolerance warning (DEV-UI-007), the order targets (DEV-UI-008), the minimap
  pylons (DEV-UI-002), the police-turn count (DEV-UI-014), the opponent strip (DEV-UI-013), the
  research progress (DEV-RESEARCH-001) and the title screen's version label (DEV-UI-012). The
  held-item boxes of the Equip panel were listed here as DEV-EQUIP-002 until FND-EQUIP-010 showed
  that the original draws them too, and that deviation is dropped. Each shows only what the player already knows or
  could work out, removes no control and changes no rule, so each is a quality-of-life improvement
  with no downside. Each entry's Justification says what it saves the player. Screen tests allow
  for them by ID.
- Intro only once (DEV-VIDEO-003) starts on. Players rarely want to watch the intro again and
  again, and one showing is plenty; the INTRO button replays it on request and a player who wants
  it at every start switches the option off. This reverses the default of the 2026-09-25 entry
  below. Preferences format v12 has not been released, so no format moves: a v11 file migrates
  with the option on. DEV-UI-004, which described the same behaviour without a setting, is
  dropped.
- In-memory layouts: a FMT-STATE entry counts as `complete` when its row's notes, or a document
  they link, map every field a rule reads or writes to the rebuild state that holds the same
  value at the same point. A difference of representation that no rule result can observe, such
  as the Force-0 marker of an empty roster slot where the original writes sector 100
  (RULE-GANG-002), needs no deviation. A field the rebuild holds with a different value, or does
  not hold where a rule reads it, keeps the row `partial` until the rule is fixed or a deviation
  covers it.

## 2026-09-25 — Play the intro at every start unless Intro only once is on

- Decision: the logo and intro movies play at every start, as in the original.
  The 2026-09-13 behaviour, playing them only until one run has shown them,
  moves behind an Intro only once option, off by default (DEV-VIDEO-003). The
  option is stored in client preferences format v12; a v11 file migrates with
  it off and keeps its `IntroMoviesSeen` record, so switching the option on
  later does not replay the movies once more. The title screen's `INTRO`
  button stays in both modes.
- Reason: the deviation log starts a setting at the original's behaviour unless
  the rebuild's is strictly better, and a player who expects the intro at every
  start is not better served by losing it. The 2026-09-13 entry below made the
  departure without a setting.
- Boundary: as before, the option and the record are presentation preferences
  only and never enter saves, replays, phase hashes or multiplayer state.

## 2026-09-24 — Record the gangs that fought in each combat event

**Decision.** A gang-on-gang combat event records both combatants, and a police
attack records its target, as a `CombatantDetails` (owner, gang definition,
sector and equipment) taken when the fight resolved. The combat reports read a
gang that has left the roster from its event instead of dropping the fight. The
records are part of the canonical event encoding, so the state-hash format moves
to 3, native save format to 28, replay format to 32 and the multiplayer session
version to 10, under
[State fingerprint format](../AGENTS.md#state-fingerprint-format).

**Reasoning.** A gang wiped out in Combat keeps its roster slot only until the
Hire phase of the same turn: the first hire its owner resolves reuses the slot,
and the dead gang's id stops resolving. The events carried only ids, so Combat
Summary, Combat Detail and the automatic presentation left out exactly the
battles that eliminated a gang, which online, where every seat hires on most
turns, was the common case. A presentation-side memory of every gang the client
had seen covered only turns this client had watched being planned: the turn a
match was loaded on, and a turn that sealed while an online client was away,
stayed unlisted. The event is the one record every client, save and replay
already shares.

**Compatibility.** The game has not been released, so no player's file is
stranded. Older saves and journals are refused as older formats before their
fingerprint is compared, and the session bump retires in-progress online
matches, as [AGENTS.md](../AGENTS.md) requires when a state hash changes. The
protocol version does not move: the server stores sessions as opaque history and
never reads an event.

## 2026-09-24 — Refuse a hire drop on a sector already holding six friendly gangs

**Decision.** Dropping a Hire offer on a sector where the player already has
six active gangs is refused on the spot with "Sector gang limit reached." The
offer stays unselected instead of being reserved as a hire. The count is the
current friendly count a Move into that sector is validated against, less any
gang the player has already ordered to Move away or Terminate: both resolve in
the Execution phase, before hires, so the room they leave is there when the
hire is placed.

**Original behavior.** `FND-HIRE-001` establishes that the shipped drop handler
writes the destination without any capacity check, and the resolver only
counts the gangs in the target sector at resolution, where six fail the hire.
The recreation keeps that resolver check unchanged.

**Reasoning and compatibility.** A reserved hire into a full sector showed as
hired for the rest of the planning turn and could only fail. The refusal lives
in the client's drop handling, not in `HireRules`, so AI hiring, replay
validation and turn resolution are untouched; no session, save, replay or
fingerprint version moves.

## 2026-09-24 — Mark objective sectors on the detailed-sector minimap

**Decision.** The detailed-sector screen's 3-by-3 neighborhood minimap draws
the same exact-white-keyed `PX00129` objective pylons `(344,15,54,52)` that the
whole-city map draws, over every visible Siege landmark and Big Man center
sector 27, 28, 35, or 36. The crop is scaled into the minimap cell exactly as
the cell's city artwork is, so the pylons keep their city-map placement.

**Original behavior.** The recovered evidence places the pylon overlay only on
the whole-city map; no native copy of that crop into the detailed-sector
neighborhood has been identified.

**Reasoning and compatibility.** Big Man points accrue only in the center
sectors, and Siege landmarks decide that scenario, so a player working in the
detailed view should not have to return to the city to see which neighboring
sectors are objectives. The change is presentation only: rules, orders, saves,
replays, fingerprints, and the multiplayer protocol and session versions are
unaffected.

## 2026-09-24 — Resolve cash transactions in player order

**Decision.** Equip and Sell debit or credit cash in the order the player last
submitted those orders. Replacing an order moves it to the end. Transactions
still resolve by player slot, and Give retains its deferred roster-ordered
recipient writes. The city console shows current cash, the whole-cycle Delta,
and `UNSPENT = cash - sum(queued Bribe, Equip and Hire prices)` on one row as
`CASH 20 [18] (+1)`: cash, unspent cash in brackets, and the delta in
parentheses. Hovering the row explains each figure in its own section, breaks
the delta down by component, and shows every queued Bribe, Equip and Hire, numbered in
resolution order with its price: Instant Bribes first, then Equips in submission order, then
the hire.

**Original behavior.** `FND-EQUIP-002` and `FND-EQUIP-006` establish that the
shipped resolver instead scans fixed gang roster slots. An earlier-slot Sell
can fund a later-slot Equip regardless of which was queued first. The original
picker neither hides unaffordable researched items nor checks cash when an
item is chosen. The later Equip comparison is signed `cash < adjusted price`,
with equality permitted.

**Reasoning and compatibility.** A player can see and control submission order,
while the fixed roster index is hidden. Cash timing remains execution-time:
an earlier submitted Sell can fund Equip, but a later Sell, Chaos payout, or
next Upkeep income cannot. This deliberate rule deviation changes deterministic
turn outcomes, so multiplayer session version 9 retires sessions started under
version 8. Native saves and replay journals retain their format gates because
their schema and fingerprint encoding have not changed.

## 2026-09-23 — Do not animate the panel slide-out

**Decision.** With Slide Panels enabled, a panel slides in over the recovered
344- or 320-pixel travel, but the recreation does not animate the matching
slide-out. A closing panel disappears in the frame it closes. This is the only
panel-motion or audio behavior where the recreation deliberately departs from
the original. Every other effect cue keeps its recovered trigger, order and
interruption: one effect voice, and each new cue stops the one before it
(`FND-AUDIO-003`). Slide Panels stays off by default, a separate modern choice
recorded in the Options parity row.

**Reasoning.** The original's close helper `0x004196f5` plays slot 1 and then
runs a blocking copy loop of about a quarter second before the next handler can
open anything (see the interface-and-options panel slide evidence and
`FND-AUDIO-002`). That delay adds no information and holds input on every panel
change, including nested panel hops, and the entrance alone already shows where
the panel came from.

**Audio consequence.** The slide-out was also the gap between cues, so without
it a cue that the original separated in time now starts in the same frame and
cuts off the one before it. This is accepted rather than covered with delays or
with overlapping voices, which would add behavior the original never had.
Only the Slide Panels-gated cues are affected: a panel-to-panel change plays slot 1
and then slot 0, and the slot-1 close cue is not heard. Confirming the idle-gang
warning used to play slot 1 and then the turn-start slot 9; slot 9 now plays only
in an online match, when the server seals the turn (RULE-AUDIO-006), so nothing
follows slot 1 on confirm.

With Slide Panels off, the original plays neither slot 0 nor slot 1, and the
recreation matches it exactly.

**Rules out.** Adding a slide-out animation or a timed gap between cues to make
up for it, and letting effects overlap. Each would need its own decision.

## 2026-09-22 — Fold the definition set into a fingerprint as a digest

**Decision.** A state fingerprint folds in the gameplay definitions as a cached
128-bit digest of their canonical block rather than the block itself, and the
state-hash format moves to 2. Native save format 27 and replay format 31 carry
the new encoding and refuse every older format, and the multiplayer session
version moves to 6. The rule that these move together is written up under
[State fingerprint format](../AGENTS.md#state-fingerprint-format) and held by
`StateFingerprintVersionCouplingTests`.

**Reasoning.** The definition block is the same bytes on every call for a given
definition set, and a turn hashes 8 + 2P boundaries, so the whole of every site,
gang and item — names and descriptions included — was re-serialised and re-hashed
for each one. The digest is computed once per `OriginalData` instance and held
weakly, so a definition set the process stops using is still collectable. The
fingerprint stays a comparison, never a claim of authenticity, so a 128-bit
digest in place of the block costs nothing that matters.

**Compatibility.** A fingerprint under the new encoding is well-formed and
simply differs from the old one, and nothing in a save or a journal records
which encoding wrote it. Left to compare, an older file would be reported as
damage rather than as an older format — a save browser row drawn as playable,
then a failed verification that costs the intact backup generation, and a
journal reported as diverging on its first step. The format gates therefore move
with the encoding, and an older file is refused as `OlderFormat` before its
fingerprint is ever read. The game has not been released, so no player's file is
stranded; the session bump retires in-progress online matches for the same
reason, as [AGENTS.md](../AGENTS.md) requires when a state hash changes.

## 2026-09-21 — Fingerprint match state with XxHash128, not SHA-256

**Decision.** The canonical match-state fingerprint is 128 bits of XxHash128
over the canonical state encoding, with the event history and the
phase-boundary history folded in as running digests chained entry by entry.
Native save format 26 and replay format 30 carry it, refuse every older
format, and the multiplayer protocol and session versions move to 13 and 5.

**Reasoning.** The fingerprint is compared, never trusted: a replay step, a
save and an online turn report each carry one so that a divergence or a
corrupted file is noticed. Nothing depends on it being hard to forge, so a
cryptographic digest bought nothing, and it was the dominant cost of a match.
Every recorder step hashed the whole growing event history twice, so a
headless 200-turn match spent most of its time in SHA-256 and got slower with
every turn. Chaining the histories makes a fingerprint cost the same on any
turn; a 200-turn headless match dropped from 76 s to 5 s across the two
changes.

**Compatibility.** Older saves and journals were verified through preserved
projections of the SHA-256 of their day and cannot be checked under the new
fingerprint, so they are refused as `OlderFormat` rather than loaded on trust.
The game has not been released, so no player's file is stranded; the
multiplayer session bump retires in-progress online matches for the same
reason, as [AGENTS.md](../AGENTS.md) requires when a state hash changes.

## 2026-09-19 — Order a ctrl-picked selection of gangs at once

- Decision: the Sector workspace's gang cards take a ctrl-click as a pick. With
  more than one gang picked, an arrow on any picked card orders the whole
  selection, and dragging any picked card gives the selection the order that
  drop would have given the one gang. A bulk order is drawn from an allowlist —
  Attack, Control, Heal, Hide, Influence, Move — and is carried out by every
  picked gang the rules allow, skipping the rest rather than failing whole. The
  selection is per sector and per turn: another sector, a borrowed opponent
  roster, leaving the workspace, the order itself, or the end of the turn clear
  it. Information panels do not count as leaving, however deep they stack — a
  site inspected from the bulk influence picker stands two panels above the
  workspace and comes back to the same picks. A turn ends for this purpose
  whenever the turn on screen is replaced, including an online turn the
  authoritative clock seals without the player submitting it.
- Evidence: the original issued one order per gang and carried no multi-select
  affordance on the sector screen, so no native layout or handler constrains
  this addition.
- Reason: a player moving six gangs out of a falling sector, or healing whoever
  is hurt, is giving one decision six times. The allowlist is what stays
  unambiguous in bulk: equipping, researching, giving and selling read a single
  gang's inventory and purse, and bribing, snitching, raising chaos or
  terminating a whole selection in one click is a mistake nobody wants to make
  at speed. Partial execution follows from the same principle — the selection
  is a wish, not a promise, so a sector with room for two takes two and says so
  rather than refusing all six.
- Compatibility boundary: recreation-only input. Every picked gang goes through
  `CommandValidator` and queues its own ordinary `GameCommand`, so what reaches
  the queue is indistinguishable from the same orders given one at a time, and
  replays, saves, and the multiplayer protocol are unaffected. Move counts the
  selection against the destination's capacity before it queues, because the
  validator only refuses a sector that is already full and the surplus would
  otherwise be turned back during resolution.

## 2026-09-18 — Scope the Sector workspace's opponent gang view to detection

- Decision: the detailed-sector portrait strip marks an opponent with a red
  `GANGS` banner and lends the gang cards to that opponent's roster only for
  gangs the viewer already detects. The borrowed roster is per-visit state: the
  viewer's own portrait, moving the selected sector, or re-entering the screen
  restores their own gangs, and hovering an opponent's card no longer highlights
  what its queued command targets.
- Evidence: the original sector screen listed only the viewing overlord's gangs
  and carried no portrait-strip affordance for reading another overlord's, so no
  native layout or handler constrains this addition.
- Reason: the roster answers the question the sector screen already poses —
  who else is standing here — without widening what a player knows. Reusing
  `MatchState.CanPlayerDetectGang` keeps the strip, the cards, and attack
  targeting on one detection rule, and keeping the enemy action strip and
  queued-command highlight suppressed keeps orders private.
- Compatibility boundary: recreation-only presentation. It reads match state and
  queues nothing, so replays, saves, and the multiplayer protocol are unaffected.

## 2026-09-17 — Do not substitute rejected recovered AI commands

- Decision: once Original-policy preparation has produced a recovered family
  action and target, failure to find an equivalent modern legal command leaves
  that gang without a submitted command. It must not fall through to the
  recreation-native scalar scorer.
- Evidence: native family handlers write directly into the 16-byte planning
  records consumed by the resolver and contain no validator-rejection or
  alternate-command branch. The shared sector selector can legitimately return
  the source sector when capacity blocks every routed step, producing a native
  same-sector Move that the recreation's player-facing adjacency validator does
  not expose.
- Reason: inventing a different legal action changes strategy, RNG-independent
  outcomes, and later action history without original evidence. Retaining the
  exact prepared tuple while submitting nothing preserves its continuation and
  replay state without weakening validation for player commands.
- Compatibility boundary: this is a projection boundary, not a claim that the
  native game left the gang idle internally. The original retained and resolved
  its raw tuple; the recreation records that tuple in AI planning state but
  omits an unrepresentable command from the modern queue.

## 2026-09-17 — Correct the original registry persistence defects

- Decision: retain the recreation's validated, atomic, per-user preferences file
  rather than reproduce the executable's machine-wide registry implementation.
- Evidence: loader `0x0046439a` and writer `0x00464783` both open
  `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` with `0x20019`
  (`KEY_READ`). The writer then issues twelve `RegSetValueExA` calls without a
  handle carrying `KEY_SET_VALUE`; the loader attempts the same for a generated
  `serialNum`. Every result is ignored. The loader also reuses one DWORD across
  thirteen unchecked queries, allowing a missing later value to inherit stale
  data instead of its compiled default.
- Reason: silent non-persistence and cross-setting contamination are clear API
  misuse, not game design. Reproducing them would lose user choices and make
  malformed legacy state affect unrelated settings.
- Compatibility boundary: original compiled defaults and option semantics remain
  evidence for recreation defaults, except for separately documented modern
  choices such as Slide Panels off. Preference writes are reliable and bounded;
  malformed data falls back per field. The obsolete `serialNum` side effect is
  excluded from authoritative RNG, as documented in `FND-RNG-001`.

## 2026-09-17 — Correct the original AI hire slot/role indexing defect

- Decision: when the AI hire scheduler considers its scenario-specific family-6
  slot, compare the previous hire role with role 4 in every scenario instead of
  copying the executable's comparisons with slot numbers 6, 5, 2, 10, and 5.
- Evidence: selector `0x8f` in `0x00402d70` reads `0x00482160`, and planner
  `0x00458fa0` fills that location from current-role array `0x00482128` before
  choosing the next role. Each guarded special slot writes role 4, which the
  recovered scenario/family table maps to family 6. Dominance's original
  comparison with 10 is impossible for the verified 0..6 role domain; the other
  values can only suppress unrelated roles by numerical coincidence.
- Reason: this is a clear slot-versus-role indexing defect rather than ambiguous
  game design. Preserving it would create arbitrary scenario-dependent repeated
  family-6 hiring. The corrected comparison implements the common apparent
  intent—do not immediately choose another family-6 hire—while leaving all
  schedule tables, quotas, availability tests, ranking, and RNG behavior intact.
- Compatibility boundary: the `Original` policy deliberately differs from the
  shipped executable at this guard. Static evidence and tests preserve both the
  original finding and the exact recreation exception; no claim of bit-for-bit
  AI decision parity includes this bug.

## 2026-09-13 — Decode the supported Smacker subset at runtime

- Decision: keep the two verified user-owned `.smk` files in the extracted
  pack and implement their validated Smacker-v2 subset in managed runtime code.
- Reason: this keeps asset import and playback cross-platform and deterministic
  without requiring an ambient FFmpeg executable, host codec registration, or
  thousands of derived frame files.
- Boundary: malformed/unsupported video or audio must skip presentation and
  continue to the title. Movie state is never authoritative simulation,
  persistence, replay, or multiplayer state.
- Status: bounded container, palette, packed-audio, and indexed-frame decoding
  plus streaming presentation are implemented. The recreation policy is tested;
  original trigger/skip evidence and native visual/audio fidelity remain pending.

## 2026-09-13 — Stream the intro once, then keep it on the title screen

- Decision: the logo and intro movies play unattended only until one run has
  reached the end of the queue. The recreation has no player profiles, so the
  record lives in the single local preferences file alongside the other client
  settings. Completing it records `IntroMoviesSeen` in
  the then-current client preferences format v8 (subsequently migrated through
  the current format), and the title screen gains an `INTRO`
  button that replays the same queue on demand.
- Reason: 135 seconds of startup video, even with skip input, is a toll on every
  launch of a recreation that players restart often, while the movies themselves
  are content worth keeping reachable.
- Boundary: the flag is presentation preference only. It never enters saves,
  replays, phase hashes, or multiplayer state, a failed preference write leaves
  playback unaffected, and an unreadable movie pack reports on the title screen
  instead of blocking it.

## 2026-09-13 — Bug reports carry a replayable journal, stored apart from matches

- Decision: the in-game Escape menu can file a bug report to a hardcoded
  central address, and by default attaches the whole match as an event-sourced
  journal that replays from its first turn. The journal is anonymized before it
  is compressed and sent: player names become seat labels and Comlink text is
  redacted, both by re-running the match and recomputing every state
  fingerprint, so what is sent is a valid journal rather than an edited one.
  Names the original reads as cheat codes are game rules and are kept.
- Reason: a described bug in a deterministic simulation is a guess, and a
  journal is the bug itself. Recording one costs nothing — the replay recorder
  already wraps every mutation — but it only became a session's history once
  saves carried it: a load used to start a fresh recorder, so the turns that
  produced a bug were exactly what a report filed afterwards did not have.
- Companion, not a format change: a save writes `<save>.rchjournal` beside
  itself and a load resumes it when it ends at that save's own state. An old
  save still loads, a missing or corrupt journal is never a failed load, and a
  slot saved without one loses the journal already there rather than pairing
  with another game's history.
- A load adopts the journal rather than replaying it. Re-deriving the state from
  the steps means re-running the whole match — every recorded operation plus a
  full-state fingerprint each — on the thread the player is waiting on: a
  30-turn match measured 147 ms, and it grows with the match, so the reward for
  a long session would be a load that visibly stops. The save already *is* that
  state, so what the journal supplies is the history, and the one thing worth
  proving is that the two belong together: the last step's fingerprint against
  the restored state's. That is the same equality the replay was reduced to at
  the end, and it is the recorder's own invariant, so a companion left by
  another game in the same slot is still refused. Adopting measures 7 ms. Where
  something needs every step to still reproduce — a bug report, which replays
  the journal to anonymize it — that check happens there, off the game loop and
  on the copy about to be sent.
- Compression is Brotli, not zstd, with the codec byte reserved for zstd. A
  journal is the opening snapshot plus every recorded command, each with the
  fingerprint of the state it produced; the commands are most of the bytes and
  almost none of the compressed size, and the fingerprints are the reverse. In a
  measured 27-turn match the step array went from 106 KB to 12.6 KB, of which
  12.3 KB was the fingerprints and 0.2 KB everything else — and 364 hashes carry
  11.6 KB of entropy, so Brotli is already within a few percent of the floor.
  That makes the codec a question of what each side already has rather than of
  ratio: Brotli ships in .NET, in Node and in a Cloudflare Worker under
  `nodejs_compat`, while zstd needs a package on the game side and has no
  Workers decoder. A new dependency in a game that must build offline is the
  larger cost.
- Storage: reports go to the same deployment that hosts multiplayer, over a
  route of its own, into a separate D1 instance (a separate SQLite file when
  self-hosted) with its own migration lineage. They arrive unauthenticated,
  outlive the matches they describe, and carry other players' journals, so they
  share no schema, no lock and no blast radius with live matches. The journals
  themselves go to R2: one is hundreds of kilobytes to a few megabytes, D1
  refuses a row over 2 MB, and even the ones that fit would be dragged through
  every triage query. A deployment with no object store keeps archives under
  256 KiB inline and accepts the report without the journal above that.
- Boundary: the server never decompresses or parses an archive. It verifies the
  digest the client computed over the compressed bytes and stores opaque bytes.
- Status: implemented and tested. The central address is a placeholder
  (`http://localhost:8787`) until the public deployment exists.

## 2026-09-10 — Save compatibility scope

- Importing or exporting original *Chaos Overlords* saves is an explicit
  non-goal.
- Recreation-native saves and replays may change incompatibly before version
  1.0.0. Compatibility between pre-1.0 development formats is useful but is not
  a release gate.
- Keep the existing version discriminator, bounded readers, legacy hash
  selection, and migration structure as infrastructure for post-1.0 evolution.
- Existing pre-1.0 readers and tests may be retained when inexpensive, but new
  schema work may remove or replace them rather than accumulate migration debt.
- Starting with 1.0.0, incompatible format changes must increment the format
  version and provide either a deterministic migration or an explicitly
  documented safe rejection path.

## 2026-09-10 — Networking scope

Original network code and protocols are outside the parity target and are never
reproduced. Modern online play is a new design: a coordination server under
`multiplayer/` that relays sealed orders between deterministic clients and
verifies state hashes, hostable by players or run centrally
([`MULTIPLAYER.md`](./MULTIPLAYER.md)). Hot-seat play remains the local mode
and the client-side wiring of online play is tracked as follow-up work.
