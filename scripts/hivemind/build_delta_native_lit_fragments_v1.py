"""Assemble native Lit/prepass fragments from exact Delta surface contracts."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
H = W / 'native-hdrp-nine-source-programs-v1'
ROOT = W / 'delta-native-lit-fragments-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    contract_path = W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'
    surface_path = W / 'delta-native-surface-interfaces-v1/RESULT.v1.json'
    reopened_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'
    contracts, surfaces, reopened = read(contract_path), read(surface_path), read(reopened_path)
    assert contracts['contracts_reopened'] == surfaces['native_surface_interfaces_composed'] == reopened['native_surface_interfaces_reopened'] == 6
    for evidence in [contracts, reopened]:
        for path, digest in evidence['input_pins'].items():
            assert sha(path) == digest
    typed_result_path = H / 'source-graph-typed-surface-and-depth-v1/RESULT.v1.json'
    lighting_result_path = H / 'source-graph-native-light-transport-v4/RESULT.v1.json'
    prepass_result_path = H / 'source-graph-native-prepass-outputs-v1/RESULT.v1.json'
    composer_path = H / 'source-graph-real-lighting-composer-v2/RESULT.v1.json'
    typed_result, lighting_result, prepass_result, composer = [read(path) for path in
        [typed_result_path, lighting_result_path, prepass_result_path, composer_path]]
    exemplar = ('primary', '109901881d9e33643a5063b5c74da7cf')

    def exemplar_row(result):
        return next(row for row in result['rows'] if (row['group'], row['material_guid']) == exemplar)

    typed_row, forward_row, prepass_row = [exemplar_row(result) for result in [typed_result, lighting_result, prepass_result]]
    typed_path, forward_path, prepass_path = [Path(row[key]) for row, key in
        [(typed_row, 'native_functions'), (forward_row, 'shader_path'), (prepass_row, 'shader_path')]]
    assert sha(typed_path) == typed_row['native_functions_sha256']
    assert sha(forward_path) == forward_row['shader_sha256']
    assert sha(prepass_path) == prepass_row['shader_sha256']
    typed = typed_path.read_text('utf-8-sig')
    math_path = H / 'source-graph-typed-surface-and-depth-v1/source_surface_math.wgsl'
    math = math_path.read_text('utf-8-sig')
    assert math in typed
    view_helper = typed.split('// Bevy 0.19.1 calculate_view,', 1)[1].split('struct SourceGraphDepthPosition', 1)[0]
    view_helper = '// Bevy 0.19.1 calculate_view,' + view_helper
    lit_builder = typed[typed.index('struct SourceGraphNativeLitSurface {'):]
    lit_builder = lit_builder.replace('source_graph_surface_from_native_frame(in,is_front)', 'source_graph_surface_from_native_frame(in)')
    assert lit_builder.count('vec3<f32>(1.0,1.0,1.0)') == 2
    assert lit_builder.count('select(1.0,1.0,is_front)') == 1
    transport_path = H / 'source-graph-native-light-transport-v4/source_graph_native_light_transport.wgsl'
    assert sha(transport_path) == lighting_result['ported_native_helper_sha256']
    transport = transport_path.read_text('utf-8-sig')
    forward_text = forward_path.read_text('utf-8-sig')
    forward_tail = forward_text[forward_text.index('#import bevy_pbr::mesh_bindings::mesh'):]
    prepass_text = prepass_path.read_text('utf-8-sig')
    prepass_tail = prepass_text[prepass_text.index('// Source clipping, normal mapping, opacity and POM depth reach'):]
    validator = H / 'source-graph-real-lighting-composer-v2/validate_real_bevy_closure.exe'
    modules = H / 'source-graph-real-lighting-composer-v2/module-map.tsv'
    assert sha(validator) == composer['exe_sha256']
    assert sha(modules) == composer['module_map_sha256']
    assert sha(composer_path) == lighting_result['real_composer_result_sha256']
    pins = {str(path): sha(path) for path in [contract_path, surface_path, reopened_path, typed_result_path,
        lighting_result_path, prepass_result_path, composer_path, typed_path, forward_path, prepass_path,
        math_path, transport_path, validator, modules, Path(__file__).resolve()]}
    for module in composer['primary_modules']:
        assert sha(module['path']) == module['sha256']
        pins[module['path']] = module['sha256']
    ROOT.mkdir()
    (ROOT / 'LICENSE.Bevy.MIT.txt').write_bytes((H / 'source-graph-native-surface-bridge-v1/LICENSE.Bevy.MIT.txt').read_bytes())
    material_by_id = {row['material_guid']: row for row in contracts['rows']}
    configs = {'forward': ('forward', []), 'forward_environment': ('forward', ['ENVIRONMENT_MAP']),
        'forward_environment_ssao': ('forward', ['ENVIRONMENT_MAP', 'SCREEN_SPACE_AMBIENT_OCCLUSION']),
        'depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS']),
        'normal_depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS']),
        'shadow_depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'SOURCE_GRAPH_SHADOW_PASS']),
        'ortho_normal_depth': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS', 'UNCLIPPED_DEPTH_ORTHO_EMULATION']),
        'normal_depth_coverage': ('prepass', ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS', 'SOURCE_GRAPH_ALPHA_TO_COVERAGE'])}
    rows, failures = [], []
    for row in surfaces['rows']:
        case = ROOT / row['material_guid']
        case.mkdir()
        try:
            contract = material_by_id[row['material_guid']]
            assert contract['hdrp_standard_active_data'] and contract['hdrp_normal_tangent_space']
            assert contract['hdrp_specular_occlusion_mode'] == 1
            assert not contract['source_depth_offset_enabled'] and not contract['source_keyword_transparent']
            assert not {'_ALPHAPREMULTIPLY_ON', '_ALPHAMODULATE_ON'} & set(contract['source_valid_keywords'])
            path = Path(row['native'])
            assert sha(path) == row['native_sha256']
            pins[str(path)] = sha(path)
            native = path.read_text('utf-8-sig')
            flip = contract['original_double_sided_constants_xyz'] if contract['source_keyword_double_sided'] else [1, 1, 1]
            assert len(flip) == 3 and flip[0] == flip[1], 'Asymmetric XY mode requires separate source normal port'
            flip_literal = 'vec3<f32>(' + ','.join(str(float(value)) for value in flip) + ')'
            builder = lit_builder.replace('vec3<f32>(1.0,1.0,1.0)', flip_literal)
            builder = builder.replace('select(1.0,1.0,is_front)', f'select({float(flip[2])},1.0,is_front)')
            builtin = next(obj for obj in contract['original_active_data'] if obj['m_Type'].endswith('.BuiltinData'))
            clip = '\nfn source_graph_apply_original_clip(surface:SourceMaterialSurfaceOutputs,shadow_pass:bool) {\n#ifdef SOURCE_GRAPH_COVERAGE_PREPASS_REQUIRED\n discard;\n#endif\n'
            if contract['source_keyword_alpha_test']:
                threshold = 'select(surface.alpha_clip_threshold,surface.alpha_clip_threshold_shadow,shadow_pass)' if builtin['m_AlphaTestShadow'] else 'surface.alpha_clip_threshold'
                clip += f' if surface.alpha < {threshold} {{ discard; }}\n'
            clip += '}\n'
            common = native + view_helper + clip + math + builder
            targets = {'forward': case / 'source_native_forward.wgsl', 'prepass': case / 'source_native_prepass.wgsl'}
            targets['forward'].write_text(common + transport + forward_tail, 'utf-8')
            targets['prepass'].write_text(common + prepass_tail, 'utf-8')
            actual = []
            for name, (stage, defines) in configs.items():
                if name == 'normal_depth_coverage' and not contract['source_keyword_alpha_to_mask']:
                    continue
                out_path, err_path = case / (name + '.stdout.log'), case / (name + '.stderr.log')
                with out_path.open('xb') as out, err_path.open('xb') as err:
                    process = subprocess.run([str(validator), str(modules), str(targets[stage]), '!VERTEX_COLORS', *defines],
                        stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                actual.append(dict(permutation=name, shader_stage=stage, shader_defines=['!VERTEX_COLORS', *defines],
                    actual_exit_code=process.returncode, stdout_sha256=sha(out_path), stderr_sha256=sha(err_path)))
                assert process.returncode == 0, err_path.read_text('utf-8-sig')[:2600]
            rows.append(dict(material_guid=row['material_guid'], forward=str(targets['forward']), forward_sha256=sha(targets['forward']),
                prepass=str(targets['prepass']), prepass_sha256=sha(targets['prepass']), actual_compositions=actual,
                original_double_sided_constants=flip, original_alpha_test=contract['source_keyword_alpha_test'],
                original_alpha_to_mask=contract['source_keyword_alpha_to_mask'], original_shadow_threshold_enabled=builtin['m_AlphaTestShadow'],
                native_opaque_light_transport_consumes_source_surface=True, native_prepass_normal_depth_and_coverage_present=True,
                original_package_version_and_native_instance_capture_pending=True, pipeline_state_admission_pending=True))
        except Exception as error:
            failures.append(dict(material_guid=row['material_guid'], error=str(error)))
    assert all(sha(path) == digest for path, digest in pins.items())
    result = dict(schema='hivemind.delta-native-lit-fragments.v1', observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        requested=6, native_lit_programs_composed=len(rows), actual_positive_compositions=sum(len(row['actual_compositions']) for row in rows),
        rows=rows, failures=failures, input_pins=pins, native_camera_exposure_policy=True,
        original_unity_light_transport_parity_qualified=False, source_texture_storage_qualified=False,
        renderer_installed=False, gpu_rendered=False, full_import=False)
    with (ROOT / 'RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2)
    print(json.dumps(dict(composed=len(rows), compositions=result['actual_positive_compositions'], failures=failures)))


if __name__ == '__main__':
    main()
