// Development-only source extraction; all shader bodies are evaluated upstream.
export async function exportVegetationMaterials({ moduleAt, json, write, binary, GPU, vegetation }) {
  await moduleAt('src/engine/render/Frame.js');
  const { collectModules } = await moduleAt('src/engine/gpu/Shader.js');
  const { getDetailTexture } = await moduleAt('src/world/terrain/DetailTextures.js');
  const { SunShadows } = await moduleAt('src/engine/render/Shadows.js');
  // The upstream bake template declares shadow bindings even when unlit.
  // Initialize upstream resources without replacing its bake or material code.
  new SunShadows();
  GPU.device.pushErrorScope('validation');
  const atlas = vegetation.leafAtlas;
  atlas.bake();
  vegetation.atlas.bake();
  const materials = vegetation.materials;
  const modules = collectModules(materials.flatMap(material => material.modules));
  const uniformDescription = block => ({
    struct: block.structName, bytes: block.byteLength, wgsl: block.wgsl,
    fields: Object.fromEntries(block.order.map(name => [name, {
      type: block.layout[name].typeStr, offset: block.layout[name].offset,
      value: block.fields[name].value,
    }])),
  });
  const moduleDescriptions = [];
  for (const module of modules) {
    const path = `materials/modules/${module.name}.wgsl`;
    write(path, Buffer.from(module.code));
    moduleDescriptions.push({ name: module.name, code: path, dependencies: module.deps.map(dep => dep.name),
      bindings: Object.fromEntries(Object.entries(module.bindings).map(([name, binding]) => [name,
        binding.uniform ? { uniform: uniformDescription(binding.uniform) } : { kind: 'texture', source_binding: name },
      ])),
    });
  }
  json('materials/vegetation.json', { schema:'forge.tidewater.source-materials', version:1,
    status:'upstream_snippets_require_forge_adapter', modules:moduleDescriptions,
    materials:materials.map(material => ({ name:material.name, side:material.side,
      attributes:material.attributes, varyings:material.varyings, vertex:material.vertex,
      surface:material.surface, shadow:material.shadow, defines:material.defines,
      modules:material.modules.map(module => module.name), uniforms:uniformDescription(material.uniformBlock),
    })),
  });
  const detail = getDetailTexture();
  binary('materials/terrain-detail.rgba8.bin', detail.image.data);
  // Preserve the GPU-generated coverage/brightness/cell atlas as data, never
  // substitute a decorative leaf picture. Every source mip is copied intact.
  async function copyMips(texture, prefix) {
  const buffers = [];
  for (let level = 0; level < texture.mipLevelCount; level++) {
    const width = Math.max(1, texture.width >> level), height = Math.max(1, texture.height >> level);
    const bytesPerRow = Math.ceil(width * 4 / 256) * 256;
    const buffer = GPU.device.createBuffer({size:bytesPerRow * height, usage:GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ});
    GPU.getEncoder().copyTextureToBuffer({texture:texture.getGPU(),mipLevel:level},
      {buffer,bytesPerRow,rowsPerImage:height}, {width,height,depthOrArrayLayers:1});
    buffers.push({buffer,width,height,bytesPerRow,level});
  }
  GPU.submit();
  const mips = [];
  for (const {buffer,width,height,bytesPerRow,level} of buffers) {
    await buffer.mapAsync(GPUMapMode.READ);
    const padded = new Uint8Array(buffer.getMappedRange());
    const data = new Uint8Array(width * height * 4);
    for (let row=0;row<height;row++) data.set(padded.subarray(row*bytesPerRow,row*bytesPerRow+width*4),row*width*4);
    mips.push({width,height,level,...binary(`materials/${prefix}-mip-${level}.rgba8.bin`,data)});
    buffer.unmap(); buffer.destroy();
  }
  return {format:texture.format,sampler:texture.sampler,mips};
  }
  const leaf = await copyMips(atlas.texture, 'leaf-atlas');
  const impostorA = await copyMips(vegetation.atlas.rtA.texture, 'impostor-a');
  const impostorB = await copyMips(vegetation.atlas.rtB.texture, 'impostor-b');
  const error = await GPU.device.popErrorScope();
  if (error) throw new Error(`Upstream vegetation bake validation: ${error.message}`);
  json('materials/textures.json', {
    terrain_detail:{width:detail.width,height:detail.height,format:detail.format,
      channels:['rock','soil','sand','fbm'],path:'materials/terrain-detail.rgba8.bin',mips:'runtime_generation_required'},
    leaf_atlas:{...leaf,channels:['coverage','brightness_div_1_4','cell','one'],
      orientation:'source_texture_top_row_first'},
    impostor_a:{...impostorA,channels:['brightness','leaf_flag','card_random','coverage']},
    impostor_b:{...impostorB,channels:['normal_x','normal_y','normal_z','exposure']},
    impostor_groups:vegetation.atlas.groups.map(g=>({center:g.center,radius:g.radius,
      horizontal_radius:g.rh,half_height:g.hv,variant_base:g.variantBase,variants:g.variants.length})),
    gpu_generation:true, cross_adapter_bit_identity:'not_qualified',
  });
  const geometry = (mesh, path) => {
    const attributes = {};
    for (const [name, attr] of Object.entries(mesh.geometry.attributes)) {
      if (name === 'iPos' || name === 'iDat') continue;
      const data = new Float32Array(attr.count * attr.itemSize);
      const backing = attr.isInterleavedBufferAttribute ? attr.data.array : attr.array;
      const stride = attr.isInterleavedBufferAttribute ? attr.data.stride : attr.itemSize;
      const offset = attr.isInterleavedBufferAttribute ? attr.offset : 0;
      for(let i=0;i<attr.count;i++) for(let c=0;c<attr.itemSize;c++) data[i*attr.itemSize+c]=backing[i*stride+offset+c];
      attributes[name]={...binary(`${path}/${name}.bin`,data),count:attr.count,item_size:attr.itemSize};
    }
    return {name:mesh.name,material:mesh.material.name,cast_shadow:mesh.castShadow,
      attributes,indices:binary(`${path}/indices.bin`,new Uint32Array(mesh.geometry.index.array))};
  };
  const types = vegetation.types.map(type => {
    const path=`vegetation/runtime/${type.name}`;
    const streams=Object.fromEntries(['matrices','iPos','iDat','qr2'].map(name=>
      [name,binary(`${path}/${name}.bin`,type.inst[name])]));
    return {name:type.name,count:type.inst.count,streams,query_radius:type.queryRadius,
      near_range:type.nearRange,margin:type.margin,refresh_distance:type.refreshDistance,
      far_exclude_near:!!type.farExcludeNear,sort_near:type.sortNear,sort_far:type.sortFar,
      far_refresh:type.farRefresh,cell_size:type.inst.cellSize,
      levels:type.levels.map((level,index)=>({capacity:level.capacity,lod_range:level.lodRange,
        meshes:level.meshes.map((mesh,i)=>geometry(mesh,`${path}/level-${index}-${i}`))}))};
  });
  json('vegetation/runtime.json',{schema:'forge.tidewater.source-runtime-vegetation',version:1,
    status:'upstream_packed_data_requires_forge_adapter',matrix_order:'column-major',types,
    species_order:{palms:['palms'],bananas:['bananas'],understory:['youngPalms','ferns'],
      monsteras:['monsteras'],broadleaf:['elephantEars','heliconias','strelitzias'],canopy:['trees','shrubs']},
    encoded_fields:'Original Vegetation constructor encodes plant kind in seed, banana H and query radii',
    grass:'density mask exported; dynamic grass patches and shaders not yet exported'});
}
