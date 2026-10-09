from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,os
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"source-texture-layout-admission-v1";S=Path(__file__).parent;R=S.parents[1]
BIN=Path("C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin")
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def save(name,data):
    p=ROOT/name;t=p.with_suffix(".tmp");t.write_text(json.dumps(data,indent=2),encoding="utf-8");t.replace(p)
def prepare():
    assert not ROOT.exists()
    assert subprocess.check_output(["powershell","-NoProfile","-Command","@(Get-Process cargo,rustc -ErrorAction SilentlyContinue).Count"],text=True).strip()=="0"
    ROOT.mkdir();(ROOT/"src").mkdir()
    baseline=W/"source-punctual-light-contract-check-v1"
    for name in ["Cargo.toml","Cargo.lock"]:
        text=(baseline/name).read_text()
        old="openrpg-firepit-source-ao-scene-v1";assert old in text
        (ROOT/name).write_text(text.replace(old,"openrpg-source-texture-layout-admission-v1"),encoding="utf-8")
    rust=S/"candidates/SourceTextureLayoutAdmissionV1.rs";(ROOT/"src/main.rs").write_bytes(rust.read_bytes())
    files=[rust,Path(__file__),ROOT/"src/main.rs",ROOT/"Cargo.toml",ROOT/"Cargo.lock"]
    for rel in ["crates/forge_engine/src/source_graph.rs","crates/forge_assets/src/source_graph_bundle.rs","crates/forge_engine/src/source_graph_program.rs"]:
        files.append(R/rel)
    save("PREPARED.v1.json",{"input_pins":{str(p):sha(p) for p in files},"native_layout_admission_only":True,"full_import":False})
    with (ROOT/"controller.log").open("xb") as log:
        child=subprocess.Popen([sys.executable,"-B",str(Path(__file__).resolve()),"run"],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json",{"pid":child.pid});print(json.dumps({"pid":child.pid,"root":str(ROOT)}))
def run():
    state={"status":"common_source_texture_layout_native_check_running","full_import":False}
    try:
        prep=read(ROOT/"PREPARED.v1.json")
        for p,h in prep["input_pins"].items():assert sha(p)==h,p
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get("PATH",""),RUSTC=str(BIN/"rustc.exe"),CARGO_BUILD_JOBS="1",CARGO_INCREMENTAL="0",CARGO_TARGET_DIR=str(W/"firepit-static-glb-product-check-v2/target"),TEMP=str(ROOT),TMP=str(ROOT))
        with (ROOT/"stdout.log").open("xb") as out,(ROOT/"stderr.log").open("xb") as err:
            child=subprocess.Popen([str(BIN/"cargo.exe"),"run","--locked","--offline","--",str(ROOT)],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state["cargo_pid"]=child.pid;save("RUN-RESULT.v1.json",state);state["actual_exit_code"]=child.wait();assert child.returncode==0
        native=read(ROOT/"NATIVE-RESULT.v1.json")
        assert native["baseline_layout_created"] and native["proposed_layout_created"]
        for p,h in prep["input_pins"].items():assert sha(p)==h,p
        state.update(status="actual_common_and_proposed_ten_texture_native_layouts_created_inputs_unchanged",native_receipt_sha256=sha(ROOT/"NATIVE-RESULT.v1.json"),actual_render_pipeline_created=False,source_asset_fidelity_qualified=False,web_gpu_verified=False,complete_assets_delivered_increment=0)
    except Exception as e:state.update(status="common_source_texture_layout_native_refusal_retained",error=repr(e))
    state["finished_utc"]=datetime.now(timezone.utc).isoformat();save("RUN-RESULT.v1.json",state)
if __name__=="__main__":prepare() if len(sys.argv)==1 else run()
