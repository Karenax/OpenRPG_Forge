"""Compose real Bevy static vertex/prepass stages for the six Delta kernels."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-vector-bevy-vertex-stages-v2'
OLD = W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


INVERSE = '''
// Exact affine inverse. Admission must reject singular/nonfinite transforms.
fn source_graph_inverse_affine(m:mat4x4<f32>)->mat4x4<f32> {
 let a=m[0].xyz;let b=m[1].xyz;let c=m[2].xyz;
 let d=dot(a,cross(b,c));
 let inv=transpose(mat3x3<f32>(cross(b,c),cross(c,a),cross(a,b)))*(1.0/d);
 return mat4x4<f32>(vec4<f32>(inv[0],0.0),vec4<f32>(inv[1],0.0),vec4<f32>(inv[2],0.0),vec4<f32>(-(inv*m[3].xyz),1.0));
}
'''


def main():
    assert not ROOT.exists()
    old_path = OLD / 'RESULT.v1.json'
    old = read(old_path)
    validator = OLD / 'validate_bevy_vertex.exe'
    assert sha(validator) == old['validator_sha256']
    modules = OLD / 'module-list.txt'
    wrapper_path = OLD / 'source_graph_bevy_vertex_and_prepass.wgsl'
    wrapper = wrapper_path.read_text('utf-8-sig')
    reference = old['rows'][0]
    reference_path = Path(reference['forward_vertex'])
    assert sha(reference_path) == reference['sha256']
    full = reference_path.read_text('utf-8-sig')
    assert full.endswith(wrapper)
    coords = full[full.index('struct SourceGraphHostPacket {'):len(full)-len(wrapper)]
    coords = coords.replace('HouseInputs', 'SourceMaterialInputs')
    coords = re.sub(r'output\.normal_mode_\d+=.*?;\s*', '', coords)
    coords = coords.replace(' source_uv0:vec4<f32>,',
        ' source_uv0:vec4<f32>,\n source_uv1:vec4<f32>,\n target_world_from_object:mat4x4<f32>,', 1)
    anchor = 'output.uv0=packet.source_uv0;'
    assert coords.count(anchor) == 1
    coords = coords.replace(anchor, anchor + '''
output.uv1=packet.source_uv1;
output.source_world_to_object=source_graph_inverse_affine(world_m*packet.target_world_from_object*source_graph.target_object_from_source_object);
output.source_camera_relative_offset=abs_m[3].xyz-world_m[3].xyz;
output.view_direction_absolute_world=normalize(camera_absolute-source_absolute);
output.view_direction_world=normalize((world_m*vec4<f32>(packet.target_camera_world,1.0)).xyz-source_world);
output.view_direction_object=normalize(source_graph_linear(output.source_world_to_object)*output.view_direction_world);
''', 1)
    assert wrapper.count(' @location(11) source_color:vec4<f32>,') == 1
    wrapper = wrapper.replace(' @location(11) source_color:vec4<f32>,',
                              ' @location(12) source_uv1:vec4<f32>,', 1)
    wrapper = wrapper.replace(' packet.source_vertex_color=vertex.source_color;',
                              ' packet.source_vertex_color=vec4<f32>(0.0);\n packet.source_uv1=vertex.source_uv1;\n packet.target_world_from_object=world_from_local;', 1)
    wrapper = wrapper.replace('source_house_vertex(', 'evaluate_original_source_vertex(')
    wrapper = wrapper.replace(' out.color=vertex.source_color;', ' source_graph_unused_vertex_color_requires_matching_program_abi();')
    stages_path = W / 'delta-vector-native-stages-v1/RESULT.v1.json'
    stages = read(stages_path)
    assert stages['vertex_and_fragment_stage_probes_validated'] == len(stages['rows']) == 6
    kernels_path = W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'
    topology_path = W / 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
    topology = {row['material_guid']: row for row in read(topology_path)['rows']}
    pins = {str(path): sha(path) for path in [old_path, validator, modules, wrapper_path,
            reference_path, stages_path, kernels_path, topology_path, Path(__file__).resolve()]}
    previous = W / 'delta-vector-bevy-vertex-stages-v1/RESULT.v1.json'
    assert read(previous)['native_vertex_programs_composed'] == 0
    pins[str(previous)] = sha(previous)
    for module in old['bevy_primary_module_pins']:
        assert sha(module['copy']) == module['sha256']
        pins[module['copy']] = module['sha256']
    assert set(modules.read_text('utf-8-sig').splitlines()) == set(row['copy'] for row in old['bevy_primary_module_pins'])
    configs = {'forward': [], 'depth_prepass': ['PREPASS_PIPELINE', 'DEPTH_PREPASS'],
        'normal_prepass': ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS'],
        'ortho_normal_prepass': ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS', 'UNCLIPPED_DEPTH_ORTHO_EMULATION']}
    ROOT.mkdir()
    (ROOT / 'LICENSE.Bevy.MIT.txt').write_bytes((OLD / 'LICENSE.Bevy.MIT.txt').read_bytes())
    rows, failures = [], []
    for row in stages['rows']:
        case = ROOT / row['material_guid']
        case.mkdir()
        try:
            ir_path = Path(topology[row['material_guid']]['output'])
            assert sha(ir_path) == topology[row['material_guid']]['output_sha256']
            ir = read(ir_path)
            assert not any('VertexColorNode' in str(node) for node in ir['nodes']), 'This ABI deliberately excludes active vertex color'
            pins[str(ir_path)] = sha(ir_path)
            program_path = Path(row['program'])
            assert sha(program_path) == row['program_sha256']
            pins[str(program_path)] = sha(program_path)
            program = program_path.read_text('utf-8-sig')
            normals = re.findall(r'normal_mode_(\d+):f32', program)
            adapted_coords = coords.replace('return output;',
                '\n'.join(f'output.normal_mode_{slot}=source_graph.normal_decode_modes[{int(slot)//4}].{"xyzw"[int(slot)%4]};' for slot in normals) + '\nreturn output;')
            output = case / 'source_graph_vertex_and_prepass.wgsl'
            output.write_text(program + INVERSE + adapted_coords + wrapper, encoding='utf-8')
            results = []
            for name, defines in configs.items():
                with (case / (name + '.stdout.log')).open('xb') as out, (case / (name + '.stderr.log')).open('xb') as err:
                    code = subprocess.run([str(validator), str(modules), str(output), '!VERTEX_COLORS', *defines],
                                          stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW).returncode
                assert code == 0, 'Bevy module composition refused: ' + name
                results.append(dict(permutation=name, actual_exit_code=code, shader_defines=['!VERTEX_COLORS', *defines]))
            rows.append(dict(material_guid=row['material_guid'], shader=str(output), sha256=sha(output),
                             actual_stage_compositions=results, required_original_uv_channels=2,
                             original_uv1_shader_location=12, requires_original_vertex_color=False,
                             matrices_derived_from_actual_instance_transform=True, bevy_imports_composed_without_stubs=True,
                             host_transform_admission_required=True, fragment_adapter_required=True, renderer_installed=False))
        except Exception as error:
            failures.append(dict(material_guid=row['material_guid'], error=type(error).__name__ + ': ' + str(error)))
    for path, expected in pins.items():
        assert sha(path) == expected
    result = dict(schema='hivemind.delta-vector-bevy-vertex-stages.v2',
                  observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), requested=6,
                  native_vertex_programs_composed=len(rows), actual_positive_compositions=4*len(rows),
                  rows=rows, failures=failures, input_pins=pins, fragment_adapter_complete=False,
                  renderer_installed=False, gpu_rendered=False, original_render_parity_qualified=False, full_import=False)
    with (ROOT / 'RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2)
    print(json.dumps(dict(composed=len(rows), failures=failures, receipt=str(ROOT / 'RESULT.v1.json'))))


if __name__ == '__main__':
    main()
