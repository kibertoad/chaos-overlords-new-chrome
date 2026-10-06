// Runs the documentation standard's checks over spec/, parity/ and deviations/ with the shared
// checker (@scientific-method/standard-checker, pinned in the root package.json; install it with
// `pnpm install` at the repository root) and this game's settings:
//
//   --references multiplayer           the multiplayer server may cite spec and deviation IDs
//   --images 0x00400000..0x004C9000    the extent of the original's executable image (FND-DATA-005)
//
// Every other argument goes to the checker unchanged, so `--check`, `--no-ksy`, `--base <ref>` and
// `--record-validation <builds>` work as the checker documents them.
//
// --allow-unrecorded-validation (this script's own option) passes a run whose only problems are
// test files of validated rows that VALIDATION.md does not record yet or recorded at another
// version. The pre-commit hook uses it: the record can only be written after the tests ran on the
// committed tree, so the commit that changes such a test has to go in before its record. CI runs
// without it.
//
// STANDARD_CHECKER_ROOT names the directory whose node_modules holds the checker, for a run over a
// copy of the tree (the pre-commit hook checks the staged files in a temporary directory). It
// defaults to the repository this script is in.
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const installRoot = process.env.STANDARD_CHECKER_ROOT ?? repositoryRoot;
const checker = join(
  installRoot,
  "node_modules/@scientific-method/standard-checker/dist/standard-checker.js",
);
if (!existsSync(checker)) {
  console.error(
    `check-documentation: ${checker} is missing; run pnpm install at the repository root.`,
  );
  process.exit(2);
}

const argv = process.argv.slice(2);
const allowUnrecorded = argv.includes("--allow-unrecorded-validation");
const forwarded = argv.filter((a) => a !== "--allow-unrecorded-validation");
const args = [
  checker,
  "--references",
  "multiplayer",
  "--images",
  "0x00400000..0x004C9000",
  ...(forwarded.includes("--root") ? [] : ["--root", repositoryRoot]),
  // In CI the Kaitai definitions always compile (AGENTS.md), and a pull request that deletes a
  // spec ID or area that exists on its base always fails.
  ...(process.env.CI && !forwarded.includes("--no-ksy") ? ["--require-ksc"] : []),
  ...(process.env.CI ? ["--require-base"] : []),
  ...forwarded,
];

if (!allowUnrecorded) {
  const run = spawnSync(process.execPath, args, { stdio: "inherit" });
  process.exit(run.status ?? 1);
}

const run = spawnSync(process.execPath, args, { encoding: "utf8" });
process.stdout.write(run.stdout);
if (run.status === 0) process.exit(0);
const unrecorded =
  /: (?:\S+ is not in VALIDATION\.md, so the row cannot be validated|\S+ has changed since VALIDATION\.md recorded it)/;
const problems = run.stderr
  .split(/\r?\n/)
  .filter((line) => /^\S+: /.test(line) && !/^Skipped: /.test(line));
if (run.status === 1 && problems.length > 0 && problems.every((line) => unrecorded.test(line))) {
  for (const line of problems) console.error(`warning: ${line}`);
  console.error(
    "check-documentation: only VALIDATION.md records are missing or stale; after this commit, run " +
      "the listed tests against the original's files and record them with " +
      "node tools/check-documentation.mjs --record-validation BLD-GOG-EN-1.1.",
  );
  process.exit(0);
}
process.stderr.write(run.stderr);
process.exit(run.status ?? 1);
