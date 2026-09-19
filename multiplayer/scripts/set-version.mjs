#!/usr/bin/env node
// Records one version in every publishable manifest.
//
//   node scripts/set-version.mjs 0.2.0
//
// The publish workflow runs this twice over: once on `main` to record the version it is about to
// release, and once in the checkout it publishes from, where a release cut from a tag may need the
// version applied that `main` never carried. The second run is a no-op for the first's commit.
//
// The manifests it rewrote go to stdout, one path per line and named as git names them, so the
// workflow commits exactly those rather than whatever else it finds changed. The summary a person
// reads goes to stderr, where it stays out of that list.

import { run } from './lib/cli.mjs'
import { repositoryPath, writeRecordedVersion } from './lib/versions.mjs'

run(() => {
  const [version] = process.argv.slice(2)
  if (!version) {
    throw new Error('Usage: node scripts/set-version.mjs <version>')
  }

  const changed = writeRecordedVersion(version).map(repositoryPath)
  if (changed.length === 0) {
    console.error(`Every publishable package already records ${version}.`)
    return
  }

  console.error(`Recorded ${version} in ${changed.length} manifest(s).`)
  console.log(changed.join('\n'))
})
