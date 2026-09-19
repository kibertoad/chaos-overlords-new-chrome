// Where the multiplayer release version is written down, and the arithmetic a release form implies.
//
// The publishable package manifests are the record. They carry the version that was last released,
// the way `version.txt` does for the installers, so a release asked for by kind — major, minor or
// patch — counts on from them and nobody types a number. `check-versions.mjs` holds them to one
// version; this reads the highest of them, so a bump that landed in only some manifests still
// counts on from the version furthest along rather than the one left behind.

import { globSync, readFileSync, writeFileSync } from 'node:fs'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const workspaceRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')

// Manifest paths are workspace-relative throughout. This workspace sits one level below the
// repository root, which is where the release workflow runs and how git names a file.
export function repositoryPath(manifestPath) {
  return [basename(workspaceRoot), ...manifestPath.split(/[\\/]/)].join('/')
}

const MANIFEST_GLOBS = ['packages/*/package.json', 'runtimes/*/package.json']
const RELEASE_VERSION = /^(\d+)\.(\d+)\.(\d+)$/

/** The kinds of release the workflow's dropdown offers, in the order it offers them. */
export const BUMPS = ['patch', 'minor', 'major']

/** Every manifest npm would receive a tarball for, ordered by path. */
export function readPublishableManifests() {
  const manifests = globSync(MANIFEST_GLOBS, { cwd: workspaceRoot })
    .sort()
    .map((path) => ({ path, ...JSON.parse(readFileSync(join(workspaceRoot, path), 'utf8')) }))
    .filter((manifest) => manifest.private !== true)

  if (manifests.length === 0) {
    throw new Error('No publishable packages found.')
  }

  return manifests
}

export function isReleaseVersion(version) {
  return RELEASE_VERSION.test(version)
}

export function parseVersion(version) {
  const parts = RELEASE_VERSION.exec(version)
  if (!parts) {
    throw new Error(`A release version must look like 0.1.0; found '${version}'.`)
  }

  const [major, minor, patch] = parts.slice(1).map(Number)
  return { major, minor, patch }
}

/** Orders two release versions by number rather than by text, so 0.10.0 follows 0.9.0. */
export function compareVersions(left, right) {
  const a = parseVersion(left)
  const b = parseVersion(right)
  return a.major - b.major || a.minor - b.minor || a.patch - b.patch
}

/** The version the given kind of release publishes: major and minor reset what follows them. */
export function nextVersion(current, bump) {
  const { major, minor, patch } = parseVersion(current)
  switch (bump) {
    case 'major':
      return `${major + 1}.0.0`
    case 'minor':
      return `${major}.${minor + 1}.0`
    case 'patch':
      return `${major}.${minor}.${patch + 1}`
    default:
      throw new Error(`A release advances by ${BUMPS.join(', ')}; found '${bump}'.`)
  }
}

/** The version the publishable packages record — the one a release counts on from. */
export function readRecordedVersion() {
  const versions = readPublishableManifests().map((manifest) => {
    if (!isReleaseVersion(manifest.version)) {
      throw new Error(
        `${manifest.path} must record one x.y.z version; found '${manifest.version}'.`,
      )
    }
    return manifest.version
  })

  return versions.reduce((highest, version) =>
    compareVersions(version, highest) > 0 ? version : highest,
  )
}

/**
 * Records one version in every publishable manifest and returns the paths that changed.
 *
 * Only the manifest's own `version` line is rewritten. Re-serialising nine hand-formatted manifests
 * would put unrelated churn in a release commit, so the replacement is checked against a reparse
 * instead: the result has to be the same manifest with the same key order and one new version.
 */
export function writeRecordedVersion(version) {
  parseVersion(version)

  const changed = []
  for (const manifest of readPublishableManifests()) {
    if (manifest.version === version) continue

    const file = join(workspaceRoot, manifest.path)
    const before = readFileSync(file, 'utf8')
    const after = before.replace(/^(\s*"version":\s*)"[^"]*"/m, `$1"${version}"`)
    const expected = JSON.stringify({ ...JSON.parse(before), version })
    if (JSON.stringify(JSON.parse(after)) !== expected) {
      throw new Error(`Unable to record version ${version} in ${manifest.path}.`)
    }

    writeFileSync(file, after)
    changed.push(manifest.path)
  }

  return changed
}
