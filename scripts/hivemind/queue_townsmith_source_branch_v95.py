"""Read actual GPU GI flags in the pinned camera, preserving the source SDK."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, sys, shutil
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-door-source-branch-v95';PRIOR=W/'townsmith-door-source-camera-brdf-v10'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
CS=PROJECT/'Assets/Editor/SourceTownsmithBranchV95.cs'
SHADER=PROJECT/'Assets/Editor/SourceBranchProbeV95.shader'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,value):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(value,indent=2),encoding='utf-8');t.replace(p)
CAPTURE=r'''
public sealed class SourceBranchCaptureV95 : CustomPass {
 public Camera sourceCamera; public string root,id; public int submitted,completed;
 public List<string> errors=new List<string>(); Material material;
 protected override void Execute(CustomPassContext ctx) {
  if(ctx.hdCamera.camera!=sourceCamera)return;
  if(material==null){var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/SourceBranchProbeV95.shader");
   if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Branch shader unresolved");
   material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};}
  var output=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
  if(!output.Create())throw new Exception("Branch target creation failed");
  int ordinal=submitted++;string path=Path.Combine(root,id+".branch-"+ordinal+".rgba32f");
  ctx.cmd.SetRenderTarget(output);ctx.cmd.SetViewport(new Rect(0,0,1,1));
  ctx.cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3,1);
  ctx.cmd.RequestAsyncReadback(output,request=>{
   try {if(request.hasError)throw new Exception("Branch GPU readback failed");var data=request.GetData<float>();
    if(data.Length!=4)throw new Exception("Branch readback dimensions differ");
    using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(float x in data){if(!float.IsFinite(x))throw new Exception("Nonfinite branch flag");writer.Write(x);}completed++;
   }catch(Exception e){errors.Add(e.ToString());}finally{output.Release();UnityEngine.Object.DestroyImmediate(output);}
  });
  CoreUtils.SetRenderTarget(ctx.cmd,ctx.cameraColorBuffer,ctx.cameraDepthBuffer);
 }
 public void ReleaseOwned(){if(material!=null)UnityEngine.Object.DestroyImmediate(material);}
}
'''
def prepare():
 assert not ROOT.exists() and not CS.exists() and not SHADER.exists()
 assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 prior=read(PRIOR/'PREPARED.v1.json');pins=dict(prior['input_pins']);private=dict(prior['private_before_pins'])
 for p,h in {**pins,**private}.items():assert sha(Path(p))==h,p
 ROOT.mkdir();base=S/'candidates/SourceTownsmithCameraBrdfV10.cs'
 text=base.read_text(encoding='utf-8').replace('OpenRPGSourceTownsmithCameraBrdfV10','OpenRPGSourceTownsmithBranchV95')
 anchor='VolumeProfile profile=null;RenderTexture target=null;';assert text.count(anchor)==1
 text=text.replace(anchor,anchor+'SourceBranchCaptureV95 branchPass=null;')
 anchor='    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);';assert text.count(anchor)==1
 text=text.replace(anchor,'    branchPass=new SourceBranchCaptureV95{sourceCamera=camera,root=root,id=item.id};\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.BeforePostProcess,branchPass);\n'+anchor)
 anchor='    var prior=RenderTexture.active;Texture2D raw=null,png=null;';assert text.count(anchor)==1
 text=text.replace(anchor,'    AsyncGPUReadback.WaitAllRequests();if(branchPass.completed<3||branchPass.completed!=branchPass.submitted||branchPass.errors.Count!=0)throw new Exception("Branch readback incomplete: "+string.Join(";",branchPass.errors));\n'+anchor)
 anchor='   }finally{if(target!=null)';assert text.count(anchor)==1
 text=text.replace(anchor,'   }finally{if(branchPass!=null){CustomPassVolume.UnregisterGlobalCustomPass(branchPass);AsyncGPUReadback.WaitAllRequests();branchPass.ReleaseOwned();}if(target!=null)')
 text+='\n'+CAPTURE;CS.write_bytes(text.encode())
 shutil.copyfile(S/'candidates/SourceBranchProbeV95.shader',SHADER)
 shutil.copyfile(CS,ROOT/CS.name);shutil.copyfile(SHADER,ROOT/SHADER.name)
 request=read(PRIOR/'REQUEST.v1.json');request['output']=str(ROOT/'NATIVE-RESULT.v1.json');save('REQUEST.v1.json',request)
 sdk=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
 paths=[Path(__file__),base,S/'candidates/SourceBranchProbeV95.shader',CS,SHADER,ROOT/CS.name,ROOT/SHADER.name,ROOT/'REQUEST.v1.json',PRIOR/'PREPARED.v1.json',PRIOR/'RUN-RESULT.v1.json',sdk/'Material/Lit/Lit.hlsl',sdk/'Lighting/LightLoop/LightLoop.hlsl',sdk/'ShaderLibrary/ShaderVariablesGlobal.cs.hlsl',sdk/'RenderPipeline/HDRenderPipeline.cs',sdk/'RenderPipeline/RenderPass/CustomPass/CustomPass.cs']
 pins.update({str(p):sha(p) for p in paths});save('PREPARED.v1.json',dict(input_pins=pins,private_before_pins=private,source_sdk_modified=False,full_import=False))
 save('RUN-RESULT.v1.json',dict(status='gpu_source_branch_prepared',full_import=False))
 with (ROOT/'controller.log').open('xb') as log:
  child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(root=str(ROOT),controller_pid=child.pid)))
def run():
 state=read(ROOT/'RUN-RESULT.v1.json')
 try:
  prep=read(ROOT/'PREPARED.v1.json')
  for p,h in {**prep['input_pins'],**prep['private_before_pins']}.items():assert sha(Path(p))==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_TOWNSMITH_DRAW_OUT=str(ROOT))
  command=[str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceTownsmithBranchV95.Export','-logFile',str(ROOT/'Unity.log')]
  save('COMMAND.v1.json',dict(command=command))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state.update(status='gpu_source_branch_camera_running',unity_pid=child.pid);save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['backend']=='Direct3D11' and native['source_objects_unchanged']
  assert len(native['rows'])==3 and all(r['original_material_runtime_states_unchanged'] for r in native['rows'])
  for p,h in {**prep['input_pins'],**prep['private_before_pins']}.items():assert sha(Path(p))==h,p
  for name in ['door-neutral.rgba16f','door-nopost-linear.rgba16f']:assert sha(ROOT/name)==sha(PRIOR/name),name
  state.update(status='source_branch_gpu_readbacks_captured_reference_images_byte_equal',source_inputs_unchanged=True,source_sdk_modified=False,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),reference_images_byte_equal=True)
 except Exception as e:state.update(status='source_branch_refusal_retained',error=repr(e),traceback=__import__('traceback').format_exc())
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()
