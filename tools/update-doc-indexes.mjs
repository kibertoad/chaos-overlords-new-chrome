#!/usr/bin/env node
// Regenerates the generated index blocks in docs/*.md.
//
// A block is delimited by two HTML comments:
//
//   <!-- doc-index:begin <kind> [key=value ...] -->
//   ...generated content...
//   <!-- doc-index:end -->
//
// Kinds:
//   toc depth=N       table of contents of this file's `##`..`#`*N headings
//   finding-index     BIN-* findings of ORIGINAL-INTERNALS.md grouped by subsystem
//   rule-index        RULE-* rules of GAME-RULES.md with their section
//   decision-index    dated decisions of DECISIONS.md
//
// Usage:
//   node tools/update-doc-indexes.mjs          rewrite stale blocks
//   node tools/update-doc-indexes.mjs --check  exit 1 when a block is stale
//
// No dependencies. Anchors follow GitHub's heading-slug rules.

import { readFileSync, writeFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const docsDir = join(dirname(fileURLToPath(import.meta.url)), "..", "docs");
const check = process.argv.includes("--check");

const BEGIN = /^<!-- doc-index:begin ([a-z-]+)((?: [a-z]+=[^\s]+)*) -->$/;
const END = "<!-- doc-index:end -->";

/** Subsystem labels for BIN-* finding families, keyed by the ID without its number. */
const FINDING_FAMILIES = new Map([
  ["BIN-PE", "Executable image"],
  ["BIN-TOOL", "Executable image"],
  ["BIN-API", "Platform boundaries visible in imports"],
  ["BIN-ASSET", "Resource lookup"],
  ["BIN-RNG", "Randomness and seeding"],
  ["BIN-CITY", "New-game setup and city generation"],
  ["BIN-SETUP", "New-game setup and city generation"],
  ["BIN-HOTSEAT", "New-game setup and city generation"],
  ["BIN-TURN-PLAYER-ORDER", "Turn structure and phase order"],
  ["BIN-ENDTURN", "Turn structure and phase order"],
  ["BIN-REPEAT", "Turn structure and phase order"],
  ["BIN-COMMAND-ASSIGN", "Turn structure and phase order"],
  ["BIN-HIDE-LIFECYCLE", "Turn structure and phase order"],
  ["BIN-HIRE", "Hiring"],
  ["BIN-HIRE-COMPARISON", "Hiring"],
  ["BIN-INSTANT", "Instant commands"],
  ["BIN-INFLUENCE", "Instant commands"],
  ["BIN-RESEARCH", "Instant commands"],
  ["BIN-BRIBE", "Instant commands"],
  ["BIN-SNITCH", "Instant commands"],
  ["BIN-CHAOS", "Chaos and police"],
  ["BIN-POLICE", "Chaos and police"],
  ["BIN-POLICE-COMBAT", "Chaos and police"],
  ["BIN-ATTACK", "Combat"],
  ["BIN-COMBAT-ORDER", "Combat"],
  ["BIN-COMBAT-STATS", "Combat"],
  ["BIN-COMBAT-RESULTS", "Combat"],
  ["BIN-DETECT", "Combat"],
  ["BIN-EFFECTIVE-STATS", "Gang statistics and equipment"],
  ["BIN-EQUIP", "Gang statistics and equipment"],
  ["BIN-GANG-RETIRE", "Gang statistics and equipment"],
  ["BIN-GANG-DEFINITION", "Gang statistics and equipment"],
  ["BIN-GANG-VALUES", "Gang statistics and equipment"],
  ["BIN-MOVEMENT", "Movement and sector control"],
  ["BIN-CONTROL", "Movement and sector control"],
  ["BIN-UPKEEP", "Economy and finance"],
  ["BIN-FINANCE", "Economy and finance"],
  ["BIN-RANKING", "Objectives, ranking, and awards"],
  ["BIN-AWARDS", "Objectives, ranking, and awards"],
  ["BIN-AI", "Computer players"],
  ["BIN-EVENT", "Turn reports and Comlink"],
  ["BIN-EVENTS", "Turn reports and Comlink"],
  ["BIN-COMLINK", "Turn reports and Comlink"],
  ["BIN-SEARCH", "Turn reports and Comlink"],
  ["BIN-OPTIONS", "Options and preferences"],
  ["BIN-UI", "Screens, panels, and hit geometry"],
  ["BIN-UI-CREDITS", "Screens, panels, and hit geometry"],
  ["BIN-UI-TITLE", "Screens, panels, and hit geometry"],
  ["BIN-UI-MENU", "Screens, panels, and hit geometry"],
  ["BIN-SECTOR-GANGS", "Screens, panels, and hit geometry"],
  ["BIN-ITEM-INFO", "Screens, panels, and hit geometry"],
  ["BIN-SITE-INFO", "Screens, panels, and hit geometry"],
  ["BIN-GAME-INFO", "Screens, panels, and hit geometry"],
  ["BIN-NUMBER-HELPERS", "Screens, panels, and hit geometry"],
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

function renderFindingIndex(headings) {
  const pattern = /^(BIN-[A-Z][A-Z-]*?)-(\d+[A-Z]?) - (.+)$/;
  const groups = new Map(FAMILY_ORDER.map((name) => [name, []]));
  const ids = new Set();
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
    groups.get(group).push({ id, title, anchor: h.anchor, section: h.section });
  }
  const rows = [`${ids.size} findings.`, ""];
  for (const [group, findings] of groups) {
    if (findings.length === 0) continue;
    findings.sort((a, b) => a.id.localeCompare(b.id, "en", { numeric: true }));
    rows.push(`**${group}**`, "", "| ID | Finding | Section |", "|---|---|---|");
    for (const f of findings) {
      rows.push(`| [${f.id}](#${f.anchor}) | ${plain(f.title)} | ${plain(f.section)} |`);
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

function render(kind, attrs, headings) {
  switch (kind) {
    case "toc":
      return renderToc(headings, Number(attrs.depth ?? 2));
    case "finding-index":
      return renderFindingIndex(headings);
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
  const lines = original.split("\n");
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
    out.push(lines[i], ...render(kind, attrs, headings), END);
    i = end;
    blocks++;
  }
  const updated = out.join("\n");
  return { blocks, stale: updated !== original, updated };
}

let stale = 0;
for (const name of readdirSync(docsDir).filter((n) => n.endsWith(".md")).sort()) {
  const path = join(docsDir, name);
  const result = processFile(path);
  if (result.blocks === 0) continue;
  if (result.stale) {
    stale++;
    if (check) {
      console.error(`stale: docs/${name}`);
    } else {
      writeFileSync(path, result.updated);
      console.log(`updated: docs/${name}`);
    }
  }
}
if (check && stale > 0) {
  console.error(`${stale} file(s) have stale doc-index blocks; run node tools/update-doc-indexes.mjs`);
  process.exit(1);
}
if (!check && stale === 0) console.log("all doc-index blocks are current");
