#!/usr/bin/env node
// Asserts that every publishable package carries the same version, and — when a version is passed —
// that it is that one.
//
//   node scripts/check-versions.mjs          # all publishable packages agree
//   node scripts/check-versions.mjs 0.2.0    # ...and they are all 0.2.0
//
// The packages depend on each other through `workspace:*`, which pnpm rewrites to the exact version
// at publish time. A half-finished bump therefore does not fail loudly: it publishes a package that
// pins a sibling version nobody released. The release workflow records the version it is releasing
// in every manifest and then runs this against that version before anything reaches the registry.

import { run } from './lib/cli.mjs'
import { readPublishableManifests } from './lib/versions.mjs'

run(() => {
  const expected = process.argv[2]
  const manifests = readPublishableManifests()
  const target = expected ?? manifests[0].version
  const mismatched = manifests.filter((manifest) => manifest.version !== target)

  if (mismatched.length > 0) {
    const detail = mismatched.map((m) => `  ${m.name}: ${m.version} (${m.path})`).join('\n')
    throw new Error(`Expected every publishable package to be ${target}, but:\n${detail}`)
  }

  console.log(`${manifests.length} publishable packages, all at ${target}.`)
})
