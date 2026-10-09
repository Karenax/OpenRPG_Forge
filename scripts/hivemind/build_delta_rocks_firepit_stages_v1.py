"""Build exact vertex and fragment stages from the two already emitted kernels."""
from pathlib import Path
import datetime
import json
import runpy
import subprocess
from verify_native_urp_material_snapshot import sha

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-rocks-firepit-native-stages-v1'
WANTED={'abc00000000014678987794877247210','abc00000000009294800760372203426'}

def read(path):return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    builder=W/'dungeon-source-graph-native-stages-v1/generate.py'
    previous=read(builder.parent/'RESULT.v1.json')
    assert sha(builder)==previous['source_pins'][str(builder)]
    scope=runpy.run_path(str(builder),run_name='trusted_stage_builder_functions')
    kernels_path=W/'nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'
    topology_path=W/'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
    contract_path=W/'delta-rocks-firepit-render-contracts-v1/RESULT.v1.json'
    contracts=read(contract_path);assert contracts['contracts_reopened']==2
    irs={r['material_guid']:r for r in read(topology_path)['rows']}
    selected=[r for r in read(kernels_path)['rows'] if r['material_guid'] in WANTED]
    assert len(selected)==2 and all(r['actual_naga_exit_code']==0 for r in selected)
    validator=W/'native-hdrp-nine-source-programs-v1/rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe'
    assert sha(validator)=='bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
    pins={str(p):sha(p) for p in [Path(__file__),builder,validator,kernels_path,topology_path,contract_path]}
    pins.update(contracts['input_pins'])
    assert all(sha(p)==h for p,h in pins.items())
    ROOT.mkdir();rows=[];failures=[]
    for row in selected:
        case=ROOT/row['material_guid'];case.mkdir()
        path=Path(row['wgsl']);irpath=Path(irs[row['material_guid']]['output'])
        assert sha(path)==row['wgsl_sha256']
        assert sha(irpath)==row['active_ir_sha256']==irs[row['material_guid']]['output_sha256']
        pins[str(path)]=sha(path);pins[str(irpath)]=sha(irpath)
        try:
            program,probe,closure,textures,normal_slots=scope['build'](path.read_text('utf-8'),read(irpath))
            program_path=case/'source_program.wgsl';probe_path=case/'stage_probe.wgsl'
            program_path.write_text(program,encoding='utf-8');probe_path.write_text(probe,encoding='utf-8')
            with (case/'naga.stdout.log').open('xb') as out,(case/'naga.stderr.log').open('xb') as err:
                code=subprocess.run([str(validator),str(probe_path)],stdout=out,stderr=err,
                    creationflags=subprocess.CREATE_NO_WINDOW).returncode
            assert code==0, 'Exact stages refused; preserve native errors'
            rows.append(dict(material_guid=row['material_guid'],pack_id=row['pack_id'],
                program=str(program_path),program_sha256=sha(program_path),probe=str(probe_path),
                probe_sha256=sha(probe_path),actual_naga_exit_code=code,vertex_dependency_nodes=closure,
                texture_count=textures,normal_mode_slots=normal_slots,input_requirements=row['input_requirements'],
                terminal_fields=row['terminal_fields'],renderer_installed=False))
        except Exception as error:
            failures.append(dict(material_guid=row['material_guid'],error=type(error).__name__+': '+str(error)))
    assert all(sha(p)==h for p,h in pins.items())
    result=dict(status='two_original_kernel_stages_compiled' if len(rows)==2 else 'refused_or_partial',
        requested=2,vertex_and_fragment_stage_probes_validated=len(rows),rows=rows,failures=failures,
        observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),input_pins=pins,
        original_vertex_and_surface_calculations_preserved=True,host_input_bindings_qualified=False,
        gpu_rendered=False,renderer_installed=False,full_import=False)
    with (ROOT/'RESULT.v1.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    print(json.dumps(dict(stages_validated=len(rows),failures=failures,full_import=False)))

if __name__=='__main__':main()
