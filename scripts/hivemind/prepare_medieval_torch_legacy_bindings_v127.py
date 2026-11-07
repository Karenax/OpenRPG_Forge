"""Prepare private material variants; admission remains closed pending native GPU parity."""
from pathlib import Path
from datetime import datetime,timezone
import copy,hashlib,json,shutil,struct
W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-legacy-bindings-v127'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def linear(v):
 assert 0<=v<=1
 return v/12.92 if v<=0.04045 else ((v+0.055)/1.055)**2.4
assert not O.exists()
cp=W/'medieval-torch-particle-material-closure-v124/RESULT.v1.json';proof=read(cp);ip=Path(next(iter(proof['output_pins'])));assert sha(ip)==proof['output_pins'][str(ip)];current=read(ip)
oldp=W/'delta-firepit-legacy-material-bindings-v1/RESULT.v1.json';old=read(oldp);identity=W/'medieval-torch-shader-identity-and-material-variants-v126/RESULT.v1.json';assert read(identity)['shader']['native_name']=='Legacy Shaders/Particles/Alpha Blended'
pins={str(p):sha(p) for p in [cp,ip,oldp,identity,Path(__file__).resolve()]};oldrows={x['material_guid']:x for x in old['material_bindings']};O.mkdir();output={};rows=[]
library=oldp.parent/'legacy_particle_alpha_blended.wgsl';assert sha(library)==old['output_pins'][str(library)];shutil.copyfile(library,O/library.name);pins[str(library)]=sha(library);output[str(O/library.name)]=sha(O/library.name)
licenses=[]
for p in oldp.parent.glob('LICENSE.Unity.*.txt'):
 assert sha(p)==old['output_pins'][str(p)];q=O/p.name;shutil.copyfile(p,q);pins[str(p)]=sha(p);output[str(q)]=sha(q);licenses.append(str(q))
assert licenses
manifest=W/'hivemind-run/medieval-kingdom/source.v1.json';m=read(manifest);pins[str(manifest)]=sha(manifest)
for case in current['materials']:
 e=case['material'];guid=e['guid'];base_path=Path(oldrows[guid]['binding']);assert sha(base_path)==old['output_pins'][str(base_path)];base=read(base_path);pins[str(base_path)]=sha(base_path)
 material=next(d['data'] for d in case['complete_documents'] if d['type']=='Material');saved=material['m_SavedProperties'];colors={k:v for item in saved['m_Colors'] for k,v in item.items()};texs={k:v for item in saved['m_TexEnvs'] for k,v in item.items()};floats={k:v for item in saved['m_Floats'] for k,v in item.items()};color={k:f32(v) for k,v in colors['_TintColor'].items()};texture=texs['_MainTex'];st=[f32(texture['m_Scale'][k]) for k in 'xy']+[f32(texture['m_Offset'][k]) for k in 'xy'];inv=f32(floats['_InvFade'])
 assert st==[base['source_maintex_scale_offset'][k] for k in 'xyzw'] and inv==base['source_inv_fade']
 ref=texture['m_Texture'];dep=next(x for x in case['direct_references'] if x['field'].endswith('._MainTex.m_Texture'));assert ref['guid']==dep['guid'] and ref['fileID']==dep['file_id']=='2800000';assert sha(dep['path'])==dep['sha256'];pins[dep['path']]=dep['sha256'];meta=Path(dep['path']+'.meta');pins[str(meta)]=sha(meta)
 values=[linear(color[k]) for k in 'rgb']+[color['a']]+st+[inv,0,0,0];raw=struct.pack('<12f',*values);decoded=struct.unpack('<12f',raw)
 original_blob=Path(oldrows[guid]['uniform_blob']);assert sha(original_blob)==old['output_pins'][str(original_blob)];pins[str(original_blob)]=sha(original_blob)
 smoke=guid=='251f329e9f65944409f96791dedb09b0'
 if smoke:assert raw==original_blob.read_bytes(),'Identical smoke variant must reproduce prior float32 uniform'
 else:assert raw!=original_blob.read_bytes(),'Distinct fire tint must not reuse old uniform'
 folder=O/guid;folder.mkdir();blob=folder/'MATERIAL-UNIFORMS.bin';blob.write_bytes(raw)
 binding=dict(schema_version=1,pack_id='hivemind.medieval-kingdom',material_guid=guid,shader_family=base['shader_family'],original_shader_guid=material['m_Shader']['guid'],original_shader_file_id=material['m_Shader']['fileID'],original_material_source=e['path'],original_material_sha256=e['sha256'],original_material_documents=case['complete_documents'],original_keyword_list=material.get('m_ValidKeywords',material.get('m_ShaderKeywords')),source_tint_rgba_f32=color,candidate_tint_linear_f32=list(decoded[:4]),maintex_scale_offset_f32=list(decoded[4:8]),inv_fade_f32=decoded[8],source_texture=dep,source_texture_meta_sha256=pins[str(meta)],uniform_blob_sha256=sha(blob),uniform_layout=base['uniform_layout'],source_shader_render_state=base['source_shader_render_state'],shader_library_sha256=sha(O/library.name),shader_license_paths=licenses,asset_license=m['license'],archive_sha256=m['archive_sha256'],source_gpu_tint_for_this_variant_verified=smoke,fire_variant_tint_is_candidate_requires_native_gpu_comparison=not smoke,active_texture_payload_matches_old_binding=dep['sha256']==base['source_texture']['source_sha256'],sampler_or_native_storage_reused=False,renderer_admission=False,billboard_runtime_converted=False,studio_verified=False,full_import=False)
 bp=folder/'binding.v1.json';bp.write_text(json.dumps(binding,indent=2,allow_nan=False),'utf-8');assert len(blob.read_bytes())==48 and read(bp)['uniform_blob_sha256']==sha(blob)
 for q in [bp,blob]:output[str(q)]=sha(q)
 rows.append(dict(material_guid=guid,binding=str(bp),uniform_blob=str(blob),same_uniform_as_old_binding=smoke,renderer_admission=False))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='two_pack_specific_legacy_material_uniform_candidates_prepared_and_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L06.MEDIEVAL.TORCH.001',material_bindings=rows,input_pins=pins,output_pins=output,new_authored_3d_asset=False,new_gpu_capture=False,renderer_installed=False,full_import=False);(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(prepared_bindings=len(rows),uniform_bytes_each=48,smoke_uniform_reproduced=True,fire_variant_uniform_separate=True,renderer_admission=False,root=str(O))))
