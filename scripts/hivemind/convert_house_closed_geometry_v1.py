"""Convert/reopen admitted House streams in three immutable batches of 16."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,importlib.util,json
W=Path('F:/OpenRPG_ImportWork');CAP=W/'house-native-mesh-streams-v1';META=W/'house-native-mesh-metadata-v1'
CONVERTER=W/'dungeon-captured-geometry-converter-v1/convert_geometry_batch_v2.py'
CONTROLS=CONVERTER.with_name('CONTROLS.v2.json')
CHECK=Path(__file__).with_name('convert_closed_nested_geometry.py')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def load(name,p):
 spec=importlib.util.spec_from_file_location(name,p);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
def write(p,data):
 with Path(p).open('x',encoding='utf-8') as f:json.dump(data,f,indent=2,allow_nan=False)
def main():
 proof=read(META/'RUN-RESULT.v1.json');assert proof['meshes_verified']==proof['static_adapter_candidates']==48 and proof['rig_or_morph_candidates']==0
 assert read(CONTROLS)['converter_sha256']==sha(CONVERTER)
 source_pins=dict(proof['input_pins'])
 for p,d in source_pins.items():assert sha(p)==d,p
 for p in [Path(__file__).resolve(),CHECK,CONVERTER,CONTROLS,META/'RUN-RESULT.v1.json',META/'NATIVE-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',W/'house-native-mesh-streams-reopen-v1/RESULT.v1.json']:source_pins[str(p)]=sha(p)
 capture=read(CAP/'NATIVE-RESULT.v1.json');metadata={(r['guid'],r['file_id']):r for r in read(META/'NATIVE-RESULT.v1.json')['meshes']}
 converter=load('house_existing_geometry_converter',CONVERTER);checks=load('house_independent_stream_reopener',CHECK)
 rows=sorted(capture['meshes'],key=lambda r:(r['mesh_guid'],r['mesh_file_id']));assert len(rows)==48
 for start in range(0,48,16):
  root=W/f'house-native-geometry-{start:04d}-{start+15:04d}-v1';assert not root.exists();root.mkdir();(root/'jobs').mkdir();results=[]
  write(root/'INPUTS.v1.json',dict(expected_meshes=16,start_ordinal=start,source_pins=source_pins,full_import=False))
  for ordinal,row in enumerate(rows[start:start+16],start):
   key=(row['mesh_guid'],row['mesh_file_id']);native=metadata[key];assert native['bind_poses']==native['blend_shapes']==0
   attributes={s.split(':')[0] for s in native['vertex_formats']};n=native['vertices']
   meta=dict(actual_guid=key[0],expected_guid=key[0],actual_file_id=key[1],expected_file_id=key[1],expected_identity_matches=True,error='',bind_poses=native['bind_poses'],blend_shapes=native['blend_shapes'],vertices=n,submeshes=native['submeshes'],normals_count=n if 'Normal' in attributes else 0,tangents_count=n if 'Tangent' in attributes else 0,colors_count=n if 'Color' in attributes else 0,uv_counts=[n if f'TexCoord{i}' in attributes else 0 for i in range(8)])
   source=Path(row['directory']);destination=root/'jobs'/f'{ordinal:03d}'
   report=converter.convert_closed_case(source,meta,dict(guid=key[0],file_id=key[1]),destination)
   raw={name:(source/name).read_bytes() for name in row['streams']};derived={name:(destination/name).read_bytes() for name in report['derived_streams']}
   checked=checks.reopen_streams(raw,derived,meta)
   for name,details in report['derived_streams'].items():assert sha(destination/name)==details['sha256']
   receipt=dict(mesh_guid=key[0],mesh_file_id=key[1],capture_ordinal=ordinal,vertices=n,submeshes=native['submeshes'],derived_streams_reopened=checked,geometry_receipt_sha256=sha(destination/'GEOMETRY.v1.json'),native_metadata_receipt_sha256=sha(META/'NATIVE-RESULT.v1.json'),source_capture_terminal_verified=True,glb_created=False,asset_delivered=False,full_import=False)
   write(destination/'REOPEN.v1.json',receipt);results.append(receipt)
  for p,d in source_pins.items():assert sha(p)==d,p
  write(root/'RESULT.v1.json',dict(status='16_house_geometry_derivatives_converted_and_independently_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),expected_meshes=16,meshes_reopened=len(results),start_ordinal=start,rows=results,source_pins=source_pins,original_sources_unchanged=True,glb_created=False,material_render_qualified=False,studio_verified=False,full_import=False))
  print(json.dumps(dict(root=str(root),meshes_reopened=16,glb_created=False)))
if __name__=='__main__':main()
