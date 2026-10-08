# Tests against the original

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

The rows in `parity/` list, for each row, only the tests that compare the rebuild with
evidence from the original: decoding every file a format entry lists, replaying
an experiment fixture, or matching a capture. Decoder tests on synthetic files
and tests that compare the rebuild with an earlier version of itself are still
required but are left out of that column. Manual play never counts, and listed
tests run with every deviation that has a setting switched off. A `mandatory`
deviation cannot be switched off, so a listed test that reaches the behaviour it
changes cites the deviation's ID and leaves that case out or compares with the
original's result as the deviation changes it.

A `MatchSetup` and a `HeadlessMatchOptions` have no default for the deviation
settings a match carries (DEV-AI-003, DEV-AI-007, DEV-AI-008): every caller
passes a `MatchDeviations`. A test that builds its own setup passes
`MatchDeviations.Original`, with every setting off, and a test of a deviation
itself switches that one setting on and cites its ID. The game, the online match
bootstrap and the `ai-tournament` command start from `MatchDeviations.Defaults`,
the Default column of `DEVIATIONS.md`, with the Advanced AI choice of the player,
the host or `--policy` in place of its DEV-AI-003 value. A test that starts its
match through the online bootstrap therefore runs with DEV-AI-007 and DEV-AI-008
on and is not listed in `PARITY.md`.
