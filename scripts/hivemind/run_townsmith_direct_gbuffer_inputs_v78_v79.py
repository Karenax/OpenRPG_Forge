from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,time
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-direct-gbuffer-input-captures-v78-v79';SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPass.cs'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(v):
 tmp=O/'BATCH.tmp';tmp.write_text(json.dumps(v,indent=2));tmp.replace(O/'BATCH.v1.json')
if len(sys.argv)==1:
 assert not O.exists();O.mkdir()
 with (O/'controller.log').open('xb') as f:p=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=f,stderr=f,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid)));print(json.dumps(dict(controller_pid=p.pid,root=str(O))));sys.exit()
state=dict(status='source_gbuffer_inputs_batch_running',completed=[],sdk_restore_pending=False,full_import=False,complete_assets_delivered_increment=0);save(state)
try:
 for index,version,fmt in [(2,78,'R8G8B8A8_UNorm'),(0,79,'R8G8B8A8_SRGB')]:
  assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
  root=W/f'townsmith-declared-gbuffer{index}-instrumentation-v{version}';assert not root.exists();root.mkdir()
  original=SDK.read_bytes();assert sha(SDK)=='d868ae696912ede5a932ea4fd4357a55906ba403915b035513e11de592df5fbd';(root/'CustomPass.original.cs').write_bytes(original)
  patched=(W/'townsmith-declared-gbuffer3-instrumentation-v75/CustomPass.instrumented.cs').read_bytes();assert b'OpenRpgDeclareReadTexture' in patched;(root/'CustomPass.instrumented.cs').write_bytes(patched)
  q=(S/'queue_townsmith_declared_gbuffer3_v75.py').read_text().replace('v75',f'v{version}').replace('V75',f'V{version}').replace('gbuffer3',f'gbuffer{index}').replace('Gbuffer3',f'Gbuffer{index}').replace('GBuffer3',f'GBuffer{index}').replace('handles[3]',f'handles[{index}]').replace('handles.Length<=3',f'handles.Length<={index}').replace('B10G11R11_UFloatPack32',fmt)
  qp=S/f'queue_townsmith_declared_gbuffer{index}_v{version}.py';assert not qp.exists();compile(q,str(qp),'exec');qp.write_text(q)
  st=dict(status=f'declared_gbuffer{index}_source_capture_running',sdk_path=str(SDK),sdk_original_sha256=sha(SDK),sdk_instrumented_sha256=sha(root/'CustomPass.instrumented.cs'),sdk_restore_pending=True,input_pins={str(p):sha(p) for p in [qp,root/'CustomPass.original.cs',root/'CustomPass.instrumented.cs',Path(__file__)]});(root/'RESULT.v1.json').write_text(json.dumps(st,indent=2));state.update(current_buffer=index,current_version=version,sdk_restore_pending=True);save(state)
  try:
   SDK.write_bytes(patched);subprocess.run([sys.executable,'-B',str(qp)],check=True)
   receipt=W/f'townsmith-door-source-declared-gbuffer{index}-v{version}/RUN-RESULT.v1.json'
   while True:
    cap=json.loads(receipt.read_text(encoding='utf-8-sig'))
    if 'finished_utc' in cap:break
    time.sleep(2)
   st.update(capture_receipt=str(receipt),capture_actual_exit_code=cap.get('actual_exit_code'),neutral_reference_byte_equal=cap.get('neutral_reference_byte_equal',False),source_inputs_unchanged=cap.get('source_inputs_unchanged',False));assert cap.get('actual_exit_code')==0,cap
   st['status']=f'declared_gbuffer{index}_source_capture_terminal'
  finally:
   assert SDK.read_bytes() in [original,patched],'Unexpected concurrent SDK changes';SDK.write_bytes(original);st.update(sdk_restored_byte_equal=SDK.read_bytes()==original,sdk_restore_pending=False,finished_utc=datetime.now(timezone.utc).isoformat());(root/'RESULT.v1.json').write_text(json.dumps(st,indent=2));state['sdk_restore_pending']=False;save(state)
  seal=(S/'seal_townsmith_declared_gbuffer3_v75.py').read_text().replace('v75',f'v{version}').replace('gbuffer3',f'gbuffer{index}').replace('GBuffer3',f'GBuffer{index}').replace('_GBufferTexture3',f'_GBufferTexture{index}').replace('B10G11R11_UFloatPack32',fmt)
  sp=S/f'seal_townsmith_declared_gbuffer{index}_v{version}.py';assert not sp.exists();compile(seal,str(sp),'exec');sp.write_text(seal);np=Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe');subprocess.run([str(np),'-B',str(sp)],check=True);state['completed'].append(dict(buffer=index,version=version,proof=str(W/f'townsmith-door-source-declared-gbuffer{index}-reopen-v{version}/RESULT.v1.json')));save(state)
 state['status']='two_source_gbuffer_inputs_captured_and_reopened'
except Exception as e:state.update(status='source_gbuffer_inputs_batch_refusal_retained',error=repr(e),traceback=__import__('traceback').format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
