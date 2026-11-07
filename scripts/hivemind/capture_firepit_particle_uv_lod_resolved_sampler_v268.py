"""White-texture GPU control for the identical acquired mesh streams."""
from pathlib import Path
import json,hashlib,shutil,subprocess,os,datetime,copy
from resolve_unity_sampling_policy_v267 import resolve_unity_sampling_policy
W=Path('F:/OpenRPG_ImportWork');B=W/'firepit-particle-mesh-gpu-v244';N=W/'firepit-particle-native-uv-lod-probe-v259';O=W/'firepit-particle-uv-lod-resolved-sampler-v268';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');NAME='openrpg-firepit-particle-uv-lod-resolved-sampler-v268'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(n,d):(O/n).write_text(json.dumps(d,indent=2),'utf-8')
def main():
 native=read(N/'RUN-RESULT.v1.json');assert native.get('unity_exit_code')==0 and native['native_geometry_verified'],'Native control must finish before GPU'
 assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
 for p in (B/'src').iterdir():shutil.copyfile(p,O/'src'/p.name)
 shader=O/'src/fixture.wgsl';text=shader.read_text('utf-8');a='let texel=textureSample(source_texture,source_sampler,input.uv);';assert text.count(a)==1;text=text.replace(a,'let texel=select(textureSample(source_texture,source_sampler,input.uv),vec4<f32>(1.0),fixture.color.a>0.0);');begin=text.index('@fragment fn fragment(');text=text[:begin]+'''@fragment fn fragment(input:VertexOutput)->@location(0) vec4<f32>{let dims=vec2<f32>(textureDimensions(source_texture,0));let dx=dpdx(input.uv)*dims;let dy=dpdy(input.uv)*dims;let lod=clamp(0.5*log2(max(dot(dx,dx),dot(dy,dy))),0.0,f32(textureNumLevels(source_texture)-1u));let sampled=textureSample(source_texture,source_sampler,input.uv);return vec4<f32>(input.uv,lod,sampled.a*fixture.color.a);}
''';shader.write_text(text,'utf-8')
 source=O/'src/main.rs';text=source.read_text('utf-8');a='"rgba":pixels';assert text.count(a)==1;text=text.replace(a,'"pixel_count":pixels.len()/4');text=text.replace('wgpu::TextureFormat::Rgba16Float','wgpu::TextureFormat::Rgba32Float')
 text=text.replace('blend: Some(wgpu::BlendState {color: blend, alpha: blend})','blend: None').replace('wgpu::ColorWrites::RED | wgpu::ColorWrites::GREEN | wgpu::ColorWrites::BLUE','wgpu::ColorWrites::ALL')
 text='\n'.join(line for line in text.splitlines() if 'flags.contains(wgpu::TextureFormatFeatureFlags::BLENDABLE)' not in line)+'\n'
 text=text.replace('size: 524288,','size: 1048576,').replace('bytes_per_row: Some(2048)','bytes_per_row: Some(4096)').replace('mapped.chunks_exact(2048).flat_map(|row| row[..2048]','mapped.chunks_exact(4096).flat_map(|row| row[..4096]').replace('raw_pixels.len(), 524288','raw_pixels.len(), 1048576')
 text=text.replace('raw_pixels.chunks_exact(2).map(|chunk| half_to_float(u16::from_le_bytes(chunk.try_into().unwrap())))','raw_pixels.chunks_exact(4).map(|chunk| f32::from_le_bytes(chunk.try_into().unwrap()))')
 text=text.replace('.rgba.f16','.rgba.f32.raw').replace('gpu_rgba_f16','gpu_rgba_f32').replace('"render_target_format":"Rgba16Float"','"render_target_format":"Rgba32Float"')
 source.write_text(text,'utf-8')
 sampler_source=O/'src/sampler.rs';sampler_text=sampler_source.read_text('utf-8')
 a='if settings.anisotropy!=1{return Err("Source anisotropy requires separate native qualification");}';assert sampler_text.count(a)==1;sampler_text=sampler_text.replace(a,'if settings.anisotropy==0 || settings.anisotropy>16{return Err("Native resolved anisotropy outside supported range");}')
 a='min_filter,mag_filter,mipmap_filter,lod_min_clamp:';assert sampler_text.count(a)==1;sampler_text=sampler_text.replace(a,'min_filter,mag_filter,mipmap_filter:if settings.anisotropy>1 {ImageFilterMode::Linear} else {mipmap_filter},lod_min_clamp:')
 assert sampler_text.count('anisotropy_clamp:1')==1;sampler_text=sampler_text.replace('anisotropy_clamp:1','anisotropy_clamp:settings.anisotropy');sampler_source.write_text(sampler_text,'utf-8')
 for n in ['Cargo.toml','Cargo.lock']:(O/n).write_text((B/n).read_text('utf-8').replace('openrpg-firepit-particle-mesh-gpu-v244',NAME),'utf-8')
 inp=read(B/'INPUT.v1.json');
 for fixture in inp['fixtures']:fixture['background']=[0,0,0,0]
 pins=inp['input_pins'];pins.update({str(p):sha(p) for p in list((O/'src').iterdir())+[O/'Cargo.toml',O/'Cargo.lock',Path(__file__),N/'RUN-RESULT.v1.json',N/'INDEPENDENT-REOPEN.v1.json']})
 resource_capture=W/'firepit-particle-native-uv-lod-aniso-disabled-v265/GEOMETRY.v1.json';resources={geo['native_texture_resource_json'] for case in read(resource_capture)['cases'] for row in case['rows'] for geo in row['geometry']};assert len(resources)==1;resource=json.loads(next(iter(resources)));assert resource['quality_anisotropy']=='ForceEnable' and resource['aniso']==1 and resource['filter']=='Bilinear'
 policy=resolve_unity_sampling_policy(resource['aniso'],1,resource['quality_anisotropy'],resource['gpu_type']);assert policy['effective_anisotropy']==9 and policy['effective_mip_filter']=='Linear'
 original=copy.deepcopy(inp['texture']['resolved_sampler']);inp['texture']['texture_declared_sampler']=original;inp['texture']['resolved_sampler']['anisotropy']=policy['effective_anisotropy'];inp['texture']['source_global_sampling_policy']=policy
 pins[str(resource_capture)]=sha(resource_capture);policy_helper=Path(__file__).with_name('resolve_unity_sampling_policy_v267.py');pins[str(policy_helper)]=sha(policy_helper);save('SAMPLING-POLICY.v1.json',policy)
 inp.update(input_pins=pins,uv_lod_control=True,actual_source_texture_sampling_qualified=False);save('INPUT.v1.json',inp)
 env=os.environ.copy();env.update(RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'),PATH=str(BIN)+os.pathsep+env['PATH'])
 state=dict(status='building_uv_lod_mesh_control',expected_meshes=336,uv_lod_control=True,full_import=False);save('RUN-RESULT.v1.json',state)
 with (O/'build.log').open('xb') as log:r=subprocess.run([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml')],env=env,stdout=log,stderr=log)
 state['build_exit_code']=r.returncode;save('RUN-RESULT.v1.json',state);assert r.returncode==0
 exe=Path(env['CARGO_TARGET_DIR'])/'debug'/f'{NAME}.exe';state.update(executable_sha256=sha(exe),status='rendering_uv_lod_mesh_control');save('RUN-RESULT.v1.json',state)
 with (O/'gpu.log').open('xb') as log:r=subprocess.run([str(exe),str(O/'INPUT.v1.json'),str(O/'gpu')],env=env,stdout=log,stderr=log)
 state.update(gpu_exit_code=r.returncode,status='uv_lod_mesh_control_terminal',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat());save('RUN-RESULT.v1.json',state);assert r.returncode==0;print(json.dumps(state))
if __name__=='__main__':main()
