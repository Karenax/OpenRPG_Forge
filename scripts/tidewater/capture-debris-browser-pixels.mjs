// Acquire the frozen source's literal loadPixels body in the browser, without a decoder substitute.
// Usage: SOURCE RETAINED_SCANS NEW_OUTPUT PORT
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
const [sourceArg, scansArg, outputArg, portArg] = process.argv.slice(2);
if (!portArg) throw Error('SOURCE RETAINED_SCANS NEW_OUTPUT PORT required');
const source = path.resolve(sourceArg), scans = path.resolve(scansArg), output = path.resolve(outputArg);
const port = Number(portArg), origin = `http://127.0.0.1:${port}`;
if (!Number.isInteger(port) || port < 1024 || port > 65535 || fs.existsSync(output)) throw Error('Fresh output/valid port required');
for (const root of [source, scans]) {
  const rel = path.relative(root, output);
  if (!rel || (!rel.startsWith('..') && !path.isAbsolute(rel))) throw Error('External output required');
}
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`, '-C', source, ...args], {encoding:'utf8'}).trim();
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain', '--untracked-files=all')) throw Error('Clean frozen source required');
const sourceBytes = fs.readFileSync(path.join(source, 'src/world/debris/ScannedDebris.js'));
const retainedBytes = fs.readFileSync(path.join(scans, 'scanned-source.json'));
const retained = JSON.parse(retainedBytes);
if (retained.source_commit !== pin || retained.source_contract_sha256 !== sha(sourceBytes)) throw Error('Scanned source contract differs');
const code = sourceBytes.toString().replaceAll('\r\n', '\n');
const start = code.indexOf('async function loadPixels( url ) {'), end = code.indexOf('\nexport class ScannedDebris', start);
if (start < 0 || end < start || code.indexOf('async function loadPixels( url ) {', start + 1) >= 0) throw Error('Unknown source pixel seam');
const sourceFunction = code.slice(start, end).trimEnd();
const images = new Map();
for (const asset of retained.assets) for (const [map, ref] of Object.entries(asset.images)) {
  if (path.basename(ref.file) !== ref.file || ref.width !== 1024 || ref.height !== 1024) throw Error('Invalid tile contract');
  const bytes = fs.readFileSync(path.join(scans, ref.file));
  if (bytes.length !== ref.bytes || sha(bytes) !== ref.sha256) throw Error('Retained JPEG differs');
  images.set(ref.file, {bytes, asset:asset.ordinal, map, source_sha256:ref.sha256, sampling:ref.source_sampling});
}
if (images.size !== 12) throw Error('Incomplete source images');
fs.mkdirSync(output);
fs.writeFileSync(path.join(output, 'source-load-pixels.mjs'), sourceFunction + '\nexport { loadPixels };\n');
const token = crypto.randomUUID(), captured = {};
const client = fs.readFileSync(new URL('./capture-debris-browser-pixels-client.mjs', import.meta.url));
const respond = (res, status, type, raw) => { res.writeHead(status, {'Content-Type':type, 'Cache-Control':'no-store'}); res.end(raw); };
const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, origin);
    if (req.method === 'GET') {
      if (url.pathname === '/') return respond(res, 200, 'text/html', '<!doctype html><meta charset="utf-8"><title>Tidewater — pixels source</title><h1>Capture des textures source</h1><pre id="status">Chargement…</pre><script type="module" src="/client.mjs"></script>');
      if (url.pathname === '/client.mjs') return respond(res, 200, 'text/javascript', client);
      if (url.pathname === '/source-load-pixels.mjs') return respond(res, 200, 'text/javascript', sourceFunction + '\nexport { loadPixels };\n');
      if (url.pathname === '/inputs.json') return respond(res, 200, 'application/json', JSON.stringify({token, images:[...images.keys()]}));
      const image = images.get(decodeURIComponent(url.pathname.slice('/images/'.length)));
      if (url.pathname.startsWith('/images/') && image) return respond(res, 200, 'image/jpeg', image.bytes);
    }
    if (req.method !== 'POST' || req.headers.origin !== origin || req.headers['x-forge-capture'] !== token) return respond(res, 403, 'text/plain', 'Rejected');
    const limit = url.pathname === '/complete' ? 4096 : 1024 * 1024 * 4;
    const chunks = []; let bytes = 0;
    for await (const chunk of req) { bytes += chunk.length; if (bytes > limit) throw Error('Oversize capture'); chunks.push(chunk); }
    const raw = Buffer.concat(chunks);
    if (url.pathname === '/complete') {
      if (Object.keys(captured).length !== 12) throw Error('Incomplete capture');
      if (sha(fs.readFileSync(path.join(source, 'src/world/debris/ScannedDebris.js'))) !== sha(sourceBytes) || git('status', '--porcelain', '--untracked-files=all')) throw Error('Source changed');
      const metadata = JSON.parse(raw);
      const result = {schema:'forge.tidewater.debris-browser-pixels', version:1, source_commit:pin,
        source_contract_sha256:sha(sourceBytes), source_function_sha256:sha(sourceFunction),
        retained_descriptor_sha256:sha(retainedBytes), acquisition_sha256:sha(fs.readFileSync(import.meta.filename)),
        client_sha256:sha(client), browser:metadata, images:captured,
        limits:['Literal source browser JPEG decoding only; no atlas mips, material draw, Studio or visual parity.']};
      fs.writeFileSync(path.join(output, 'browser-pixels.json'), JSON.stringify(result, null, 2), {flag:'wx'});
      respond(res, 200, 'text/plain', 'PASS — 12 images');
      console.log(JSON.stringify({status:'PASS', output, images:12, descriptor_sha256:sha(fs.readFileSync(path.join(output, 'browser-pixels.json')))}));
      server.close(); return;
    }
    const name = decodeURIComponent(url.pathname.slice('/capture/'.length)), image = images.get(name);
    if (!url.pathname.startsWith('/capture/') || !image || captured[name] || raw.length !== limit) throw Error('Invalid image capture');
    for (let i = 3; i < raw.length; i += 4) if (raw[i] !== 255) throw Error('Nonopaque source JPEG');
    const file = name + '.rgba'; fs.writeFileSync(path.join(output, file), raw, {flag:'wx'});
    captured[name] = {file, bytes:raw.length, sha256:sha(raw), width:1024, height:1024, asset:image.asset, map:image.map,
      source_sha256:image.source_sha256, sampling:image.sampling, row_order:'top-first'};
    respond(res, 200, 'text/plain', 'Saved');
  } catch (error) { console.error(String(error)); respond(res, 400, 'text/plain', String(error)); }
});
server.listen(port, '127.0.0.1', () => console.log(JSON.stringify({url:origin, output, expected_images:12})));
