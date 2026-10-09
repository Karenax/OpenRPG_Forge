from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,os
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"townsmith-s-paint-native-textures-v1"
PROJECT=W/"unity-dungeon-original-shadergraph-hdrp-startup-v4/project"
CS=Path(__file__).parent/"candidates/SourceTownsmithPaintTexturesV1.cs"
HELPER=PROJECT/"Assets/Editor/SourceTownsmithPaintTexturesV1.cs"
UNITY=Path("F:/Unity/6000.5.8f1/Editor/Unity.exe")
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def save(name,data):
    p=ROOT/name;t=p.with_suffix(".tmp");t.write_text(json.dumps(data,indent=2),encoding="utf-8");t.replace(p)
def prepare():
    assert not ROOT.exists() and not HELPER.exists()
    assert subprocess.check_output(["powershell","-NoProfile","-Command","@(Get-Process Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=="0"
    source=W/"townsmith-s-paint-source-program-v1/RESULT.v1.json";proof=read(source)
    assert proof["actual_exit_code"]==0 and proof["active_nodes"]==134
    pins=dict(proof["input_pins"]);pins[str(source)]=sha(source)
    last=W/"townsmith-shadow-mode-controls-v1/PREPARED.v1.json"
    private=read(last)["private_before_pins"];pins[str(last)]=sha(last)
    for p,h in {**pins,**private}.items():assert sha(p)==h,p
    ROOT.mkdir();HELPER.write_bytes(CS.read_bytes())
    texture_inventory=W/"townsmith-s-paint-texture-inventory-v1/RESULT.v1.json";inventory=read(texture_inventory)
    for p,h in inventory["input_pins"].items():assert sha(p)==h,p
    pins[str(texture_inventory)]=sha(texture_inventory)
    input_path=ROOT/"TEXTURES-INPUT.v1.json";input_path.write_text(json.dumps({"rows":[{"guid":r["guid"],"authored_texture_index":r["authored_texture_index"]} for r in inventory["rows"]]},indent=2),encoding="utf-8")
    pins[str(input_path)]=sha(input_path)
    for p in [CS,HELPER,Path(__file__),UNITY]:pins[str(p)]=sha(p)
    for rel in ["ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset","ProjectSettings/ProjectSettings.asset","Packages/manifest.json","Packages/packages-lock.json"]:
        p=PROJECT/rel;pins[str(p)]=sha(p)
    sdk=PROJECT/"Library/PackageCache/com.unity.shadergraph@8d7376ec43ee/Editor/Importers/ShaderGraphImporter.cs"
    pins[str(sdk)]=sha(sdk)
    save("PREPARED.v1.json",{"input_pins":pins,"private_before_pins":private,"full_import":False})
    with (ROOT/"controller.log").open("xb") as log:
        child=subprocess.Popen([sys.executable,"-B",str(Path(__file__).resolve()),"run"],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json",{"pid":child.pid});print(json.dumps({"pid":child.pid,"root":str(ROOT)}))
def run():
    state={"status":"townsmith_paint_native_texture_capture_running","full_import":False}
    try:
        prep=read(ROOT/"PREPARED.v1.json")
        for p,h in {**prep["input_pins"],**prep["private_before_pins"]}.items():assert sha(p)==h,p
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/"unity-upm-cache"),OPENRPG_PAINT_OUT=str(ROOT))
        with (ROOT/"stdout.log").open("xb") as out,(ROOT/"stderr.log").open("xb") as err:
            child=subprocess.Popen([str(UNITY),"-batchmode","-force-d3d11","-quit","-job-worker-count","2","-projectPath",str(PROJECT),"-executeMethod","OpenRPGSourceTownsmithPaintTexturesV1.Export","-logFile",str(ROOT/"Unity.log")],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state["unity_pid"]=child.pid;save("RUN-RESULT.v1.json",state);state["actual_exit_code"]=child.wait();assert child.returncode==0
        native=read(ROOT/"NATIVE-RESULT.v1.json")
        assert len(native["rows"])==10 and all(len(r["mips"])==r["mipmap_count"] for r in native["rows"])
        assert not native["source_importer_modified"]
        storage_pins={m["file"]:sha(Path(m["file"])) for r in native["rows"] for m in r["mips"] if m["native_readback_ok"]}
        for p,h in {**prep["input_pins"],**prep["private_before_pins"]}.items():assert sha(p)==h,p
        state.update(status="townsmith_paint_native_texture_profiles_captured_inputs_unchanged",source_inputs_unchanged=True,native_receipt_sha256=sha(ROOT/"NATIVE-RESULT.v1.json"),native_readback_complete=native["native_readback_complete"],native_mip_pins=storage_pins,material_render_qualified=False,complete_assets_delivered_increment=0)
    except Exception as e:state.update(status="townsmith_paint_native_texture_capture_refusal_retained",error=repr(e))
    state["finished_utc"]=datetime.now(timezone.utc).isoformat();save("RUN-RESULT.v1.json",state)
if __name__=="__main__":prepare() if sys.argv[-1]=="prepare" else run()
