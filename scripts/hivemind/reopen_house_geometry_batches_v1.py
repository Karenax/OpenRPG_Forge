"""Close all three derivative batches against the earlier frozen native stream hashes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,importlib.util,json
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'house-geometry-batches-independent-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def main():
 assert not ROOT.exists();ROOT.mkdir();proof=read(W/'house-native-mesh-streams-reopen-v1/RESULT.v1.json');assert proof['meshes_verified']==48
 pins=dict(proof['input_pins']);expected={(r['mesh_guid'],r['mesh_file_id']) for r in proof['rows']};seen=set();outputs={};roots=[]
 check=Path(__file__).with_name('convert_closed_nested_geometry.py');spec=importlib.util.spec_from_file_location('house_closed_independent_checker',check);checker=importlib.util.module_from_spec(spec);spec.loader.exec_module(checker)
 for start in [0,16,32]:
  root=W/f'house-native-geometry-{start:04d}-{start+15:04d}-v1';batch=read(root/'RESULT.v1.json');assert batch['expected_meshes']==batch['meshes_reopened']==16
  roots.append(str(root));pins[str(root/'RESULT.v1.json')]=sha(root/'RESULT.v1.json')
  for row in batch['rows']:
   key=(row['mesh_guid'],row['mesh_file_id']);assert key in expected and key not in seen;seen.add(key)
   job=root/'jobs'/f"{row['capture_ordinal']:03d}";receipt=read(job/'GEOMETRY.v1.json');assert sha(job/'GEOMETRY.v1.json')==row['geometry_receipt_sha256']
   source={}
   for p,d in receipt['source_stream_pins'].items():assert pins[p]==d and sha(p)==d,p;source[Path(p).name]=Path(p).read_bytes()
   derived={}
   for name,details in receipt['derived_streams'].items():
    p=job/name;assert sha(p)==details['sha256'] and p.stat().st_size==details['bytes'];outputs[str(p)]=sha(p);derived[name]=p.read_bytes()
   assert checker.reopen_streams(source,derived,receipt['metadata'])==row['derived_streams_reopened']
   for p in [job/'GEOMETRY.v1.json',job/'REOPEN.v1.json']:outputs[str(p)]=sha(p)
 assert seen==expected
 for p,d in pins.items():assert sha(p)==d,p
 pins[str(Path(__file__).resolve())]=sha(Path(__file__).resolve());pins[str(check)]=sha(check)
 result=dict(status='48_house_geometry_derivatives_reopened_against_frozen_native_streams',finished_utc=datetime.now(timezone.utc).isoformat(),meshes_verified=48,batches_verified=3,batch_roots=roots,source_pins=pins,output_pins=outputs,original_sources_unchanged=True,glb_created=False,material_qualified=False,studio_verified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['source_pins','output_pins']}))
if __name__=='__main__':main()
