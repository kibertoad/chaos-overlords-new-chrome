#!/usr/bin/env node
// Regenerates the generated index blocks in docs/*.md, and checks that every relative link in
// those documents, the root README, AGENTS.md and the rebuild's ledgers still resolves.
//
// A block is delimited by two HTML comments:
//
//   <!-- doc-index:begin <kind> [key=value ...] -->
//   ...generated content...
//   <!-- doc-index:end -->
//
// Kinds:
//   toc depth=N       table of contents of this file's `##`..`#`*N headings
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

/** Every maintained markdown document in docs/. */
function documentPaths() {
  return readdirSync(docsDir).filter((n) => n.endsWith(".md")).sort().map((name) => join(docsDir, name));
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
const rootDocuments = ["README.md", "AGENTS.md", "PARITY.md", "DEVIATIONS.md", "static_validation_plan.md", "manual_validation_plan.md"]
  .map((name) => join(repoDir, name))
  .filter((path) => existsSync(path));
const broken = brokenLinks([...documentPaths(), ...rootDocuments]);
for (const problem of broken) console.error(`broken link: ${problem}`);

if (check && stale > 0) {
  console.error(`${stale} file(s) have stale doc-index blocks; run node tools/update-doc-indexes.mjs`);
}
if (!check && stale === 0) console.log("all doc-index blocks are current");
if (broken.length === 0) console.log("all relative links resolve");
if (broken.length > 0 || (check && stale > 0)) process.exit(1);
