#!/usr/bin/env node
// Measures how much of the executable the spec describes, and writes the function index
// docs/FUNCTION-INDEX.md. It lives outside spec/index/, which holds only what the documentation
// standard's check writes.
//
// Usage:
//   node tools/spec-coverage.mjs                      rewrite docs/FUNCTION-INDEX.md when stale
//   node tools/spec-coverage.mjs --check              fail when docs/FUNCTION-INDEX.md is stale
//   node tools/spec-coverage.mjs --scheduled-generation
//                                                     compute the index without writing or comparing
//                                                     it, and fail when the change since the fork point
//                                                     edits docs/FUNCTION-INDEX.md
//   node tools/spec-coverage.mjs --inventory <file>   also report coverage against a function
//                                                     inventory from tools/ghidra/ReportFunctionInventory.java
//
// The list of game functions and their extents comes from the table in FND-EXE-004. An entry
// describes a function when it names it (`fn_0046E766`), writes an eight-digit address inside its
// body, or writes a range `0x...A..0x...B` of game code that overlaps its body. A range counts as
// game code when both its ends lie inside game functions; a wider extent, such as the whole image,
// cites nothing. A function that begins exactly at a range's end is not counted, since such a range
// stops where the function starts. A constant written as an eight-digit hexadecimal number is read
// as an address. FND-EXE-004 itself is left out, since it lists every function, and so is an entry
// whose locations all name another file, such as a library the game ships, since its addresses
// are that file's.
//
// docs/FUNCTION-INDEX.md is regenerated on main by .github/workflows/nightly-generated.yml, which
// runs this script without options. Branches leave it alone, so concurrent pull requests do not
// conflict on it; the pre-commit hook and the fast gate run --scheduled-generation. The fork point
// is `git merge-base HEAD origin/$GITHUB_BASE_REF` (origin/main when GITHUB_BASE_REF is unset),
// taken with MERGE_HEAD as well while a merge is being committed, so a merge that brings main's
// index passes. The change is the working tree against the fork point plus untracked files git
// does not ignore. When the fork point does not resolve (no git, a shallow clone, a base branch
// that was never fetched), the comparison is skipped with a message instead of failing.
//
// The inventory is a tab-separated file with the columns entry, end, bytes, callers, callees,
// imports (LIBRARY::name), reads and writes. It stays outside the repository like every other
// Ghidra output.
//
// No dependencies.

import { existsSync, readFileSync, writeFileSync, readdirSync } from "node:fs";
import { join, dirname, relative } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const specDir = join(repoDir, "spec");
const argv = process.argv.slice(2);
const checkOnly = argv.includes("--check");
const scheduledGeneration = argv.includes("--scheduled-generation");
if (checkOnly && scheduledGeneration) {
  console.error("--check and --scheduled-generation cannot be combined");
  process.exit(2);
}
const inventoryPath = argv.includes("--inventory") ? argv[argv.indexOf("--inventory") + 1] : null;
const MAP_ID = "FND-EXE-004";
const indexPath = join(repoDir, "docs", "FUNCTION-INDEX.md");

const hex = (n) => "0x" + n.toString(16).toUpperCase().padStart(8, "0");
const fnName = (n) => "fn_" + n.toString(16).toUpperCase().padStart(8, "0");

// Spec entries: ID, file text.
const entries = [];
for (const dir of ["findings", "rules", "formats", "screens", "bugs", "experiments"]) {
  const d = join(specDir, dir);
  if (!existsSync(d)) continue;
  for (const f of readdirSync(d)) {
    if (!f.endsWith(".md")) continue;
    const text = readFileSync(join(d, f), "utf8").replace(/\r\n/g, "\n");
    entries.push({ id: f.slice(0, -3), text });
  }
}
// The glossary, one file per term in spec/glossary/, counts as one entry.
const glossaryDir = join(specDir, "glossary");
if (existsSync(glossaryDir)) {
  const terms = readdirSync(glossaryDir).filter((f) => f.endsWith(".md")).sort();
  const text = terms.map((f) => readFileSync(join(glossaryDir, f), "utf8").replace(/\r\n/g, "\n")).join("\n");
  entries.push({ id: "glossary", text });
}

