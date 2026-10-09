"""Reopen GPU controls for all three door LODs, without accepting residual differences."""
from pathlib import Path
import json, hashlib
from datetime import datetime, timezone
import numpy as np
from PIL import Image
W=Path("F:/OpenRPG_ImportWork")
ROOT=W/"townsmith-door-pass-controls-v1"
OUT=W/"townsmith-door-pass-controls-reopen-v1"
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def main():
    assert not OUT.exists()
    run=read(ROOT/"RUN-RESULT.v1.json")
    assert run["actual_exit_code"]==0 and run["status"]=="source_townsmith_door_pass_controls_captured_comparison_pending"
    prep=read(ROOT/"PREPARED.v1.json")
    pins=dict(prep["input_pins"])
    for p,h in {**pins,**prep["private_before_pins"]}.items():assert sha(p)==h,p
    request=read(ROOT/"REQUEST.v1.json");report=read(ROOT/"NATIVE-RESULT.v1.json")
    assert len(request["cases"])==len(report["rows"])==9
    assert report["source_objects_unchanged"] and report["backend"]=="Direct3D11"
    context_path=W/"townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json"
    context=read(context_path)
    comparisons=[]
    for case,row in zip(request["cases"],report["rows"]):
        assert row["id"]==case["id"] and row["original_material_runtime_states_unchanged"]
        assert row["mesh_identity"]==case["mesh"]["guid"]+":"+case["mesh"]["file_id"]
        assert row["material_identities"]==[r["guid"]+":"+r["file_id"] for r in case["materials"]]
        assert all(p["compiled"] for p in row["compiled_passes"])
        ref=W/"townsmith-native-draw-slots-v3"/(case["id"][:7]+".rgba16f")
        assert sha(ref)==context["input_pins"][str(ref)]
        raw=Path(row["raw_image"]);image=Path(row["image"]);control=ROOT/(case["id"]+".control.txt")
        text=control.read_text()
        assert text.startswith("mode="+case["control_mode"]) and "vertex attributes and selected indices exact" in text
        if case["control_mode"]=="visible_source_shadow":assert "original shadow renderer=True; child shadow=Off" in text
        else:assert "original shadow renderer=False; child shadow=On" in text
        assert raw.stat().st_size==ref.stat().st_size==512*512*8
        a=np.fromfile(raw,dtype="<f2").reshape(512,512,4).astype(np.float32)
        b=np.fromfile(ref,dtype="<f2").reshape(512,512,4).astype(np.float32)
        assert np.isfinite(a).all() and np.isfinite(b).all()
        with Image.open(image) as png:png.load();assert png.size==(512,512)
        diff=np.abs(a[:,:,:3]-b[:,:,:3])
        comparisons.append({"id":case["id"],"mode":case["control_mode"],"source_bytes_identical":sha(raw)==sha(ref),"rgb_changed_pixels":int(np.any(diff!=0,axis=2).sum()),"max_absolute_rgb_error":float(diff.max())})
        for p in [raw,image,control,ref]+[Path(p) for k in ["material_before_paths","material_after_compile_paths","material_after_render_paths"] for p in row[k]]:
            pins[str(p)]=sha(p)
    for p in [context_path,Path(__file__)]+[ROOT/n for n in ["RUN-RESULT.v1.json","PREPARED.v1.json","NATIVE-RESULT.v1.json","Unity.log"]]:pins[str(p)]=sha(p)
    result={"status":"nine_original_door_renderer_and_shadow_controls_reopened","sub_lot":"L03.TOWNSMITH.DESSINS.PASSES","finished_utc":datetime.now(timezone.utc).isoformat(),"comparisons":comparisons,"source_inputs_unchanged":True,"native_draw_event_mapping_qualified":False,"thresholds_changed":False,"forge_conversion_qualified":False,"forge_editor_verified":False,"complete_assets_delivered_increment":0,"full_import":False,"input_pins":pins}
    OUT.mkdir()
    (OUT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps({"status":result["status"],"comparisons":comparisons}))
if __name__=="__main__":main()
