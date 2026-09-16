#!/usr/bin/env node
// Asserts that every publishable package carries the same version, and — when a version is passed —
// that it is that one.
//
//   node scripts/check-versions.mjs          # all publishable packages agree
//   node scripts/check-versions.mjs 0.2.0    # ...and they are all 0.2.0
//
// The packages depend on each other through `workspace:*`, which pnpm rewrites to the exact version
// at publish time. A half-finished bump therefore does not fail loudly: it publishes a package that
// pins a sibling version nobody released. The release workflow bumps every manifest in its checkout
// and then runs this against the requested version before anything reaches the registry.

import { globSync, readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const workspaceRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const expected = process.argv[2]

const manifests = globSync(['packages/*/package.json', 'runtimes/*/package.json'], {
  cwd: workspaceRoot,
})
  .sort()
  .map((path) => ({ path, ...JSON.parse(readFileSync(join(workspaceRoot, path), 'utf8')) }))
  .filter((manifest) => manifest.private !== true)

if (manifests.length === 0) {
  console.error('No publishable packages found.')
  process.exit(1)
}

const target = expected ?? manifests[0].version
const mismatched = manifests.filter((manifest) => manifest.version !== target)

if (mismatched.length > 0) {
  const detail = mismatched.map((m) => `  ${m.name}: ${m.version} (${m.path})`).join('\n')
  console.error(`Expected every publishable package to be ${target}, but:\n${detail}`)
  process.exit(1)
}

console.log(`${manifests.length} publishable packages, all at ${target}.`)
