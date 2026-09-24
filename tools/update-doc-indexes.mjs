#!/usr/bin/env node
// Regenerates the generated index blocks in docs/*.md and docs/original-internals/*.md,
// and checks that every relative link between those documents still resolves.
//
// A block is delimited by two HTML comments:
//
//   <!-- doc-index:begin <kind> [key=value ...] -->
//   ...generated content...
//   <!-- doc-index:end -->
//
// Kinds:
//   toc depth=N       table of contents of this file's `##`..`#`*N headings
//   finding-index     BIN-* findings of the docs/original-internals/ documents,
//                     grouped by subsystem, each group naming its document
//   rule-index        RULE-* rules of GAME-RULES.md with their section
//   decision-index    dated decisions of DECISIONS.md
//
// Usage:
//   node tools/update-doc-indexes.mjs          rewrite stale blocks
//   node tools/update-doc-indexes.mjs --check  exit 1 when a block is stale
//
// Either way, broken links fail the run; they cannot be repaired automatically.
//
// No dependencies. Anchors follow GitHub's heading-slug rules.

import { existsSync, readFileSync, writeFileSync, readdirSync } from "node:fs";
import { join, dirname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repoDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const docsDir = join(repoDir, "docs");
const findingsDir = join(docsDir, "original-internals");
const check = process.argv.includes("--check");

const BEGIN = /^<!-- doc-index:begin ([a-z-]+)((?: [a-z]+=[^\s]+)*) -->$/;
const END = "<!-- doc-index:end -->";

/**
 * Lines of a document without their terminators. A Windows checkout with
 * core.autocrlf delivers CRLF, and a line that kept its `\r` would never match
 * BEGIN or END, so every block would be skipped and `--check` would pass.
 */
function splitLines(text) {
  return text.split(/\r?\n/);
}

/** The line terminator a document uses, so a rewrite keeps the checkout's endings. */
function lineEnding(text) {
  return text.includes("\r\n") ? "\r\n" : "\n";
}

/**
 * Subsystem labels for BIN-* finding families, keyed by the ID without its number.
 * A subsystem must live in exactly one document under docs/original-internals/, and
 * this order is the order the finding index prints, so keep it grouped by document.
 */
const FINDING_FAMILIES = new Map([
  // executable-and-platform.md
  ["BIN-PE", "Executable image"],
  ["BIN-TOOL", "Executable image"],
  ["BIN-API", "Platform boundaries visible in imports"],
  ["BIN-ASSET", "Resource lookup"],
  // randomness-and-turn-structure.md
  ["BIN-RNG", "Randomness and seeding"],
  ["BIN-TURN-PLAYER-ORDER", "Turn structure and phase order"],
  ["BIN-ENDTURN", "Turn structure and phase order"],
  ["BIN-REPEAT", "Turn structure and phase order"],
  ["BIN-COMMAND-ASSIGN", "Turn structure and phase order"],
  ["BIN-HIDE-LIFECYCLE", "Turn structure and phase order"],
  // new-game-setup.md
  ["BIN-CITY", "New-game setup and city generation"],
  ["BIN-SETUP", "New-game setup and city generation"],
  ["BIN-HOTSEAT", "New-game setup and city generation"],
  // commands-and-economy.md
  ["BIN-HIRE", "Hiring"],
  ["BIN-HIRE-COMPARISON", "Hiring"],
  ["BIN-INSTANT", "Instant commands"],
  ["BIN-INFLUENCE", "Instant commands"],
  ["BIN-RESEARCH", "Instant commands"],
  ["BIN-BRIBE", "Instant commands"],
  ["BIN-SNITCH", "Instant commands"],
  ["BIN-MOVEMENT", "Movement and sector control"],
  ["BIN-CONTROL", "Movement and sector control"],
  ["BIN-EFFECTIVE-STATS", "Gang statistics and equipment"],
  ["BIN-EQUIP", "Gang statistics and equipment"],
  ["BIN-GANG-RETIRE", "Gang statistics and equipment"],
  ["BIN-GANG-DEFINITION", "Gang statistics and equipment"],
  ["BIN-GANG-VALUES", "Gang statistics and equipment"],
  ["BIN-UPKEEP", "Economy and finance"],
  ["BIN-FINANCE", "Economy and finance"],
  // combat-and-police.md
  ["BIN-ATTACK", "Combat"],
  ["BIN-COMBAT-ORDER", "Combat"],
  ["BIN-COMBAT-STATS", "Combat"],
  ["BIN-COMBAT-PRESENT", "Combat"],
  ["BIN-COMBAT-RESULTS", "Combat"],
  ["BIN-DETECT", "Combat"],
  ["BIN-CHAOS", "Chaos and police"],
  ["BIN-POLICE", "Chaos and police"],
  ["BIN-POLICE-COMBAT", "Chaos and police"],
  // objectives-and-awards.md
  ["BIN-RANKING", "Objectives, ranking, and awards"],
  ["BIN-AWARDS", "Objectives, ranking, and awards"],
  // computer-players.md
  ["BIN-AI", "Computer players"],
  // interface-and-options.md
  ["BIN-UI", "Screens, panels, and hit geometry"],
  ["BIN-UI-CREDITS", "Screens, panels, and hit geometry"],
  ["BIN-UI-TITLE", "Screens, panels, and hit geometry"],
  ["BIN-UI-MENU", "Screens, panels, and hit geometry"],
  ["BIN-SECTOR-GANGS", "Screens, panels, and hit geometry"],
  ["BIN-ITEM-INFO", "Screens, panels, and hit geometry"],
  ["BIN-SITE-INFO", "Screens, panels, and hit geometry"],
  ["BIN-GAME-INFO", "Screens, panels, and hit geometry"],
  ["BIN-NUMBER-HELPERS", "Screens, panels, and hit geometry"],
  ["BIN-OPTIONS", "Options and preferences"],
  // reports-and-comlink.md
  ["BIN-EVENT", "Turn reports and Comlink"],
  ["BIN-EVENTS", "Turn reports and Comlink"],
  ["BIN-COMLINK", "Turn reports and Comlink"],
  ["BIN-SEARCH", "Turn reports and Comlink"],
  // audio-and-video.md
  ["BIN-MUSIC", "Audio and video"],
  ["BIN-SOUND", "Audio and video"],
]);

/** Display order of the subsystem groups in the finding index. */
const FAMILY_ORDER = [...new Set(FINDING_FAMILIES.values())];

/** GitHub's heading anchor: lowercase, drop punctuation, spaces to hyphens, dedupe with -N. */
function makeSlugger() {
  const seen = new Map();
  return (headingText) => {
    let slug = headingText
      .trim()
      .toLowerCase()
      .replace(/[^\p{L}\p{N}\s_-]/gu, "")
      .replace(/\s/g, "-");
    const count = seen.get(slug) ?? 0;
    seen.set(slug, count + 1);
    if (count > 0) slug = `${slug}-${count}`;
    return slug;
  };
}

/** Headings of a file with their level, text, anchor and enclosing `##` section. */
function parseHeadings(lines) {
  const slug = makeSlugger();
  const headings = [];
  let inFence = false;
  let section = null;
  for (const line of lines) {
    if (/^(```|~~~)/.test(line)) inFence = !inFence;
    if (inFence) continue;
    const m = /^(#{1,6}) (.+?)\s*$/.exec(line);
    if (!m) continue;
    const level = m[1].length;
    const text = m[2];
    if (level === 2) section = text;
    headings.push({ level, text, anchor: slug(text), section });
  }
  return headings;
}

/** Plain text of a heading without inline code markers, for table cells. */
function plain(text) {
  return text.replace(/`/g, "");
}

function renderToc(headings, depth) {
  const rows = [];
  for (const h of headings) {
    if (h.level < 2 || h.level > depth) continue;
    const indent = "  ".repeat(h.level - 2);
    rows.push(`${indent}- [${plain(h.text)}](#${h.anchor})`);
  }
  return rows;
}

/**
 * Every BIN-* finding across docs/original-internals/, grouped by subsystem in
 * FINDING_FAMILIES order. Each group records the one document that holds it.
 */
function collectFindings() {
  const pattern = /^(BIN-[A-Z][A-Z-]*?)-(\d+[A-Z]?) - (.+)$/;
  const groups = new Map(FAMILY_ORDER.map((name) => [name, { document: null, findings: [] }]));
  const ids = new Set();
  for (const name of readdirSync(findingsDir).filter((n) => n.endsWith(".md")).sort()) {
    const headings = parseHeadings(splitLines(readFileSync(join(findingsDir, name), "utf8")));
    for (const h of headings) {
      if (h.level !== 3) continue;
      const m = pattern.exec(h.text);
      if (!m) continue;
      const [, family, number, title] = m;
      const id = `${family}-${number}`;
      if (ids.has(id)) throw new Error(`duplicate finding ID ${id}`);
      ids.add(id);
      const group = FINDING_FAMILIES.get(family);
      if (!group) throw new Error(`finding family ${family} has no subsystem in FINDING_FAMILIES (${id})`);
      const entry = groups.get(group);
      if (entry.document && entry.document !== name) {
        throw new Error(
          `subsystem "${group}" is split across ${entry.document} and ${name}; keep a subsystem in one document`,
        );
      }
      entry.document = name;
      entry.findings.push({ id, title, anchor: h.anchor });
    }
  }
  const seen = new Set();
  let previous = null;
  for (const { document } of groups.values()) {
    if (!document || document === previous) continue;
    if (seen.has(document)) {
      throw new Error(
        `FINDING_FAMILIES returns to ${document} after leaving it; keep each document's subsystems adjacent`,
      );
    }
    seen.add(document);
    previous = document;
  }
  return { groups, total: ids.size };
}

function renderFindingIndex(path) {
  const { groups, total } = collectFindings();
  const prefix = relative(dirname(path), findingsDir).split(/[\\/]/).join("/");
  const rows = [`${total} findings.`, ""];
  for (const [group, { document, findings }] of groups) {
    if (findings.length === 0) continue;
    findings.sort((a, b) => a.id.localeCompare(b.id, "en", { numeric: true }));
    rows.push(
      `**${group}** — [${document}](${prefix}/${document})`,
      "",
      "| ID | Finding |",
      "|---|---|",
    );
    for (const f of findings) {
      rows.push(`| [${f.id}](${prefix}/${document}#${f.anchor}) | ${plain(f.title)} |`);
    }
    rows.push("");
  }
  rows.pop();
  return rows;
}

function renderRuleIndex(headings) {
  const pattern = /^(RULE-[A-Z-]+-\d+) — (.+)$/;
  const rows = ["| ID | Rule | Section |", "|---|---|---|"];
  const ids = new Set();
  for (const h of headings) {
    if (h.level !== 3) continue;
    const m = pattern.exec(h.text);
    if (!m) continue;
    if (ids.has(m[1])) throw new Error(`duplicate rule ID ${m[1]}`);
    ids.add(m[1]);
    rows.push(`| [${m[1]}](#${h.anchor}) | ${plain(m[2])} | ${plain(h.section)} |`);
  }
  return rows;
}

function renderDecisionIndex(headings) {
  const pattern = /^(\d{4}-\d{2}-\d{2}) — (.+)$/;
  const rows = ["| Date | Decision |", "|---|---|"];
  for (const h of headings) {
    if (h.level !== 2) continue;
    const m = pattern.exec(h.text);
    if (!m) continue;
    rows.push(`| ${m[1]} | [${plain(m[2])}](#${h.anchor}) |`);
  }
  return rows;
}

function render(kind, attrs, headings, path) {
  switch (kind) {
    case "toc":
      return renderToc(headings, Number(attrs.depth ?? 2));
    case "finding-index":
      return renderFindingIndex(path);
    case "rule-index":
      return renderRuleIndex(headings);
    case "decision-index":
      return renderDecisionIndex(headings);
    default:
      throw new Error(`unknown doc-index kind "${kind}"`);
  }
}

function processFile(path) {
  const original = readFileSync(path, "utf8");
  const lines = splitLines(original);
  const headings = parseHeadings(lines);
  const out = [];
  let blocks = 0;
  for (let i = 0; i < lines.length; i++) {
    const m = BEGIN.exec(lines[i]);
    if (!m) {
      out.push(lines[i]);
      continue;
    }
    const kind = m[1];
    const attrs = Object.fromEntries(
      m[2].trim().split(/\s+/).filter(Boolean).map((pair) => pair.split("=")),
    );
    const end = lines.indexOf(END, i + 1);
    if (end < 0) throw new Error(`${path}: unterminated doc-index block at line ${i + 1}`);
    out.push(lines[i], ...render(kind, attrs, headings, path), END);
    i = end;
    blocks++;
  }
  const updated = out.join(lineEnding(original));
  return { blocks, stale: updated !== original, updated };
}

/** Every maintained markdown document, docs/ first and then its subdirectories. */
function documentPaths() {
  const paths = [];
  for (const dir of [docsDir, findingsDir]) {
    for (const name of readdirSync(dir).filter((n) => n.endsWith(".md")).sort()) {
      paths.push(join(dir, name));
    }
  }
  return paths;
}

/** Anchors a markdown file offers, cached per path. */
const anchorCache = new Map();
function anchorsOf(path) {
  if (!anchorCache.has(path)) {
    const headings = parseHeadings(splitLines(readFileSync(path, "utf8")));
    anchorCache.set(path, new Set(headings.map((h) => h.anchor)));
  }
  return anchorCache.get(path);
}

/**
 * Relative links of the maintained documents that no longer resolve: a missing
 * file, or an `#anchor` no heading produces. External and absolute links are
 * left alone.
 */
function brokenLinks(paths) {
  const LINK = /\[[^\]]*\]\(([^)\s]+)\)/g;
  const broken = [];
  for (const path of paths) {
    const label = relative(repoDir, path).split(/[\\/]/).join("/");
    let inFence = false;
    let line = 0;
    for (const text of splitLines(readFileSync(path, "utf8"))) {
      line++;
      if (/^(```|~~~)/.test(text)) inFence = !inFence;
      if (inFence) continue;
      for (const [, target] of text.matchAll(LINK)) {
        if (/^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith("/")) continue;
        const [file, anchor] = target.split("#");
        const resolved = file ? resolve(dirname(path), file) : path;
        if (file && !existsSync(resolved)) {
          broken.push(`${label}:${line}: no such file: ${target}`);
          continue;
        }
        if (!anchor || !resolved.endsWith(".md")) continue;
        if (!anchorsOf(resolved).has(anchor)) {
          broken.push(`${label}:${line}: no such heading: ${target}`);
        }
      }
    }
  }
  return broken;
}

let stale = 0;
for (const path of documentPaths()) {
  const label = relative(repoDir, path).split(/[\\/]/).join("/");
  const result = processFile(path);
  if (result.blocks === 0) continue;
  if (result.stale) {
    stale++;
    if (check) {
      console.error(`stale: ${label}`);
    } else {
      writeFileSync(path, result.updated);
      console.log(`updated: ${label}`);
    }
  }
}
const broken = brokenLinks([...documentPaths(), join(repoDir, "README.md"), join(repoDir, "AGENTS.md")]);
for (const problem of broken) console.error(`broken link: ${problem}`);

if (check && stale > 0) {
  console.error(`${stale} file(s) have stale doc-index blocks; run node tools/update-doc-indexes.mjs`);
}
if (!check && stale === 0) console.log("all doc-index blocks are current");
if (broken.length === 0) console.log("all relative links resolve");
if (broken.length > 0 || (check && stale > 0)) process.exit(1);
