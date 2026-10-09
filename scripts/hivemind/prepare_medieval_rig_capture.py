"""Compile and queue an isolated exact-identity rig capture; no flattening or purchased scripts."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W=Path(r"F:\OpenRPG_ImportWork")
ROOT=W/"unity-medieval-rig-source-capture-v1"
OLD=W/"unity-nested-prefab-static-geometry-capture-v1/medieval-kingdom"
HELPER=Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust\scripts\hivemind\candidates\RigSourceSnapshot.cs")
PS=Path(r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe")

def sha(path):
 with Path(path).open("rb") as stream:return hashlib.file_digest(stream,"sha256").hexdigest()
def read(path):return json.loads(Path(path).read_text("utf-8-sig"))
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def save(name,value):
 path=ROOT/name;temporary=path.with_suffix(f".json.{os.getpid()}.tmp");temporary.write_text(json.dumps(value,indent=2),"utf-8");os.replace(temporary,path)

def prepare():
 ROOT.mkdir(exist_ok=False)
 request=next(row for row in read(OLD/"manifest.json")["cases"] if row["expected_guid"]=="abc00000000011112206424264823986")
 matches=[row for row in read(OLD/"PREPARED.normalized-before-native.v2.json")["copies"] if Path(row["project_copy"]).as_posix()==(OLD/"project"/request["asset_path"]).as_posix()]
 if len(matches)!=1:raise RuntimeError("Ambiguous original FBX")
 source=matches[0];project=ROOT/"project";model=project/request["asset_path"];model.parent.mkdir(parents=True)
 for field,target in [("original",model),("original_meta",Path(str(model)+".meta"))]:
  if sha(source[field])!=source[field+"_sha256"]:raise RuntimeError("Original source changed")
  target.write_bytes(Path(source[field]).read_bytes())
 folder=project/"Assets/Editor";folder.mkdir();copied=folder/HELPER.name;copied.write_bytes(HELPER.read_bytes())
 (project/"Packages").mkdir();(project/"Packages/manifest.json").write_text('{"dependencies":{}}',"utf-8")
 (project/"ProjectSettings").mkdir();(project/"ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.5.8f1\nm_EditorVersionWithRevision: 6000.5.8f1 (5cb7df797b7d)\n","utf-8")
 request["output_root"]=str(ROOT/"export");save("manifest.json",request)
 base=W/"unity-blood-raw-mip-sampling-reference-v1/api-check.v1.rsp"
 lines=[line for line in base.read_text("utf-8-sig").splitlines() if line.startswith("-") and not line.startswith("-out:")]
 lines.extend([f'-out:"{ROOT / "RigSourceSnapshot.api.dll"}"',f'"{copied}"'])
 rsp=ROOT/"api-check.v1.rsp";rsp.write_text("\n".join(lines),"utf-8")
 sdk=Path(r"F:\Unity\6000.5.8f1\Editor\Data\DotNetSdk");compilers=list((sdk/"sdk").glob("*/Roslyn/bincore/csc.dll"))
 if len(compilers)!=1:raise RuntimeError("Ambiguous installed compiler")
 with (ROOT/"api.stdout.log").open("xb") as out,(ROOT/"api.stderr.log").open("xb") as err:
  code=subprocess.run([str(sdk/"dotnet.exe"),str(compilers[0]),"@"+str(rsp)],cwd=ROOT,stdout=out,stderr=err,check=False).returncode
 save("API-COMPILE.v1.json",{"actual_exit_code":code,"helper_sha256":sha(copied),"helper":str(copied),"native_unity_launched":False})
 if code:raise RuntimeError("API compile refused; preserve diagnostics")
 paths=[HELPER,Path(__file__),Path(source["original"]),Path(source["original_meta"]),model,Path(str(model)+".meta"),copied,ROOT/"manifest.json",project/"Packages/manifest.json"]
 save("PREPARED.v1.json",{"project":str(project),"input_pins":{str(path):sha(path) for path in paths},"rig_flattened":False,"original_sources_unchanged":True,"full_import":False})
 with (ROOT/"controller.log").open("xb") as log:
  process=subprocess.Popen([sys.executable,str(Path(__file__)),"run"],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save("JOB.v1.json",{"pid":process.pid,"created_utc":now()});print(json.dumps({"queued_pid":process.pid,"actual_api_compile_exit_code":code,"root":str(ROOT)}))

def run():
 state={"status":"waiting_for_prior_builtin_normal_probe","started_utc":now(),"native_unity_launched":False,"rig_flattened":False,"full_import":False};save("RUN-RESULT.v1.json",state)
 try:
  job=read(W/"unity-builtin-normal-constant-reference-v1/JOB.v1.json")
  # Birth identity must match; a recycled PID cannot block or authorize a launch.
  wait=f"$ErrorActionPreference='Stop';while($true){{$p=Get-Process -Id {job['pid']} -ErrorAction SilentlyContinue;if(!$p){{break}};if([Math]::Abs(($p.StartTime.ToUniversalTime()-[DateTime]::Parse('{job['created_utc']}').ToUniversalTime()).TotalSeconds)-gt 2){{break}};$p.WaitForExit(45000)|Out-Null}};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){{Start-Sleep -Seconds 30}}"
  subprocess.run([str(PS),"-NoProfile","-Command",wait],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
  prepared=read(ROOT/"PREPARED.v1.json")
  if any(sha(path)!=expected for path,expected in prepared["input_pins"].items()):raise RuntimeError("Original rig/probe input changed")
  args=[r"F:\Unity\6000.5.8f1\Editor\Unity.exe","-batchmode","-noUpm","-nographics","-quit","-projectPath",prepared["project"],"-executeMethod","OpenRPGRigCapture.Snapshot.Export","--openrpg-manifest",str(ROOT/"manifest.json"),"-logFile",str(ROOT/"unity.log")]
  env=os.environ.copy();env["TEMP"]=str(ROOT);env["TMP"]=str(ROOT);env["UPM_CACHE_ROOT"]=str(ROOT/"upm-cache")
  with (ROOT/"unity.stdout.log").open("xb") as out,(ROOT/"unity.stderr.log").open("xb") as err:
   process=subprocess.Popen(args,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state.update(status="native_complete_rig_source_capture",native_unity_launched=True,unity_pid=process.pid);save("RUN-RESULT.v1.json",state);state["actual_unity_exit_code"]=process.wait()
  if state["actual_unity_exit_code"]!=0:raise RuntimeError("Native rig capture failed; preserve logs")
  if any(sha(path)!=expected for path,expected in prepared["input_pins"].items()):raise RuntimeError("Rig source changed during capture")
  capture=read(ROOT/"export/RESULT.json")
  if capture["rig_flattened"] or capture["animation_events_executed"] or capture["bind_poses"]<=0:raise RuntimeError("Complete preserved rig absent")
  state.update(status="native_rig_source_captured_independent_reopening_remaining",native_result_sha256=sha(ROOT/"export/RESULT.json"),original_sources_unchanged=True,skin_glb_converted=False)
 except Exception as error:state.update(status="refused",error=str(error))
 state["finished_utc"]=now();save("RUN-RESULT.v1.json",state);print(json.dumps(state))

if __name__=="__main__":{"prepare":prepare,"run":run}[sys.argv[1]]()
