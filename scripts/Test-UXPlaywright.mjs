// Exercise the real configured MCP server over stdio; never touches the product.
import {spawn} from 'node:child_process';
import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';

const specPath = process.argv[2];
if (!specPath) throw new Error('Usage: node scripts/Test-UXPlaywright.mjs <playwright-only-config.json>');
const spec = JSON.parse(await readFile(specPath, 'utf8'));
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const html = `<!doctype html><html lang="fr"><meta charset="utf-8"><title>Test outillage UX</title>
<style>body{font:18px system-ui;margin:32px;color:#222;background:white}label,input,button{display:block;margin:16px 0}input,button{font:inherit;padding:8px}:focus-visible{outline:3px solid #005fcc}</style>
<h1>Projet de test</h1><label for="name">Nom du projet</label><input id="name"><button type="button" onclick="document.querySelector('output').textContent='Enregistré'">Enregistrer</button><output aria-live="polite"></output></html>`;
const server = createServer((req,res) => {res.setHeader('Content-Type','text/html; charset=utf-8');res.end(html);});
await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
const proc = spawn(spec.command,spec.args,{cwd:root,env:{...process.env,...spec.env},windowsHide:true,stdio:['pipe','pipe','pipe']});
const pending = new Map();
let sequence=0, buffer='', stderr='';
proc.stderr.on('data',d => {stderr=(stderr+d).slice(-6000);});
proc.stdout.setEncoding('utf8');
proc.stdout.on('data',data => {
  buffer+=data;
  for (;;) {
    const end=buffer.indexOf('\n');
    if(end<0) break;
    const line=buffer.slice(0,end); buffer=buffer.slice(end+1);
    if(!line.trim()) continue;
    let msg;
    try {msg=JSON.parse(line);} catch {continue;}
    if(pending.has(msg.id)) {const {resolve,reject,timer}=pending.get(msg.id);clearTimeout(timer);pending.delete(msg.id);msg.error?reject(new Error(JSON.stringify(msg.error))):resolve(msg.result);}
  }
});
function rejectAll(error){for(const p of pending.values()){clearTimeout(p.timer);p.reject(error);}pending.clear();}
proc.on('error',rejectAll);
proc.on('exit',(code)=>rejectAll(new Error(`MCP exited ${code}: ${stderr}`)));
function rpc(method,params={}){
  const id=++sequence;
  return new Promise((resolve,reject)=>{
    const timer=setTimeout(()=>{pending.delete(id);reject(new Error(`MCP timeout: ${method}: ${stderr}`));},60000);
    pending.set(id,{resolve,reject,timer});
    proc.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');
  });
}
async function call(name,args={}){
  const r=await rpc('tools/call',{name,arguments:args});
  if(r.isError) throw new Error(`${name}: ${JSON.stringify(r.content)}`);
  return r;
}
const text = r => (r.content||[]).filter(c=>c.type==='text').map(c=>c.text).join('\n');
let browserOpened=false;
try {
  const init=await rpc('initialize',{protocolVersion:'2024-11-05',capabilities:{},clientInfo:{name:'forge-ux-install-validation',version:'1.0.0'}});
  proc.stdin.write(JSON.stringify({jsonrpc:'2.0',method:'notifications/initialized'})+'\n');
  const {tools}=await rpc('tools/list');
  for(const name of ['browser_navigate','browser_snapshot','browser_press_key','browser_evaluate','browser_take_screenshot','browser_close']) assert.ok(tools.some(t=>t.name===name),name);
  await call('browser_navigate',{url:`http://127.0.0.1:${server.address().port}/`});
  browserOpened=true;
  const snapshot=text(await call('browser_snapshot'));
  assert.match(snapshot,/Nom du projet/); assert.match(snapshot,/Enregistrer/);
  await call('browser_press_key',{key:'Tab'});
  assert.match(text(await call('browser_evaluate',{function:'() => document.activeElement.id'})),/name/);
  await call('browser_press_key',{key:'Q'});
  assert.match(text(await call('browser_evaluate',{function:'() => document.querySelector("input").value'})),/Q/);
  await call('browser_press_key',{key:'Tab'});
  await call('browser_press_key',{key:'Enter'});
  assert.match(text(await call('browser_snapshot')),/Enregistré/);
  const capture = text(await call('browser_take_screenshot',{type:'png',fullPage:true}));
  console.log(JSON.stringify({status:'PASS',server:init.serverInfo,mcpPid:proc.pid,toolCount:tools.length,checks:['initialize','tools/list','loopback navigation','accessible snapshot','keyboard focus','keyboard input','keyboard submit','live success','screenshot'],capture,scope:'Ephemeral fixture only; no Studio qualification'},null,2));
} finally {
  if(browserOpened) await call('browser_close').catch(e=>console.error('browser_close:',e.message));
  proc.stdin.end();
  if(proc.exitCode===null) {
    await Promise.race([new Promise(resolve=>proc.once('exit',resolve)),new Promise(resolve=>setTimeout(resolve,3000))]);
    if(proc.exitCode===null) {
      proc.kill();
      await new Promise(resolve=>proc.once('exit',resolve));
    }
  }
  await new Promise(resolve=>server.close(resolve));
  assert.ok(!server.listening,'Test HTTP server stopped');
  assert.ok(proc.exitCode!==null || proc.signalCode!==null,'MCP process stopped');
  console.log('Cleanup: browser_close requested; MCP stdio closed; loopback server stopped.');
}
