from pathlib import Path
from datetime import datetime,timezone
import ast,ctypes,hashlib,struct,importlib.util,json,subprocess,sys,traceback
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-native-tint-variant-v129'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(v):
 v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False);(O/'QUEUE.v1.json').write_text(json.dumps(v,indent=2),'utf-8')
state={'status':'waiting_for_exact_pirate_capture_controller','lot':'L06.MEDIEVAL.TORCH.001'}
try:
 kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
 cmd=subprocess.check_output([PS,'-NoProfile','-Command','(Get-CimInstance Win32_Process -Filter "ProcessId=8828").CommandLine'],text=True).strip()
 if cmd:
  assert 'run_pirate_thumbnail_batch_0105_0112_after_glb_v1.py' in cmd
  handle=kernel.OpenProcess(0x100000,False,8828);assert handle;state['waiting_pid']=8828;write(state)
  try:assert kernel.WaitForSingleObject(handle,0xffffffff)==0
  finally:kernel.CloseHandle(handle)
 q=json.loads((W/'pirate-source-thumbnail-batch-0105-0112-gpu-queue-v1/RESULT.v1.json').read_text('utf-8'));assert q['status'] in ['native_capture_terminal_review_pending','serialized_capture_refusal_retained']
 spec=importlib.util.spec_from_file_location('native_ra',S/'queue_firepit_native_ra_shader_v1.py');native=importlib.util.module_from_spec(spec);spec.loader.exec_module(native);native.ROOT=O;native.probe.ROOT=O;native.probe.job.ROOT=O
 def verify(report):
  count=native.verify(report);manifest=json.loads((O/'manifest.json').read_text('utf-8'));assert len(report['cases'])==2
  for case in report['cases']:
   expected=next(x for x in manifest['cases'] if x['material_guid']==case['material_guid']);assert case['private_tint_override'] and case['variant_source_sha256']==expected['variant_source_sha256'];assert all(struct.pack('<f',case['get_color_tint'][k])==struct.pack('<f',expected['variant_tint'][k]) for k in 'rgba')
  return count
 source=(S/'queue_firepit_legacy_shader_gpu_v1.py').read_text('utf-8');tree=ast.parse(source);fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='run');code=ast.unparse(ast.Module(body=[fn],type_ignores=[]));assert code.count('OpenRPGLegacyParticleShaderGpuProbe.Export')==1;code=code.replace('OpenRPGLegacyParticleShaderGpuProbe.Export','OpenRPGMedievalTorchTintVariantV128.Export');code=code.replace('capturing_16_original_legacy_shader_gpu_fixtures','capturing_two_private_tint_variants_16_shader_fixtures').replace('16_original_legacy_shader_gpu_fixtures_reopened','two_private_tint_variants_shader_fixtures_reopened')
 namespace=dict(native.probe.__dict__);namespace.update(ROOT=O,verify=verify,HERE=S);state.update(status='native_variant_capture_starting',pirate_terminal_status=q['status']);write(state);exec(compile(code,str(S/'queue_firepit_legacy_shader_gpu_v1.py'),'exec'),namespace);namespace['run']()
 result=json.loads((O/'RUN-RESULT.v1.json').read_text('utf-8'));state.update(status='native_variant_capture_terminal',native_status=result['status'],native_receipt=str(O/'RUN-RESULT.v1.json'));write(state)
except Exception as e:state.update(status='queued_native_variant_refusal_retained',error=repr(e),traceback=traceback.format_exc());write(state)
