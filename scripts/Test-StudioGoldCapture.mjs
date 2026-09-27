import test from 'node:test';
import assert from 'node:assert/strict';
import { captureDimensions } from './studio-gold-capture.mjs';
function header(w,h) {
  const b = Buffer.alloc(24);
  Buffer.from([137,80,78,71,13,10,26,10]).copy(b);
  b.write('IHDR',12); b.writeUInt32BE(w,16); b.writeUInt32BE(h,20);
  return b;
}
test('exact native 4K bitmap is complete',()=>assert.equal(captureDimensions(header(3840,2160),[3840,2160],1).complete,true));
test('physical bitmap respects fractional logical rounding',()=>assert.equal(captureDimensions(header(2560,1369),[1463,782],1.75).complete,true));
test('monitor crop is not a complete 4K capture',()=>assert.equal(captureDimensions(header(2560,1440),[3840,2160],1).complete,false));
test('wrong scale and oversized frames fail closed',()=>{
  assert.equal(captureDimensions(header(1920,1080),[1920,1080],2).complete,false);
  assert.equal(captureDimensions(header(3840,2160),[1920,1080],1).complete,false);
});
test('missing or invalid geometry does not certify a capture',()=>{
  for(const [size,dpr] of [[null,1],[[0,1080],1],[[1920,1080],NaN],[[1920,1080],0]])
    assert.equal(captureDimensions(header(1920,1080),size,dpr).complete,false);
});
test('malformed capture is rejected',()=>assert.throws(()=>captureDimensions(Buffer.alloc(24),[1920,1080],1)));
