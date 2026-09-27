// Extract intact source village render data; does not claim editable object separation.
import {readRgbaMips} from './read-rgba-mips.mjs';

export async function exportVillage({moduleAt,json,write,binary,GPU,village,hash}) {
  const {collectModules}=await moduleAt('src/engine/gpu/Shader.js');
  const {Texture}=await moduleAt('src/engine/gpu/Texture.js');
  GPU.device.pushErrorScope('validation');
  village.textures.bake();
  const textureNames=new Map(Object.entries(village.textures.textures).map(([n,t])=>[t,n]));
  const textures={};
  // Original textures lack COPY_SRC. A textureLoad/store identity pass makes a
  // readable wrapper; no generator, source usage flags, mip or sampler is changed.
  const shader=GPU.device.createShaderModule({code:`
@group(0) @binding(0) var source:texture_2d<f32>;
@group(0) @binding(1) var copied:texture_storage_2d<rgba8unorm,write>;
@compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
  if(any(id.xy>=textureDimensions(copied))){return;}
  textureStore(copied,vec2i(id.xy),textureLoad(source,vec2i(id.xy),0));
}`});
  const pipeline=GPU.device.createComputePipeline({layout:'auto',compute:{module:shader,entryPoint:'main'}});
  for(const [name,texture] of Object.entries(village.textures.textures)) {
    if(texture.format!=='rgba8unorm') throw new Error('Unexpected village texture');
    const staging=new Texture({width:texture.width,height:texture.height,format:'rgba8unorm',mips:true,
      usage:['sample','storage','copySrc']});
    for(let level=0;level<texture.mipLevelCount;level++) {
      const view={baseMipLevel:level,mipLevelCount:1};
      const group=GPU.device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[
        {binding:0,resource:texture.getGPU().createView(view)},
        {binding:1,resource:staging.getGPU().createView(view)}]});
      const pass=GPU.getEncoder().beginComputePass();pass.setPipeline(pipeline);pass.setBindGroup(0,group);
      pass.dispatchWorkgroups(Math.ceil(Math.max(1,texture.width>>level)/8),Math.ceil(Math.max(1,texture.height>>level)/8));pass.end();
    }
    textures[name]={format:texture.format,sampler:texture.sampler,
      mips:await readRgbaMips({texture:staging,GPU,binary,prefix:`village/textures/${name}`})};
    staging.destroy();
  }
  json('village/textures.json',{schema:'forge.tidewater.village-textures',version:1,textures,
    acquisition:'original_bake_and_mips_via_textureLoad_store_identity',cross_adapter_bit_identity:'not_qualified'});
  const uniform=block=>({struct:block.structName,bytes:block.byteLength,wgsl:block.wgsl,
    fields:Object.fromEntries(block.order.map(n=>[n,{type:block.layout[n].typeStr,offset:block.layout[n].offset,value:block.fields[n].value}]))});
  const materials=[],materialIds=new Map(),modules=new Map();
  function materialId(material){
    if(materialIds.has(material)) return materialIds.get(material);
    const id=`material-${materials.length}`;materialIds.set(material,id);
    for(const m of collectModules(material.modules)){
      const key=hash(Buffer.from(m.code));
      if(!modules.has(key)){
        const path=`village/modules/${key}.wgsl`;write(path,Buffer.from(m.code));
        modules.set(key,{name:m.name,code:path,dependencies:m.deps.map(d=>d.name),
          bindings:Object.fromEntries(Object.entries(m.bindings).map(([n,b])=>[n,b.uniform?
            {uniform:uniform(b.uniform)}:{texture:textureNames.get(b.texture)??null}]))});
      }
    }
    materials.push({id,name:material.name,side:material.side,transparent:material.transparent,
      depthWrite:material.depthWrite,alphaTest:material.alphaTest,attributes:material.attributes,
      bindings:Object.fromEntries(Object.entries(material.bindings).map(([n,b])=>{
        const texture=textureNames.get(b.texture);
        if(!texture) throw new Error(`Unresolved village binding ${n}`);
        return [n,{texture}];
      })),
      varyings:material.varyings,vertex:material.vertex,surface:material.surface,shadow:material.shadow,
      defines:material.defines,uniforms:uniform(material.uniformBlock),
      modules:collectModules(material.modules).map(m=>hash(Buffer.from(m.code)))});
    return id;
  }
  const payloads=new Map(),geometries=new Map(),nodes=[],omitted=[];
  function payload(array){
    const data=Buffer.from(array.buffer,array.byteOffset,array.byteLength),id=hash(data);
    if(!payloads.has(id)) payloads.set(id,binary(`village/geometry/${id}.bin`,array));
    return payloads.get(id);
  }
  village.group.updateMatrixWorld(true);
  function visit(node,path){
    if(node.name==='village_fish'){
      omitted.push({path,name:node.name,reason:'GPU_batched_fish_props_require_storage_and_LOD_export'});return;
    }
    if(node.geometry){
      const geo=node.geometry,attributes={};
      for(const [name,a] of Object.entries(geo.attributes)){
        const src=a.isInterleavedBufferAttribute?a.data.array:a.array;
        const stride=a.isInterleavedBufferAttribute?a.data.stride:a.itemSize;
        const offset=a.isInterleavedBufferAttribute?a.offset:0;
        const data=new Float32Array(a.count*a.itemSize);
        for(let i=0;i<a.count;i++) for(let c=0;c<a.itemSize;c++) data[i*a.itemSize+c]=src[i*stride+offset+c];
        attributes[name]={...payload(data),item_size:a.itemSize,count:a.count};
      }
      const start=geo.drawRange?.start??0,count=Math.min(geo.drawRange?.count??Infinity,geo.index.count-start);
      if(!Number.isInteger(start)||!Number.isInteger(count)||count%3) throw new Error('Invalid village draw range');
      const descriptor={attributes,indices:payload(new Uint32Array(geo.index.array.slice(start,start+count))),
        source_draw_range:{start,count},triangles:count/3};
      const id=hash(Buffer.from(JSON.stringify(descriptor)));geometries.set(id,descriptor);
      nodes.push({source_id:`tidewater:1438b1ab:village:${path}`,path,name:node.name,
        geometry:id,material:materialId(node.material),matrix_world:Array.from(node.matrixWorld.elements),
        visible:node.visible,cast_shadow:node.castShadow,receive_shadow:node.receiveShadow});
    }
    node.children.forEach((child,i)=>visit(child,`${path}/${i}`));
  }
  visit(village.group,'0');
  json('village/render.json',{schema:'forge.tidewater.village-render',version:1,nodes,
    geometries:Object.fromEntries(geometries),materials,modules:Object.fromEntries(modules),omitted,
    limitations:['merged_batches_not_individual_placeable_assets','dynamic_transforms_initial_pose','source_lighting_not_exported']});
  await GPU.queue.onSubmittedWorkDone();
  const error=await GPU.device.popErrorScope();if(error) throw new Error(error.message);
}
