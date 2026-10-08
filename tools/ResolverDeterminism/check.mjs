// Holds the WebAssembly resolver to the native build's hashes, under Node and under workerd.
//
//   node tools/ResolverDeterminism/check.mjs <_framework dir> <transcript.json> [node|workerd]...
//
// <_framework dir> is the trimmed publish of src/Rechaos.Resolver.Wasm, and the transcript is what
// Program.cs wrote. With no runtime named, both run. workerd is driven through wrangler, which is
// resolved from multiplayer/runtimes/cloudflare, so `pnpm install` in multiplayer/ comes first.
// Exits non-zero when any hash differs.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { replayTranscript } from './driver.mjs';

const [framework, transcriptPath, ...requested] = process.argv.slice(2);
if (!framework || !transcriptPath) {
  console.error('usage: node check.mjs <_framework dir> <transcript.json> [node|workerd]...');
  process.exit(2);
}
const runtimes = requested.length > 0 ? requested : ['node', 'workerd'];
const transcriptText = fs.readFileSync(transcriptPath, 'utf8');
const transcript = JSON.parse(transcriptText);
const here = path.dirname(fileURLToPath(import.meta.url));
const repository = path.resolve(here, '..', '..');

async function onNode() {
  const started = performance.now();
  const { dotnet } = await import(pathToFileURL(path.join(path.resolve(framework), 'dotnet.js')).href);
  const runtime = await dotnet.create();
  const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  const bootMs = Math.round(performance.now() - started);
  const result = replayTranscript(exports.Rechaos.Resolver.Wasm.ResolverExports, transcript);
  return { ...result, bootMs, heapBytes: runtime.Module.HEAPU8.byteLength };
}

async function onWorkerd() {
  const bundle = fs.mkdtempSync(path.join(os.tmpdir(), 'rechaos-resolver-workerd-'));
  const directory = process.cwd();
  let worker;
  try {
    const { execFileSync } = await import('node:child_process');
    execFileSync(process.execPath, [path.join(here, 'bundle-workerd.mjs'), framework, bundle], { stdio: 'inherit' });
    const require = createRequire(path.join(repository, 'multiplayer', 'runtimes', 'cloudflare', 'package.json'));
    const { unstable_dev } = require('wrangler');
    // wrangler keeps its local state under the working directory; the bundle's is thrown away.
    process.chdir(bundle);
    worker = await unstable_dev(path.join(bundle, 'index.mjs'), {
      config: path.join(bundle, 'wrangler.toml'),
      logLevel: 'warn',
      experimental: { disableExperimentalWarning: true },
    });
    const response = await worker.fetch('http://resolver/', { method: 'POST', body: transcriptText });
    const text = await response.text();
    if (!response.ok) throw new Error(`the Worker answered ${response.status}: ${text}`);
    return JSON.parse(text);
  } finally {
    await worker?.stop();
    process.chdir(directory);
    fs.rmSync(bundle, { recursive: true, force: true });
  }
}

let failed = false;
for (const name of runtimes) {
  const started = performance.now();
  const result = name === 'node' ? await onNode() : name === 'workerd' ? await onWorkerd() : null;
  if (!result) throw new Error(`unknown runtime ${name}`);
  const wallMs = Math.round(performance.now() - started);
  const heapMb = (result.heapBytes / 1048576).toFixed(0);
  // workerd's clocks advance only on I/O, so Date.now() inside the one request the whole match is
  // resolved in reads the same before and after: its boot and resolving times come out as 0, and
  // only the wall time measured here says what the run cost.
  const timings =
    name === 'workerd' ? 'boot and resolving not measurable inside a request' : `boot ${result.bootMs} ms, resolving ${result.resolveMs} ms`;
  console.log(
    `${name}: ${result.checks} checks, ${result.mismatches.length} mismatches; ` +
      `${timings}, wall ${wallMs} ms, wasm heap ${heapMb} MiB`,
  );
  for (const mismatch of result.mismatches) {
    console.log(`  ${mismatch.what}: expected ${mismatch.expected}, got ${mismatch.actual}`);
  }
  failed ||= result.mismatches.length > 0;
}
process.exit(failed ? 1 : 0);
