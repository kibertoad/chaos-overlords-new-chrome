#!/usr/bin/env node
// Prints the version a release of the given kind would publish, without starting one.
//
//   node scripts/next-version.mjs patch          # count on from the recorded version
//   node scripts/next-version.mjs minor 0.4.2    # ...or from the version given instead
//
// This is the arithmetic alone. What a release would actually publish also depends on what is
// already tagged, which is `release-version.mjs`.

import { run } from './lib/cli.mjs'
import { BUMPS, nextVersion, readRecordedVersion } from './lib/versions.mjs'

run(() => {
  const [bump, currentVersion] = process.argv.slice(2)
  if (!BUMPS.includes(bump)) {
    throw new Error(`Usage: node scripts/next-version.mjs <${BUMPS.join('|')}> [current-version]`)
  }

  console.log(nextVersion(currentVersion ?? readRecordedVersion(), bump))
})
