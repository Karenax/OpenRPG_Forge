from pathlib import Path
import json, hashlib
from datetime import datetime, timezone
import reopen_townsmith_explicit_slot_controls_v1 as shared

W=Path("F:/OpenRPG_ImportWork")
shared.ROOT=W/"townsmith-descriptor-slot-controls-v1"
shared.OUT=W/"townsmith-descriptor-slot-controls-reopen-v1"
shared.main()
proof_path=shared.OUT/"RESULT.v1.json"
proof=json.loads(proof_path.read_text(encoding="utf-8"))
request=json.loads((shared.ROOT/"REQUEST.v1.json").read_text(encoding="utf-8"))
prior_path=W/"townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json"
prior=json.loads(prior_path.read_text(encoding="utf-8"))
pins={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [proof_path,prior_path,Path(__file__)]}
descriptors=[]
for row,case in zip(proof["comparisons"],request["cases"]):
    for draw in range(row["explicit_groups"]):
        p=shared.ROOT/(case["id"]+".group-"+str(draw)+".descriptor.txt")
        text=p.read_text(encoding="utf-8")
        assert text.endswith("indices and vertex attributes exact")
        descriptors.append({"id":case["id"],"draw":draw,"descriptor":text})
        pins[str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
comparisons=[]
for current,previous in zip(proof["comparisons"],prior["comparisons"]):
    assert current["id"]==previous["id"]
    comparisons.append({"id":current["id"],"pixels_different_before":previous["rgb_changed_pixels"],"pixels_different_with_original_descriptor":current["rgb_changed_pixels"],"source_bytes_identical":current["reference_bytes_identical"]})
result={"status":"original_index_descriptors_and_vertex_attributes_verified_gpu_differences_measured","finished_utc":datetime.now(timezone.utc).isoformat(),"descriptors_verified":len(descriptors),"descriptors":descriptors,"comparisons":comparisons,"native_draw_event_mapping_qualified":False,"forge_conversion_qualified":False,"full_import":False,"complete_assets_delivered_increment":0,"input_pins":pins}
(shared.OUT/"DESCRIPTOR-RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
print(json.dumps({"status":result["status"],"descriptors_verified":len(descriptors),"comparisons":comparisons}))
