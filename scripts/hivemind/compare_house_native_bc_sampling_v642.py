"""Compare unchanged WGSL using exact retained BC resources instead of float upload."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_implicit_surface_gpu_v622.py')
code=recipe.read_text('utf-8-sig').replace("O=W/'house-implicit-surface-gpu-comparison-v622'","O=W/'house-native-bc-sampling-comparison-v642'").replace("N=W/'house-implicit-surface-reference-v621'","N=W/'house-hdrp-bias-surface-reference-v623'").replace('openrpg-house-implicit-gpu-v622','openrpg-house-native-bc-sampling-v642')
needle="chains={r['texture_guid']:r for r in read(mp)}"
replacement="""BC=W/'house-compressed-storage-v641';bc_run=read(BC/'RUN-RESULT.v1.json');assert bc_run['status']=='house_compressed_storage_captured_review_pending' and not bc_run['private_importer_changes'];bc=read(BC/'NATIVE-RESULT.v1.json');assert len(bc['textures'])==18
chains={r['guid']:dict(texture_guid=r['guid'],native_graphics_format=r['graphics_format'],levels=r['levels']) for r in bc['textures']}"""
assert code.count(needle)==1;code=code.replace(needle,replacement)
needle='O.mkdir();(O/\'src\').mkdir();'
addition="""for p,h in read(BC/'PREPARED.v1.json')['input_pins'].items():assert sha(p)==h,p;pins[p]=h
for r in bc['textures']:
    assert r['native_decoded_mips_equal'] and r['restored_decoded_mips_equal'] and r['importer_restored_equal'] and r['meta_bytes_restored_equal']
    assert sha(r['whole_path'])==r['whole_sha256'];pins[r['whole_path']]=r['whole_sha256']
    for l in r['levels']:assert Path(l['path']).stat().st_size==l['bytes'];pins[l['path']]=sha(l['path'])
for p in [BC/'PREPARED.v1.json',BC/'NATIVE-RESULT.v1.json',BC/'RUN-RESULT.v1.json']:pins[str(p)]=sha(p)
"""+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
needle="(O/'src/main.rs').write_bytes((S/'candidates/house_implicit_surface_gpu_v622.rs').read_bytes())"
replacement="""renderer=(S/'candidates/house_implicit_surface_gpu_v622.rs').read_text('utf-8-sig')
renderer=renderer.replace('required_features:wgpu::Features::FLOAT32_FILTERABLE','required_features:wgpu::Features::FLOAT32_FILTERABLE|wgpu::Features::TEXTURE_COMPRESSION_BC')
a='let texture=device.create_texture(&wgpu::TextureDescriptor{label:None,size:wgpu::Extent3d{width,height,depth_or_array_layers:1},mip_level_count:levels.len() as u32'
assert renderer.count(a)==1
renderer=renderer.replace(a,'let format=match t["native_graphics_format"].as_str().unwrap(){"RGBA_DXT1_SRGB"=>wgpu::TextureFormat::Bc1RgbaUnormSrgb,"RGBA_DXT5_SRGB"=>wgpu::TextureFormat::Bc3RgbaUnormSrgb,"RGBA_DXT5_UNorm"=>wgpu::TextureFormat::Bc3RgbaUnorm,_=>panic!("Unsupported original BC format")};let block_bytes=if format==wgpu::TextureFormat::Bc1RgbaUnormSrgb{8}else{16};'+a)
a='format:wgpu::TextureFormat::Rgba32Float,usage:wgpu::TextureUsages::TEXTURE_BINDING';assert renderer.count(a)==1;renderer=renderer.replace(a,'format,usage:wgpu::TextureUsages::TEXTURE_BINDING')
a='assert_eq!(bytes.len(),(w*h*16) as usize)';assert renderer.count(a)==1;renderer=renderer.replace(a,'assert_eq!(bytes.len(),(w.div_ceil(4)*h.div_ceil(4)*block_bytes) as usize)')
a='bytes_per_row:Some(w*16),rows_per_image:Some(h)';assert renderer.count(a)==1;renderer=renderer.replace(a,'bytes_per_row:Some(w.div_ceil(4)*block_bytes),rows_per_image:Some(h.div_ceil(4))')
a='wgpu::Extent3d{width:w,height:h,depth_or_array_layers:1}';assert renderer.count(a)==1;renderer=renderer.replace(a,'wgpu::Extent3d{width:w.div_ceil(4)*4,height:h.div_ceil(4)*4,depth_or_array_layers:1}')
(O/'src/main.rs').write_text(renderer,'utf-8')"""
assert code.count(needle)==1;code=code.replace(needle,replacement)
code=code.replace('for p in [Path(__file__),tp,mp,','for p in [Path(__file__),recipe,tp,mp,')
code=code.replace('Actual generated surface rasterization vs WGSL on controlled planes with native mip chains/implicit derivatives; no lighting, prefab mesh or depth-host qualification','Actual generated surface rasterization vs unchanged WGSL with original native BC mip blocks and implicit derivatives; controlled planes, zero global bias; no depth/prefab/Studio qualification')
exec(compile(code,str(recipe)+'#native-BC-resources','exec'),globals())
