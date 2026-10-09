// Execute the Rust-exported depth shader; compare every pixel to the source Bayer rule.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
const [sourceArg, shaderArg, outputArg] = process.argv.slice(2);
if (!outputArg) throw Error('Usage: CLEAN_SOURCE RUST_SHADER NEW_RECEIPT');
const source=path.resolve(sourceArg), output=path.resolve(outputArg);
const git=(...args)=>execFileSync('git',['-c',`safe.directory=${source.replaceAll('\\','/')}`,'-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!=='1438b1abfcaee3267092b75573014f4d9b4a983c'||git('status','--porcelain','--untracked-files=all')) throw Error('Clean pinned source required');
if(fs.existsSync(output))throw Error('Fresh receipt required');
const relative=path.relative(source,output);
if(!relative||(!relative.startsWith('..')&&!path.isAbsolute(relative)))throw Error('Receipt must be outside source');
const bytes=fs.readFileSync(shaderArg), code=bytes.toString('utf8');
if(code.includes('#')||!code.includes('fn sourceShadow('))throw Error('Rust depth shader required');
await import(pathToFileURL(path.join(source,'test/headless.mjs')).href);
const {GPU}=await import(pathToFileURL(path.join(source,'src/engine/gpu/GPU.js')).href);
await GPU.init({headless:true});
const device=GPU.device;
try {
  device.pushErrorScope('validation');
  const module=device.createShaderModule({code});
  const info=await module.getCompilationInfo();
  if(info.messages.some(m=>m.type==='error'))throw Error(JSON.stringify(info.messages));
  const pipeline=await device.createRenderPipelineAsync({layout:'auto',vertex:{module,entryPoint:'vertex',buffers:[{arrayStride:12,attributes:[{shaderLocation:0,offset:0,format:'float32x3'}]}]},
    fragment:{module,entryPoint:'fragment',targets:[]},primitive:{topology:'triangle-list',cullMode:'back'},depthStencil:{format:'depth32float',depthWriteEnabled:true,depthCompare:'less-equal'}});
  const vertices=new Float32Array([-1,-1,.5,1,-1,.5,-1,1,.5,-1,1,.5,1,-1,.5,1,1,.5]);
  const vertex=device.createBuffer({size:vertices.byteLength,usage:GPUBufferUsage.VERTEX|GPUBufferUsage.COPY_DST});
  device.queue.writeBuffer(vertex,0,vertices);
  const uniform=device.createBuffer({size:160,usage:GPUBufferUsage.UNIFORM|GPUBufferUsage.COPY_DST});
  const bind=device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[{binding:0,resource:{buffer:uniform}}]});
  const texture=device.createTexture({size:[4,4],format:'depth32float',usage:GPUTextureUsage.RENDER_ATTACHMENT|GPUTextureUsage.COPY_SRC});
  const read=device.createBuffer({size:1024,usage:GPUBufferUsage.COPY_DST|GPUBufferUsage.MAP_READ});
  let cases=0,pixels=0;
  for(let phase=0;phase<16;phase++)for(const fade of [0,.25,.5,.75,1])for(const outgoing of [false,true]) {
    const raw=new ArrayBuffer(160), floats=new Float32Array(raw), integers=new Uint32Array(raw);
    for(const start of [0,16])for(const i of [0,5,10,15])floats[start+i]=1;
    floats[34]=fade;floats[35]=outgoing?1:0;integers[36]=phase;
    device.queue.writeBuffer(uniform,0,raw);
    const encoder=device.createCommandEncoder();
    const pass=encoder.beginRenderPass({colorAttachments:[],depthStencilAttachment:{view:texture.createView(),depthClearValue:1,depthLoadOp:'clear',depthStoreOp:'store'}});
    pass.setPipeline(pipeline);pass.setBindGroup(0,bind);pass.setVertexBuffer(0,vertex);pass.draw(6);pass.end();
    encoder.copyTextureToBuffer({texture,aspect:'depth-only'},{buffer:read,bytesPerRow:256,rowsPerImage:4},[4,4,1]);
    device.queue.submit([encoder.finish()]);
    await read.mapAsync(GPUMapMode.READ);
    const depth=new Float32Array(read.getMappedRange());
    for(let y=0;y<4;y++)for(let x=0;x<4;x++) {
      const px=x+phase*3, py=y+(phase>>2),x0=px&1,x1=(px>>1)&1,y0=py&1,y1=(py>>1)&1;
      const threshold=(((x0^y0)<<3)|(y0<<2)|((x1^y1)<<1)|y1)+.5;
      const visible=outgoing?threshold/16>=fade:threshold/16<fade;
      if(depth[y*64+x] !== (visible?.5:1))throw Error(`Depth mismatch phase=${phase} fade=${fade} outgoing=${outgoing} x=${x} y=${y} actual=${depth[y*64+x]}`);
      pixels++;
    }
    read.unmap();cases++;
  }
  const error=await device.popErrorScope();if(error)throw Error(error.message);
  if(git('status','--porcelain','--untracked-files=all'))throw Error('Source changed');
  fs.writeFileSync(output,JSON.stringify({schema:'forge.tidewater.scanned-depth-gpu-check',version:1,source_commit:git('rev-parse','HEAD'),shader_sha256:crypto.createHash('sha256').update(bytes).digest('hex'),pipeline_created:true,cases,pixels,verdict:'PASS',limits:['synthetic quad only','no source mesh or common root draw','no shared cascade graph submission','no image fidelity claim']},null,2)+'\n',{flag:'wx'});
  console.log(`PASS: ${cases} depth cases, ${pixels} pixels; ${output}`);
} finally {device.destroy();}
