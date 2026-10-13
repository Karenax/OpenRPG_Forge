const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../editor-gpu-pacing.js'), 'utf8');
function environment(search = '?authoring=1') {
  const native = new Map();const errors = [];const events = new Map();let id = 1;let now = 0;let loading = false;let paused = false;
  const context = { location: { search }, URLSearchParams,
    requestAnimationFrame: callback => { const key=id++;native.set(key,callback);return key; },
    cancelAnimationFrame: key => native.delete(key), setTimeout: callback => errors.push(callback),
    performance: { now: () => now }, addEventListener: (event, callback) => events.set(event, callback),
    document: { querySelector: selector => selector.includes('data-game-paused') ? paused : loading } };
  const original=context.requestAnimationFrame;const originalCancel=context.cancelAnimationFrame;
  vm.runInNewContext(source, context);
  return { context,native,errors,original,originalCancel,
    event(event) { events.get(event)(); }, loading(value) { loading=value; }, pause(value) { paused=value; }, frame(timestamp) {
    now=timestamp;
    const batch=[...native.values()];native.clear();for(const callback of batch) callback(timestamp);
  } };
}
test('callbacks share a bounded frame and newly requested work waits', () => {
  const e=environment();const calls=[];
  e.context.requestAnimationFrame(time => { calls.push(['first',time]);e.context.requestAnimationFrame(next=>calls.push(['next',next])); });
  e.context.requestAnimationFrame(time=>calls.push(['second',time]));
  assert.equal(e.native.size,1);
  e.frame(100);assert.deepEqual(calls,[['first',100],['second',100]]);
  e.frame(107);assert.equal(calls.length,2);
  e.frame(121);assert.deepEqual(calls[2],['next',121]);assert.equal(e.native.size,0);
});
test('cancellation works before delivery and within a batch', () => {
  const e=environment();let calls=0;
  const cancelled=e.context.requestAnimationFrame(()=>calls++);
  e.context.cancelAnimationFrame(cancelled);assert.equal(e.native.size,0);
  let later;
  e.context.requestAnimationFrame(()=>{calls++;e.context.cancelAnimationFrame(later);});
  later=e.context.requestAnimationFrame(()=>calls++);
  e.frame(100);assert.equal(calls,1);assert.equal(e.native.size,0);
});
test('callback failure stays visible without suppressing other callbacks', () => {
  const e=environment();const error=new Error('callback');let later=false;
  e.context.requestAnimationFrame(()=>{throw error;});e.context.requestAnimationFrame(()=>{later=true;});
  e.frame(100);assert.equal(later,true);assert.equal(e.errors.length,1);
  assert.throws(e.errors[0],actual=>actual===error);
});
test('other views keep both original animation APIs', () => {
  const e=environment('?scene=gate0');
  assert.equal(e.context.requestAnimationFrame,e.original);assert.equal(e.context.cancelAnimationFrame,e.originalCancel);
});
test('idle GPU work slows down and input restores active cadence', () => {
  const e=environment();const calls=[];
  function update(time) { calls.push(time);e.context.requestAnimationFrame(update); }
  e.context.requestAnimationFrame(update);
  e.frame(1900);e.frame(2001);e.frame(2399);assert.deepEqual(calls,[1900]);
  e.frame(2400);assert.deepEqual(calls,[1900,2400]);
  e.event('pointermove');e.frame(2420);assert.deepEqual(calls,[1900,2400,2420]);
  e.frame(2427);assert.equal(calls.length,3);
  e.frame(2440);assert.equal(calls.length,4);
});
test('map preparation stays active and then returns to idle', () => {
  const e=environment();const calls=[];
  function update(time) { calls.push(time);e.context.requestAnimationFrame(update); }
  e.context.requestAnimationFrame(update);e.loading(true);
  e.frame(5000);e.frame(5020);e.frame(5040);assert.equal(calls.length,3);
  e.loading(false);e.frame(5060);assert.equal(calls.length,4);
  e.frame(7050);e.frame(7070);assert.equal(calls.length,5);
});
test('game pause retains the same callbacks and resumes without reconstruction', () => {
  const e=environment();const calls=[];
  const id=e.context.requestAnimationFrame(time=>calls.push(time));
  e.pause(true);e.frame(100);e.frame(1000);e.frame(8000);
  assert.deepEqual(calls,[]);assert.equal(e.native.size,1);
  e.pause(false);e.event('click');e.frame(8020);
  assert.deepEqual(calls,[8020]);assert.equal(e.native.size,0);
  assert.ok(id<0);
});
test('cancelled work is never revived after game pause', () => {
  const e=environment();let calls=0;
  const id=e.context.requestAnimationFrame(()=>calls++);
  e.pause(true);e.frame(100);e.context.cancelAnimationFrame(id);
  e.pause(false);e.event('click');e.frame(200);
  assert.equal(calls,0);assert.equal(e.native.size,0);
});
