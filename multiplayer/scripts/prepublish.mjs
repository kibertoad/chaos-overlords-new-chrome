#!/usr/bin/env node
// Runs from each publishable package's `prepublishOnly`, with the package directory as the cwd.
//
// Two jobs, both of which are easier to get wrong than to check. It drops the workspace's licence
// texts into the package — the packages carry the MIT License and npm ships one tarball per package, so
// each tarball has to carry them itself. And it asserts that everything the manifest promises in
// `files` is actually on disk, because a publish that silently ships an empty `dist` is only
// discovered by whoever installs it.

import { copyFileSync, existsSync, readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// The licence texts live at the repository root, one level above this workspace.
const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')
const packageDir = process.cwd()
const LICENCE_FILES = ['LICENSE', 'NOTICE']

const manifest = JSON.parse(readFileSync(join(packageDir, 'package.json'), 'utf8'))

for (const file of LICENCE_FILES) {
  copyFileSync(join(repositoryRoot, file), join(packageDir, file))
}

// Only the plain paths are checked. An exclusion (`!dist/.tsbuildinfo`) has nothing to exist, and a
// glob would need matching rather than a stat — neither is what this guards against, which is a
// package whose whole `dist` was never built.
const missing = (manifest.files ?? [])
  .filter((entry) => !LICENCE_FILES.includes(entry))
  .filter((entry) => !/^!|[*?[\]{}]/.test(entry))
  .filter((entry) => !existsSync(join(packageDir, entry)))

if (missing.length > 0) {
  console.error(
    `${manifest.name}: package.json lists ${missing.map((entry) => `"${entry}"`).join(', ')} in ` +
      '"files", but they are missing. Run `pnpm build` before publishing.',
  )
  process.exit(1)
}
