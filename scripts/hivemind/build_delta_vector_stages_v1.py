"""Build the vertex/fragment probe stages for the six converted source kernels."""
from pathlib import Path
import datetime,hashlib,json,runpy,subprocess
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'delta-vector-native-stages-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 builder=W/'dungeon-source-graph-native-stages-v1/generate.py'
 old=read(builder.parent/'RESULT.v1.json');assert sha(builder)==old['source_pins'][str(builder)]
 scope=runpy.run_path(str(builder),run_name='trusted_stage_builder_functions')
 kernels_path=W/'delta-vector-node-wgsl-v3/RESULT.v1.json';kernels=read(kernels_path)
 reopened=W/'delta-vector-node-converter-v3/INDEPENDENT-REOPEN.v1.json';assert read(reopened)['converted_shader_kernels_reopened']==6
 topology_path=W/'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json';topology=read(topology_path);irs={r['material_guid']:r for r in topology['rows']}
 validator=W/'native-hdrp-nine-source-programs-v1/rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe'
 assert sha(validator)=='bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
 pins={str(p):sha(p) for p in [builder,kernels_path,reopened,topology_path,validator,Path(__file__)]};rows=[];failures=[];ROOT.mkdir()
 for row in kernels['rows']:
  case=ROOT/row['material_guid'];case.mkdir();path=Path(row['wgsl']);irpath=Path(irs[row['material_guid']]['output'])
  assert sha(path)==row['wgsl_sha256'] and sha(irpath)==irs[row['material_guid']]['output_sha256']
  pins[str(path)]=sha(path);pins[str(irpath)]=sha(irpath)
  try:
   program,probe,closure,texture_count,normal_slots=scope['build'](path.read_text('utf-8'),read(irpath))
   program_path=case/'source_program.wgsl';probe_path=case/'stage_probe.wgsl';program_path.write_text(program,encoding='utf-8');probe_path.write_text(probe,encoding='utf-8')
   with (case/'naga.stdout.log').open('xb') as out,(case/'naga.stderr.log').open('xb') as err:code=subprocess.run([str(validator),str(probe_path)],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
   assert code==0,'Vertex/fragment stage validation refused; exact logs retained'
   rows.append(dict(material_guid=row['material_guid'],pack_id=row['pack_id'],program=str(program_path),program_sha256=sha(program_path),probe=str(probe_path),probe_sha256=sha(probe_path),actual_naga_exit_code=code,vertex_dependency_nodes=closure,texture_count=texture_count,normal_mode_slots=normal_slots,required_new_host_fields=['uv1','source_world_to_object','source_camera_relative_offset'],renderer_installed=False))
  except Exception as e:failures.append(dict(material_guid=row['material_guid'],error=type(e).__name__+': '+str(e)))
 for p,h in pins.items():assert sha(p)==h
 result=dict(schema='hivemind.delta-vector-source-stages.v1',observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),requested=6,vertex_and_fragment_stage_probes_validated=len(rows),rows=rows,failures=failures,input_pins=pins,existing_stage_builder_unchanged=True,host_input_bindings_qualified=False,gpu_rendered=False,original_shader_render_parity_qualified=False,full_import=False)
 with (ROOT/'RESULT.v1.json').open('x',encoding='utf-8') as f:json.dump(result,f,indent=2)
 print(json.dumps(dict(validated=len(rows),failures=failures,receipt=str(ROOT/'RESULT.v1.json'))))
if __name__=='__main__':main()
