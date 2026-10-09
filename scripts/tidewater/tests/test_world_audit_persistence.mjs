// Exercise the actual PowerShell-installed fetch observer without a live project.
import fs from 'node:fs';
import assert from 'node:assert/strict';
const text=fs.readFileSync(new URL('../../Invoke-Gate2WorldEditingProof.ps1',import.meta.url),'utf8');
const start=text.indexOf('function Install-WorldAudit {');
const end=text.indexOf('function Get-WorldAuditSnapshot {',start);
const section=text.slice(start,end);
const expression=section.slice(section.indexOf('$expression = @"')+16,section.indexOf('\n"@')).replaceAll('$assetMode','false').trim();
assert.ok(expression.startsWith('(async () => {'));
globalThis.location={href:'http://tauri.localhost/index.html',origin:'http://tauri.localhost'};
const storage=new Map();let quota=2*1024*1024;let calls=0;
globalThis.sessionStorage={getItem:key=>storage.get(key)??null,setItem:(key,value)=>{
 const last=window.__forgeGate2WorldAudit?.entries.at(-1);
 if(last?.request_kind && last.attempts.length)assert.equal(last.outcome,'pending','Durability work must not expose a resolved request before the fetch promise can return');
 if(value.length*2>quota)throw new DOMException('Test storage quota exceeded','QuotaExceededError');storage.set(key,value);
},removeItem:key=>storage.delete(key)};
const original=async (_url,init)=>{
 calls++;const wire=JSON.parse(JSON.parse(init.body).command);
 if(wire.command==='atelier')return new Response(JSON.stringify({response:'atelier'}));
 const request=wire.request.request;
 return new Response(JSON.stringify({response:'authoring',state:{zone_receipt:{request,applied:{objects:request.corrections.objects},inverse:{objects:[]},resulting_revision:String(calls),resulting_hash:'fixture-hash'}}}));
};
globalThis.window={__TAURI__:{core:{invoke(){}}},fetch:original,location};
const install=()=>eval(expression);
const send=wire=>window.fetch('http://ipc.localhost/execute_studio_command',{method:'POST',body:JSON.stringify({command:JSON.stringify(wire)})});
await install();
const objects=Array.from({length:120},(_,i)=>({id:`object-${i}`,asset_hash:'a'.repeat(64),transform:{translation_m:{x:i,y:0,z:1}},name:'Repeated fixture property '.repeat(12)}));
for(let i=0;i<90;i++){
 const response=await send({command:'authoring',request:{action:'apply_zone',request:{command_id:`fixture-${i}`,corrections:{objects}}}});
 assert.equal(response.status,200);assert.equal(window.__forgeGate2WorldAudit.entries.at(-1).outcome,'resolved');
}
for(let i=0;i<400;i++)await send({command:'atelier',request:{operation:'read'}});
const before=window.__forgeGate2WorldAudit.entries.filter(e=>e.request_kind);
assert.ok(JSON.stringify(before).length*2>quota,'Fixture must exceed the uncompressed quota');
assert.equal(window.__forgeGate2WorldAudit.persistence_error,null);
const key=window.__forgeGate2WorldAudit.storageKey;
assert.equal(JSON.parse(storage.get(key)).encoding,'gzip-base64');
assert.ok(storage.get(key).length*2<quota);
window.fetch=original;delete window.__forgeGate2WorldAudit;
await install();
assert.deepEqual(window.__forgeGate2WorldAudit.entries,before,'Every authoring request and response must survive reload exactly');
const next=window.__forgeGate2WorldAudit.next_index;
await send({command:'authoring',request:{action:'apply_zone',request:{command_id:'after-reload',corrections:{objects:[]}}}});
assert.equal(window.__forgeGate2WorldAudit.entries.at(-1).index,next);
quota=1;
const response=await send({command:'authoring',request:{action:'apply_zone',request:{command_id:'forced-storage-failure',corrections:{objects:[]}}}});
assert.equal(response.status,200,'Observer storage failure must not reject a successful product response');
assert.equal(window.__forgeGate2WorldAudit.entries.at(-1).outcome,'resolved');
assert.match(window.__forgeGate2WorldAudit.persistence_error,/QuotaExceededError/);
const snapshotSection=text.slice(end,text.indexOf('function Arm-WorldAuditRetry {',end));
const snapshot=snapshotSection.match(/-Expression @'\r?\n([\s\S]*?)\r?\n'@/)[1];
assert.throws(()=>eval(snapshot),/World audit persistence failed/,'Persistence loss must prevent a successful proof verdict');
quota=2*1024*1024;window.fetch=original;delete window.__forgeGate2WorldAudit;
storage.set(key,JSON.stringify([before[0]]));
await install();assert.deepEqual(window.__forgeGate2WorldAudit.entries,[before[0]],'Legacy uncompressed audit must remain readable');
console.log(JSON.stringify({passed:true,authoring_entries_restored:90,observation_entries_in_memory:400,lossless_reload:true,storage_failure_does_not_reject_product:true}));