// Game functions from the table in FND-EXE-004.
const mapEntry = entries.find((e) => e.id === MAP_ID);
if (!mapEntry) {
  console.error(`${MAP_ID} not found`);
  process.exit(1);
}
const functions = [];
for (const m of mapEntry.text.matchAll(/^\| `0x([0-9A-F]{8})` \| `0x([0-9A-F]{8})` \| (\d+) \| (\d+) \|$/gm)) {
  functions.push({ entry: parseInt(m[1], 16), end: parseInt(m[2], 16), bytes: Number(m[3]), callers: Number(m[4]), citedBy: new Set() });
}
functions.sort((a, b) => a.entry - b.entry);
const byEntry = new Map(functions.map((f) => [f.entry, f]));
function containing(addr) {
  let lo = 0;
  let hi = functions.length - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    const f = functions[mid];
    if (addr < f.entry) hi = mid - 1;
    else if (addr > f.end) lo = mid + 1;
    else return f;
  }
  return null;
}

// Cited code and data addresses per entry.
const citedData = new Map(); // address -> Set of entry IDs
const EXE_FILE = "Chaos Overlords.exe";
function readsAnotherFile(text) {
  const front = text.startsWith("---\n") ? text.slice(4, text.indexOf("\n---", 4)) : "";
  const files = [...front.matchAll(/^\s+file:\s*(.+?)\s*$/gm)].map((m) => m[1].replace(/^["']|["']$/g, ""));
  return files.length > 0 && !files.includes(EXE_FILE);
}
for (const e of entries) {
  if (e.id === MAP_ID || readsAnotherFile(e.text)) continue;
  for (const m of e.text.matchAll(/\bfn_([0-9A-Fa-f]{8})\b/g)) {
    const f = byEntry.get(parseInt(m[1], 16)) ?? containing(parseInt(m[1], 16));
    if (f) f.citedBy.add(e.id);
  }
  // A range of game code cites every function it overlaps, except one that begins exactly at its
  // end; a range whose ends are not both in game functions cites nothing.
  for (const m of e.text.matchAll(/\b0x([0-9A-Fa-f]{8})`?\s*\.\.\s*`?0x([0-9A-Fa-f]{8})\b/g)) {
    const start = parseInt(m[1], 16);
    const end = parseInt(m[2], 16);
    if (!containing(start) || !containing(end)) continue;
    for (const f of functions)
      if (f.entry <= end && f.end >= start && !(f.entry === end && start < end)) f.citedBy.add(e.id);
  }
  // A range's end is handled above; every other address cites the function it lies in.
  for (const m of e.text.matchAll(/(?<!\.\.`?\s*)\b(?:0x|g_)([0-9A-Fa-f]{8})\b/g)) {
    const a = parseInt(m[1], 16);
    const f = containing(a);
    if (f) f.citedBy.add(e.id);
    else {
      if (!citedData.has(a)) citedData.set(a, new Set());
      citedData.get(a).add(e.id);
    }
  }
}

// The index.
const idOrder = (a, b) => (a === "glossary") - (b === "glossary") || a.localeCompare(b);
const lines = [
  "# Function index",
  "",
  `Generated by \`node tools/spec-coverage.mjs\` from the function table of ${MAP_ID} and the`,
  "names and addresses the entries cite. Do not edit by hand.",
  "",
  "An entry is listed against a function when it names the function, gives an address inside its",
  "body, or gives a range `0x...A..0x...B` of game code that overlaps its body. A range whose ends are",
  "not both inside game functions is not listed, and a range whose end is the entry of a function is",
  "not listed against that function.",
  "",
];
const cited = functions.filter((f) => f.citedBy.size > 0);
lines.push(`${cited.length} of ${functions.length} game functions are cited, ${cited.reduce((s, f) => s + f.bytes, 0).toLocaleString("en-US")} of ${functions.reduce((s, f) => s + f.bytes, 0).toLocaleString("en-US")} bytes.`);
lines.push("");
lines.push("| Function | Range | Bytes | Callers | Cited by |");
lines.push("|---|---|---|---|---|");
for (const f of functions) {
  const by = [...f.citedBy].sort(idOrder).join(", ");
  lines.push(`| \`${fnName(f.entry)}\` | \`${hex(f.entry)}..${hex(f.end)}\` | ${f.bytes} | ${f.callers} | ${by || "None"} |`);
}
lines.push("");
const index = lines.join("\n");
const current = existsSync(indexPath) ? readFileSync(indexPath, "utf8").replace(/\r\n/g, "\n") : null;
let failed = false;
if (scheduledGeneration) {
  failed = !checkUnchangedSinceForkPoint("docs/FUNCTION-INDEX.md");
} else if (current !== index) {
  if (checkOnly) {
    console.error("docs/FUNCTION-INDEX.md is stale; run node tools/spec-coverage.mjs");
    failed = true;
  } else {
    writeFileSync(indexPath, index);
    console.log("wrote docs/FUNCTION-INDEX.md");
  }
}
console.log(`function index: ${cited.length} of ${functions.length} game functions cited`);

// Coverage against an inventory.
if (inventoryPath) {
  const rows = readFileSync(inventoryPath, "utf8").replace(/\r\n/g, "\n").split("\n").filter(Boolean);
  const header = rows.shift().split("\t");
  const col = (name) => header.indexOf(name);
  const inv = new Map();
  for (const r of rows) {
    const c = r.split("\t");
    const entry = parseInt(c[col("entry")], 16);
    inv.set(entry, {
      entry,
      bytes: Number(c[col("bytes")]),
      callees: (c[col("callees")] || "").split(",").filter(Boolean).map((x) => parseInt(x, 16)),
      imports: (c[col("imports")] || "").split(",").filter(Boolean),
      reads: (c[col("reads")] || "").split(",").filter(Boolean),
      writes: (c[col("writes")] || "").split(",").filter(Boolean),
      callers: new Set(),
    });
  }
  for (const f of inv.values()) for (const c of f.callees) inv.get(c)?.callers.add(f.entry);
  const game = functions.filter((f) => inv.has(f.entry));
  if (game.length !== functions.length) console.log(`warning: ${functions.length - game.length} functions of ${MAP_ID} are missing from the inventory`);

  // Network play: imports anything from WinSock or TAPI, or a serial port function of KERNEL32, or
  // is called only by such functions. An import without a library (an older inventory) never counts.
  const NET_LIBRARIES = new Set(["WSOCK32.DLL", "WS2_32.DLL", "TAPI32.DLL"]);
  const SERIAL = /^(BuildCommDCB\w*|ClearCommBreak|ClearCommError|EscapeCommFunction|GetCommConfig|GetCommMask|GetCommModemStatus|GetCommProperties|GetCommState|GetCommTimeouts|PurgeComm|SetCommBreak|SetCommConfig|SetCommMask|SetCommState|SetCommTimeouts|SetupComm|TransmitCommChar|WaitCommEvent)$/;
  const isNetImport = (i) => {
    const [library, name] = i.includes("::") ? i.split("::") : ["", i];
    return NET_LIBRARIES.has(library) || (library === "KERNEL32.DLL" && SERIAL.test(name));
  };
  const net = new Set(game.filter((f) => inv.get(f.entry).imports.some(isNetImport)).map((f) => f.entry));
  for (let changed = true; changed; ) {
    changed = false;
    for (const f of game) {
      if (net.has(f.entry)) continue;
      const callers = [...inv.get(f.entry).callers];
      if (callers.length > 0 && callers.every((c) => net.has(c))) {
        net.add(f.entry);
        changed = true;
      }
    }
  }
  const sum = (list) => list.reduce((s, f) => s + f.bytes, 0);
  const uncited = game.filter((f) => f.citedBy.size === 0);
  const uncitedNet = uncited.filter((f) => net.has(f.entry));
  const uncitedRest = uncited.filter((f) => !net.has(f.entry));
  console.log("");
  console.log(`game functions: ${game.length}, ${sum(game)} bytes`);
  console.log(`cited: ${game.length - uncited.length}, ${sum(game) - sum(uncited)} bytes`);
  console.log(`not cited: ${uncited.length}, ${sum(uncited)} bytes`);
  console.log(`  network play: ${uncitedNet.length}, ${sum(uncitedNet)} bytes`);
  console.log(`  other: ${uncitedRest.length}, ${sum(uncitedRest)} bytes`);
  for (const f of uncitedRest.sort((a, b) => b.bytes - a.bytes)) {
    const i = inv.get(f.entry);
    console.log(`    ${fnName(f.entry)} ${f.bytes} bytes, ${i.callers.size} callers${i.imports.length ? ", imports " + i.imports.join(" ") : ""}`);
  }

  const thin = game.filter((f) => f.bytes > 1000 && f.citedBy.size > 0 && f.citedBy.size <= 2);
  console.log("");
  console.log(`functions over 1,000 bytes cited by one or two entries: ${thin.length}`);
  for (const f of thin.sort((a, b) => b.bytes - a.bytes)) console.log(`    ${fnName(f.entry)} ${f.bytes} bytes: ${[...f.citedBy].join(", ")}`);

  // Data addresses the game code uses, and how many lie within 16 bytes of a cited address.
  const citedList = [...citedData.keys()].sort((a, b) => a - b);
  const near = (a) => {
    let lo = 0;
    let hi = citedList.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (Math.abs(citedList[mid] - a) <= 16) return true;
      if (citedList[mid] < a) lo = mid + 1;
      else hi = mid - 1;
    }
    return false;
  };
  const used = new Map(); // "block:address" -> { block, addr, users }
  for (const f of game) {
    const i = inv.get(f.entry);
    for (const x of [...i.reads, ...i.writes]) {
      const [block, a] = x.split(":");
      if (block === ".idata" || block === "Headers") continue; // import address table and PE headers
      const addr = parseInt(a, 16);
      if (!used.has(x)) used.set(x, { block, addr, users: new Set() });
      used.get(x).users.add(f.entry);
    }
  }
  console.log("");
  for (const block of [...new Set([...used.values()].map((u) => u.block))].sort()) {
    const list = [...used.values()].filter((u) => u.block === block);
    const covered = list.filter((u) => near(u.addr));
    console.log(`${block}: ${list.length} addresses used by game code, ${covered.length} within 16 bytes of a cited address`);
  }
  const regions = [...used.values()].filter((u) => !near(u.addr)).sort((a, b) => b.users.size - a.users.size).slice(0, 40);
  console.log("most used data addresses no entry cites:");
  for (const u of regions) console.log(`    ${u.block} ${hex(u.addr)} used by ${u.users.size} functions`);
}

if (failed) process.exit(1);

// Runs git in the repository; returns its trimmed output, or null when git fails or is missing.
function git(...args) {
  const r = spawnSync("git", args, { cwd: repoDir, encoding: "utf8" });
  return r.status === 0 ? r.stdout.trim() : null;
}

// Fails (returns false) when the change since the fork point edits `path`, a generated file that
// only main carries. Returns true when the file is unchanged or the comparison cannot be made.
function checkUnchangedSinceForkPoint(path) {
  const base = `origin/${process.env.GITHUB_BASE_REF || "main"}`;
  if (git("rev-parse", "--is-inside-work-tree") !== "true") {
    console.log(`${path}: comparison with the fork point skipped: git is unavailable or this is not a git work tree`);
    return true;
  }
  if (git("rev-parse", "--verify", "--quiet", `${base}^{commit}`) === null) {
    console.log(`${path}: comparison with the fork point skipped: ${base} is not fetched`);
    return true;
  }
  // While a merge is being committed, the fork point is the one the merge commit will have.
  const mergeHead = git("rev-parse", "--verify", "--quiet", "MERGE_HEAD");
  const fork = git("merge-base", base, "HEAD", ...(mergeHead ? [mergeHead] : []));
  if (!fork) {
    console.log(`${path}: comparison with the fork point skipped: HEAD and ${base} have no merge base in this clone (shallow, or unrelated)`);
    return true;
  }
  const changed = git("diff", "--name-only", fork, "--", path);
  const untracked = git("ls-files", "--others", "--exclude-standard", "--", path);
  if (changed === null || untracked === null) {
    console.log(`${path}: comparison with the fork point skipped: git diff failed`);
    return true;
  }
  if (changed === "" && untracked === "") return true;
  const short = fork.slice(0, 12);
  console.error(
    `${path} differs from the fork point ${short} (merge base with ${base}). It is generated, and only ` +
      "main carries it: the nightly job (.github/workflows/nightly-generated.yml) regenerates it there. " +
      `Drop the change with \`git checkout ${short} -- ${path}\` or \`git restore --source=${short} -- ${path}\`` +
      (untracked ? ` (an untracked ${path}: delete it)` : "") +
      ".",
  );
  return false;
}
