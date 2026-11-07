from pathlib import Path
import subprocess,sys,os,json,ctypes,hashlib
from ctypes import wintypes
from datetime import datetime,timezone
R=Path("E:/OpenRPG_Forge/OpenRPG_Forge-Rust");W=Path("F:/OpenRPG_ImportWork");S=R/"scripts/hivemind";O=W/"common-srgb-test-draw-chain-v90"
TEST=W/"native-srgb-binding-tests-v90"
D=W/"townsmith-common-native-srgb-layer-0-v89"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(v):
 p=O/"RUN-RESULT.tmp";p.write_text(json.dumps(v,indent=2),encoding="utf-8");p.replace(O/"RUN-RESULT.v1.json")
def wait_pid(pid,result):
 k=ctypes.WinDLL("kernel32",use_last_error=True);k.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];k.OpenProcess.restype=wintypes.HANDLE;k.WaitForSingleObject.argtypes=[wintypes.HANDLE,wintypes.DWORD];k.WaitForSingleObject.restype=wintypes.DWORD;k.CloseHandle.argtypes=[wintypes.HANDLE]
 handle=k.OpenProcess(0x100000,False,pid)
 if not handle:
  assert read(result)["status"] not in ["srgb_binding_tests_running","actual_source_glb_draw_prepared","actual_source_glb_draw_build_and_camera_running"],"Process handle unavailable while state nonterminal"
  return
 try:
  while True:
   status=k.WaitForSingleObject(handle,30000)
   if status==0:return
   assert status==258,"Process wait failed"
 finally:k.CloseHandle(handle)
if len(sys.argv)==1:
 assert not O.exists() and not D.exists();O.mkdir()
 paths=[Path(__file__),S/"queue_source_glb_common_native_srgb_v89.py",S/"compare_townsmith_common_native_srgb_v89.py",R/"crates/forge_engine/examples/verify_source_glb_common_native_srgb_v89.rs"]
 (O/"PREPARED.v1.json").write_text(json.dumps({"input_pins":{str(p):sha(p) for p in paths},"test_job":str(TEST/"JOB.v1.json"),"full_import":False},indent=2),encoding="utf-8")
 with (O/"controller.log").open("xb") as log:child=subprocess.Popen([sys.executable,"-B",str(Path(__file__).resolve()),"run"],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/"JOB.v1.json").write_text(json.dumps({"controller_pid":child.pid}),encoding="utf-8");print(json.dumps({"controller_pid":child.pid,"root":str(O)}));sys.exit()
r={"status":"waiting_on_existing_v90_test_controller","full_import":False};save(r)
try:
 pins=read(O/"PREPARED.v1.json")["input_pins"]
 wait_pid(read(TEST/"JOB.v1.json")["controller_pid"],TEST/"RUN-RESULT.v1.json")
 assert read(TEST/"RUN-RESULT.v1.json")["status"]=="srgb_binding_contract_tests_passed","Required tests did not pass"
 for path,h in read(TEST/"PREPARED.v1.json")["input_pins"].items():assert sha(Path(path))==h,path
 for path,h in pins.items():assert sha(Path(path))==h,path
 env=os.environ.copy();env['OPENRPG_PRIVATE_LIGHT_LAYER']="0"
 subprocess.run([sys.executable,"-B",str(S/"queue_source_glb_common_native_srgb_v89.py")],env=env,check=True)
 r.update(status="waiting_on_glb_draw_controller",draw_job=str(D/"JOB.v1.json"));save(r)
 wait_pid(read(D/"JOB.v1.json")["controller_pid"],D/"RUN-RESULT.v1.json")
 assert read(D/"RUN-RESULT.v1.json")["actual_exit_code"]==0,"GLB draw failed"
 primary=Path("C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe")
 subprocess.run([str(primary),"-B",str(S/"compare_townsmith_common_native_srgb_v89.py")],check=True)
 r.update(status="common_srgb_contract_glb_draw_and_comparison_finished_fidelity_open",comparison=str(W/"townsmith-common-native-srgb-comparison-v89/RESULT.v1.json"))
except Exception as e:r.update(status="common_srgb_chain_refusal_retained",error=repr(e),traceback=__import__("traceback").format_exc())
r["finished_utc"]=datetime.now(timezone.utc).isoformat();save(r)
