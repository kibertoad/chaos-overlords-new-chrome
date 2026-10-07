// Fails each line of a Markdown file in spec/, other than the generated spec/index/, that names a
// file of the rebuild: a path into src/, tests/ or multiplayer/ (optionally after ./ or ../ parts,
// with / or \ between the parts), or a source file found there by its file name. The spec
// documents the original and never names a class, file or setting of the rebuild (AGENTS.md); the
// parity rows in parity/ carry the test files. A path counts when it exists, has an extension or
// goes more than one level down, so prose such as "tests/experiments" passes. tools/ is left out,
// since a finding may name a research tool such as the probe.
//
// This is the check that kibertoad/refurbished-dinosaurs-toolkit#321 adds to the shared checker as
// --rebuild, with the same patterns; delete this script once tools/check-documentation.mjs runs a
// release that has it, passing --rebuild src,tests,multiplayer.
//
// Usage: node tools/check-rebuild-paths.mjs [--root <dir>]
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const argv = process.argv.slice(2);
const rootIndex = argv.indexOf("--root");
if (rootIndex >= 0 && !argv[rootIndex + 1]) {
  console.error("Usage: node tools/check-rebuild-paths.mjs [--root <dir>]");
  process.exit(2);
}
const root =
  rootIndex >= 0 ? resolve(argv[rootIndex + 1]) : resolve(dirname(fileURLToPath(import.meta.url)), "..");
const rebuildDirs = ["src", "tests", "multiplayer"];
const sourceExtensions = /\.(?:cs|fs|ts|mjs|js|ps1)$/;
const skipped = new Set(["node_modules", "bin", "obj", "dist"]);

function filesUnder(dir) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory()
      ? skipped.has(entry.name)
        ? []
        : filesUnder(join(dir, entry.name))
      : [join(dir, entry.name)],
  );
}

const sourceNames = new Set(
  rebuildDirs
    .flatMap((d) => filesUnder(join(root, d)))
    .map((p) => p.split(/[\\/]/).pop())
    .filter((name) => sourceExtensions.test(name)),
);
const pathPattern = new RegExp(
  `(?<![\\w./\\\\-])(?:\\.{1,2}[/\\\\])*(?:${rebuildDirs.join("|")})[/\\\\][\\w.\\\\/-]*[\\w-]`,
  "g",
);
const namePattern = /(?<![\w./\\-])[\w.-]+\.(?:cs|fs|ts|mjs|js|ps1)(?![\w-])/g;

const problems = [];
const specDir = join(root, "spec");
for (const file of filesUnder(specDir)) {
  const rel = relative(root, file).replaceAll("\\", "/");
  if (!rel.endsWith(".md") || rel.startsWith("spec/index/")) continue;
  const lines = readFileSync(file, "utf8").split(/\r?\n/);
  lines.forEach((line, i) => {
    const found = new Set();
    for (const [match] of line.matchAll(pathPattern)) {
      const path = match.replaceAll("\\", "/").replace(/^(?:\.{1,2}\/)+/, "");
      const counts =
        existsSync(join(root, path)) || /\.\w+$/.test(path) || path.split("/").length > 2;
      if (counts) found.add(path);
    }
    for (const [match] of line.matchAll(namePattern)) {
      if (sourceNames.has(match)) found.add(match);
    }
    for (const name of found) problems.push(`${rel}: line ${i + 1} names ${name}, a file of the rebuild`);
  });
}

for (const p of problems) console.error(p);
if (problems.length) {
  console.error(
    `\n${problems.length} spec line(s) name the rebuild's files. Describe the comparison without ` +
      "naming the rebuild, and list the tests in the parity row instead.",
  );
  process.exit(1);
}
console.log("rebuild path check passed: no spec line names a file of the rebuild");
