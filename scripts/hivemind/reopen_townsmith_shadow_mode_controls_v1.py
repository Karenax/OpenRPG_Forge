from pathlib import Path
import json,hashlib
from datetime import datetime,timezone
import numpy as np
from PIL import Image
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"townsmith-shadow-mode-controls-v1";OUT=W/"townsmith-shadow-mode-controls-reopen-v1"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def main():
    assert not OUT.exists()
    run=read(ROOT/"RUN-RESULT.v1.json")
    assert run["actual_exit_code"]==0 and run["status"]=="source_townsmith_shadow_mode_controls_captured_comparison_pending"
    prep=read(ROOT/"PREPARED.v1.json");pins=dict(prep["input_pins"])
    for p,h in {**pins,**prep["private_before_pins"]}.items():assert sha(p)==h,p
    report=read(ROOT/"NATIVE-RESULT.v1.json");request=read(ROOT/"REQUEST.v1.json")
    assert len(report["rows"])==len(request["cases"])==15 and report["source_objects_unchanged"]
    context_path=W/"townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json";context=read(context_path)
    rows=[]
    for case,row in zip(request["cases"],report["rows"]):
        assert case["id"]==row["id"] and row["original_material_runtime_states_unchanged"]
        assert row["mesh_identity"]==case["mesh"]["guid"]+":"+case["mesh"]["file_id"]
        assert row["material_identities"]==[r["guid"]+":"+r["file_id"] for r in case["materials"]]
        assert all(p["compiled"] for p in row["compiled_passes"])
        control=ROOT/(case["id"]+".control.txt");text=control.read_text();mode=case["control_mode"]
        assert "mode="+mode in text and "original selected geometry exact" in text
        assert "renderer shadow casting="+("Off" if mode=="visible_cast_off" else "On") in text
        light_mode=text.split("; light shadows=")[1].split(";")[0]
        if mode.endswith("_shadowed"):assert light_mode=="Soft"
        base_id=case["id"][:7]
        legacy=W/"townsmith-native-draw-slots-v3"/(base_id+".rgba16f")
        assert sha(legacy)==context["input_pins"][str(legacy)]
        reference=ROOT/(base_id+"-source_shadowed.rgba16f") if mode.endswith("_shadowed") else legacy
        raw=Path(row["raw_image"])
        assert raw.stat().st_size==reference.stat().st_size==legacy.stat().st_size==512*512*8
        a=np.fromfile(raw,dtype="<f2").reshape(512,512,4).astype(np.float32)
        b=np.fromfile(reference,dtype="<f2").reshape(512,512,4).astype(np.float32)
        old=np.fromfile(legacy,dtype="<f2").reshape(512,512,4).astype(np.float32)
        assert np.isfinite(a).all() and np.isfinite(b).all()
        diff=np.abs(a[:,:,:3]-b[:,:,:3])
        image=Path(row["image"])
        with Image.open(image) as png:png.load();assert png.size==(512,512)
        rows.append({"id":case["id"],"mode":mode,"light_shadows":light_mode,"reference":str(reference),"reference_is_this_capture":reference==raw,"reference_bytes_identical":sha(raw)==sha(reference),"rgb_changed_pixels":int(np.any(diff!=0,axis=2).sum()),"max_rgb_error":float(diff.max()),"legacy_source_changed_pixels":int(np.any(a[:,:,:3]!=old[:,:,:3],axis=2).sum())})
        for p in [control,raw,reference,legacy,image,ROOT/(case["id"]+".light.json"),ROOT/(case["id"]+".hdlight.json")]:
            pins[str(p)]=sha(p)
    for p in [Path(__file__),context_path]+[ROOT/n for n in ["RUN-RESULT.v1.json","NATIVE-RESULT.v1.json","PREPARED.v1.json","Unity.log"]]:pins[str(p)]=sha(p)
    result={"status":"fifteen_source_renderer_and_light_shadow_modes_reopened","sub_lot":"L03.TOWNSMITH.DESSINS.MODES_OMBRES","finished_utc":datetime.now(timezone.utc).isoformat(),"comparisons":rows,"source_inputs_unchanged":True,"thresholds_changed":False,"native_draw_event_mapping_qualified":False,"shadow_geometry_mapping_qualified":False,"forge_conversion_qualified":False,"forge_editor_verified":False,"full_import":False,"complete_assets_delivered_increment":0,"input_pins":pins}
    OUT.mkdir()
    (OUT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps({"status":result["status"],"comparisons":rows}))
if __name__=="__main__":main()
