from pathlib import Path
import re,struct,json,hashlib
from datetime import datetime,timezone
S=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/scripts/hivemind');W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-ao-tag-deferred-carrier-v77';assert not O.exists()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
proof=W/'townsmith-gbuffer3-ao-tag-control-v76/RESULT.v1.json';pr=json.loads(proof.read_text());assert pr['ao_tag_pixels']==34445 and next(r for r in pr['rows'] if r['target']=='source_graph_floor_uf11')['equal_pixels']==34445
parent=W/'townsmith-material-quad-difference-carrier-v68/SM_Door-Single-B.material-quad-difference.candidate.glb';pp=W/'townsmith-material-quad-difference-comparison-v68/RESULT.v1.json';assert sha(parent)==json.loads(pp.read_text())['input_pins'][str(parent)]
raw=parent.read_bytes();sz=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+sz]);original=raw[28+sz:];data=bytearray(original);c=d['materials'][0]['extensions']['OPENRPG_source_graph'];v=d['bufferViews'][c['forward']['buffer_view']];b=original[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];assert hashlib.sha256(b).hexdigest()==c['forward']['sha256'];t=b.decode()
anchor='out.diffuse_occlusion=surface.occlusion;';assert t.count(anchor)==1;t=t.replace(anchor,'out.diffuse_occlusion=source_private_ao_tag_store(surface.occlusion);')
t+='''
// Private deferred AO tag branch measured in the original source GBuffer3.
// Convert the native graph value to unsigned 11-bit float storage (6 mantissa bits).
// No source image samples are consumed; specular AO remains unchanged.
fn source_private_ao_tag_store(value:f32)->f32 {
 let x=clamp(value,0.0,1.0);
 if(x<0.00006103515625){return floor(x/0.00000095367431640625)*0.00000095367431640625;}
 return bitcast<f32>(bitcast<u32>(x)&0xfffe0000u);
}
''';b=t.encode();data.extend(bytes(-len(data)%4));idx=len(d['bufferViews']);d['bufferViews'].append(dict(buffer=0,byteOffset=len(data),byteLength=len(b)));data.extend(b);data.extend(bytes(-len(data)%4));c['forward']=dict(buffer_view=idx,sha256=hashlib.sha256(b).hexdigest());d['buffers'][0]['byteLength']=len(data);d.setdefault('extras',{})['private_ao_tag_deferred_control']=dict(observed_source_branch='AO_IN_GBUFFER3_TAG',mantissa_bits=6,diagnostic_only=True,source_pixels_applied_to_native=False)
j=json.dumps(d,separators=(',',':')).encode();j+=b' '*(-len(j)%4);gl=struct.pack('<III',0x46546c67,2,28+len(j)+len(data))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(data),0x004e4942)+data;O.mkdir();p=O/'SM_Door-Single-B.ao-tag-deferred.candidate.glb';p.write_bytes(gl);wg=O/'ao-tag-deferred.forward.wgsl';wg.write_bytes(b);assert data[:len(original)]==original
for stem in ['queue_source_glb_diffuse_hdr_store_v72','compare_townsmith_diffuse_hdr_store_v72']:
 old=S/(stem+'.py');name=stem.replace('diffuse_hdr_store','ao_tag_deferred').replace('v72','v77');n=S/(name+'.py');assert not n.exists();txt=old.read_text().replace('v72','v77').replace('diffuse-hdr-store','ao-tag-deferred').replace('diffuse_hdr_store','ao_tag_deferred').replace('quad_difference_with_diffuse_store','quad_difference_with_ao_tag_store').replace('quad_difference_without_diffuse_store','quad_difference_without_ao_tag_store');compile(txt,str(n),'exec');n.write_text(txt)
pins={str(z):sha(z) for z in [Path(__file__),parent,pp,proof,p,wg,S/'queue_source_glb_ao_tag_deferred_v77.py',S/'compare_townsmith_ao_tag_deferred_v77.py']}
r=dict(status='private_observed_ao_tag_candidate_reopened_gpu_pending',finished_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,original_binary_prefix_byte_equal=True,geometry_images_material_state_and_prepass_unchanged=True,only_diffuse_occlusion_storage_changed=True,installed_in_product=False,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,full_material_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0);(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2)+'\n');print(r['status'])
