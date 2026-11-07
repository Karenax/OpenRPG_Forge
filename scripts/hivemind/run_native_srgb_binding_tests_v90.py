from pathlib import Path
import os,subprocess,json,hashlib,sys
from datetime import datetime,timezone
R=Path("E:/OpenRPG_Forge/OpenRPG_Forge-Rust");W=Path("F:/OpenRPG_ImportWork");O=W/"native-srgb-binding-tests-v90";BIN=Path("C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin")
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(r):
 p=O/"RUN-RESULT.tmp";p.write_text(json.dumps(r,indent=2),encoding="utf-8");p.replace(O/"RUN-RESULT.v1.json")
if len(sys.argv)==1:
 assert not O.exists();O.mkdir()
 paths=[Path(__file__),R/"Cargo.lock",R/"Cargo.toml",R/".cargo/config.toml"]
 for crate in ["forge_engine","forge_assets","forge_navigation"]:
  paths+=list((R/"crates"/crate/"src").rglob("*.rs"))+list((R/"crates"/crate/"src").rglob("*.wgsl"))
 pins={str(p):sha(p) for p in paths};(O/"PREPARED.v1.json").write_text(json.dumps({"input_pins":pins,"full_import":False},indent=2),encoding="utf-8")
 with (O/"controller.log").open("xb") as log:
  child=subprocess.Popen([sys.executable,"-B",str(Path(__file__).resolve()),"run"],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/"JOB.v1.json").write_text(json.dumps({"controller_pid":child.pid}),encoding="utf-8");print(json.dumps({"controller_pid":child.pid,"root":str(O)}));sys.exit()
r={"status":"srgb_binding_tests_running","started_utc":datetime.now(timezone.utc).isoformat(),"checks":[],"full_import":False};save(r)
try:
 pins=json.loads((O/"PREPARED.v1.json").read_text())["input_pins"]
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get("PATH",""),RUSTC=str(BIN/"rustc.exe"),RUSTDOC=str(BIN/"rustdoc.exe"),CARGO_BUILD_JOBS="1",CARGO_INCREMENTAL="0",CARGO_TARGET_DIR=str(W/"firepit-static-glb-product-check-v2/target"),TEMP=str(O),TMP=str(O))
 for crate in ["forge_assets","forge_engine"]:
  command=[str(BIN/"cargo.exe"),"test","--locked","--offline","-p",crate,"--lib","srgb","--","--nocapture"]
  with (O/f"{crate}.stdout.log").open("xb") as out,(O/f"{crate}.stderr.log").open("xb") as err:
   child=subprocess.Popen(command,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   r.update(active_crate=crate,cargo_pid=child.pid);save(r);code=child.wait()
  r["checks"].append({"crate":crate,"command":command,"actual_exit_code":code});save(r);assert code==0,crate
 assert all(sha(Path(p))==h for p,h in pins.items()),"Inputs changed"
 r.update(status="srgb_binding_contract_tests_passed",inputs_unchanged=True)
except Exception as e:r.update(status="srgb_binding_tests_refusal_retained",error=repr(e),traceback=__import__("traceback").format_exc())
r["finished_utc"]=datetime.now(timezone.utc).isoformat();save(r)
