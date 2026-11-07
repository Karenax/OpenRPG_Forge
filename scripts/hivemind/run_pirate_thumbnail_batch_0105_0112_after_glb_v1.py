from pathlib import Path
from datetime import datetime,timezone
import ctypes,json,subprocess,sys,traceback
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-source-thumbnail-batch-0105-0112-gpu-queue-v1'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
RUN=Path(__file__).with_name('queue_pirate_thumbnail_batch_0105_0112_v1.py')
def save(v):
 v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False)
 tmp=O/'RESULT.tmp';tmp.write_text(json.dumps(v,indent=2),'utf-8');tmp.replace(O/'RESULT.v1.json')
def main():
 kernel=ctypes.WinDLL('kernel32',use_last_error=True)
 kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p
 kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.WaitForSingleObject.restype=ctypes.c_ulong
 kernel.CloseHandle.argtypes=[ctypes.c_void_p]
 handles=[];state={'status':'waiting_for_exact_preparation_and_glb_process_handles','lot':'L08.IMAGE.PIRATE.06'}
 try:
  for pid,needle in [(36584,'queue_pirate_thumbnail_batch_0105_0112_v1.py'),(16336,'run_browser_source_glb_v114.py')]:
   cmd=subprocess.check_output([PS,'-NoProfile','-Command',f'(Get-CimInstance Win32_Process -Filter "ProcessId={pid}").CommandLine'],text=True).strip()
   if not cmd:continue
   assert needle in cmd,cmd
   handle=kernel.OpenProcess(0x00100000,False,pid);assert handle,ctypes.get_last_error();handles.append((pid,handle))
  state['wait_process_ids']=[pid for pid,_ in handles];save(state)
  for pid,handle in handles:assert kernel.WaitForSingleObject(handle,0xffffffff)==0
  prep=W/'pirate-source-thumbnail-batch-0105-0112-v1/PREPARED.v1.json';assert prep.is_file(),'Preparation ended without a prepared receipt'
  glb=json.loads((W/'browser-source-glb-v114/RUN-RESULT.v1.json').read_text('utf-8'))
  assert glb['status'] in ['actual_common_browser_glb_binding_draw_and_recovery_passed','browser_glb_probe_refusal_retained'] and 'finished_utc' in glb
  state.update(status='native_capture_started_after_glb_terminal',glb_terminal_status=glb['status']);save(state)
  with (O/'capture.stdout.log').open('xb') as out,(O/'capture.stderr.log').open('xb') as err:
   child=subprocess.Popen([sys.executable,'-B',str(RUN),'run'],stdout=out,stderr=err,stdin=subprocess.DEVNULL,cwd=O,creationflags=subprocess.CREATE_NO_WINDOW)
   state['capture_controller_pid']=child.pid;save(state);state['capture_controller_exit_code']=child.wait()
  result=json.loads((W/'pirate-source-thumbnail-batch-0105-0112-v1/RUN-RESULT.v1.json').read_text('utf-8'))
  state.update(status='native_capture_terminal_review_pending',capture_terminal_status=result['status'],capture_receipt=str(W/'pirate-source-thumbnail-batch-0105-0112-v1/RUN-RESULT.v1.json'))
 except Exception as exc:state.update(status='serialized_capture_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
 finally:
  for _,h in handles:kernel.CloseHandle(h)
  save(state)
if __name__=='__main__':main()
