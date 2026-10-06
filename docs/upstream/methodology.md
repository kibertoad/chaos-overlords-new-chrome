---
title: "Methodology"
meta_title: "Methodology: how the games are reverse engineered, documented and checked"
description: "How we study the original game, write down how it works, and check the rebuild against it."
draft: false
---

Every restoration follows the rules on this page. Where our own repositories fall short of them, we change the repositories.

The project has two goals, described under [Philosophy](/philosophy/): a game people can play, and documentation complete enough that anyone can check that game or build their own. Most of these rules serve the second goal, because without it nobody can verify the first. How the work is planned, tracked and handed on is set out in the [work protocol](/work-protocol/).

## Ground rules

We only take on games released in 2004 or earlier. We leave a game alone if an official remake or remaster of it is on sale, because its rights holders are already keeping it playable.

You bring the game. No restoration ships original art, audio, text, data tables or executables. It reads them from a copy you own. A game nobody sells any more still belongs to someone.

We write a new engine from documentation of the original. A rebuild written against a specification leaves behind a game that runs and a document that explains it, and the document is half of what we set out to produce. We do not translate the original machine code into matching source code, and we do not patch the original executable one function at a time.

No original code goes into a repository. Decompiler output, disassembly listings, byte dumps and analysis databases stay on the researcher's machine. What gets committed is a description of behaviour in our own words, such as a formula, a table layout or a state diagram, and the implementation is written from that description.

Every finding names the exact build it came from: the edition, the version and the xxHash3 (128-bit) hash of the file it was found in. Supported editions are listed by hash, and the importer refuses files it does not recognise instead of guessing.

## Studying the original

The executable has the final word on what the shipped game does. We read it in [Ghidra](https://github.com/NationalSecurityAgency/ghidra) and watch it run under a debugger. Functions and variables keep neutral names until their behaviour is confirmed, because a wrong name given early steers every later reading.

An experiment starts from a saved state, changes one input, and records what follows. It is repeated from the same save, with the random number generator's state varied between runs. Anything that involves random numbers needs many repetitions and a recorded distribution, since a formula inferred from one roll is a guess.

The manual tells us what the designers intended and is often wrong about what shipped. FAQs, wikis and tools written by other fans are leads, which we credit and re-check. A rule stays provisional until a complete reading of the code shows everything it says, or, where the outcome depends on something the code does not decide, such as timing, interrupts or the operating system, until a reading of the files and an observation of the original running agree. Evidence that only fits a rule, such as sizes that divide or a manual's description, never makes it more than a lead.

To check what a single function of the executable computes, we call it in the [Unicorn](https://www.unicorn-engine.org) CPU emulator on inputs we choose, with no window and no timing involved, and record each set of calls as an experiment. The [work protocol](/work-protocol/#emulated-calls) gives the procedure. It confirms only what the code decides, since the emulator stands in for the operating system.

## Writing it down

The documentation is written to be read without the code. For each game it covers every file format field by field, the rules and formulas with the order they resolve in and the points where random numbers are drawn, the AI as far as it can be reconstructed, and timing and input where they affect play. The original's bugs go into a catalogue, each with the conditions that trigger it.

Every statement carries its evidence and a status saying how well established it is. Someone who wants to build a second engine should be able to do it from these documents alone, and someone who doubts a rule should be able to check it without asking us. The structure is set out in the [documentation standard](/documentation-standard/), and the specs of [Chaos Overlords](https://github.com/kibertoad/chaos-overlords-new-chrome/tree/main/spec), [Dark Sun: Wake of the Ravager](https://github.com/kibertoad/dark-sun-wake-redux/tree/main/spec) and [Conqueror A.D. 1086](https://github.com/kibertoad/reconqueror1086/tree/main/spec) follow it. Where a community wiki already covers a game's formats, we intend to contribute our findings there too.

## Checking the rebuild

The input files must match the known hashes of a supported edition. The decoders must read every byte of every file into the right value. Original files cannot be committed, so those tests read a copy a maintainer owns when CI runs on the main branch, and synthetic files cover the edge cases the shipped files never reach. The same starting state and inputs must produce the same state changes as in the original, and the same state must produce the same screen and sound. A pass at one of these levels says nothing about the next.

The simulation is deterministic: the same inputs and seed give the same result on every platform. Where the original records replays or writes deterministic saves, we check that the rebuild reaches the same states from recordings made in the original. Where it does neither, tests come from controlled experiments, each with a starting state, an input and the expected result. Random outcomes are checked against distributions measured in the original, with a stated tolerance. A game that seems to play fine proves little, so we test against the original executable.

## The parity matrix

Every game repository has a parity matrix, kept apart from the spec: one file of rows per area under `parity/`, and `PARITY.md` with the totals. The spec says what the original does. The matrix says how much of that the rebuild does and how we know, with one row per rule, file format and screen in the spec. A row starts with the spec entry's status (unknown, sourced, supported, established or disputed). It becomes implemented when the code does everything the entry describes, and validated when the entry rests on evidence from the original (supported or established) and an automated test compares the rebuild with that evidence and passes. A row whose entry the rebuild replaces entirely on purpose, with no setting to bring the original's behaviour back, becomes deviated instead once tests check that the rebuild does what the deviation log says. Manual play never makes a row validated, and neither does code that still contains a placeholder formula. The exact format is in the [documentation standard](/documentation-standard/#parity-matrix).

The matrix is our answer to "how accurate is it", published so readers can check the answer.

## Where fidelity stops

The documentation records the original exactly, bugs included. The game we ship keeps the rules, balance, content, AI and pacing, and a player of the original should recognise every decision they face.

The interface is where we allow ourselves changes. The Chaos Overlords rebuild adds tooltips that show the exact numbers behind a mechanic, which the original left to the manual. A change like that can add information or remove friction. If it would change what the player can do, or change what the rules produce beyond the small judgement calls described below, it does not go in without a setting.

We fix crashes, corrupted saves, game speed tied to the CPU clock, and logic that plainly does not do what it was written to do. A quirk that players built strategies around is part of the game and stays. When we cannot tell a bug from a design decision, the original behaviour stays and any fix becomes an option.

For now the rebuilds only load saves in our own format, which the original game cannot read. If enough players ask for it, we may add loading of saves made by the original. Saving will always use our own format, because it lets us make saving faster and more reliable.

Screens match the original pixel for pixel except where a documented interface change draws something new.

Every deviation is listed in the documentation with its reason. Some of them are settings: Chaos Overlords lets the player choose between filters for the background art and between variants of parts of the interface. A change with no setting at all, such as the tooltips, has to be strictly better than the original, or be a small judgement call that makes the game better to play without touching anything players build strategies around, such as keeping precision the original threw away. Either way the argument is written down, with the reason the original's version is not worth keeping. A change that some players would reasonably not want, because it is a matter of taste or changes what the rules produce in a way players notice, gets a setting that starts with the original's behaviour, so a player can switch it on. The validation suite runs with every setting switched off. Rebalanced units, new features and other changes to the game itself belong in a separate mode or a separate project.

## Credits

These rules come from projects that have been rebuilding old games for more than twenty years, above all [ScummVM](https://www.scummvm.org), [OpenTTD](https://www.openttd.org), [OpenRCT2](https://openrct2.io), [Chocolate Doom](https://www.chocolate-doom.org), [OpenMW](https://openmw.org), [MAME](https://www.mamedev.org) and [IESDP](https://gibberlings3.github.io/iesdp/).
