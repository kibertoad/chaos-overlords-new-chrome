#!/usr/bin/env node
// Prints the version the publishable packages record, which is the one a release counts on from.
//
//   node scripts/current-version.mjs

import { run } from './lib/cli.mjs'
import { readRecordedVersion } from './lib/versions.mjs'

run(() => {
  console.log(readRecordedVersion())
})
