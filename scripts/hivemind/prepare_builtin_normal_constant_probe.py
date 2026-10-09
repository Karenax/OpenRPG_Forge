"""Queue a native constant-normal probe after existing Unity jobs; preserve purchased sources."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W=Path(r"F:\OpenRPG_ImportWork")
ROOT=W/"unity-builtin-normal-constant-reference-v1"
REPO=Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")
CORE=W/"unity-dungeon-original-shadergraph-compiler-v1/project/Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary"
PS=Path(r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe")

def sha(path):
 with Path(path).open("rb") as source:return hashlib.file_digest(source,"sha256").hexdigest()
def read(path):return json.loads(Path(path).read_text("utf-8-sig"))
def save(name,value):
 path=ROOT/name;temporary=path.with_suffix(f".json.{os.getpid()}.tmp")
 temporary.write_text(json.dumps(value,indent=2),"utf-8");os.replace(temporary,path)
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()

def prepare():
 ROOT.mkdir(exist_ok=False)
 project=ROOT/"project";folder=project/"Assets/Probe";folder.mkdir(parents=True)
 source=REPO/"scripts/hivemind/candidates/BuiltinNormalConstantCapture.cs"
 (folder/source.name).write_bytes(source.read_bytes())
 # Core's exact original UnpackNormal chooses the native compiler's platform branch.
 shader=('#pragma kernel Capture\n#include "'+(CORE/"Common.hlsl").as_posix()+'"\n#include "'+(CORE/"Packing.hlsl").as_posix()+'"\n'+r'''
StructuredBuffer<float4> Inputs;
RWStructuredBuffer<float4> Normals;
RWStructuredBuffer<float4> Flags;
[numthreads(1,1,1)]void Capture(uint3 id:SV_DispatchThreadID){
 float4 value=Inputs[id.x];Normals[id.x]=float4(UnpackNormal(value),value.a);
 float rgb=0,astc=0,d3d=0;
 #if defined(UNITY_NO_DXT5nm)
 rgb=1;
 #endif
 #if defined(UNITY_ASTC_NORMALMAP_ENCODING)
 astc=1;
 #endif
 #if defined(SHADER_API_D3D11)
 d3d=1;
 #endif
 Flags[id.x]=float4(rgb,astc,d3d,1);
}
''')
 (folder/"BuiltinNormalConstant.compute").write_text(shader,"utf-8")
 (project/"Packages").mkdir();(project/"Packages/manifest.json").write_text('{"dependencies":{}}',"utf-8")
 (project/"ProjectSettings").mkdir();(project/"ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.5.8f1\nm_EditorVersionWithRevision: 6000.5.8f1 (5cb7df797b7d)\n","utf-8")
 graph=W/"hivemind-run/villa-forge/source/Assets/Hivemind/VillaForge/HDRP(Default)/Art/Shaders/S_BasicTextured.shadergraph"
 pins={str(path):sha(path) for path in [source,Path(__file__),CORE/"Common.hlsl",CORE/"Packing.hlsl",graph,Path(str(graph)+".meta"),*folder.iterdir()]}
 save("PREPARED.v1.json",{"project":str(project),"input_pins":pins,"constant_white_preserved":True,"no_replacement_texture":True,"source_material_render_parity_qualified":False,"full_import":False})
 with (ROOT/"controller.log").open("xb") as log:
  process=subprocess.Popen([sys.executable,str(Path(__file__)),"run"],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 save("JOB.v1.json",{"pid":process.pid,"created_utc":now()});print(json.dumps({"queued_pid":process.pid,"root":str(ROOT)}))

def run():
 state={"status":"waiting_for_prior_hdrp_and_mesh_capture","started_utc":now(),"native_unity_launched":False,"full_import":False};save("RUN-RESULT.v1.json",state)
 try:
  wait=r'''$ErrorActionPreference='Stop';foreach($job in @(@{pid=43256;ticks=639269118471258673L},@{pid=45688;utc='2026-10-06T21:52:12.795625Z'})){while($true){$p=Get-Process -Id $job.pid -ErrorAction SilentlyContinue;if(!$p){break};if($job.ContainsKey('ticks')){if($p.StartTime.ToUniversalTime().Ticks -ne $job.ticks){break}}else{if([Math]::Abs(($p.StartTime.ToUniversalTime()-[DateTime]::Parse($job.utc).ToUniversalTime()).TotalSeconds)-gt 2){break}};$p.WaitForExit(45000)|Out-Null}};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}'''
  subprocess.run([str(PS),"-NoProfile","-Command",wait],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
  prepared=read(ROOT/"PREPARED.v1.json")
  if any(sha(path)!=expected for path,expected in prepared["input_pins"].items()):raise RuntimeError("Original shader/package/probe input changed")
  output=ROOT/"NATIVE-RESULT.v1.json"
  args=[r"F:\Unity\6000.5.8f1\Editor\Unity.exe","-batchmode","-noUpm","-force-d3d11","-quit","-projectPath",prepared["project"],"-executeMethod","OpenRPGBuiltinNormalProbe.Capture.Export","--openrpg-output",str(output),"-logFile",str(ROOT/"unity.log")]
  env=os.environ.copy();env["TEMP"]=str(ROOT);env["TMP"]=str(ROOT);env["UPM_CACHE_ROOT"]=str(ROOT/"upm-cache")
  with (ROOT/"unity.stdout.log").open("xb") as out,(ROOT/"unity.stderr.log").open("xb") as err:
   native=subprocess.Popen(args,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state.update(status="native_constant_normal_gpu_capture",native_unity_launched=True,unity_pid=native.pid);save("RUN-RESULT.v1.json",state);state["actual_unity_exit_code"]=native.wait()
  if state["actual_unity_exit_code"]!=0:raise RuntimeError("Native probe refused; preserved compiler logs")
  if any(sha(path)!=expected for path,expected in prepared["input_pins"].items()):raise RuntimeError("Input changed during native capture")
  result=read(output)
  if result["graphics_api"]!="Direct3D11" or result["unity_version"]!="6000.5.8f1" or len(result["inputs"])!=4 or len(result["decoded_normals"])!=4:raise RuntimeError("Expected actual native domain absent")
  if any(flag!={"x":0.0,"y":0.0,"z":1.0,"w":1.0} for flag in result["platform_flags"]):raise RuntimeError("Native normal encoding branch differs; do not assume RG-or-AG")
  state.update(status="native_builtin_constant_normal_samples_captured",native_result_sha256=sha(output),independent_wgsl_comparison_verified=False,source_material_render_parity_qualified=False)
 except Exception as error:state.update(status="refused",error=str(error))
 state["finished_utc"]=now();save("RUN-RESULT.v1.json",state);print(json.dumps(state))

if __name__=="__main__":{"prepare":prepare,"run":run}[sys.argv[1]]()
