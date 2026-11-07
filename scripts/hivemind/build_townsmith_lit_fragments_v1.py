"""Wire original Townsmith arithmetic to existing native lighting candidates."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
H = W / 'native-hdrp-nine-source-programs-v1'
ROOT = W / 'townsmith-s-paint-lit-fragments-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


BRIDGE = '''
#ifdef PREPASS_PIPELINE
alias SourceGraphSurfaceVertex=SourceGraphPrepassOutput;
#else
alias SourceGraphSurfaceVertex=SourceGraphForwardOutput;
#endif
fn source_graph_surface_from_native_frame(in:SourceGraphSurfaceVertex,is_front:bool)->MaterialValues {
 let world_from_object=mesh_functions::get_world_from_local(in.instance_index);
 // Exact generated Unity FragInputsToSurfaceDescriptionInputs value.
 let source_ts_normal=vec3<f32>(0.0,0.0,1.0);
 return evaluate_source_material_from_host(SourceMaterialHostInputs(in.source_uv0,in.world_position.xyz,in.world_normal,world_from_object,source_ts_normal));
}
fn source_graph_apply_original_clip(surface:MaterialValues,shadow_pass:bool) {
#ifdef SOURCE_GRAPH_COVERAGE_PREPASS_REQUIRED
 discard;
#endif
 // Authored alpha testing is disabled; do not fabricate a clipping threshold.
}
'''


def main():
    assert not ROOT.exists()
    contract_path = W / 'townsmith-s-paint-original-render-contract-v1/RESULT.v1.json'
    contract = read(contract_path)
    assert contract['contracts_reopened'] == 1
    row = contract['rows'][0]
    assert row['hdrp_standard_active_data'] and row['hdrp_normal_tangent_space']
    assert row['hdrp_specular_occlusion_mode'] == 1
    assert not any(row[k] for k in ('source_keyword_alpha_test', 'source_keyword_alpha_to_mask', 'source_keyword_double_sided', 'source_keyword_transparent', 'source_depth_offset_enabled'))
    vertex_close = W / 'townsmith-s-paint-vertex-transport-reopen-v1/RESULT.v1.json'
    assert read(vertex_close)['compositions_passed'] == 4
    vertex_result = W / 'townsmith-s-paint-vertex-transport-v1/RESULT.v1.json'
    vertex = read(vertex_result)
    program_path = Path(vertex['vertex_shader'])
    typed_result_path = H / 'source-graph-typed-surface-and-depth-v1/RESULT.v1.json'
    lighting_result_path = H / 'source-graph-native-light-transport-v4/RESULT.v1.json'
    prepass_result_path = H / 'source-graph-native-prepass-outputs-v1/RESULT.v1.json'
    typed_result, lighting_result, prepass_result = [read(p) for p in (typed_result_path, lighting_result_path, prepass_result_path)]
    def exemplar(result):
        return next(r for r in result['rows'] if (r['group'], r['material_guid']) == ('primary', '109901881d9e33643a5063b5c74da7cf'))
    typed_row, forward_row, prepass_row = [exemplar(d) for d in (typed_result, lighting_result, prepass_result)]
    typed_path, forward_path, prepass_path = Path(typed_row['native_functions']), Path(forward_row['shader_path']), Path(prepass_row['shader_path'])
    assert sha(typed_path) == typed_row['native_functions_sha256']
    assert sha(forward_path) == forward_row['shader_sha256'] and sha(prepass_path) == prepass_row['shader_sha256']
    math_path = H / 'source-graph-typed-surface-and-depth-v1/source_surface_math.wgsl'
    transport_path = H / 'source-graph-native-light-transport-v4/source_graph_native_light_transport.wgsl'
    assert sha(transport_path) == lighting_result['ported_native_helper_sha256']
    typed = typed_path.read_text(encoding='utf-8')
    math = math_path.read_text(encoding='utf-8')
    assert math in typed
    view_helper = '// Bevy 0.19.1 calculate_view,' + typed.split('// Bevy 0.19.1 calculate_view,', 1)[1].split('struct SourceGraphDepthPosition', 1)[0]
    lit_builder = typed[typed.index('struct SourceGraphNativeLitSurface {'):].replace('surface.bent_normal,', 'surface.bent_normal_ts,')
    forward_text, prepass_text = forward_path.read_text(encoding='utf-8'), prepass_path.read_text(encoding='utf-8')
    forward_tail = forward_text[forward_text.index('#import bevy_pbr::mesh_bindings::mesh'):]
    prepass_tail = prepass_text[prepass_text.index('// Source clipping, normal mapping, opacity and POM depth reach'):]
    composer_path = H / 'source-graph-real-lighting-composer-v2/RESULT.v1.json'
    composer = read(composer_path)
    tool, modules = composer_path.parent / 'validate_real_bevy_closure.exe', composer_path.parent / 'module-map.tsv'
    assert sha(tool) == composer['exe_sha256'] and sha(modules) == composer['module_map_sha256']
    pins = {str(p): sha(p) for p in (contract_path, vertex_close, vertex_result, typed_result_path, lighting_result_path, prepass_result_path, composer_path,
                                     program_path, typed_path, forward_path, prepass_path, math_path, transport_path, tool, modules, Path(__file__))}
    pins.update(contract['input_pins'])
    pins.update(read(vertex_close)['input_pins'])
    pins.update({r['path']: r['sha256'] for r in composer['primary_modules']})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    native = program_path.read_text(encoding='utf-8')
    # Depth/shadow fragments also consume the source normals and original UV0.
    # Retain their interpolated frames even when there is no normal attachment.
    declaration = '#ifdef NORMAL_PREPASS_OR_DEFERRED_PREPASS\n    @location(2) world_normal: vec3<f32>,'
    assert native.count(declaration) == 1
    native = native.replace(declaration, '    @location(2) world_normal: vec3<f32>,').replace('#endif // NORMAL_PREPASS_OR_DEFERRED_PREPASS', '')
    guard = '#ifdef NORMAL_PREPASS_OR_DEFERRED_PREPASS\n out.world_normal=frame.world_normal;\n#ifdef VERTEX_TANGENTS\n out.world_tangent=frame.world_tangent;\n#endif\n#endif'
    assert native.count(guard) == 1
    native = native.replace(guard, ' out.world_normal=frame.world_normal;\n#ifdef VERTEX_TANGENTS\n out.world_tangent=frame.world_tangent;\n#endif')
    common = native + BRIDGE + view_helper + math + lit_builder
    ROOT.mkdir()
    license_source = H / 'source-graph-native-surface-bridge-v1/LICENSE.Bevy.MIT.txt'
    license_copy = ROOT / 'LICENSE.Bevy.MIT.txt'
    license_copy.write_bytes(license_source.read_bytes())
    pins.update({str(p): sha(p) for p in (license_source, license_copy)})
    outputs = {'forward': ROOT / 'source-native-forward.wgsl', 'prepass': ROOT / 'source-native-prepass.wgsl'}
    outputs['forward'].write_text(common + transport_path.read_text(encoding='utf-8') + forward_tail, encoding='utf-8')
    outputs['prepass'].write_text(common + prepass_tail, encoding='utf-8')
    configs = {'forward': ('forward', []), 'forward_environment': ('forward', ['ENVIRONMENT_MAP']),
               'forward_environment_ssao': ('forward', ['ENVIRONMENT_MAP', 'SCREEN_SPACE_AMBIENT_OCCLUSION']),
               'depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS']),
               'normal_depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS']),
               'shadow_depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'SOURCE_GRAPH_SHADOW_PASS']),
               'ortho_normal_depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS', 'UNCLIPPED_DEPTH_ORTHO_EMULATION'])}
    cases = []
    for label, (stage, defines) in configs.items():
        process = subprocess.run([str(tool), str(modules), str(outputs[stage]), '!VERTEX_COLORS', *defines], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        out, err = ROOT / (label + '.stdout.log'), ROOT / (label + '.stderr.log')
        out.write_bytes(process.stdout)
        err.write_bytes(process.stderr)
        pins.update({str(p): sha(p) for p in (out, err)})
        cases.append({'permutation': label, 'shader_stage': stage, 'shader_defines': ['!VERTEX_COLORS', *defines], 'actual_exit_code': process.returncode})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    pins.update({str(p): sha(p) for p in outputs.values()})
    success = all(c['actual_exit_code'] == 0 for c in cases)
    result = {'status': 'townsmith_original_arithmetic_native_lit_and_prepass_composed' if success else 'townsmith_lit_fragment_composition_refusal_retained',
              'finished_utc': datetime.now(timezone.utc).isoformat(), 'input_pins': pins, 'cases': cases,
              'compositions_passed': sum(c['actual_exit_code'] == 0 for c in cases), 'forward': str(outputs['forward']), 'prepass': str(outputs['prepass']),
              'texture_slots': 10, 'original_uv0_components': 4, 'arithmetic_operations_retained': 134,
              'native_lighting_candidate_consumes_original_surface': True,
              'source_unity_light_transport_parity_qualified': False, 'actual_engine_uniform_binding_qualified': False,
              'actual_gpu_layout_qualified': False, 'actual_gpu_render_qualified': False,
              'renderer_installed': False, 'forge_editor_verified': False, 'full_import': False, 'complete_assets_delivered_increment': 0,
              'limits': ['Existing native lighting candidate is wired, not declared equivalent to Unity HDRP.', 'Original scene lighting/exposure/shadows, sampling derivatives, pipeline state and native texture uploads require actual GPU qualification.', 'Do not register a diagnostic fragment or replace the authored arithmetic with a StandardMaterial fallback.']}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
