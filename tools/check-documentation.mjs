// Runs the documentation standard's checks over spec/, parity/ and deviations/ with the shared
// checker (@scientific-method/standard-checker, pinned in the root package.json; install it with
// `pnpm install` at the repository root) and this game's settings:
//
//   --references multiplayer           the multiplayer server may cite spec and deviation IDs
//   --rebuild src,tests,multiplayer    no spec file may name a file of the rebuild, the server's
//                                      included
//   --scheduled-generation             spec/index/ and PARITY.md are updated on main only, by
//                                      .github/workflows/nightly-generated.yml: the check neither
//                                      writes nor compares them, and fails a change that edits one
//   --images 0x00400000..0x004C9000    the extent of the original's executable image (FND-DATA-005)
//   --squashed OLD=NEW,...             the superseded entries squashed into their replacements, from
//                                      tools/squashed.txt while it exists
//
// Every other argument goes to the checker unchanged, so `--check`, `--no-ksy`, `--base <ref>` and
// `--record-validation <builds>` work as the checker documents them. Without `--base`, a CI run on
// the base branch itself compares with what the push replaced or with the parent commit, since
// the checker's own default (the fork point) is HEAD there.
//
// --regenerate (this script's own option) leaves --scheduled-generation out, so the checker writes
// spec/index/ and PARITY.md. The nightly job uses it on main; elsewhere it gives fresh copies to
// read, which a branch does not commit.
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
import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
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
// STANDARD_CHECKER_ROOT may name another checkout (the pre-commit hook of a linked worktree falls
// back to the main checkout's install), whose checker can be of another version than this tree pins.
const pinned = JSON.parse(readFileSync(join(repositoryRoot, "package.json"), "utf8")).devDependencies[
  "@scientific-method/standard-checker"
];
const installed = JSON.parse(
  readFileSync(join(installRoot, "node_modules/@scientific-method/standard-checker/package.json"), "utf8"),
).version;
if (installed !== pinned) {
  console.error(
    `check-documentation: ${installRoot} has standard-checker ${installed}, but package.json pins ` +
      `${pinned}; run pnpm install at the repository root.`,
  );
  process.exit(2);
}

const argv = process.argv.slice(2);
const allowUnrecorded = argv.includes("--allow-unrecorded-validation");
const regenerate = argv.includes("--regenerate");
const forwarded = argv.filter((a) => a !== "--allow-unrecorded-validation" && a !== "--regenerate");
const root = forwarded.includes("--root")
  ? resolve(forwarded[forwarded.indexOf("--root") + 1] ?? ".")
  : repositoryRoot;
const args = [
  checker,
  "--references",
  "multiplayer",
  "--rebuild",
  "src,tests,multiplayer",
  ...(regenerate ? [] : ["--scheduled-generation"]),
  // The extent of the original's executable image, which FND-DATA-005 records.
  "--images",
  "0x00400000..0x004C9000",
  // tools/squashed.txt lists the superseded entries squashed into their replacements.
  ...squashed(),
  ...(forwarded.includes("--root") ? [] : ["--root", repositoryRoot]),
  // In CI the Kaitai definitions always compile (AGENTS.md), and a pull request that deletes a
  // spec ID or area that exists on its base always fails.
  ...(process.env.CI && !forwarded.includes("--no-ksy") ? ["--require-ksc"] : []),
  ...(process.env.CI ? ["--require-base"] : []),
  ...(forwarded.includes("--base") ? [] : baseOnTheBaseBranch(root)),
  ...forwarded,
];

/**
 * The --squashed option from tools/squashed.txt: one OLD=NEW line per squashed entry, `#` comments
 * and blank lines ignored. The checker fails a code file that names a squashed ID, and of the files
 * in tools/ it reads only .cs, .ts, .mjs, .js, .ps1, .fs, .md and .json, so the list is a .txt file
 * and not part of this script. Read from the tree being checked; nothing when the file is absent.
 */
function squashed() {
  const file = join(root, "tools", "squashed.txt");
  if (!existsSync(file)) return [];
  const items = readFileSync(file, "utf8")
    .split(/\r?\n/)
    .map((line) => line.replace(/#.*/, "").trim())
    .filter(Boolean);
  return items.length > 0 ? ["--squashed", items.join(",")] : [];
}

/**
 * Without --base the checker compares with where HEAD forked from the base branch. When CI runs on
 * the base branch itself (a push to main, a scheduled or dispatched run of main) that fork point is
 * HEAD, and the tree would be compared with itself. Returns --base with what the push replaced
 * (`before` in the push event) or, failing that, the parent commit; otherwise nothing, and the
 * checker finds the fork point on its own.
 *
 * Outside CI (the pre-commit hook, a run by hand) the tree checked is the staged or working tree on
 * top of HEAD, so HEAD is the right base even when it is on the base branch. The parent would count
 * HEAD's own changes as the tree's: on top of the nightly job's commit, every regenerated file
 * would read as a branch's edit.
 */
function baseOnTheBaseBranch(dir) {
  const git = (...gitArgs) =>
    execFileSync("git", ["-C", dir, ...gitArgs], { stdio: ["ignore", "pipe", "ignore"] })
      .toString()
      .trim();
  const resolves = (ref) => {
    try {
      git("rev-parse", "--verify", "--quiet", `${ref}^{commit}`);
      return true;
    } catch {
      return false;
    }
  };
  const target = process.env.GITHUB_BASE_REF ? `origin/${process.env.GITHUB_BASE_REF}` : "origin/main";
  let onTarget;
  try {
    onTarget = git("merge-base", "HEAD", target) === git("rev-parse", "HEAD");
  } catch {
    return []; // no fork point: the checker reports or skips the comparison itself
  }
  if (!onTarget || !process.env.CI) return [];
  let before = null;
  if (process.env.GITHUB_EVENT_NAME === "push" && process.env.GITHUB_EVENT_PATH) {
    try {
      before = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, "utf8")).before ?? null;
    } catch {
      before = null;
    }
  }
  // All zeros: the push created the branch. A force push can name a commit that is no longer in
  // the history; the parent is the next best thing in both cases.
  if (before && !/^0+$/.test(before) && resolves(before)) return ["--base", before];
  return resolves("HEAD~1") ? ["--base", "HEAD~1"] : [];
}

if (!allowUnrecorded) {
  const run = spawnSync(process.execPath, args, { stdio: "inherit" });
  process.exit(run.status ?? 1);
}

const run = spawnSync(process.execPath, args, { encoding: "utf8" });
if (run.error) {
  console.error(`check-documentation: the checker did not run: ${run.error.message}`);
  process.exit(1);
}
process.stdout.write(run.stdout);
if (run.status === 0) {
  process.stderr.write(run.stderr);
  process.exit(0);
}
const unrecorded =
  /: (?:\S+ is not in VALIDATION\.md, so the row cannot be validated|\S+ has changed since VALIDATION\.md recorded it)/;
// The checker prints one problem per line, then a blank line and its summary. Every line before
// that blank line is a problem, whatever its shape, so an unexpected line blocks the commit.
const lines = run.stderr.split(/\r?\n/);
const summaryAt = lines.indexOf("");
const problems = (summaryAt < 0 ? lines : lines.slice(0, summaryAt)).filter(
  (line) => line && !/^Skipped: /.test(line),
);
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
