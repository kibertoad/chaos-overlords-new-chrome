# Test harnesses

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

## Simulated human seats

Several AI paths react only to human players: the hunters pick their targets
among human gangs, many families treat a human owner differently, and a match
ends when its only human is eliminated. A match of computer players never
reaches them. A simulation that needs them seats a simulated human: the seat
registers as `PlayerController.Human`, so every rule and AI query sees a
person, and the computer planner plays it.

```csharp
var result = HeadlessMatchRunner.Run(definitions, new HeadlessMatchOptions(
    ScenarioId.Dominance, GameDuration.FourYears, seed, MatchDeviations.Original,
    SimulatedHumans: [new PlayerId(0)]));
```

The named seats are set up as human and the others as computer players;
seats the options do not reach are filled with computer players as in any
match. A test that drives a `MatchState` itself calls `SimulateHuman` on a
human seat before the first planning step; the planner refuses a human seat
that is not marked. The mark lives only in that `MatchState`: a save, a clone
and a replay journal do not carry it, so the runner refuses `VerifyReplay` for
a match with simulated humans. A simulated human plays like a computer player,
so it provokes less than a person would; read results about aggression with
that in mind. The `ai-tournament` command does not expose the option.

## The headless game

A test that checks what the game does with a player's input, rather than what a
rule helper returns, plays the game in `HeadlessGame`
(`tests/Rechaos.Tests/HeadlessGame.cs`). It runs the game's own update on every
tick, with no window, graphics device or audio device, from a temporary user
data folder that holds the preferences the test starts from. The test presses
keys and buttons, types text, moves the pointer and the clock, and reads the
game through the read-only members of `ChaosGame.Observation.cs`. Every sound
effect the game asks for is recorded with its volume, and an online test hands
the game the transport of a fake server. The game loads its bundled data
but no asset pack, so a test that needs pixels belongs with the screen
comparisons instead.
