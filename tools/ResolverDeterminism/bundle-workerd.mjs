// Lays the resolver's _framework folder out as workerd modules:
//
//   node bundle-workerd.mjs <_framework dir> <out dir>
//
// workerd has no file system and no dynamic import of a URL, and it compiles WebAssembly only at
// upload. So the runtime's two JavaScript modules are imported statically and handed to the loader
// as preloaded modules, dotnet.native.wasm is a CompiledWasm module, and every assembly is a Data
// module the resource loader answers from. Two edits are made to the loader's own files: the
// embedded boot config loses the module entries the Worker supplies itself (the loader adds the
// Worker's entries to the embedded ones rather than replacing them), and import.meta.url, which
// workerd leaves undefined, becomes a fixed base the loader only resolves names against.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const [framework, outDir] = process.argv.slice(2);
if (!framework || !outDir) {
  console.error('usage: node bundle-workerd.mjs <_framework dir> <out dir>');
  process.exit(2);
}
const here = path.dirname(fileURLToPath(import.meta.url));
fs.rmSync(outDir, { recursive: true, force: true });
fs.mkdirSync(path.join(outDir, 'assemblies'), { recursive: true });

const loader = fs.readFileSync(path.join(framework, 'dotnet.js'), 'utf8');
const begin = loader.indexOf('/*json-start*/') + '/*json-start*/'.length;
const end = loader.indexOf('/*json-end*/');
if (begin < '/*json-start*/'.length || end < begin) throw new Error('dotnet.js carries no embedded boot config');
const config = JSON.parse(loader.slice(begin, end));
const resources = config.resources;
const names = {
  native: resources.jsModuleNative[0].name,
  runtime: resources.jsModuleRuntime[0].name,
  wasm: resources.wasmNative[0].name,
};
delete resources.jsModuleNative;
delete resources.jsModuleRuntime;
delete resources.pdb;
delete resources.corePdb;

const base = (file) => JSON.stringify(`https://resolver.invalid/_framework/${file}`);
const write = (file, text) => fs.writeFileSync(path.join(outDir, file), text.replaceAll('import.meta.url', base(file)));
write('dotnet.js', loader.slice(0, begin) + JSON.stringify(config) + loader.slice(end));
write('dotnet.native.js', fs.readFileSync(path.join(framework, names.native), 'utf8'));
write('dotnet.runtime.js', fs.readFileSync(path.join(framework, names.runtime), 'utf8'));
fs.copyFileSync(path.join(framework, names.wasm), path.join(outDir, 'dotnet.native.wasm'));

let imports = '';
let table = '';
let bytes = 0;
let count = 0;
[...(resources.coreAssembly ?? []), ...(resources.assembly ?? [])].forEach((asset, index) => {
  const source = path.join(framework, asset.name);
  fs.copyFileSync(source, path.join(outDir, 'assemblies', `${index}.bin`));
  bytes += fs.statSync(source).size;
  count++;
  imports += `import a${index} from './assemblies/${index}.bin';\n`;
  table += `  ${JSON.stringify(asset.name)}: a${index},\n`;
});
fs.writeFileSync(
  path.join(outDir, 'assemblies.mjs'),
  `${imports}\nexport const assemblies = {\n${table}};\n\nexport const names = ${JSON.stringify(names)};\n`,
);
fs.copyFileSync(path.join(here, 'workerd-entry.mjs'), path.join(outDir, 'index.mjs'));
fs.copyFileSync(path.join(here, 'driver.mjs'), path.join(outDir, 'driver.mjs'));
fs.writeFileSync(
  path.join(outDir, 'wrangler.toml'),
  [
    'name = "rechaos-resolver-check"',
    'main = "index.mjs"',
    'compatibility_date = "2025-06-01"',
    'no_bundle = true',
    'rules = [',
    '  { type = "Data", globs = ["**/*.bin"], fallthrough = false },',
    '  { type = "CompiledWasm", globs = ["**/*.wasm"], fallthrough = false },',
    '  { type = "ESModule", globs = ["**/*.js", "**/*.mjs"], fallthrough = false },',
    ']',
    '',
  ].join('\n'),
);
const nativeBytes = fs.statSync(path.join(outDir, 'dotnet.native.wasm')).size;
console.log(`bundled ${count} assemblies (${bytes} bytes) and a ${nativeBytes}-byte runtime into ${outDir}`);
