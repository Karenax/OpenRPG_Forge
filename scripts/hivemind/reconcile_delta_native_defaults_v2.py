"""Reopen the actual one-plus-twelve captures against the original missing-texture ledger."""
import datetime,hashlib,json,copy
from pathlib import Path
from verify_delta_native_material_defaults import validate,controls
import verify_delta_native_material_defaults as verifier
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'delta-native-defaults-independent-reopen-v2'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists(),'Existing evidence preserved'
 ledger_path=W/'nested-prefab-delta-null-texture-resolution-v1/RESULT.v1.json'
 ledger=read(ledger_path);assert len(ledger['rows'])==13 and ledger['all_input_pins_unchanged']
 pins={str(p):sha(p) for p in [ledger_path,Path(__file__),Path(verifier.__file__)]}
 for p,expected in ledger['source_pins'].items():
  assert sha(p)==expected,'Original source or ledger dependency changed: '+p
  pins[p]=expected
 control=controls();observations=[];coverage=[]
 old=W/'unity-delta-warm-prepared-native-v3'
 new=W/'unity-pirate-exact-subgraph-native-v4'
 for root,version,pack in [(old,3,'hivemind.native-american-village'),(new,4,'hivemind.pirate-island')]:
  native_path=root/('NATIVE-RESULT.v%d.json'%version);manifest_path=root/('manifest.v%d.json'%version)
  native=read(native_path);requests=read(manifest_path)['cases']
  bindings=[r for r in ledger['rows'] if r['pack_id']==pack]
  wanted={r['material_guid'] for r in bindings};requests=[r for r in requests if r['material_guid'] in wanted]
  scope=copy.deepcopy(native);scope['cases']=[r for r in native['cases'] if r['material_guid'] in wanted]
  assert {r['material_guid'] for r in scope['cases']}==wanted
  assert len(scope['cases'])==len(requests)==len(bindings)
  # This in-memory subset is explicit; original aggregate receipts remain untouched.
  scope.update(requested=len(requests),captured=len(requests),failed=0)
  rows=validate(scope,requests,bindings)
  for row in rows:row.update(pack_id=pack,native_receipt=str(native_path),native_receipt_sha256=sha(native_path))
  observations.extend(rows);coverage.append(dict(pack_id=pack,materials=len(requests),original_requested=native['requested'],original_failed=native['failed'],validated_successful_subset_only=(version==3)))
  for p in [native_path,manifest_path]:pins[str(p)]=sha(p)
 assert len(observations)==len({r['material_guid'] for r in observations})==13
 for p,expected in pins.items():assert sha(p)==expected
 result=dict(schema='hivemind.native-default-observations.v2',status='thirteen_original_default_bindings_independently_reopened',observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),native_materials_reopened=13,observations=observations,coverage=coverage,controls=control,input_pins=pins,original_sources_unchanged=True,missing_authored_texture_substitution_performed=False,default_pixels_verified=False,runtime_binding_qualified=False,gpu_render_parity_qualified=False,full_import=False)
 ROOT.mkdir()
 with (ROOT/'RESULT.v2.json').open('x',encoding='utf-8') as f:json.dump(result,f,ensure_ascii=False,indent=2)
 print(json.dumps(dict(receipt=str(ROOT/'RESULT.v2.json'),materials=13,controls=control,full_import=False)))
if __name__=='__main__':main()
