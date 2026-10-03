// Run the pinned, unmodified Tidewater app in a real browser. This local Vite
// wrapper only serves the acquisition UI and records its output; it does not
// replace GPU APIs, materials, hooks, shader builders, or app source files.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';

const [sourceArgument, outputArgument, portArgument = '18844'] = process.argv.slice(2);
if (!sourceArgument || !outputArgument) throw Error('Usage: node Acquire-BoatShaderPackage.mjs SOURCE OUTPUT [PORT]');
const source = await fs.realpath(sourceArgument);
const output = path.resolve(outputArgument);
const port = Number(portArgument);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw Error('Invalid loopback port');
const commit = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: source, encoding: 'utf8' }).trim();
if (commit !== '1438b1abfcaee3267092b75573014f4d9b4a983c') throw Error('Unexpected source revision');
if (execFileSync('git', ['status', '--porcelain'], { cwd: source, encoding: 'utf8' }).trim()) throw Error('Source checkout is dirty');
await fs.mkdir(output, { recursive: true });
if (await fs.stat(path.join(output, 'capture.json')).then(() => true).catch(() => false)) throw Error('Capture exists; use a fresh output directory');
const ownDirectory = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(\w:)/, '$1'));
const html = await fs.readFile(path.join(ownDirectory, 'boat-shader-acquisition.html'));
const probe = await fs.readFile(path.join(ownDirectory, 'boat-shader-acquisition.js'));
const { createServer } = await import(pathToFileURL(path.join(source, 'node_modules/vite/dist/node/index.js')));
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const inputs = execFileSync('git', ['ls-files', 'src', 'public', 'package.json', 'package-lock.json'], { cwd: source, encoding: 'utf8' }).trim().split(/\r?\n/);
const sourceFiles = [];
for (const relative of inputs) {
  const bytes = await fs.readFile(path.join(source, relative));
  sourceFiles.push({ path: relative, bytes: bytes.length, sha256: sha(bytes) });
}
await fs.writeFile(path.join(output, 'source-inputs.json'), JSON.stringify({ commit, files: sourceFiles }, null, 2) + '\n');
const server = await createServer({
  configFile: false, root: source, base: '/', cacheDir: path.join(output, 'vite-cache'),
  server: { host: '127.0.0.1', port, strictPort: true, fs: { allow: [source] } },
  plugins: [{ name: 'forge-boat-source-acquisition', configureServer(vite) {
    vite.middlewares.use(async (req, res, next) => {
      const url = new URL(req.url, `http://127.0.0.1:${port}`);
      if (url.pathname === '/forge-boat-capture') {
        res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); return;
      }
      if (url.pathname === '/forge-boat-capture.js') {
        res.setHeader('Content-Type', 'text/javascript; charset=utf-8'); res.end(probe); return;
      }
      if (url.pathname !== '/forge-boat-capture-result' || req.method !== 'POST') { next(); return; }
      try {
        const chunks = []; let size = 0;
        for await (const chunk of req) { size += chunk.length; if (size > 32 * 1024 * 1024) throw Error('Capture too large'); chunks.push(chunk); }
        const body = Buffer.concat(chunks);
        const capture = JSON.parse(body);
        if (capture.schema !== 'forge.tidewater.boat-shader-capture' || capture.version !== 2) throw Error('Invalid capture schema');
        if (capture.status === 'complete' && (capture.materials.length !== 7 || capture.shaders.length !== 20)) throw Error('Incomplete shader inventory');
        const name = capture.status === 'complete' ? 'capture.json' : `capture-failure-${Date.now()}.json`;
        await fs.writeFile(path.join(output, name), body, { flag: 'wx' });
        await fs.writeFile(path.join(output, name + '.receipt.json'), JSON.stringify({ commit, recorded_utc: new Date().toISOString(), bytes: body.length, sha256: sha(body), source_inputs_sha256: sha(await fs.readFile(path.join(output, 'source-inputs.json'))), helper_sha256: sha(await fs.readFile(new URL(import.meta.url))), probe_sha256: sha(probe), original_source_modified: false, gpu_apis_replaced: false }, null, 2) + '\n');
        res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ saved: name, sha256: sha(body) }));
      } catch (error) { res.statusCode = 400; res.end(String(error)); }
    });
  } }],
});
await server.listen();
await fs.writeFile(path.join(output, 'server.json'), JSON.stringify({ pid: process.pid, port, url: `http://127.0.0.1:${port}/forge-boat-capture`, commit, source, output, started_utc: new Date().toISOString() }, null, 2) + '\n');
console.log(`Source acquisition ready on http://127.0.0.1:${port}/forge-boat-capture`);
process.on('SIGINT', async () => { await server.close(); process.exit(0); });
