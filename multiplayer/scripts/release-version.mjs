#!/usr/bin/env node
// Prints the version a release of the given kind would publish, given what is already tagged.
//
//   node scripts/release-version.mjs patch [remote]
//
// A release is asked for by kind, so the number is settled here, out of the version the publishable
// manifests record right now and the `multiplayer-v*` tags the remote carries. Two cases are not a
// plain bump, and they match how the installer release settles its own number:
//
//   A recorded version that carries no tag never shipped — a release that recorded it and then
//   failed, or one bumped by hand because the workflow cannot commit to main — so that version is
//   published instead of a number past it, and the release kind is ignored for that run.
//
//   A recorded version that has fallen behind a tag stops the release, because counting on from it
//   would land on a number that is taken.
//
// Only plain `x.y.z` tags count as released. A prerelease pushed by hand is outside this counting,
// as it is for the installers, and a release after one still counts on from the recorded version.
//
// The chosen version is the only thing printed to stdout, so a caller can capture it; the reasoning
// goes to stderr, where a workflow log keeps it.

import { execFileSync } from 'node:child_process'
import { run } from './lib/cli.mjs'
import {
  BUMPS,
  compareVersions,
  isReleaseVersion,
  nextVersion,
  readRecordedVersion,
} from './lib/versions.mjs'

const TAG_PREFIX = 'multiplayer-v'

function readReleasedVersions(remote) {
  let refs
  try {
    refs = execFileSync('git', ['ls-remote', '--tags', remote, `refs/tags/${TAG_PREFIX}*`], {
      encoding: 'utf8',
    })
  } catch (cause) {
    throw new Error(`Unable to list the existing release tags on '${remote}'.`, { cause })
  }

  const versions = refs
    .split('\n')
    .map((line) => line.split('\t')[1])
    .filter((ref) => ref?.startsWith(`refs/tags/${TAG_PREFIX}`))
    // A tag object and the commit it dereferences to are listed separately, the second as `^{}`.
    .map((ref) => ref.slice(`refs/tags/${TAG_PREFIX}`.length).replace(/\^\{\}$/, ''))
    .filter(isReleaseVersion)

  return [...new Set(versions)].sort(compareVersions)
}

run(() => {
  const [bump, remote = 'origin'] = process.argv.slice(2)
  if (!BUMPS.includes(bump)) {
    throw new Error(`Usage: node scripts/release-version.mjs <${BUMPS.join('|')}> [remote]`)
  }

  const recorded = readRecordedVersion()
  const released = readReleasedVersions(remote)
  const highest = released.at(-1)

  if (highest && compareVersions(highest, recorded) > 0) {
    throw new Error(
      `The packages record ${recorded}, but ${highest} is already released. ` +
        'Correct the package versions on main before releasing again.',
    )
  }

  if (!released.includes(recorded)) {
    console.error(
      `The packages record ${recorded} and no tag does, so that release never finished; ` +
        `publishing ${recorded} rather than a new ${bump} version.`,
    )
    console.log(recorded)
    return
  }

  const next = nextVersion(recorded, bump)
  console.error(`Releasing ${next}, the ${bump} release after ${recorded}.`)
  console.log(next)
})
