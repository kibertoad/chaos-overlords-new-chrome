// The Worker that check.mjs runs under workerd: it boots the resolver from modules bundled at
// upload and replays the transcript POSTed to it. bundle-workerd.mjs copies this file next to the
// generated modules it imports.
import { dotnet } from './dotnet.js';
import * as nativeModule from './dotnet.native.js';
import * as runtimeModule from './dotnet.runtime.js';
import nativeWasm from './dotnet.native.wasm';
import { assemblies, names } from './assemblies.mjs';
import { replayTranscript } from './driver.mjs';

let booted;

// workerd compiles WebAssembly only at upload; compiling bytes at run time is refused. The runtime's
// one module therefore arrives as an import, and the loader's compile step is answered with it.
// The interpreter's trace compiler (the "jiterpreter") would also compile modules at run time, so
// it is switched off.
async function boot() {
  const compiled = async () => nativeWasm;
  WebAssembly.compileStreaming = compiled;
  WebAssembly.compile = compiled;
  const runtime = await dotnet
    .withConfig({
      resources: {
        jsModuleNative: [{ name: names.native, moduleExports: nativeModule }],
        jsModuleRuntime: [{ name: names.runtime, moduleExports: runtimeModule }],
      },
    })
    .withRuntimeOptions([
      '--no-jiterpreter-traces-enabled',
      '--no-jiterpreter-interp-entry-enabled',
      '--no-jiterpreter-jit-call-enabled',
    ])
    .withResourceLoader((type, name) => {
      if (type === 'dotnetwasm') {
        return Promise.resolve(new Response(new Uint8Array(0), { headers: { 'content-type': 'application/wasm' } }));
      }
      const bytes = assemblies[name];
      return bytes ? Promise.resolve(new Response(bytes)) : undefined;
    })
    .create();
  const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  return { runtime, resolver: exports.Rechaos.Resolver.Wasm.ResolverExports };
}

export default {
  async fetch(request) {
    try {
      const started = Date.now();
      booted ??= await boot();
      const bootMs = Date.now() - started;
      const transcript = await request.json();
      const result = replayTranscript(booted.resolver, transcript);
      const heapBytes = booted.runtime.Module.HEAPU8.byteLength;
      return Response.json({ ...result, bootMs, heapBytes });
    } catch (error) {
      return new Response(String(error?.stack ?? error), { status: 500 });
    }
  },
};
