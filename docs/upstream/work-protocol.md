---
title: "Work protocol"
meta_title: "Work protocol: how restoration work is planned, tracked and carried out"
description: "How a restoration moves from an owned copy of a game to a documented, validated rebuild: the stages, the files that track open work, the size of a unit of work, and how people and coding agents hand it on."
draft: false
---

The [methodology](/methodology/) says what counts as evidence and what the rebuild may change. The [documentation standard](/documentation-standard/) says how each finding is written down. Neither says what to do first, where open questions are tracked, how big a piece of work is, or what the next person or agent needs to pick it up. The rules here apply to people and coding agents alike, and to every game repository whose spec follows the documentation standard, whether it was started from the [project template](https://github.com/kibertoad/refurbished-dinosaurs-template) or not. A repository that runs an original on a machine it shares with one of them takes the [run lock](#running-the-original) as well. The [Dark Sun: Wake of the Ravager](https://github.com/kibertoad/dark-sun-wake-redux) restoration is planned and tracked this way, with its open questions in `queue/` and its live session requests in `docs/live-sessions/`, and the template ships the working files and the agent skills described below.

## Working files

The files that track the work sit in the game repository next to the spec. They describe the work and hold no findings, so the spec's rule against naming the rebuild does not apply to them. Anything an attempt learned about the original is recorded in the spec first, and a working file points at the entry. Each one says what is true now. What was true before is in git, and none of them repeats it.

| File | Holds | Never holds |
|---|---|---|
| `docs/IMPLEMENTATION-PLAN.md` | The game profile, scope and non-goals, the stage the project is in, the slices with their exit criteria, the risks, and the questions only the owner can answer. | Dated checkpoints, status narration, research questions, lists of what was done. |
| `queue/AREA.md` | One file per spec area: the open research questions of that area, grouped by the kind of evidence each one needs. | Anything already answered. An item is deleted when it is settled. |
| `docs/RUNTIME.md` | What can be done with the original running, and who can do it (see [Runtime access](#runtime-access)). | Findings. What a run showed goes in the spec. |
| `docs/live-sessions/NAME.md` | One file per live session an agent has asked for: its status, the items it would settle, how long it takes, and its script (see [Live sessions](#live-sessions)). Deleted when the session's recordings are in the spec. The files in the directory are the list of open requests. | Results. What the session showed goes in the spec. |
| `docs/HANDOVER.md` | Where the work outside any goal stands: the last gate result, work left unfinished, blockers, and what to pick up next, naming queue items and entries by ID. At most 200 lines, rewritten at the end of every session that works under no goal. | History. The previous session's notes are replaced, not kept below the new ones. The branch, its commits and whether it is pushed, which Git always has more current. What research found or tried, which belongs in the spec and the queue. The goals running and the live session requests, which the files in `docs/goals/` and `docs/live-sessions/` list. |
| `docs/reports/R-NNN.md` | One file per report from testing that has not been triaged yet, or that is waiting for what it is missing (see [Reports from testing](#reports-from-testing)). | Screenshots, which stay in the local store. What triage decided, which goes in the spec, the queue and the parity rows. |
| `docs/goals/NAME.md` | One file per long-running goal while it runs: its condition, the areas it claims, what it must not touch, the dead ends its tools and approaches have hit, and its own handover with the same content as `docs/HANDOVER.md`, rewritten at the end of every session under the goal. Deleted when the goal is met or dropped, and whatever in its handover is still worth handing on, such as a blocker, moves to `docs/HANDOVER.md`. | Progress reports. The queue and the commits show progress. |

Decisions the owner makes go in `docs/DECISIONS.md`, dated and newest first. A decision that departs from the original is a deviation as well, in `deviations/`. When the file would pass the size limit below, its oldest entries move to a numbered file in `docs/decisions/`, starting at `001.md`, filled up to the limit and never changed after. `docs/DECISIONS.md` keeps the newest entries and opens with links to the numbered files, and a decision is never renumbered or reworded when it moves.

Every file here is at most 1,000 lines, the same limit the standard sets for its own files. A queue file that would pass it becomes a directory of the same name, with a `README.md` holding the area's heading and its `Next ID:` line (see [The queue](#the-queue)) and one file per kind of evidence that has items, named after the section in lower case with a hyphen for the space: `queue/COMBAT/static.md`, `queue/COMBAT/agent-run.md`. Each file opens with the area and the section as a `#` heading, `# COMBAT: Static`. A file that would still pass is split by the kind of the first entry each item names, `queue/COMBAT/static/RULE.md`, the same way the standard splits its own files.

Tool paths, installed versions and the pitfalls of the local setup go in `docs/GHIDRA.md` and `docs/DEVELOPMENT.md`, so that nobody searches for them twice.

Where the template's files and this page disagree, this page applies and the template is corrected.

## Stages

The plan records the stage the project is in. The stages come in the order below, and a stage ends when its exit criteria hold, which a script or a reviewer can check without asking anyone. Work from a later stage may start early, but it does not count towards that stage's exit until the earlier stages have ended.

### Intake

Establish which game, which editions the owner has, and whether the game qualifies. Find the latest official version and patch the analysis copy to it. The template's `AGENTS.md` and bootstrap checklist give the steps.

Exit: eligibility and the latest version are recorded, the analysis build has a build entry and a manifest, and each manual, FAQ or earlier tool the work will rely on has a source entry.

### Runtime access

Find out what can be done with the original running, before any plan depends on it. Evidence from the original comes from four places. Reading the executable and the data files is the main one for every game. Calling single functions of the executable in a CPU emulator checks those readings for everything the code decides, with no window and no timing involved (see [Emulated calls](#emulated-calls)). People who play, in live sessions and in the testing that [reports](#reports-from-testing) come from, see what the original and the rebuild do on screen. Runs an agent drives on its own come last, since many of these games cannot be controlled by an agent at all, and where one can, the run is the most fragile evidence the project has (see [Running the original](#running-the-original)).

`docs/RUNTIME.md` records, for each build that will be run, how it runs (natively, under Wine, in DOSBox-X or another emulator, in a virtual machine), and whether an agent can do each of the following alone, only while a person runs the game, or not at all:

- start the original and bring it to a given state without a person;
- send it input;
- read its memory, set breakpoints and dump structures while it runs;
- load a patched save;
- capture frames and sound;
- play back a recording the original made;
- call a single function of the executable in the emulator harness (see [Emulated calls](#emulated-calls)).

Finding the answers is the one time the original is run before the static work is done, and it takes the run lock like any other run. The emulator harness starts no process of the game, so checking it takes no lock. Its answer is `agent` wherever the harness loads the build, whoever may run the game, because [emulated calls](#emulated-calls) are always allowed. Until the harness exists the answer is `none`, and what would change it is the tooling batch that builds it. Each answer names the tool and version that was tried and what happened, and, where the answer is no, what would change it. The record says what is true now, and it is updated whenever a tool, a machine or an emulator changes the answer.

Exit: `docs/RUNTIME.md` answers every capability for the analysis build, and each answer names the attempt it comes from.

The answers decide where a run goes in the queue: an agent run where an agent can make it, a live session where it needs a person. They also set what the project can reach. An entry reaches `established` through a [complete reading](/documentation-standard/#complete-readings) of the code alone, unless what it says depends on something the code does not decide, such as timing, an interrupt or the operating system, and only then does it need a run that agrees with the reading. A reading whose procedure an emulated call agrees with, on every branch, establishes it as well, once the reading of its callers and inputs is complete. A rule or screen row reaches `validated` only through tests that compare the rebuild with evidence recorded from the original running. An emulated call is such evidence, so a rule the code decides can reach `validated` even where nobody can run the game. A format row whose entry lists files can reach `validated` without any runs, because its evidence is the original files. A format with no files, a memory structure or a message, is compared with what was recorded from the original running, so it needs a run the same way a rule does. A project whose record says no agent can run the game plans its slices around that, and keeps the rest for live sessions and for what testers' captures of the original turn up.

### Survey

Map the whole game shallowly before studying any part of it deeply. List the installation and the media the game reads in full, and record how the listing was made. List in the manifest every file the game uses, and the rest of the listing under the build entry's Other files, as the standard's [Builds](/documentation-standard/#builds) section describes. Give the format of every file the manifest lists as `data` an entry, `unknown` where nothing is known yet. A CD audio track has one fixed layout and needs no format entry. Take an inventory of the functions of each file the analysis reads, as [Measuring progress](#measuring-progress) describes. Write down the screens the manual or a playthrough shows, and the rules the manual states, as `sourced` entries. Fix the area list, since areas can never be renamed. Seed the queue with the questions this turns up.

Exit: every path in the listing of the installation and the media the game reads is in the manifest or in the build's Other files, every file in the manifest with format `data` is listed by a format entry, every screen the manual mentions has a screen entry, every file the analysis reads has a function inventory committed in `coverage/` (see [Measuring progress](#measuring-progress)), and every area has a queue file.

Checking the format definitions against some of the files, such as the few whose hashes identify the release, says nothing about the rest and does not pass this gate. Until every path in the listing is accounted for, the plan records what is still missing and Survey stays open.

A survey keeps later work from being planned around the one subsystem somebody happened to open first, and it makes the size of the game visible before anyone estimates it.

### Slices

Build the game in vertical slices, each of which leaves it playable. The plan lists the slices in order, and each slice names the spec areas or entries it needs and the parity rows it has to bring to `implemented`, `deviated` or `validated`. Each target is one the runtime record makes reachable, and where a row needs a live session nobody has agreed to yet, the plan asks for `implemented` and names the gap in its risks. The first slice gives the rebuild a headless runner that a test drives from a fixture, since every later test that compares the rebuild with the original needs one. Within a slice, research and implementation alternate in small batches, described below.

Exit, per slice: every parity row the slice names has reached the status the plan asked for, and every queue item that blocked the slice, named by its ID, is closed or accepted as a known gap in the plan's risks.

### Audit

Close the gaps the slices left. Every function in the inventories is cited by an entry or recorded as out of scope with its reason, every parity row is `validated`, or `deviated` where a `mandatory` deviation names it in its Replaces item, or has a note saying why it can be neither, the queue holds only items the owner has accepted as out of reach, and every open report is waiting for what its `Missing:` line names.

## The queue

The queue holds the research questions still open. Every item names the spec entries it concerns, so behaviour with no entry gets an `unknown` entry before it gets a queue item, the same way it gets one before any code. The entry's own Open questions section describes what is unknown about it. The queue item says what to do next about it, and an item that duplicates the whole Open questions section is not needed.

A queue file opens with the area as a `#` heading and a line giving the ID the next new item takes, `Next ID: Q-COMBAT-013`, and has one `##` section for each kind of evidence, in this order:

1. Static: a reading of the executable or data files settles it.
2. Emulated call: a call of one function of the original in the emulator harness settles it (see [Emulated calls](#emulated-calls)).
3. Agent run: a run of the original that the runtime record says an agent can make alone.
4. Live session: a run that needs a person to run the original while an agent measures it (see [Live sessions](#live-sessions)).
5. Source: a document that has to be found, bought or read.
6. Blocked: work stopped until something else changes.

An item is a single list entry:

```markdown
- Q-COMBAT-012. RULE-COMBAT-012, FMT-STATE-001: Which of two gangs attacking
  each other rolls first? Settles it: the order of the two calls at the
  resolver's entry, and one experiment from a save where both attack.
  Blocks: slice 4.
```

It gives its ID, the entries, the question as a question, what would settle it, and the slice it blocks or `none`. An item asks one question that one piece of evidence can settle. A question whose parts can be answered separately, such as a formula and the caller that decides when it runs, is split into items before anyone works on it. It goes in the queue file of the area of the first entry it names. The ID is `Q-`, the area of the file it was created in and a number, taken from the file's `Next ID:` line, which then goes up by one. It is never reused, even after the item is deleted, and it stays the same when the item moves to another section or another area. When an item is split, one half keeps the ID and the other takes a new one. Everything outside the queue refers to an item by its ID: the handovers, live session requests, slice exits, `Spec gap:` notes and status blocks. Two branches that create the same item ID are handled the way the standard handles two that create the same spec ID.

When the runtime record changes, the items it affects move between Agent run and Live session in the same commit. An item that has already been worked on adds `Tried:` saying what was examined and why that did not settle the question, so that nobody repeats it. What the attempt did learn about the original goes in the spec as a finding first, and `Tried:` names the finding. An item under Blocked adds `Waiting on:` with what has to change first.

An item is closed by recording its answer in the spec (a finding or an experiment, and the status of the entries it concerns) and deleting the item in the same commit. An item that turns out to be two questions becomes two items. A new question found along the way becomes a new item at once, even if nobody will look at it for months.

A static reading that settles an item raises its entries as far as the reading goes. A complete reading, as the standard defines it, makes an entry `established` with no run, and that is how most entries are meant to get there. A reading that settles the question without being complete leaves the entry `supported`, and the same commit adds a Static item for what the reading still has to cover, such as a caller nobody has found or an indirect call nobody has resolved, and an Emulated call item where the harness can reach the functions the reading covers. Only an entry that depends on something the code does not decide needs a run. For such an entry, where the runtime record says an agent or a person can make the run, the same commit adds an item under Agent run or Live session for the experiment that would confirm the reading. Where nobody can, it adds none, and the entry's Open questions section says which observation of the original would confirm it, so that a capture that arrives later with a report can.

An attempt that does not settle an item records what it tried under `Tried:`, and the item is taken up again only with something the first attempt did not have: new evidence, a new tool, or a reading of the code nobody has tried. If the second attempt ends in the same place, the item moves to the section of the evidence that would change the outcome, with what was tried: Emulated call, Agent run or Live session for a run, Source for a document. It goes to Blocked only when that evidence is out of reach for now, such as a tool nobody has, a second edition the owner does not own, or a run the runtime record says nobody can make, and its `Waiting on:` names what is missing.

### Order of work

Items are picked in this order, from every section alike. Within each step, an item under Static comes first, then one under Emulated call, then one that needs a run of the game, and [Running the original](#running-the-original) says when a run item can be taken up at all.

1. Items that block the current slice.
2. Items that others depend on: the random number generator, the main loop and the order of its phases, the save format and the structures the game keeps. Most other experiments need these.
3. Items where one piece of evidence raises the most entries. A complete reading has to meet every condition the standard sets for one, and an experiment that raises an entry to `established` has to cover everything the entry says, every branch of a procedure and, for a random outcome, enough repetitions for its comparison, and a rule stays below `established` while any glossary claim it relies on is `(unknown)`. Where the experiment covers only part of the entry, the entry stays `supported` and its Open questions say which part is left.
4. Items with the cheapest evidence: a data file before a static reading.

## The life of a claim

A claim is what a rule, format, screen or bug entry says the original does. It changes only in a research batch, when the evidence changes, and the entry's status says how far that evidence goes.

A claim starts as a listing. The Survey, an implementation batch that meets a gap, a report or a function no entry cites gives the behaviour an `unknown` entry, which claims nothing yet, and a queue item. A manual or another outside source can give the entry a description at once, at `sourced`, and only evidence from the original takes it further.

Research then forms the competing readings. A reading is plausible while the evidence for it is only circumstantial: sizes that divide exactly, a pattern in a file's values, a name, the manual, what similar games do. Such evidence is recorded as findings but never raises a status, as the standard's [direct evidence](/documentation-standard/#direct-and-circumstantial-evidence) rule says. While more than one reading is still standing, or the one left rests only on circumstantial evidence, they sit in the entry's Open questions section, each with what it claims, the findings and experiments for and against it by ID, and the evidence that would rule it out, and each has a queue item whose Settles it names that evidence. Outside the spec a reading is cited by its queue item's ID, and the commit that closes the item names it in a `Queue:` trailer, so the reading can be found in the history after the item is gone. Readings get no IDs of their own. An entry whose readings are all still open stays `unknown`, or `sourced` if a source describes it, whatever evidence the readings cite.

When direct evidence, the code that produces the behaviour, rules out every reading but one, that reading becomes the entry's description at `supported`, and the readings ruled out move to the Alternatives section of the finding that ruled them out. A description says no more than its evidence shows. What the evidence does not reach stays in Open questions, as readings or as questions. A source that disagrees with the findings stays cited, with the disagreement written in the entry's body and in the source's Known errors section, as the standard says.

The entry is corroborated, and reaches `established`, when the reading of the code is complete: every branch, every caller and every write to what it reads, every indirect call resolved, and nothing left to timing, interrupts or the operating system. Most entries get there by reading alone, and the entry lists the findings of that reading in `complete_reading`. A reading that is not complete gets there when an [emulated call](#emulated-calls) agrees with it on every branch the entry describes, provided the reading of everything outside the function is complete: every caller, every write to the state it reads, and so everything the entry's When it runs and Inputs sections say. An entry that does depend on something the code does not decide gets there only when a run of the original agrees with the reading, and the run can be an agent run, a live session, or a capture a tester sent with a report. A parity row still reaches `validated` only through tests against evidence recorded from the original running, as the standard requires, so a rule or screen row established by reading alone stops at `implemented` until such evidence exists. For a rule the code decides, that evidence is usually an emulated call's fixture.

Evidence that contradicts the description makes the entry `disputed` at any point, even after its row was `validated`, and the row shows `disputed` until the dispute is settled. If the contradicting finding or experiment turns out to be wrong, it is superseded, and the entry goes back to the status its remaining evidence supports. If the description turns out to be wrong, the entry is superseded by a corrected entry, by the parts it splits into, or, when the mechanic does not exist, by the findings that show it, and citations and code follow `superseded_by` as [Research batches](#research-batches) describes. A superseded entry is never deleted, and it keeps the links it had.

Implementation meets a claim only as the entry's description, or as a question. An open reading never reaches code: a row whose entry is `unknown` cannot be `complete`, and code that needs an open reading stops at a `Spec gap:` note or a `PLACEHOLDER:` comment.

## Batches

A batch is one unit of work and ends in one commit, or one pull request where the repository uses them. It is small enough to review in one sitting, and it leaves the documentation check and the fast validation gate passing. A batch is research, implementation or tooling, and never more than one of them.

### Research batches

A research batch settles one queue item, a few items about the same entry, or the items of one area that one live session's recordings settle, or it triages one report from testing. It states the question, forms the competing readings, looks for the evidence that could rule each one out, and records the result as findings or experiments under the standard. Readings that are still open when the batch ends are written down, never left in the session's memory. They go in the Open questions section of the entry they concern, one per reading, each with what it claims, the findings and experiments for and against it by ID, and the evidence that would rule it out, and each has a queue item whose Settles it names that evidence (see [The life of a claim](#the-life-of-a-claim)). A reading that the evidence rules out moves to the Alternatives section of the finding that ruled it out, and one that survives becomes what the entry says, at the status its evidence supports. A reading never reaches code on its own: an implementation batch builds only what an entry says, and meets an open reading as a question. The entries the evidence concerns take the status it supports, the item is deleted or updated with what was tried, and new questions become new items. The batch leaves the documentation check passing, so it makes the changes elsewhere that the check requires of what it did to the spec, and no others:

- where an entry's status changes, the Spec status and Status columns of its parity row, and where it becomes `disputed`, a note in the row's Notes naming the evidence in the entry's `conflicting`, which goes again when the dispute is settled;
- for a new rule, format or screen entry, a parity row with Code `missing`, and for one that is retitled, the Title of its row;
- for an entry it supersedes, its row is removed, and every citation of it in `src/`, `tests/`, `tools/`, `parity/` and the deviations that have not been dropped moves to the entries its `superseded_by` names, all of them where it names several. A dropped deviation's Departs from and Replaces items stay as they were when it was dropped, as the standard says;
- where code was written for a superseded entry, each rule, format or screen that replaced it takes Code `partial` in its row and a note naming the old entry, so that an implementation batch checks the code against the new one. Where only findings or experiments replaced it, because the mechanic does not exist, the code cites them from then on, and the next implementation session removes it (see [Implementation batches](#implementation-batches));
- where a deviation's Departs from moves to a new entry, the Deviations column of that entry's row;
- for a `Spec gap:` note, the queue item's ID when the note becomes an item, and the note's removal when the item is closed;
- for a report that shows the rebuild departing from the spec, Code `partial` in place of `complete` and a `Defect (R-NNN):` note, as [Reports from testing](#reports-from-testing) describes;
- `PARITY.md` and `spec/index/`, which the documentation check writes again.

Moving a citation changes an ID and nothing else. Apart from that, code is not changed, beyond tools in `tools/` the research needed.

### Implementation batches

An implementation batch brings parity rows of the current slice up to `implemented`, `deviated` or `validated`. It works from the spec only. It does not open Ghidra, a decompiler listing, a debugger log or research notes, and it learns what the original does by reading spec entries. Where the spec does not say enough to write the code, the batch stops at that point and writes down what the code needs to know as a question in the entry's Open questions section. Where no entry describes the behaviour at all, it creates an `unknown` entry that holds only that question, with its parity row. It then either leaves the row `partial` or writes the code with a `PLACEHOLDER:` comment citing the entry, and it starts the row's Notes with `Spec gap:` and the question. The next research session turns each such note into a queue item and adds the item's ID to the note, `Spec gap (Q-COMBAT-014):`, so the note shows that research has it. The note stays until the research batch that closes the item removes it, and from then on the row is `partial` with no note, which tells an implementation batch that the spec now answers the question. An implementation batch never reads the queue, since its items hold what research tried.

Every decision the original makes, whether a gate, a chain of flags, a branch table, the choice of an outcome or a change of state, lives in the rebuild's rules layer as a function of the serializable game state and its inputs, even where only the presentation calls it. The presentation turns those decisions into screens, art, input and timing, and is never the only home of a rule. A rule already written inline in a screen handler moves to the rules layer the first time a batch touches it. If showing a behaviour needs a window, a graphics device or the original's assets, its rule is not in the rules layer yet. Each entry's branches are implemented whole: every branch the entry describes has its own test over synthetic state, including the branches the presentation cannot reach yet and the arms of a guard that look impossible. A branch that lives only in a comment is the one that gets implemented inverted, and a play session reaches only the branches it happens to take. A bug traced to branch logic in the presentation is fixed by moving the rule into the rules layer, testing every branch, and fixing it there.

Where the rebuild computes a rule with a different algorithm from the procedure in its entry, the claim that the two agree names the state and outputs it compares, including whatever a later call reads. For a search that keeps a queue of work, the tests cover a node queued twice, a node whose stored score improves while an older queue entry for it is still waiting, and a search that stops at its step limit straight after adding a node's successors and is then resumed. For other algorithms they cover the limits and the failure paths the entry describes. Two algorithms that reach the same result can still leave different state behind, raise events in a different order or carry on differently when called again, so a matching result shows only that the results match. These tests compare the rebuild with the spec, and like any other test over synthetic state they never raise an entry's status or validate a parity row.

When a batch connects the parts of the rebuild that use a rule, it tests the intermediate states the spec describes. It follows one position or other shared value from where it is set up, through input, the rules layer's updates and the presentation, and through a save and a restore, with a different value on each axis so that swapped coordinates or fields show up. Each change the rules layer makes visible is compared with the presentation that shows it: which object it belongs to and on which update it takes effect, including when a unit turns and when the change is abandoned part way through. A check that the game loads, or that the final image matches, is labelled as that and says nothing about the path in between.

The tests also cover what a second actor sees after a reservation or an update to a shared table, including while the first actor is between two of the points the spec marks with `# visible:` comments. Where the spec says a reservation survives a refresh of the table that holds it, or a save and restore, a test checks that. Where the spec says a side effect changes a route, a test checks that the route, and the timing the rules layer keeps for it, change along with what is drawn. A rejection by whatever requested the action is tested apart from a route that fails, and an object the rebuild keeps between updates is tested after the state it depends on has changed, such as which squares are occupied. A point the spec does not describe is a gap in the spec like any other, and the batch handles it as above: it adds the question and starts the row's Notes with `Spec gap:`, then either leaves the row `partial` or writes the code with a `PLACEHOLDER:` comment. None of these tests is evidence from the original, so they do not bring a row to `validated`.

Code in `src/` cites rules, formats, screens and bugs. A citation there of a finding or an experiment marks code written for an entry that turned out to describe nothing, and an implementation session removes that code, with the tests that exercise it, before it picks any rows.

This keeps the clean-room separation that the methodology asks for, and it is also a test of the spec. The standard's aim is that someone could build a second engine from the documentation alone, and every implementation batch tries exactly that on a small scale. Under `spec/` the batch only asks: it adds questions and `unknown` entries that hold nothing else, and it never adds evidence or changes a status or a description. With coding agents the separation costs little: an implementation batch runs in a fresh session, or a subagent, that is given the entry IDs and nothing from the research that produced them.

A feature that is not part of a parity row, such as online play, an optional improved AI or an accessibility setting, is ordinary engineering. It goes in implementation batches of its own, and a decision in `docs/DECISIONS.md` says why the project takes it on. It never counts towards a slice's exit.

### Tooling batches

A tooling batch builds something the stages need that has no parity row of its own: the extractor, the Ghidra scripts, the emulator harness, the export of the function inventories, the scripts a live session measures with, the rebuild's headless runner and the test harness that replays fixtures. It needs no decision and no approval, because this page already asks for it. A tool that reads the original belongs to the research side, and one that runs the rebuild to the implementation side, and each is built in a session of that side.

A tooling batch that adds or extends an analysis reporter checks the supported queries against the standard's [bounded analysis report contracts](/documentation-standard/#bounded-analysis-reports). It records the executable acceptance cases, unsupported cases and remaining limits separately from any changes to research guidance. A request for reporter behaviour that a game's repository records stays open until the reporter has been checked against that request's own case; adopting the guidance alone does not close it. The verification record names the request, case, source fingerprint, tool version, input assumptions, expected observation and outcome, and describes the case and observation in the researcher's words. It says which results require a callee that has not been read to return or preserve state. Configurations and reports derived from the original stay in `GAME_DIR`, and a regression test added to the shared reporter uses synthetic instructions and state.

### Checkpoints and replay

A checkpoint or replay API in the rebuild states what its identity covers. A state fingerprint that a test compares with a recording covers only the fields that test compares. The identity of a save or a replay has to cover everything that decides how the game continues from it, so a fingerprint can serve as one only where it has been shown to cover all of that. The identity includes the resources that drive behaviour, such as motion banks, actor profiles, navigation costs and animation instructions, wherever they affect the next update of the game state, even when their filenames are unchanged. A map's name and dimensions do not identify its content. The identity is a hash of named fields in a portable encoding, and the API states that encoding, its version, and whether a checkpoint with an older identity is migrated or rejected.

Tests change each of those inputs on its own, including fields a diagnostic export leaves out and resources changed under the same filename, and check that a checkpoint whose identity no longer matches is rejected without changing the host, the running instance of the rebuild that was asked to restore it. Passing them shows that the rebuild's checkpoints agree with themselves. They say nothing about compatibility with the original's save format, which needs format entries and rule evidence of its own.

The contract also says whether one snapshot can be restored several times with each copy run on its own, or must be restored into a fresh host. A snapshot record that never changes can still point at state that does, such as how far an animation has played, the open list of a path search that is still running, or the buffer that collects the sounds and messages an update emits. Continuation is deterministic only once each of those is restored or rebuilt as well. Where the rebuild deliberately leaves some state out of a save, such as a path search paused halfway, the contract says either that saving is refused while that state exists or how the state is rebuilt from what was saved, since the rules layer decides from the serializable game state alone. The state is never dropped without saying so.

Tests compare a run that carries on uninterrupted with one restored into a fresh host, from checkpoints taken just before and just after a point where the rules hand a result on, such as a unit arriving at the end of its path. Where an animation can advance while the unit stands still, its progress is part of the compared state. Tests also restore an older snapshot a second time: a contract that allows several copies keeps each copy's effects apart, and one that allows a single line of play rejects the stale snapshot without advancing the host. They cover an order replaced or interrupted partway, and compare the effects emitted along the way as well as the final values. These tests compare the rebuild with itself, so they never bring a parity row to `validated`.

### Commits

A commit message says what changed and what evidence it rests on, in plain words. It ends with trailers naming the entries the batch created or changed, so that the git history can be searched by ID:

```text
Settle which gang rolls first in a mutual attack

The resolver calls the attacker's roll before the defender's, and a run of
200 attacks from a patched save agrees.

Spec: RULE-COMBAT-012, FND-COMBAT-031, EXP-COMBAT-009
```

An implementation batch uses the same trailer for the entries it implemented. A batch that changes parity rows adds `Parity:` with the rows whose status it changed, and one that closes queue items adds `Queue:` with their IDs, so that an item, and the reading it tested, can still be found after the item is deleted.

## Sessions

A session is one sitting of work, by a person or an agent. It may hold several batches, all of the same side: research and tooling that reads the original, or implementation and tooling that runs the rebuild.

A session's handover is the handover section of the goal file it works under, or `docs/HANDOVER.md` when it works under no goal, so two sessions running at once never write the same one.

At the start: read the session's handover, and for a goal the rest of its file, check the branch and the working tree against what the handover says, and run the documentation check. A research session then triages open reports it may take, turns any `Spec gap:` note that has no queue item ID yet into a queue item, and picks the next item from its goal or the queue in the order above. An implementation session removes any code in `src/` that cites a finding or an experiment, fixes rows with a `Defect` note, then picks the next rows of the current slice from its goal or the plan, and opens neither the queue nor the files of research goals.

At the end: stop every process the session started (Ghidra, the original game, test hosts), leave processes that belong to anyone else alone, and release the run lock if the session holds it. Every finished batch has its commit already. Work left half done is finished, discarded, or left out of the batch commits and described under the handover's unfinished work, since a commit that fails the documentation check or the fast gate, or holds two kinds of batch, is never made on the working branch. Where the working tree does not outlive the session, as in a cloud container, the half-done work is committed to a branch of its own, `wip/` followed by the working branch's name, and pushed there, and the handover names it. Then rewrite the handover to describe the state now, commit it on its own, which is the one commit that is not a batch, and push the working branch, unless the repository's `AGENTS.md` says the owner pushes or the person running the session says otherwise. Anyone who needs to know how far the branch is ahead of its remote asks Git, and the handover never records it.

## Running the original

Runs of the original that an agent drives are the last resort for each question. They are fragile: an old game loses window focus, depends on timing, behaves differently under an emulator's automation, and stops at a dialog nobody expected, and a result that cannot be repeated is not evidence. An [emulated call](#emulated-calls) starts no process of the game, so none of this section applies to it, and it comes before any run in the order of work. Runs of the game are kept to the questions nothing else can answer, and every agent run is scripted, starts from a fixed state, and records enough to be repeated, as [Recorded runs](#recorded-runs) describes. An item under Agent run or Live session is taken up only after a static reading of its own question has been tried and recorded under `Tried:`, or when it asks for the run that confirms a static reading of an entry that depends on something the code does not decide. A question that a static reading can settle is settled that way, even where a run could settle it too. Runs do not wait for the rest of the queue to be empty. They take their place in the [order of work](#order-of-work), where a run that blocks the current slice comes before static work that does not, and within one step of the order the static items come first.

Several agents usually work on different games on the same machine at once, under one account or several, and two runs at the same time take each other's window focus, input, emulator or debugger. An agent therefore runs an original only while it holds the machine's run lock. That is one file for the whole machine, shared by every game repository and every account on it: `C:\ProgramData\refurbished-dinosaurs\run.lock` on Windows and `/var/tmp/refurbished-dinosaurs/run.lock` elsewhere, unless the environment variable `REFURBISHED_DINOSAURS_RUN_LOCK` names another path, which the owner then sets the same way for every account. The owner creates the directory once, so that every account that runs sessions can create and delete files in it.

The agent takes the lock with an exclusive create, one that fails when the file already exists (`[IO.File]::Open($path, 'CreateNew')` in PowerShell, or a redirect after `set -C` in a POSIX shell), and never by checking for the file and then writing it. The file names the repository, the session, the time the lock was taken, and the process ID of every process the run starts, added as each one starts. If the create fails, the agent does not wait, and goes on with work that needs no run. It deletes the file when its run ends, and never deletes one it did not create, with one exception. A lock is abandoned when it was taken more than an hour ago and none of the processes it names is still running. Any agent may then clear it by renaming it to a name of its own, which only one agent can do, checking that the renamed file is still the lock it judged abandoned (and renaming it back if not), and deleting it. The handover says which lock it cleared. An abandoned lock the agent cannot clear, for example because another account owns the file, goes in the handover's blockers for the owner.

Outside a live session, an agent never attaches to, sends input to or stops a process it did not start.

### Recorded runs

In the [Chaos Overlords](https://github.com/kibertoad/chaos-overlords-new-chrome) restoration, a script called the probe starts the original under a debugger and plays new games for up to fifteen turns with nobody at the keyboard, and a test replays every run against the rebuild. The same rules apply wherever the runtime record says an agent can start the original without a person, read its memory and set breakpoints while it runs. In a live session the agent reads the process and never writes to it, so there the recording of draws and the copies of memory apply, and the memory writes and the replaced seed do not.

A run records the seed the game gives its random number generator and every call of the function that draws from it, each with the rule that made it, the bound it asked for and the result it got, as well as the state at the end. The probe finds the rule from the call instruction: the function inventory gives the function that holds it, and the rule entry that cites that function is the one the draw is recorded under, by its ID. A draw from a function no rule cites stops the recording, and the function gets an `unknown` entry and a queue item the way any function no entry cites does. The rebuild's code cites the same rules, so each of its draws carries the rule ID too, and the test that replays the run compares the rebuild's draws with the recording one by one, the rule as well as the bound and the result. When one differs, it names the first draw that differs by its position and the rule each side made it under. The failure speaks in entry IDs, which an implementation session may read, and no address of the original reaches a fixture or a test. That puts a divergence at one decision, where a comparison of the end state would only show that something went wrong earlier, and it catches differences that leave the end state alone, such as two rules that make the same draws in a different order. Where the rebuild reproduces the generator exactly, the comparison needs no tolerance: every draw and the generator's position at the end agree, or the test fails. The rebuild's generator takes the rule ID with every draw and offers a hook that lets a test observe each draw, and nothing else uses the hook.

A run reaches its state through breakpoints and memory writes wherever it can, because input is the part of a run that breaks. The settings a setup screen would commit are written at their addresses before the game reads them. A movie is ended by setting the flag its loop polls, since a click posted between two polls is lost. Options that stop play to wait for the player, such as warnings and detailed reports, are switched off in memory, and the game runs in a window so that it cannot take the screen. Where the run has to click, it clicks at coordinates the screen's entry gives, and it waits for a state the probe can read, such as a counter reaching a value or a breakpoint in the loop that waits for input, and never for a fixed time or for a stretch with no draws. Every address the probe writes or watches is a format field, or a global whose glossary term gives evidence for its address rather than `(unknown)`. Every field it writes is `supported` or `established`, the same as for a save patch, and the experiment's Setup lists each write.

The probe can replace the seed as the game seeds its generator, so that a recorded run can be played again. A repeated run counts only once it has made the same draws as the recording, which the probe checks before anything else. When the rebuild diverges from a run, the run is repeated with its seed, and the probe copies the writable memory at the entry of the draw that differs, counted from zero as the test counts. The copy shows what the original held at that moment, and a static reading of the code that produced it shows why. The reading becomes the finding, and the values the copy showed go in the experiment's Observations. The finding changes the entry it concerns, and an implementation batch then changes the rebuild to what the entry says. The rebuild is never fitted to the recording until the draws agree. A copy of memory can hold the game's text and code, so it is never committed. It is kept in `GAME_DIR/captures/`, named by its hash, which the Observations give, and the fixture holds only numbers read from it by format field path or glossary name.

A run that passes shows little on its own. Each experiment has at least two runs with different seeds, and new experiments vary what decides which code runs: the settings a new game offers, the scenario, the seats given to people, and the length of the run. Long runs matter most, because they reach states that short ones never do. A list that keeps values from one call to the next, a table that fills up, and a loop without a bound that reads past the end of its array all showed up only after several turns. What such a read finds is part of the original's behaviour, so the finding records what lies in memory after the array, the entry describes it, and the rebuild follows the entry. A read whose result would put the game in a state it cannot continue from, such as a unit on a map square that does not exist, is a candidate for a deviation, since the [methodology](/methodology/) lets the rebuild fix crashes and corrupted state.

An installed game can carry compatibility settings tied to its path, such as a demand for elevation. A copy of the executable in another directory escapes them, and links to the installation's data directories let it still find its files. The settings the run does need are set for the child process alone. `docs/RUNTIME.md` gives the probe's command line, and with the experiment's Setup that is enough for anyone to repeat a run from its experiment entry.

### Live sessions

Where the runtime record says a run needs a person, the evidence comes from a live session: the maintainer runs the original on their machine and plays to each point the script names, tells the agent when the game is there, and the agent takes its measurement against the running process (a memory read, a breakpoint, a dump of a structure, a frame capture) before the maintainer carries on. The agent holds the run lock for the whole session and adds the process's ID to it. The process the maintainer started for the session is the one process the agent may attach to and read without having started it. It never sends that process input or stops it, since the maintainer plays. The maintainer's time is the scarcest resource a restoration has, so a session is prepared so that it spends that time only on what needs a person.

A live session is requested, never assumed, and the request is a file in the repository, so that the owner can find it after the agent's session has ended and plan a sitting around it. `docs/live-sessions/NAME.md` opens with a Status line (`requested`, `accepted` with the date agreed, or `declined` with the reason), then names the build and the machine it needs, the queue items it would settle by their IDs, the slices they block, and how long the session is expected to take, and then gives the script. The owner answers by changing the Status line. An agent does not ask again for a declined session without something the request did not have, such as a new item that blocks a slice.

An agent writes a request once the rules above let each Live session item it would settle be taken up, and when the items block a slice or are enough to fill a sitting. A request covers only items the goal writing it may take up (see [Coding agents and long-running goals](#coding-agents-and-long-running-goals)). Until the owner accepts, the items stay under Live session, the plan keeps the rows that need them at `implemented`, and work goes on with everything a live session does not block. An agent never waits idle for one.

The script lists steps. Each step gives the entries it concerns, how to reach the starting state (a save patch, or the choices on the way into a new game), the one input to vary, the moment the maintainer signals, what the agent measures then and in which form, and where captures go (`GAME_DIR/captures/`, named by hash). The measurements are written and tried beforehand as far as the original allows without a person, the addresses to read and the breakpoints to set come from the static readings, and steps from the same starting state follow each other. Nobody should need to ask a question during the session.

After the session, the request's Status becomes `held` with the date. The recordings are turned into findings and experiments with fixtures, as the standard describes, in one research batch per area, and each batch deletes the items of its area that the recordings settle. The last of those batches deletes the request file. What the session showed about the tools, such as a measurement that a person has to trigger or one that works unattended after all, goes into the runtime record in the first of them.

## Emulated calls

An emulated call runs one function of the original executable in a CPU emulator, on arguments and memory the experimenter chooses, and records what the function returns and what it writes. Nothing else of the game runs: there is no window, no timer, no input and no sound, so none of the fragility of the runs above applies. An emulated call starts no process of the game and needs no run lock, and any number of sessions can make them at once.

Any agent may build the harness and make emulated calls in every game repository. That holds where the runtime record says nobody can run the game, and where the repository's `AGENTS.md` keeps agents from starting, controlling or attaching to the original or its emulator, since an emulated call does none of those things. A repository can narrow what this page lets agents do with the original running, and such a rule covers runs of the game only. Emulated calls need no decision from the owner. A call takes well under a millisecond, so one experiment can cover thousands of cases, and every run repeats exactly from its fixture.

The emulator is [Unicorn](https://www.unicorn-engine.org), driven through its Python bindings. It emulates the CPU and nothing else, which for x86 covers 16-bit real mode, the 386 instructions that 16-bit compilers emitted, and 32-bit code, and it lets the harness stop at any address.

### What an emulated call can settle

As evidence, an emulated call counts as a run of the original for everything the code decides. It checks what a function computes from its inputs: a formula, the order of its draws from the random number generator, its arithmetic at the edges of its types, the fields of a structure it builds, the bytes a decoder produces. It is recorded as an experiment, as the standard's [Experiments](/documentation-standard/#experiments) section describes, so it counts wherever the standard asks for a run of the original. An emulated call checks the body of a function and nothing around it. An entry reaches `established` through one only when the cases reach every branch the entry describes, and when the static reading of what lies outside the function is complete: every caller, every write to the state the function reads, and so what the entry's When it runs and Inputs sections say. Until both hold, the entry stays `supported`, and its Open questions say which branches or callers are left. A rule row whose tests replay the fixture reaches `validated`. Every rule the code decides and whose functions the harness can reach therefore gets an Emulated call item once its reading is recorded, even when a complete reading has already established it, because that item's fixture is what its row's tests need.

It settles nothing the code does not decide. A rule whose procedure has a `# may run:` comment is never established or validated by emulated calls alone, since where the interruption falls is exactly what the harness does not model. The harness supplies the operating system, the interrupts, the hardware ports and the clock, so an entry that depends on them still needs an agent run, a live session or a capture. A write the harness sees at a video address or an I/O port shows that the function made the write, and nothing about the pixels, the sound or the timing that followed. It covers only the functions it calls: which callers reach a function, and with what state, is a question for the static reading or for a run of the whole game. Where the rules live in a script or bytecode that an interpreter in the executable runs, as in SCI or SCUMM games, calling the interpreter settles nothing about a rule. Overlay code, which the game loads while it runs, is out of reach as well.

A static reading of code that reads or writes I/O ports records each port access and where it falls on every path that reaches it. An emulated call can check which addresses a copy to video memory writes and how many bytes it copies, and what appears on the screen stays unconfirmed. A write to a VGA register can change what a later copy does to video memory, and a path that copies to a scratch buffer instead leaves the other paths as dependent on the hardware as before. The finding says which of the effects are CPU registers the function restores before it returns, which are the contents of a scratch buffer that other code shares, and which are state held by the device.

An experiment whose harness maps ordinary RAM where video memory would be, or whose stubs supply port values, names those substitutions in its Setup section and says which memory effects it compares. It also states what that comparison assumes about the slot, the segment, the byte count, the mask and any addresses that alias each other. Its fixture gives each port value a stub supplies by the glossary name of the value from outside the game it stands for, and the video memory it compares through the format entry for that memory's layout, the same as any other memory. The experiment is evidence for those memory effects and nothing more. Whether the card holds valid data in its own memory, and the pixels that result, need evidence of their own, which neither a port value from a stub nor a copy that ran to the end can give.

### The harness

Each game repository keeps its harness in `tools/emu/`, built in a tooling batch on the research side. The harness has a loader for the executable's format (MZ, NE or PE) that checks the file's hash against the build entry, maps its segments or sections, and applies the file's own relocations. It has an explicit stub for each import, interrupt or port access that a function under test reaches. Any import, interrupt or port access without one stops the run with an error that names the instruction and the service or port, and for a port access whether it reads or writes and how wide it is. The value a CPU emulator returns from a port nobody modelled does not count as a stub. A stub does only what the call needs, such as handing out heap memory or reading a file, because each stub is our model of the operating system or the hardware and adds nothing from the original. A function that reaches the screen, sound, input or the timer cannot be settled by an emulated call.

The harness is committed. It reads the executable from `GAME_DIR` the way a test does, and holds no bytes, disassembly or strings from it. `docs/RUNTIME.md` records the Unicorn version, the builds the harness loads, and the stubs it has, including any port models and the limits of what they test.

### Protocol

An item under Emulated call names the function a static finding has located and the entries whose reading it tests. One research batch settles it:

1. It states the reading under test from the entry's description, or the competing readings from its Open questions, each written in `tools/emu/` as a procedure that takes the same inputs as the function.
2. It sets up the state the function reads: its arguments, the globals it reads, such as the generator's state, and the structures it follows pointers into. The harness starts from the memory the executable initialises when it loads and changes only what the experiment needs. A structure is written through its format entry's layout table, and every field written must be `supported` or `established`, the same as for a save patch. The fixture gives the arguments by the names in the rule's Parameters section and the memory by format field path or glossary name, never by register, stack offset or address. The harness does the translation to the original's calling convention and layout, so that a test of the rebuild can replay the fixture without knowing either.
3. It chooses the cases: the values the reading treats specially, the edges of each type (zero, one, negative values, the largest signed and unsigned values, and ranges that overflow the width the code computes in), inputs that take every branch the entry describes, and random cases drawn from a seed the experiment records. The harness records the instructions each run executed, and the experiment gives the share of the function's branches its cases reached. A branch no case reached is not covered, and the entry's Open questions say so.
4. It runs every case in the emulator and in each reading, and compares the results exactly. A reading that disagrees on any case is ruled out, and the case goes in the Alternatives section of the finding that rules it out.
5. It records the experiment and moves the entries along [the life of a claim](#the-life-of-a-claim): `established` where the one reading left agrees on every case, the cases reached every branch the entry describes, and the reading of its callers and inputs is complete; `supported` with the gap in Open questions where any of those is missing; `disputed` where the results contradict its description.

When a result looks wrong, or disputes a reading that looked complete, the batch may confirm it by replaying the same cases in Ghidra's p-code emulator, which loads the executable and runs the CPU with code of its own, and recording in the experiment whether the two emulators agree. When they disagree, the harness or an emulator has a defect, and nothing in the spec changes until it is fixed. The item moves to Blocked with `Waiting on:` naming the defect and the case that shows it, and a tooling batch fixes the harness or records the emulator's defect in `docs/RUNTIME.md`.

A function whose results do not settle an entry, because a stub or a structure the harness would need is missing, keeps its item, with `Tried:` naming what was missing.

## Reports from testing

People play the rebuild and report what does not work the way they expect, in words and with screenshots. They do it when they happen to, and nobody schedules it. An agent makes no assumption about when a report arrives or whether one ever will: it never waits for one, never makes testing a step of its own work, and never counts a row as checked because someone might play it. It handles a report when one arrives and otherwise keeps working through the queue and the slices.

A report is recorded at once, by whichever session receives it and on either side of the clean room, as `docs/reports/R-NNN.md`, numbered from the `Next ID:` line in `docs/reports/README.md` the way queue items are. The file gives who reported it and when, the rebuild's commit or release they played, what they said in their own words, the steps to get there if they gave them, and each screenshot by its hash. Screenshots show the game's images, so none is committed. They go into the local reference store below, and the report cites them by hash.

Triage compares the rebuild with the original, so it is research-side work. A research session triages open reports before it picks a queue item, and each report takes one research batch. The session is one whose goal claims the areas the report concerns, or one under no goal. Triage decides which of these the report is, and the batch deletes the report file once its content is somewhere else:

- The rebuild does not do what the spec says, such as a crash, a value an entry gives, or a screen that differs from a capture the spec records. The row goes back to Code `partial` if it was `complete`, and its Notes start with `Defect (R-012):` and what the rebuild does against what the entry says, in the spec's terms only. An implementation session takes rows with a `Defect` note before other rows, adds a test that fails without the fix, and removes the note.
- The spec is silent, or the report suggests the original does something else. The entry gets an open question, or an `unknown` entry is created, and a queue item names the report's ID. What a tester remembers of the original is a lead to check, not evidence.
- The report comes with a screenshot or recording of the original, of a known build, and says how the game got to that point. That is a dynamic finding, recorded with the capture's hash under the standard's rules for captures. It is checked against the Live session and Agent run items it could settle as well as the report's own question. A tester's screenshot is rarely taken at the canvas size with scaling and filtering off, so it can show what is on the screen but is not compared pixel for pixel.
- The rebuild does what a deviation that is switched on says. Nothing changes, and the reply names the deviation.
- The report does not say enough to tell. It stays open, with a `Missing:` line saying what would decide it, and is taken up again only when more arrives. The work goes on without it.

The session that receives a report answers the reporter with what happened, naming the IDs. A commit that settles one ends with a `Report:` trailer, so its history can be found by the report's ID. An implementation session that receives a report directly acts on it only as far as the spec already says what the rebuild should do, and records everything else for research. It may look at the tester's screenshots of the rebuild, never at screenshots of the original.

Screenshots of the original are what a report is compared with, so the project keeps a durable local store of them. It is `GAME_DIR/captures/`, where the standard already keeps the captures and saves that cannot be committed, each named by its hash. It lives with the maintainer's copy of the game, is backed up with it, and loses nothing when a report or item closes, since findings cite captures by hash. A local index in the store, `GAME_DIR/captures/index.tsv`, gives each capture's hash, build, the screen entry it shows and the state it was taken in, so that the capture for a reported screen can be found. The index is not committed, since its descriptions of states can hold the game's text. Screenshots of the rebuild from reports go in `GAME_DIR/reports/`, named the same way. Where the store has no capture of a screen a report concerns, the queue item for it says which capture would settle it.

## Measuring progress

Progress is measured by numbers a script computes from the repository, never by numbers someone writes down:

- the parity totals in `PARITY.md`, by Status and by Code;
- the spec's entries by status, from the status index;
- for each analysed file, the share of its functions, and of its bytes, that some entry cites, measured against its function inventory;
- the share of the manifest's data files whose format entry is `supported` or higher;
- the number of queue items of each kind of evidence;
- the number of open reports.

A function inventory lists the functions of one file the analysis reads: the executable that runs the rules, and each DLL, overlay or other file of code the analysis goes into. It is `coverage/` at the root of the game repository, then the build's ID, then the file's path as the manifest writes it with `.tsv` added: `coverage/BLD-GOG-EN-1.1/Chaos Overlords.exe.tsv`. A file read from the disc keeps its path on the disc, and its `CD:` or `CD2:` prefix becomes a directory named `@CD` or `@CD2`, since Windows allows no colon in a file name: `coverage/BLD-CD-EN-1.0/@CD/GAME.EXE.tsv`. A script in `tools/` exports it from the analysis database, which stays on the researcher's machine as the methodology requires, and exports it again whenever the analysis finds functions it had missed or merged.

Each line gives a function's start address, written the way the standard's notation writes one for that file, its size in bytes, and optionally a name the researcher gave it and the reason it is out of scope. That is a catalogue of where the code is and how much of it there is, and it reproduces none of it, the way a list of a film's scenes with their timestamps shows none of the film. It never holds decompiled code, disassembly, bytes or byte signatures, strings or constants from the original, or names that came from the original, such as debug symbols, RTTI class names or export tables. A function that got its name from one of those is recorded under a name of the researcher's own, or with none. The template's `AGENTS.md` forbids committing broad decompiler exports, and an inventory that holds only these columns is the one export that is committed. The coverage script reads only the inventories and the spec.

Coverage counts what the inventories list, with the functions they exclude named as excluded, and is never a share of the behaviour understood. A function whose bytes an entry cites has been looked at, not read completely, which only the entry's status says.

A test count is not progress, and neither are lines of code or the number of assets the importer extracts. A percentage of completion is taken from the parity matrix or not given at all.

## What needs the owner

The owner decides eligibility and the supported editions, the scope and the non-goals, any deviation whose default is `on` or `mandatory`, any feature outside the parity matrix such as online play, when a release goes out, and whether and when live sessions happen, which they answer in the request files. Emulated calls and the harness that makes them need no decision, even where the owner keeps agents from running the original. Anything else goes ahead without approval, and the owner reviews the result. A question only the owner can answer goes in the plan's owner questions, and the work that depends on it waits under Blocked in the queue.

## Coding agents and long-running goals

A long-running goal, such as Claude Code's [`/goal`](https://code.claude.com/docs/en/goal), keeps an agent working until a condition holds. In Claude Code a second model judges that condition after every turn from what the conversation shows, without running anything itself. A good condition therefore names a state the agent can demonstrate by running something, has a scope, and has a limit:

```text
Every item under Static in queue/COMBAT.md is closed or moved to Blocked with
what was tried, the documentation check passes on the last commit, and each
batch ended with a status block; or stop after 40 turns.
```

```text
Every parity row that slice 3 of docs/IMPLEMENTATION-PLAN.md names has Code
complete, or is partial with a Spec gap note saying what the spec is missing, the
fast validation gate and the documentation check pass on the last commit, and
the only changes under spec/ are added open questions and unknown entries; or
stop after 60 turns.
```

A goal as broad as "finish the combat system" gives the judge nothing to check and the agent no reason to stop. The goal's file in `docs/goals/` holds the same condition with its scope and the areas it must not touch, so a second session can see that the areas are taken.

Every batch ends by printing a status block, which is the evidence the judge reads and a summary the next person can use:

```text
Status
Goal: docs/goals/combat-static.md
Batch: research, RULE-COMBAT-012 supported -> established
Queue COMBAT: static 3, emulated call 2, agent run 1, live session 4, source 0, blocked 2
Checks: documentation check passed, fast gate passed
Commit: 3f2a9c1 (not pushed)
Next: Q-COMBAT-015, RULE-COMBAT-014, the retaliation roll
```

The template ships the protocol's recurring procedures as agent skills in `.claude/skills/`, one per procedure: `runtime-access` checks what can be done with the original running, `plan-work` plans and seeds the queue, `start-session` and `end-session` open and close a session, `research-item` and `implement-rows` carry out a research or implementation batch, `triage-report` records and triages a report from testing, and `live-session` requests, prepares and ingests a live session. A skill holds the steps and points to this page and the standard for the rules. Where a skill and this page disagree, this page applies, and the skill is fixed.

Agents read this page, the [methodology](/methodology/) and the [documentation standard](/documentation-standard/) from a copy in the repository, not from this site. The template keeps them in `docs/upstream/`, byte for byte as published, with the commit they came from and their hashes, and every link to them in the repository points at the copy. A link to a section gives the section's lines, and an agent reads those lines rather than the whole page, and never reads a section again once it has it. Agents assume the copy is current and do not go online to check. When these pages change, a person refreshes the copy, and only then.

Several agents can work on one game at once when each takes different areas. A goal file claims its areas, and the batches of one goal change only those areas' entries, queue files, parity rows and deviations. A goal takes up a queue item only if every entry the item names is in an area it claims. It may add an area to its claim, in its goal file, while no other goal file claims that area. Every session has to see the claims, so a goal file, and every change to its claim, reaches the main branch before the first batch that relies on it. Where sessions cannot push, only one goal runs at a time. Each agent works in its own worktree or branch, and writes its state only in its own goal file's handover.

`PARITY.md` and the files in `spec/index/` change with almost every batch, and the documentation check writes them. A merge conflict in them is never resolved by hand: take either side and run the check, which writes them again from the merged spec and parity rows. Two branches that create the same spec ID, deviation ID or queue item ID are handled the way the standard's [Identifiers](/documentation-standard/#identifiers) section says: the one merged second renumbers.

## Versions

This is version 1 of the protocol. A change that would make a repository that follows this version stop following it gets a new version number, listed here with what to change.

| Version | Changes | Converting a repository |
|---|---|---|
| 1 | The first version. | Write `docs/RUNTIME.md`, move open research questions from the plan and handover into `queue/`, cut the handover down to the current state, and move dated checkpoints out of the plan (git keeps them). |
