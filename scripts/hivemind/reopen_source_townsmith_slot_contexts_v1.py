"""Reopen source GPU views and explicitly scope Unity's private material serialization."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil
import numpy as np
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-native-draw-slots-v3'
OUT=W/'townsmith-hdrp-slot-contexts-reopen-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
SOURCE=W/'hivemind-run/townsmith'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def shader_reference_format(text):
 # Only join the original wrapped inline shader reference. No property omitted.
 return re.sub(r'm_Shader:\s*\{([^}]+)\}',lambda m:'m_Shader: {'+re.sub(r'\s+',' ',m.group(1)).strip()+'}',text)
def material_normalization(original,private):
 additions=['  m_Parent: {fileID: 0}\n','  m_ModifiedSerializedProperties: 0\n','  m_LockedProperties: \n','  m_AllowLocking: 1\n']
 for line in additions:
  assert original.count(line)==0 and private.count(line)==1,line
  private=private.replace(line,'')
 assert original.count('  version: 12\n')==private.count('  version: 13\n')==1
 private=private.replace('  version: 13\n','  version: 12\n')
 a=shader_reference_format(original);b=shader_reference_format(private)
 assert a==b,'Unexpected material data change outside recorded serialization additions'
 return hashlib.sha256(a.encode('utf-8')).hexdigest()
def main():
 assert not OUT.exists()
 run=read(ROOT/'RUN-RESULT.v1.json')
 assert run['actual_exit_code']==0 and run['status']=='source_townsmith_draw_slot_refusal_retained'
 prepared=read(ROOT/'PREPARED.v1.json');report=read(ROOT/'NATIVE-RESULT.v1.json');request=read(ROOT/'REQUEST.v1.json')
 assert report['backend']=='Direct3D11' and report['source_objects_unchanged'] and len(report['rows'])==8
 assert report['private_material_instances_used']
 pins=prepared['input_pins'].copy()
 for p,h in pins.items():assert sha(p)==h,p
 source_manifest=read(SOURCE/'source.v1.json');files={r['stored_path'].lower():r for r in source_manifest['files']}
 altered=[];unchanged=0
 for name,expected in prepared['private_before_pins'].items():
  p=Path(name);actual=sha(p)
  if actual==expected:unchanged+=1;pins[name]=expected;continue
  assert p.suffix=='.mat'
  rel=p.relative_to(PROJECT);info=files[('source/'+rel.as_posix()).lower()];source=SOURCE/info['stored_path']
  assert sha(source)==expected==info['sha256']
  canonical=material_normalization(source.read_text(encoding='utf-8'),p.read_text(encoding='utf-8'))
  altered.append({'source':str(source),'private':str(p),'original_sha256':expected,'private_serialized_sha256':actual,'canonical_material_data_sha256':canonical,'render_properties_preserved_in_serialized_text':True,'editor_material_version_before':12,'editor_material_version_after':13,'added_fields':{'m_Parent':{'fileID':0},'m_ModifiedSerializedProperties':0,'m_LockedProperties':None,'m_AllowLocking':1}})
  pins[name]=actual
 assert len(altered)==7 and unchanged==89
 rows=[];compiled=0
 for expected,row in zip(request['cases'],report['rows']):
  assert row['id']==expected['id']
  assert row['mesh_identity']==expected['mesh']['guid']+':'+expected['mesh']['file_id']
  assert row['submeshes']==expected['expected_submeshes'] and row['material_slots']==len(expected['materials'])
  assert row['material_identities']==[r['guid']+':'+r['file_id'] for r in expected['materials']]
  assert row['original_material_runtime_states_unchanged'] and row['private_material_instances_used']
  assert row['compiled_passes'] and all(p['compiled'] for p in row['compiled_passes']);compiled+=len(row['compiled_passes'])
  p=Path(row['raw_image']);a=np.fromfile(p,dtype='<f2').reshape(512,512,4)
  assert np.isfinite(a).all()
  image=Path(row['image'])
  with Image.open(image) as png:assert png.size==(512,512) and png.format=='PNG'
  contrast=int(np.count_nonzero(np.max(np.abs(a[:,:,:3].astype('f4')-a[0,0,:3].astype('f4')),axis=2)>1/1024))
  assert contrast>32,'Empty or insufficient image contrast'
  prior=W/'townsmith-native-draw-slots-v2'/p.name
  old_equal=sha(prior)==sha(p)
  rows.append({'id':row['id'],'mesh_identity':row['mesh_identity'],'material_identities':row['material_identities'],'submeshes':row['submeshes'],'material_slots':row['material_slots'],'compiled_passes':len(row['compiled_passes']),'image':str(image),'image_sha256':sha(image),'raw_image':str(p),'raw_sha256':sha(p),'contrast_pixels':contrast,'same_hdr_pixels_as_direct_material_v2':old_equal,'native_draw_mapping_observed':row['native_draw_mapping_observed'],'frame_debugger_error':row['frame_debugger_error']})
  for q in [image,p,prior,*[Path(f) for group in ['material_before_paths','material_after_compile_paths','material_after_render_paths'] for f in row[group]]]:pins[str(q)]=sha(q)
 assert not report['frame_debugger_locally_supported'] and all(not r['native_draw_mapping_observed'] for r in rows)
 processor=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Editor/AssetProcessors/MaterialPostProcessor.cs'
 for p in [Path(__file__),processor,ROOT/'RUN-RESULT.v1.json',ROOT/'PREPARED.v1.json',ROOT/'NATIVE-RESULT.v1.json',ROOT/'REQUEST.v1.json',ROOT/'Unity.log',W/'townsmith-private-material-restore-v1/RESULT.v1.json']:
  pins[str(p)]=sha(p)
 OUT.mkdir()
 for record in altered:
  private=Path(record['private']);retained=OUT/'private-imported-materials'/private.relative_to(PROJECT)
  retained.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(private,retained);assert sha(retained)==record['private_serialized_sha256'];record['retained_private_derivative']=str(retained);pins[str(retained)]=sha(retained)
 result={'status':'eight_source_hdrp_slot_contexts_reopened_private_serialization_scoped_draw_mapping_unobserved','sub_lot':'L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP','finished_utc':datetime.now(timezone.utc).isoformat(),'source_archive_on_f_sha256_verified':True,'source_and_license_preserved':True,'source_files_unchanged':True,'original_material_runtime_objects_unchanged':True,'temporary_material_instances_used':True,'cases_reopened':8,'affected_bindings':37,'compiled_pass_observations':compiled,'source_images_reopened':8,'private_material_serializations':altered,'private_admitted_files_unchanged':89,'all_authored_material_data_preserved_after_explicit_serialization_normalization':True,'original_controller_byte_equality_refusal_retained':True,'original_unity_version_fidelity_qualified':False,'frame_debugger_locally_supported':False,'native_draw_mapping_qualified':False,'rows':rows,'shader_conversion_qualified':False,'scene_conversion_qualified':False,'editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,'next_action':'Comparer les rendus MeshRenderer de reference a des controles explicites de submeshes et de slots, sans reduire les huit cas ni qualifier les dessins a partir des seules images','input_pins':pins}
 (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','cases_reopened','compiled_pass_observations','private_admitted_files_unchanged','native_draw_mapping_qualified']},ensure_ascii=True))
 print(json.dumps({'images_identical_to_v2':sum(r['same_hdr_pixels_as_direct_material_v2'] for r in rows),'of':8},ensure_ascii=True))
if __name__=='__main__':main()
