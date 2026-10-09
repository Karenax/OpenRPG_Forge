"""Reopen the successful static rows without hiding the independently refused rig."""
import datetime
import importlib.util
import json
from pathlib import Path

W=Path(r"F:\OpenRPG_ImportWork")
ROOT=W/"unity-nested-prefab-static-geometry-capture-v1/medieval-kingdom"
VERIFIER=W/"unity-nested-prefab-static-geometry-remaining-v2/verify_export_v2.py"

def read(path):return json.loads(Path(path).read_text("utf-8-sig"))

def main():
 spec=importlib.util.spec_from_file_location("unchanged_native_geometry_verifier",VERIFIER)
 verify=importlib.util.module_from_spec(spec);spec.loader.exec_module(verify)
 prepared_path=ROOT/"PREPARED.normalized-before-native.v2.json"
 prepared=read(prepared_path)
 pins=dict(prepared["source_proof_pins"])
 for path in [prepared_path,VERIFIER,ROOT/"manifest.json",ROOT/"export/RESULT.json",Path(__file__)]:pins[str(path)]=verify.sha(path)
 if verify.sha(ROOT/"manifest.json")!=prepared["manifest_sha256"]:raise RuntimeError("Original manifest changed")
 for copy in prepared["copies"]:
  for field in ["original","original_meta","project_copy"]:
   pins[copy[field]]=copy[field+"_sha256"]
 for path,expected in pins.items():
  if verify.sha(path)!=expected:raise RuntimeError("Original/native input changed: "+path)
 manifest=read(ROOT/"manifest.json");native=read(ROOT/"export/RESULT.json")
 if native["total"]!=14 or native["matched"]!=13 or native["failed"]!=1 or len(native["cases"])!=14:raise RuntimeError("Partial capture shape changed")
 requests=manifest["cases"];expected=prepared["inventory_rows"]
 if len(requests)!=len(expected) or len(requests)!=14:raise RuntimeError("Prepared inventory changed")
 rows=[];deferred=[]
 for ordinal,(request,capture,source) in enumerate(zip(requests,native["cases"],expected)):
  if capture.get("error"):
   if capture.get("bind_poses",0)<=0 or "Rig/morph capture requires its own complete adapter; no flattening" not in capture["error"] or capture["actual_guid"]!=request["expected_guid"] or capture["actual_file_id"]!=request["expected_file_id"]:raise RuntimeError("Unexpected native refusal; inspect before routing")
   deferred.append({"asset_id":request["asset_id"],"source_guid":capture["actual_guid"],"source_file_id":capture["actual_file_id"],"vertices":capture["vertices"],"bind_poses":capture["bind_poses"],"vertex_formats":capture["vertex_formats"],"route":"L05_complete_rig_capture_and_skin_conversion","rig_flattened":False,"error":capture["error"]})
   continue
  formats=[f"{f['attribute']}:{f['format']}:{f['dimension']}:{f['stream']}" for f in capture["vertex_formats"]]
  if formats!=source["original_vertex_formats"] or capture["vertices"]!=source["vertices"] or capture["actual_guid"]!=source["source"]["guid"] or capture["actual_file_id"]!=source["source"]["file_id"]:raise RuntimeError("Exact native source identity/formats changed")
  row=verify.check_case(request,capture,ROOT/"export"/f"{ordinal:03}")
  row["native_case_ordinal"]=ordinal;row["source_formats_verified"]=True;rows.append(row)
 if len(rows)!=13 or len(deferred)!=1:raise RuntimeError("Partial coverage mismatch")
 for path,expected_hash in pins.items():
  if verify.sha(path)!=expected_hash:raise RuntimeError("Evidence changed during reopening")
 report={"schema":"openrpg.native-static-mesh-partial-reopen.v1","checked_utc":datetime.datetime.now(datetime.timezone.utc).isoformat(),"verified_static_source_meshes":13,"deferred_rig_meshes":1,"rows":rows,"deferred":deferred,"input_pins":pins,"unchanged_per_case_verifier_reused":True,"native_capture_rerun":False,"whole_original_batch_qualified":False,"geometry_converted":False,"studio_verified":False,"full_import":False}
 with (ROOT/"PARTIAL-STATIC-SOURCE-REOPEN.v1.json").open("x",encoding="utf-8") as output:json.dump(report,output,indent=2)
 print(json.dumps({"verified_static_source_meshes":13,"deferred_rig_meshes":1,"native_capture_rerun":False,"geometry_converted":False,"full_import":False}))

if __name__=="__main__":main()
