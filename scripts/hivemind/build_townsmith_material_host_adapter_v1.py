"""Connect authored material arithmetic to the common uniform ABI, without defaults."""
from pathlib import Path
from datetime import datetime, timezone
import ast
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-material-host-adapter-v1'
PROGRAM = W / 'townsmith-s-paint-texture-set-v1/material-arithmetic-canonical-bindings.wgsl'
BUILDER = W / 'dungeon-source-graph-native-stages-v1/generate.py'
REFERENCE = W / 'delta-vector-bevy-vertex-stages-v3/abc00000000002859845785112346928/source_graph_vertex_and_prepass.wgsl'
TOOLS = W / 'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'
IR = W / 'townsmith-s-paint-source-program-v1/material-family-active-ir-v1/abc00000000009486088228754689894.json'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


ADAPTER = '''
// The renderer must supply the complete original UV0 and the admitted instance frame.
// No substitute frame, truncated UV, constant tangent normal or identity matrix.
struct SourceMaterialHostInputs {
 source_uv0:vec4<f32>,
 target_world_position:vec3<f32>,
 target_world_normal:vec3<f32>,
 target_world_from_object:mat4x4<f32>,
 source_tangent_space_normal:vec3<f32>,
}
fn evaluate_source_material_from_host(input:SourceMaterialHostInputs)->MaterialValues {
 let absolute_position=(source_graph.source_absolute_world_from_target_world*vec4<f32>(input.target_world_position,1.0)).xyz;
 let source_normal=normalize(source_graph_normal_transform_raw(source_graph.source_world_from_target_world,input.target_world_normal));
 let object_to_absolute=source_graph.source_absolute_world_from_target_world*input.target_world_from_object*source_graph.target_object_from_source_object;
 let modes=vec3<u32>(source_graph.normal_decode_modes[0].xyz);
 return evaluate_source_material(input.source_uv0,absolute_position,source_normal,object_to_absolute,modes,input.source_tangent_space_normal);
}
'''


def main():
    assert not ROOT.exists()
    prior = read(W / 'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json')
    assert prior['status'] == 'canonical_arithmetic_wgsl_composition_passed'
    assert sha(PROGRAM) == prior['input_pins'][str(PROGRAM)]
    builder_receipt = read(BUILDER.parent / 'RESULT.v1.json')
    assert sha(BUILDER) == builder_receipt['source_pins'][str(BUILDER)]
    reference_row = next(r for r in read(W / 'delta-vector-bevy-vertex-stages-v3/RESULT.v1.json')['rows'] if r['material_guid'] == 'abc00000000002859845785112346928')
    assert sha(REFERENCE) == reference_row['sha256']
    tools_receipt = read(TOOLS / 'RESULT.v1.json')
    tool, modules = TOOLS / 'validate_real_bevy_closure.exe', TOOLS / 'module-map.tsv'
    assert sha(tool) == tools_receipt['exe_sha256'] and sha(modules) == tools_receipt['module_map_sha256']
    ir = read(IR)
    vertex_kinds = {'Position': 'PositionMaterialSlot', 'Normal': 'NormalMaterialSlot', 'Tangent': 'TangentMaterialSlot'}
    for name, kind in vertex_kinds.items():
        terminal = ir['terminals']['VertexDescription.' + name]
        assert 'ref' not in terminal and terminal['source_slot']['m_Type'].endswith('.' + kind)
        assert terminal['source_slot']['m_Space'] == 0
    uniform = next(ast.literal_eval(n.value) for n in ast.parse(BUILDER.read_text(encoding='utf-8')).body if isinstance(n, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'UNIFORM' for t in n.targets))
    reference = REFERENCE.read_text(encoding='utf-8')
    start = reference.index('fn source_graph_normal_transform_raw(')
    end = reference.index('\n}', start) + 2
    normal_transform = reference[start:end]
    pins = {str(p): sha(p) for p in (PROGRAM, BUILDER, REFERENCE, IR, tool, modules, Path(__file__))}
    ROOT.mkdir()
    output = ROOT / 'material-host-program.wgsl'
    output.write_text(PROGRAM.read_text(encoding='utf-8') + uniform + normal_transform + ADAPTER, encoding='utf-8')
    probe = ROOT / 'host-type-admission.wgsl'
    probe.write_text(output.read_text(encoding='utf-8') + '''
@fragment fn host_type_admission(@location(0) uv:vec4<f32>)->@location(0) vec4<f32> {
 let identity=mat4x4<f32>(vec4<f32>(1.0,0.0,0.0,0.0),vec4<f32>(0.0,1.0,0.0,0.0),vec4<f32>(0.0,0.0,1.0,0.0),vec4<f32>(0.0,0.0,0.0,1.0));
 let o=evaluate_source_material_from_host(SourceMaterialHostInputs(uv,vec3<f32>(0.0),vec3<f32>(0.0,1.0,0.0),identity,vec3<f32>(0.0,0.0,1.0)));
 return vec4<f32>(o.base_color,o.alpha);
}
''', encoding='utf-8')
    cases = []
    for group in (0, 3):
        process = subprocess.run([str(tool), str(modules), str(probe), f'MATERIAL_BIND_GROUP={group}'], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        (ROOT / f'group-{group}.stdout.log').write_bytes(process.stdout)
        (ROOT / f'group-{group}.stderr.log').write_bytes(process.stderr)
        cases.append({'material_bind_group': group, 'actual_exit_code': process.returncode})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    pins.update({str(p): sha(p) for p in (output, probe)})
    result = {'status': 'material_common_uniform_host_adapter_composed' if all(c['actual_exit_code'] == 0 for c in cases) else 'material_host_adapter_refusal_retained',
              'finished_utc': datetime.now(timezone.utc).isoformat(), 'cases': cases, 'input_pins': pins,
              'original_vertex_default_slots_verified': list(vertex_kinds), 'vertex_defaults_are_mesh_inputs': True,
              'host_inputs_required': ['original UV0 float4', 'target world position', 'target world normal', 'actual instance world-from-object transform', 'source tangent-space normal'],
              'common_uniform_binding': 100, 'normal_profile_indices': [0, 1, 2], 'texture_slots': 10,
              'uniform_abi_bytes': 336, 'complete_render_stages_created': False,
              'actual_engine_uniform_binding_qualified': False, 'gpu_rendered': False,
              'forge_editor_verified': False, 'full_import': False, 'complete_assets_delivered_increment': 0,
              'limits': ['Type probe constants are not runtime defaults.', 'Matrices must be finite invertible admitted affine frames; normal modes must be admitted before dispatch.', 'Complete original UV0 must still be transported through vertex/fragment IO.', 'Actual host upload, render stages, implicit derivative sampling and Studio remain open.']}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
