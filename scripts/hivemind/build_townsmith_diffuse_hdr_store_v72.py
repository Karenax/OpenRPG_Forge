"""Isolate HDR store ordering; retain geometry, original binary and material state."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,hashlib
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-diffuse-hdr-store-carrier-v72';assert not ROOT.exists()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
parent=W/'townsmith-material-quad-difference-carrier-v68/SM_Door-Single-B.material-quad-difference.candidate.glb';proof=W/'townsmith-material-quad-difference-comparison-v68/RESULT.v1.json';result=json.loads(proof.read_text(encoding='utf-8'));assert sha(parent)==result['input_pins'][str(parent)]
raw=parent.read_bytes();size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);original=raw[28+size:];data=bytearray(original);contract=doc['materials'][0]['extensions']['OPENRPG_source_graph'];ref=contract['forward'];view=doc['bufferViews'][ref['buffer_view']];before=original[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']];assert hashlib.sha256(before).hexdigest()==ref['sha256'];text=before.decode('utf-8')
anchor='let source_measured_ambient=source_scene_irradiance*source_scene_fgd*surface.diffuse_color*surface.diffuse_occlusion;';assert text.count(anchor)==1
helper="""
// Isolated HDR transport diagnostic: finite unsigned 11/11/10 float storage.
// Only renderer-generated radiance is consumed, never Unity pixel samples.
fn source_private_ufloat_store(value:f32,mantissa_bits:u32)->f32 {
 let x=max(value,0.0);
 if(x>=65536.0){return bitcast<f32>(0x7f800000u);}
 if(x<0.00006103515625){
  let unit=select(0.0000019073486328125,0.00000095367431640625,mantissa_bits==6u);
  return floor(x/unit)*unit;
 }
 let mask=~((1u<<(23u-mantissa_bits))-1u);
 return bitcast<f32>(bitcast<u32>(x)&mask);
}
fn source_private_hdr_store(rgb:vec3<f32>)->vec3<f32>{
 return vec3<f32>(source_private_ufloat_store(rgb.r,6u),source_private_ufloat_store(rgb.g,6u),source_private_ufloat_store(rgb.b,5u));
}
"""
text=text.replace(anchor,'let source_measured_ambient_unstored=source_scene_irradiance*source_scene_fgd*surface.diffuse_color*surface.diffuse_occlusion;\n    let source_measured_ambient=source_private_hdr_store(source_measured_ambient_unstored*view_bindings::view.exposure)/view_bindings::view.exposure;');text+=helper;stage=text.encode('utf-8');data.extend(bytes(-len(data)%4));index=len(doc['bufferViews']);doc['bufferViews'].append(dict(buffer=0,byteOffset=len(data),byteLength=len(stage)));data.extend(stage);contract['forward']=dict(buffer_view=index,sha256=hashlib.sha256(stage).hexdigest());data.extend(bytes(-len(data)%4));doc['buffers'][0]['byteLength']=len(data)
doc.setdefault('extras',{})['private_diffuse_gbuffer_store_control']=dict(source_color_format='R11G11B10', storage_stage='exposed_baked_diffuse_before_deferred_read', rounding_mode='floor_private_diagnostic',source_pixels_applied_to_native=False,diagnostic_only=True,full_material_fidelity_qualified=False)
encoded=json.dumps(doc,separators=(',',':')).encode('utf-8');encoded+=b' '*(-len(encoded)%4);output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(data))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(data),0x004e4942)+data
ROOT.mkdir();glb=ROOT/'SM_Door-Single-B.diffuse-hdr-store.candidate.glb';glb.write_bytes(output);shader=ROOT/'diffuse-hdr-store.forward.wgsl';shader.write_bytes(stage)
check=glb.read_bytes();new_size=struct.unpack_from('<I',check,12)[0];new_doc=json.loads(check[20:20+new_size]);new_data=check[28+new_size:];assert new_data[:len(original)]==original;assert new_doc['meshes']==doc['meshes'] and new_doc['nodes']==doc['nodes'];assert new_doc['materials'][0]['extensions']['OPENRPG_source_graph']['forward']['sha256']==hashlib.sha256(stage).hexdigest()
sdk=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material';pins={str(p):sha(p) for p in [Path(__file__),parent,proof,glb,shader,sdk/'Lit/Lit.hlsl',sdk/'Builtin/BuiltinData.cs']};r=dict(status='private_diffuse_hdr_store_candidate_reopened_gpu_pending',finished_utc=datetime.now(timezone.utc).isoformat(),parent_glb_sha256=sha(parent),candidate_glb_sha256=sha(glb),original_binary_prefix_byte_equal=True,geometry_material_state_images_prepass_and_source_metadata_unchanged=True,forward_shader_only_changed=True,input_pins=pins,precision_policy_diagnostic_only=True,source_pixels_applied_to_native=False,thresholds_unchanged=True,full_material_fidelity_qualified=False,full_import=False)
(ROOT/'RESULT.v1.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in r.items() if k!='input_pins'}))
