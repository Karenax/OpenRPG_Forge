import './build-aube.mjs';
import { createReadStream, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../dist/', import.meta.url)));
const mime = {
  '.css': 'text/css',
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.json': 'application/json',
  '.md': 'text/markdown',
  '.png': 'image/png',
};

createServer((request, response) => {
  let name;
  try {
    name = decodeURIComponent(new URL(request.url, 'http://127.0.0.1:1430').pathname);
  } catch {
    response.writeHead(400).end();
    return;
  }
  const file = resolve(join(root, name === '/' ? 'index.html' : `.${name}`));
  if (relative(root, file).startsWith('..')) {
    response.writeHead(403).end();
    return;
  }
  try {
    if (!statSync(file).isFile()) throw new Error('Not a file');
    response.writeHead(200, { 'Content-Type': mime[extname(file)] || 'application/octet-stream' });
    createReadStream(file).pipe(response);
  } catch {
    response.writeHead(404).end();
  }
}).listen(1430, '127.0.0.1', () => console.log('AUBE_DEV_READY http://127.0.0.1:1430'));
