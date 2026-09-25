# Chaos Overlords specification

## Scope

This spec describes *Chaos Overlords*, the turn-based strategy game developed
by Stick Man Games and published by New World Computing for Windows 95 in 1996,
as build BLD-GOG-EN-1.1 (version
1.1 as GOG sells it) runs it. It covers the data files and in-memory
structures, the rules of a turn from setup to the end of a match, the computer
players, the screens and panels with their input and sound, the options kept in
the registry, and the defects of the original. The original's network play
(Winsock networking, TAPI modems and serial links) is described only as far as its
lobby screens and file transfers. Its save files are described as a format.

The spec describes the original game and nothing else. It never names a class,
file or setting from the rebuild in this repository.

## Standard version

This spec follows version 1 of the
[documentation standard](https://dinorefurb.com/documentation-standard/).

## Areas

Areas are added to this list and never removed or renamed.

| Area | Covers |
|---|---|
| `EXE` | The executable image: its format, sections and toolchain. |
| `PLATFORM` | What the executable imports from Windows, DirectDraw, Winsock, TAPI, the multimedia API and Smacker, per subsystem. |
| `ASSET` | How the executable finds its data files. |
| `DATA` | The gameplay tables (`SITES`, `Gangs`, `ITEMS`) and the other files under `DATA/` that are neither images, sounds nor video. |
| `GFX` | The `PX16` and `PX08` image files, their palettes, and how they are copied to the screen. |
| `AUDIO` | Sound effects, music tracks and when each plays. |
| `VIDEO` | The Smacker movies and when they play. |
| `HELP` | The WinHelp file and its contents file, and how the executable opens help topics. |
| `SAVE` | The save file. |
| `STATE` | In-memory structures that several areas share: players, gangs, sectors, sites and the match. |
| `RNG` | The random number generator, its seeding, and the functions that reduce its draws to ranges. |
| `TURN` | The order of a turn: planning, the execution phases, recurring commands, and the end of a turn. |
| `SETUP` | New-game setup: the setup screen, scenario and player choices, the name modifiers, the network lobbies, and hot-seat handoff. |
| `CITY` | Generating the city: sectors, their income and tolerance, sites, headquarters and Right Hands. |
| `HIRE` | Hire offers, hiring and snubbing. |
| `HIDE` | The Hide command and hidden gangs. |
| `INFLUENCE` | The Influence command and influenced sites. |
| `HEAL` | The Heal command. |
| `RESEARCH` | The Research command and research progress. |
| `BRIBE` | The Bribe command. |
| `SNITCH` | The Snitch command. |
| `TOLERANCE` | Sector tolerance and its return toward normal. |
| `SITE` | What an influenced site adds to the gangs in its sector. |
| `MOVE` | The Move command and sector capacity. |
| `CONTROL` | Sector control. |
| `GANG` | Gang records, their statistics, and what happens to a gang record when a gang dies or is terminated. |
| `EQUIP` | Buying equipment, the Factory price, and the equipment transactions. |
| `GIVE` | The Give command. |
| `SELL` | The Sell command. |
| `TERMINATE` | The Terminate command. |
| `UPKEEP` | Income, upkeep and debt. |
| `FINANCE` | The financial panel. |
| `ATTACK` | The Attack command and its picker. |
| `COMBAT` | Resolving attacks and retaliation, combat statistics, and how combat is presented. |
| `DETECT` | Detection and what each player can see in a sector. |
| `CHAOS` | The Chaos command and its payout. |
| `POLICE` | Crackdowns and police combat. |
| `AI` | The computer players. |
| `EVENT` | The Last Turn Events report and its panel. |
| `COMLINK` | The Comlink message queue and its panels. |
| `SEARCH` | The Search panel and its city markers. |
| `OBJECTIVE` | Scenarios, objectives, elimination, the end of a match and the player ranking. |
| `AWARDS` | The endgame awards. |
| `TIMER` | The planning time limit. |
| `UI` | Screens and panels that belong to no other area, the main console, the pointer, number drawing, the title, credits and menus. |
| `OPTIONS` | The options the game keeps in the registry. |
| `NET` | Network play. |
