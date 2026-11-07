"""Compose original static vertex and fragment kernels through the existing stage builder."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,runpy,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-native-stages-v434'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
builder=W/'dungeon-source-graph-native-stages-v1/generate.py';receipt=read(builder.parent/'RESULT.v1.json');assert sha(builder)==receipt['source_pins'][str(builder)]
scope=runpy.run_path(str(builder),run_name='trusted_existing_stage_builder')
k=W/'gladiator-material-kernels-v419/RESULT.v1.json';kernels=read(k);assert len(kernels['rows'])==6 and not kernels['failures']
validated=W/'gladiator-render-state-delivery-v433/RESULT.v1.json'
for p,h in read(validated)['input_pins'].items():assert sha(p)==h
validator=W/'native-hdrp-nine-source-programs-v1/rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe';assert sha(validator)=='bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
pins={str(p):sha(p) for p in [Path(__file__),builder,k,validated,validator]};O.mkdir();rows=[];failures=[]
for row in kernels['rows']:
    guid=row['material_guid'];case=O/guid;case.mkdir()
    try:
        source=Path(row['wgsl']);assert sha(source)==row['wgsl_sha256'];pins[str(source)]=row['wgsl_sha256']
        irpath=W/'gladiator-active-material-ir-v386/material-family-active-ir-v1'/(guid+'.json');assert sha(irpath)==row['active_ir_sha256'];pins[str(irpath)]=row['active_ir_sha256'];ir=read(irpath)
        text=source.read_text('utf-8');anchor='struct SourceMaterialInputs { ';assert text.count(anchor)==1
        text=text.replace(anchor,anchor+'source_world_to_object:mat4x4<f32>,source_camera_relative_offset:vec3<f32>,uv1:vec4<f32>,',1)
        # These additional bridge fields are inactive in every original source node.
        assert not any(n['opcode']=='UVNode' and n['source_parameters'].get('m_OutputChannel')!=0 for n in ir['nodes'])
        program,probe,closure,count,normals=scope['build'](text,ir)
        assert closure==[] and count==3 and len(normals)==1
        output=case/'source_program.wgsl';qualifier=case/'stage_probe.wgsl';output.write_text(program,'utf-8');qualifier.write_text(probe,'utf-8')
        with (case/'naga.stdout.log').open('xb') as out,(case/'naga.stderr.log').open('xb') as err:code=subprocess.run([str(validator),str(qualifier)],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert code==0,'Inspect Naga logs'
        rows.append(dict(material_guid=guid,pack_id='hivemind.modular-gladiator-arena',program=str(output),program_sha256=sha(output),probe=str(qualifier),probe_sha256=sha(qualifier),actual_naga_exit_code=0,vertex_dependency_nodes=closure,texture_count=count,normal_mode_slots=normals,required_new_host_fields=['uv1','source_world_to_object','source_camera_relative_offset'],additional_bridge_fields_inactive_in_source_graph=True,renderer_installed=False))
    except Exception as e:failures.append(dict(material_guid=guid,error=repr(e)))
for p,h in pins.items():assert sha(p)==h
result=dict(status='six_native_material_vertex_fragment_stage_probes_validated' if not failures else 'native_stage_refusals_retained',updated_utc=datetime.now(timezone.utc).isoformat(),vertex_and_fragment_stage_probes_validated=len(rows),rows=rows,failures=failures,input_pins=pins,existing_stage_builder_unchanged=True,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],stages=len(rows),failures=failures)))
