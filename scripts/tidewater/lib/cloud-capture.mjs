// Acquisition only: original SkyProClouds resources on the source renderer's device.
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
import {gunzipSync} from 'node:zlib';

export class CloudCapture {
  constructor(clouds,frame,source) {
    this.clouds=clouds;
    this.frameUniforms=frame;
    this.payloads={};
    this.frames=[];
    this.snapshots=[];
    this.kernels={};
    this.textures={weather:clouds.weatherMap,bounds:clouds.weatherBounds,
      radiance:clouds.source,metadata:clouds.sourceMeta,panorama:clouds.panorama,
      shadow:clouds.shadowMap,color0:clouds.colors[0],color1:clouds.colors[1],
      meta0:clouds.metas[0],meta1:clouds.metas[1],
      transmittance:clouds.atmosphere.transmittanceLUT,
      skyView:clouds.atmosphere.skyViewLUT};
    for(const texture of Object.values(this.textures)) {
      if(texture.gpu) throw Error(`Cloud texture already allocated: ${texture.label}`);
      texture.usageList=[...texture.usageList,'copySrc'];
    }
    for(const name of ['weather','bounds','trace','temporal','pano','shadow']) {
      const kernel=clouds[name+'Kernel'];
      this.payloads[`cloud-${name}.wgsl`]=Buffer.from(kernel.source);
      const describe=bindings=>bindings.names.map((name,i)=>({name,binding:i,
        layout:bindings.described[i].layout,decl:bindings.described[i].decl}));
      this.kernels[name]={workgroup:kernel.workgroupSize,bindings:describe(kernel.bindings)};
    }
    for(const [name,block] of [['frame',clouds.frameBlock],['settings',clouds.settingsBlock],
      ['public',clouds.params],['global',frame],['atmosphere',clouds.atmosphere.params]]) {
      this.payloads[`cloud-${name}-params.wgsl`]=Buffer.from(block.wgsl);
    }
    const modules=new Map();
    const visit=module=>{
      if(modules.has(module.name)) return;
      for(const dependency of module.deps) visit(dependency);
      modules.set(module.name,module);
    };
    for(const module of [clouds.module,clouds.marchModule]) visit(module);
    this.modules={};
    for(const [name,module] of modules) {
      const file=`cloud-module-${name}.wgsl`;
      this.payloads[file]=Buffer.from(module.code);
      this.modules[name]={file,dependencies:module.deps.map(dep=>dep.name),bindings:Object.keys(module.bindings)};
    }
    for(const name of ['LICENSE','CREDITS.md','public/clouds/LICENSING.md'])
      this.payloads['cloud-'+name.replaceAll('/','-')]=readFileSync(join(source,name));
    const compressed=readFileSync(join(source,'public/clouds/baseShape64.bin'));
    this.payloads['cloud-baseShape64.bin']=compressed;
    this.payloads['cloud-blueNoise.bin']=readFileSync(join(source,'public/clouds/blueNoise.bin'));
    const volume=gunzipSync(compressed);
    if(volume.readUInt32LE(0)!==0x315a5a4e) throw Error('Unexpected cloud volume header');
    const dimensions=[6,8,10].map(offset=>volume.readUInt16LE(offset));
    let offset=16;
    this.noise={dimensions,format:'rgba8unorm',mips:[]};
    for(let level=0;level<volume[12];level++) {
      const size=dimensions.map(value=>Math.max(1,value>>level));
      const length=size.reduce((a,b)=>a*b,4),file=`cloud-volume-m${level}.rgba8.bin`;
      if(offset+length>volume.length) throw Error('Truncated cloud volume');
      this.payloads[file]=Buffer.from(volume.subarray(offset,offset+length));
      this.noise.mips.push({level,size,file});offset+=length;
    }
    if(offset!==volume.length) throw Error('Unexpected trailing cloud volume data');
  }
  frame(step) {
    const c=this.clouds,record={step,view:c.colors.indexOf(c.viewTex),uniforms:{}};
    for(const [name,block] of [['frame',c.frameBlock],['settings',c.settingsBlock],
      ['public',c.params],['global',this.frameUniforms],['atmosphere',c.atmosphere.params]]) {
      block._pack();
      const file=`cloud-step${String(step).padStart(3,'0')}-${name}.bin`;
      // _last is the mirror of the last queue.writeBuffer, whereas _pack can
      // include CPU edits skipped by a repeated binding token in this frame.
      this.payloads[file]=Buffer.from(new Uint8Array(block._last.buffer,block._last.byteOffset,block._last.byteLength));
      record.uniforms[name]=file;
      if(!this.payloads[file].equals(Buffer.from(new Uint8Array(block.data))))
        throw Error(`Cloud CPU/GPU uniform divergence at step ${step}: ${name}`);
    }
    this.frames.push(record);
  }
  async snapshot(step,readTexture) {
    const textures={};
    for(const [name,texture] of Object.entries(this.textures)) {
      const file=`cloud-step${String(step).padStart(3,'0')}-${name}.bin`;
      this.payloads[file]=Buffer.from((await readTexture(texture)).data);
      textures[name]={file,width:texture.width,height:texture.height,format:texture.format};
    }
    this.snapshots.push({step,textures});
  }
  save(save) {
    for(const [name,data] of Object.entries(this.payloads)) save(name,data);
    save('clouds.json',JSON.stringify({schema:'forge.tidewater.cloud-acquisition',version:1,
      implementation:'SkyProClouds',frames:this.frames,snapshots:this.snapshots,
      kernels:this.kernels,noise:this.noise,modules:this.modules,
      limitations:['source_gpu_only','cold_fixed_camera_120_frames','no_native_cloud_parity',
        'effective_uniforms_captured_after_update_before_submit']},null,2));
  }
}
