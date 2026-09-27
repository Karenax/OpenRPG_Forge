// Development extraction of upstream terrain shading, before dynamic shore wiring.
import {readRgbaMips} from './read-rgba-mips.mjs';
export async function exportTerrainMaterial({moduleAt,terrain,json,write,binary,GPU}) {
  const {TerrainGPU}=await moduleAt('src/world/TerrainGPU.js');
  const {Terrain}=await moduleAt('src/world/Terrain.js');
  const {Scene}=await moduleAt('src/engine/index.js');
  const {collectModules}=await moduleAt('src/engine/gpu/Shader.js');
  const {bakeTerrainMaps,buildShadowHeights}=await moduleAt('src/world/terrain/TerrainBake.js');
  const {Texture}=await moduleAt('src/engine/gpu/Texture.js');
  const {generateMipmaps}=await moduleAt('src/engine/gpu/Mipmaps.js');
  const {getDetailTexture}=await moduleAt('src/world/terrain/DetailTextures.js');
  GPU.device.pushErrorScope('validation');
  const gpu=new TerrainGPU(terrain);
  const view=new Terrain({scene:new Scene(),terrainData:terrain,terrainGPU:gpu});
  const describe=block=>({struct:block.structName,bytes:block.byteLength,wgsl:block.wgsl,
    fields:Object.fromEntries(block.order.map(name=>[name,{type:block.layout[name].typeStr,
      offset:block.layout[name].offset,value:block.fields[name].value}]))});
  const modules=collectModules(view.material.modules).map(module=>{
    const path=`materials/terrain/modules/${module.name}.wgsl`;
    write(path,Buffer.from(module.code));
    return {name:module.name,code:path,dependencies:module.deps.map(d=>d.name),
      bindings:Object.fromEntries(Object.entries(module.bindings).map(([name,binding])=>[name,
        binding.uniform?{uniform:describe(binding.uniform)}:{kind:'texture',source_binding:name}]))};
  });
  const mat=view.material;
  json('materials/terrain/material.json',{schema:'forge.tidewater.source-terrain-material',version:1,
    status:'original_shading_before_shore_wiring_not_forge_adapter',modules,
    material:{name:mat.name,attributes:mat.attributes,varyings:mat.varyings,vertex:mat.vertex,
      surface:mat.surface,defines:mat.defines,uniforms:describe(mat.uniformBlock)},
    missing:['shore_wetness_module','animated_sun_shadow_texture','camera_selected_cdlod_nodes']});
  // Re-evaluate the same original CPU bake. Its GPU wrappers do not retain CPU
  // data or permit copySrc; do not patch their texture usage or generator code.
  const maps=bakeTerrainMaps(terrain);
  const shadow=buildShadowHeights(terrain,4);
  const chains={};
  for(const [name,data,width] of [['normal',maps.normal,terrain.res],['splat',maps.splat,terrain.res],
    ['detail',getDetailTexture().image.data,512]]) {
    // A readback-capable wrapper over identical CPU bytes, using the untouched
    // upstream mip generator. Original source textures remain unchanged.
    const texture=new Texture({width,height:width,format:'rgba8unorm',mips:true,
      usage:['sample','copyDst','copySrc'],data});
    texture.getGPU();generateMipmaps(texture);
    chains[name]=await readRgbaMips({texture,GPU,binary,prefix:`materials/terrain/${name}-mip`});
    texture.destroy();
  }
  json('materials/terrain/textures.json',{schema:'forge.tidewater.source-terrain-textures',version:1,
    normal:{width:terrain.res,height:terrain.res,format:'rgba8unorm',
      channels:['encoded_normal_x','encoded_normal_z','rock','ambient_occlusion'],
      ...binary('materials/terrain/normal.rgba8.bin',maps.normal)},
    splat:{width:terrain.res,height:terrain.res,format:'rgba8unorm',
      channels:['sand','paths','gullies_or_seagrass','rubble_or_scarp'],
      ...binary('materials/terrain/splat.rgba8.bin',maps.splat)},
    shadow_heights:{format:'r16float',texel_m:shadow.texel,max_height:shadow.maxHeight,
      levels:shadow.levels.map((l,i)=>({width:l.width,height:l.height,
        ...binary(`materials/terrain/shadow-height-${i}.r16.bin`,l.data)}))},
    mip_chains:chains,mip_status:'original_gpu_mip_generator_same_cpu_inputs_readback_wrappers'});
  GPU.submit(); await GPU.queue.onSubmittedWorkDone();
  const error=await GPU.device.popErrorScope();
  if(error) throw new Error(`Source terrain resources: ${error.message}`);
}
