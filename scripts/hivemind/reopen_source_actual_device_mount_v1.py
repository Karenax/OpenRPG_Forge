"""Reopen the GPU residency receipt against frozen source data and current product pins."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'studio-source-actual-device-mount-v2';ROOT=W/'studio-source-actual-device-mount-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();ROOT.mkdir();prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json');assert run['actual_exit_code']==0 and run['status']=='actual_firepit_bevy_device_mount_and_lifecycle_passed' and sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
 for p,d in prep['input_pins'].items():assert sha(p)==d,p
 base=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/scripts/hivemind/check_source_scene_actual_device_mount_v1.py');old=read(W/'studio-source-actual-device-mount-v1/PREPARED.v1.json');assert sha(base)==old['input_pins'][str(base)]
 assert (native['nodes'],native['primitives'],native['material_instances'],native['gpu_images_resident'])==(9,4,4,3);assert native['adapter']['backend']=='Dx12' and native['adapter']['device_type']=='DiscreteGpu' and native['same_bevy_device'] and native['bc_feature']
 for flag in ['negative_singular_root_no_allocations','finite_clock_update','invalid_clock_no_stale_advance','authored_renderer_flags_verified','lod_data_preserved_driver_pending','despawn_releases_resident_and_preserves_borrowed_materials']:assert native[flag]
 assert not native['root_revealed'] and not native['draw_executed'] and not native['gpu_shader_compile_qualified'] and not native['full_import']
 fixture=Path(prep['fixture']);expected=[];mips_checked=0;formats={'bc1_rgba_unorm_srgb':'Bc1RgbaUnormSrgb','bc3_rgba_unorm':'Bc3RgbaUnorm'}
 with fixture.open('rb') as f:
  header=f.read(20);assert header[:4]==b'glTF' and header[16:20]==b'JSON';length=struct.unpack_from('<I',header,12)[0];doc=json.loads(f.read(length));bin_header=f.read(8);assert bin_header[4:]==b'BIN\0';bin_start=f.tell();bin_length=struct.unpack_from('<I',bin_header)[0]
  for material in doc['materials']:
   contract=material.get('extensions',{}).get('OPENRPG_source_graph')
   if not contract:continue
   for texture in contract['textures']:
    storage=texture['imported_storage'];view=doc['bufferViews'][storage['storage']['buffer_view']];offset=view.get('byteOffset',0);size=view['byteLength'];assert offset+size<=bin_length;f.seek(bin_start+offset);data=f.read(size);assert len(data)==size and hashlib.sha256(data).hexdigest()==storage['storage']['sha256']
    for mip in storage['mips']:
     raw=data[mip['offset']:mip['offset']+mip['bytes']];assert len(raw)==mip['bytes'] and hashlib.sha256(raw).hexdigest()==mip['sha256'];mips_checked+=1
    expected.append((formats[storage['format']],storage['width'],storage['height'],len(storage['mips'])))
 actual=[(r['format'],r['width'],r['height'],r['mips']) for r in native['gpu_image_rows']];assert Counter(expected)==Counter(actual) and mips_checked==36 and all(r['had_source_data'] for r in native['gpu_image_rows'])
 pins={str(p):sha(p) for p in [Path(__file__).resolve(),base,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',fixture]}
 result=dict(status='actual_firepit_device_mount_residency_and_source_mips_independently_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L01.GPU.01',nodes_verified=9,primitives_verified=4,material_instances_verified=4,gpu_images_resident_verified=3,source_mip_payloads_verified=36,current_product_pins_verified=len(prep['input_pins']),adapter=native['adapter'],input_pins=pins,root_revealed=False,draw_executed=False,gpu_shader_compile_qualified=False,lod_driver_qualified=False,editor_visual_verified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
