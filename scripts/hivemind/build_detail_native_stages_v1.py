"""Compose the original Detail vertex/surface stages without binding defaults."""
from pathlib import Path
import datetime
import hashlib
import json
import runpy
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W/'detail-native-default-stages-v1'

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

if __name__ == '__main__':
    assert not ROOT.exists()
    parent_path = W/'detail-native-default-kernel-v1/RESULT.v1.json'
    parent = read(parent_path)
    assert parent['actual_naga_exit_code'] == 0
    program_path = Path(parent['source_material'])
    assert sha(program_path) == parent['source_material_sha256']
    topology_path = W/'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
    ir_row = next(row for row in read(topology_path)['rows'] if row['material_guid']==parent['material_guid'])
    ir_path = Path(ir_row['output'])
    assert sha(ir_path) == ir_row['output_sha256']
    builder = W/'dungeon-source-graph-native-stages-v1/generate.py'
    builder_receipt = read(builder.parent/'RESULT.v1.json')
    assert sha(builder) == builder_receipt['source_pins'][str(builder)]
    validator = W/'native-hdrp-nine-source-programs-v1/rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe'
    assert sha(validator) == 'bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
    pins = {str(path):sha(path) for path in [parent_path,program_path,topology_path,ir_path,builder,validator,Path(__file__).resolve()]}
    ROOT.mkdir()
    functions = runpy.run_path(str(builder),run_name='trusted_existing_stage_builder')
    program,qualifier,closure,textures,normals = functions['build'](program_path.read_text('utf-8-sig'),read(ir_path))
    assert textures == 2 and len(normals) == 1
    assert 'native_default_mask_rg:vec2<f32>' in program
    assert 'i.native_default_mask_rg.x' in program and 'i.native_default_mask_rg.y' in program
    # Binding globals must retain this explicit host input, never substitute zero/white.
    assert 'output.native_default_mask_rg=' not in program
    output = ROOT/'source_program.wgsl'
    probe = ROOT/'stage_probe.wgsl'
    output.write_text(program,'utf-8')
    probe.write_text(qualifier,'utf-8')
    with (ROOT/'naga.stdout.log').open('xb') as out,(ROOT/'naga.stderr.log').open('xb') as err:
        code = subprocess.run([str(validator),str(probe)],stdout=out,stderr=err,
            creationflags=subprocess.CREATE_NO_WINDOW).returncode
    for path,digest in pins.items():
        assert sha(Path(path)) == digest
    receipt = dict(status='detail_original_vertex_and_surface_stage_probes_validated' if code==0 else 'refused_or_unverified',
        actual_naga_exit_code=code,material_guid=parent['material_guid'],
        original_vertex_dependency_nodes=closure,texture_count=textures,normal_mode_slots=normals,
        program=str(output),program_sha256=sha(output),probe=str(probe),probe_sha256=sha(probe),
        explicit_native_default_host_input_required=True,host_binding_qualified=False,
        renderer_installed=False,gpu_render_parity_qualified=False,full_import=False,
        input_pins=pins,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps({key:value for key,value in receipt.items() if key!='input_pins'}))
