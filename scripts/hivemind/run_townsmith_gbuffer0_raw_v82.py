from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,time
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-declared-gbuffer0-raw-instrumentation-v82';SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPass.cs'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(v):
 t=O/'RESULT.tmp';t.write_text(json.dumps(v,indent=2));t.replace(O/'RESULT.v1.json')
if len(sys.argv)==1:
 assert not O.exists();O.mkdir()
 with (O/'controller.log').open('xb') as f:p=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=f,stderr=f,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid)));print(json.dumps(dict(controller_pid=p.pid,root=str(O))));sys.exit()
state=dict(status='declared_gbuffer0_raw_capture_preparing',sdk_restore_pending=False,full_import=False);original=None;patched=None
try:
 assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 q=(S/'queue_townsmith_declared_gbuffer0_v79.py').read_text().replace('v79','v82').replace('V79','V82').replace('townsmith-door-source-declared-gbuffer0-v82','townsmith-door-source-declared-gbuffer0-raw-v82');qp=S/'queue_townsmith_declared_gbuffer0_raw_v82.py';assert not qp.exists()
 raw='''  string rawPath=Path.Combine(root,id+".inpass-gbuffer0-"+index+".rgba8srgb");
  ctx.cmd.RequestAsyncReadback(texture,0,request=>{
   try{if(request.hasError)throw new Exception("Actual GBuffer0 raw byte readback failed");var bytes=request.GetData<byte>();if(bytes.Length!=width*height*4)throw new Exception("Actual raw sRGB byte layout differs");
    using(var writer=new BinaryWriter(File.Open(rawPath,FileMode.CreateNew)))foreach(byte value in bytes)writer.Write(value);
   }catch(Exception error){errors.Add(error.ToString());}
  });
'''
 anchor="    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())";assert q.count(anchor)==1;ins="    split=text.index('public sealed class SourceOwnedNormalCaptureV82 : CustomPass {')\n    part=text[split:];anchor_raw='  ctx.cmd.RequestAsyncReadback(output,request=>{'\n    assert part.count(anchor_raw)==1\n    text=text[:split]+part.replace(anchor_raw,"+repr(raw)+"+anchor_raw)\n";q=q.replace(anchor,ins+anchor);compile(q,str(qp),'exec');qp.write_text(q)
 original=SDK.read_bytes();assert sha(SDK)=='d868ae696912ede5a932ea4fd4357a55906ba403915b035513e11de592df5fbd';patched=(W/'townsmith-declared-gbuffer3-instrumentation-v75/CustomPass.instrumented.cs').read_bytes();(O/'CustomPass.original.cs').write_bytes(original);(O/'CustomPass.instrumented.cs').write_bytes(patched);state.update(status='declared_gbuffer0_raw_capture_running',sdk_path=str(SDK),sdk_original_sha256=sha(SDK),sdk_restore_pending=True,input_pins={str(p):sha(p) for p in [qp,Path(__file__),O/'CustomPass.original.cs',O/'CustomPass.instrumented.cs']});save(state);SDK.write_bytes(patched)
 subprocess.run([sys.executable,'-B',str(qp)],check=True);receipt=W/'townsmith-door-source-declared-gbuffer0-raw-v82/RUN-RESULT.v1.json'
 while True:
  cap=json.loads(receipt.read_text(encoding='utf-8-sig'))
  if 'finished_utc' in cap:break
  time.sleep(2)
 state.update(capture_receipt=str(receipt),capture_actual_exit_code=cap.get('actual_exit_code'),source_inputs_unchanged=cap.get('source_inputs_unchanged',False),neutral_reference_byte_equal=cap.get('neutral_reference_byte_equal',False));assert cap.get('actual_exit_code')==0,cap;state['status']='declared_gbuffer0_raw_capture_terminal'
except Exception as e:state.update(status='declared_gbuffer0_raw_capture_refusal_retained',error=repr(e),traceback=__import__('traceback').format_exc())
finally:
 if original is not None and patched is not None:
  assert SDK.read_bytes() in [original,patched],'Unexpected concurrent SDK edit';SDK.write_bytes(original);state.update(sdk_restored_byte_equal=SDK.read_bytes()==original,sdk_restore_pending=False)
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
