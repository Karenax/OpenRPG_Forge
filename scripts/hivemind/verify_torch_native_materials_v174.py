"""Independently seal the queued native material export and source provenance."""
from pathlib import Path
from datetime import datetime,timezone
import ctypes,json,hashlib,struct,subprocess,traceback
W=Path('F:/OpenRPG_ImportWork');CAP=W/'medieval-torch-native-materials-v171';O=W/'medieval-torch-native-materials-reopen-v174';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def bits(v):return struct.pack('<f',v)
def write(s):
 s['updated_utc']=datetime.now(timezone.utc).isoformat();p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(s,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def literals(v):
 if isinstance(v,dict):
  if str(v.get('provenance','')).startswith('material:'):yield v
  for x in v.values():yield from literals(x)
 elif isinstance(v,list):
  for x in v:yield from literals(x)
def main():
 assert not O.exists();O.mkdir();state=dict(status='waiting_for_exact_native_material_export',native_export_verified=False,full_import=False)
 try:
  pid=read(CAP/'JOB.v1.json')['controller_pid'];assert pid==42120;cmd=subprocess.check_output([PS,'-NoProfile','-Command',f'(Get-CimInstance Win32_Process -Filter "ProcessId={pid}").CommandLine'],text=True).strip()
  if cmd:
   assert 'run_torch_native_materials_v171.py run' in cmd;kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.CloseHandle.argtypes=[ctypes.c_void_p];h=kernel.OpenProcess(0x100000,False,pid);assert h;state['waiting_pid']=pid;write(state)
   try:assert kernel.WaitForSingleObject(h,0xffffffff)==0
   finally:kernel.CloseHandle(h)
  run=read(CAP/'RUN-RESULT.v1.json');assert run['status']=='three_native_materials_shader_and_nine_compressed_mip_chains_verified' and run['unity_exit_code']==0
  for p,h in run['output_pins'].items():assert sha(p)==h,p
  bindings=read(W/'medieval-torch-active-texture-bindings-v156/RESULT.v1.json')
  for p,h in bindings['input_pins'].items():assert sha(p)==h,p
  irproof=read(W/'medieval-torch-active-material-ir-v154/RESULT.v1.json');native=read(CAP/'MATERIALS-NATIVE.v1.json');mapping=read(W/'medieval-torch-private-prefab-clone-v134/RESULT.v1.json')['guid_mapping'];materials={m['guid']:m for m in native['materials']};properties=[]
  pass_rows=[]
  for material in native['materials']:
   assert [p['index'] for p in material['passes']]==list(range(len(material['passes'])))
   for p in material['passes']:
    known=bool(p['name']) and not p['name'].startswith('<Unnamed')
    assert p['enabled_known']==known
    if not known:assert not p['enabled']
    pass_rows.append(dict(material_guid=material['guid'],index=p['index'],name=p['name'],enabled_known=p['enabled_known'],enabled=p['enabled'],gpu_compiled=False))
  for row in irproof['rows']:
   p=Path(row['path']);assert sha(p)==row['sha256'];ir=read(p);m=materials[mapping[row['guid']]];actual={v['name']:v for v in m['properties']};expected={v['provenance'].split(':',1)[1]:v['literal'] for v in literals(ir) if isinstance(v['literal'],(int,float,list))}
   assert '_UseMask' in expected and '_UseOpacity' in expected
   for name,value in expected.items():
    prop=actual[name]
    if isinstance(value,list):
     assert prop['type'] in ['Vector','Color'] and len(value)==4
     assert all(bits(a)==bits(prop['value_vector'][k]) for a,k in zip(value,'xyzw')),(row['guid'],name)
    else:assert prop['type'] in ['Float','Range','Int'] and bits(value)==bits(prop['value_float']),(row['guid'],name)
    properties.append(dict(material=row['guid'],property=name,source_value=value,native_float32_values_equal=True))
  expected={t['native_binding']['texture']['guid']:t['native_binding'] for m in bindings['materials'] for t in m['active_textures']};textures=read(CAP/'textures/NATIVE-RESULT.v1.json');profiles=[]
  filters={'Point':0,'Bilinear':1,'Trilinear':2};wrap={'Repeat':0,'Clamp':1,'Mirror':2,'MirrorOnce':3}
  for row in textures['rows']:
   before=expected[row['guid']];assert row['filter_mode']==filters[before['filter']] and row['wrap_u']==wrap[before['wrap_u']] and row['wrap_v']==wrap[before['wrap_v']] and row['aniso']==before['anisotropy'] and bits(row['mip_bias'])==bits(before['mip_bias'])
   assert row['graphics_format']==before['format'] and row['mip_count']==before['mips'] and row['width']==before['width'] and row['height']==before['height']
   profiles.append(dict(guid=row['guid'],sampler_profile_equal=True,format=row['graphics_format'],mips=row['mip_count']))
  assert len(profiles)==len(expected)==9 and {p['guid'] for p in profiles}==set(expected) and sum(row['mips'] for row in profiles)==sum(p['mips'] for p in expected.values())
  state.update(status='native_material_active_values_sampler_profiles_and_exact_mips_independently_reopened',native_export_verified=True,mip_count=sum(p['mips'] for p in profiles),active_properties=properties,native_shader_passes=pass_rows,unnamed_pass_enable_state_qualified=False,texture_profiles=profiles,source_input_pins_rehashed=len(bindings['input_pins']),native_export_receipt_sha256=sha(CAP/'RUN-RESULT.v1.json'),private_meta_changes_classified=not run['private_meta_changes'],shader_execution_and_color_normal_transfer_qualified=False,full_import=False)
 except Exception as exc:state.update(status='native_material_export_reopen_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
 finally:state['finished_utc']=datetime.now(timezone.utc).isoformat();write(state)
if __name__=='__main__':main()
