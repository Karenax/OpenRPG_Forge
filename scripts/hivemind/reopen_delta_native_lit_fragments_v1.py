"""Reopen native lighting/prepass programs and exercise unsupported-stage refusals."""
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
    receipt = ROOT / 'RESULT.v1.json'
    result = read(receipt)
    assert result['native_lit_programs_composed'] == len(result['rows']) == 6
    assert result['actual_positive_compositions'] == 46 and not result['failures']
    assert not result['renderer_installed'] and not result['gpu_rendered'] and not result['full_import']
    pins = dict(result['input_pins'])
    for path, expected in pins.items():
        assert sha(path) == expected
    pins[str(receipt)], pins[str(Path(__file__).resolve())] = sha(receipt), sha(__file__)
    contracts = {row['material_guid']: row for row in read(W / 'delta-foliage-render-contracts-v1/RESULT.v1.json')['rows']}
    interfaces = {row['material_guid']: row for row in read(W / 'delta-native-surface-interfaces-v1/RESULT.v1.json')['rows']}
    rows, identities = [], set()
    count = 0
    for row in result['rows']:
        guid = row['material_guid']
        assert guid not in identities
        identities.add(guid)
        contract = contracts[guid]
        assert row['original_alpha_test'] == contract['source_keyword_alpha_test']
        assert row['original_alpha_to_mask'] == contract['source_keyword_alpha_to_mask']
        flip = contract['original_double_sided_constants_xyz'] if contract['source_keyword_double_sided'] else [1, 1, 1]
        assert row['original_double_sided_constants'] == flip
        expected = {'forward', 'forward_environment', 'forward_environment_ssao', 'depth', 'normal_depth', 'shadow_depth', 'ortho_normal_depth'}
        if contract['source_keyword_alpha_to_mask']:
            expected.add('normal_depth_coverage')
        assert {entry['permutation'] for entry in row['actual_compositions']} == expected
        for stage in ['forward', 'prepass']:
            path = Path(row[stage])
            assert sha(path) == row[stage + '_sha256']
            pins[str(path)] = sha(path)
            text = path.read_text('utf-8-sig')
            assert text.startswith(Path(interfaces[guid]['native']).read_text('utf-8-sig'))
            assert 'surface_probe(' not in text and 'qualification_probe(' not in text
            assert 'fn fragment(in:SourceGraphSurfaceVertex,' in text
            assert 'surface.normal_ts,is_front,vec3<f32>(' + ','.join(str(float(value)) for value in flip) + ')' in text
            clip = text.split('fn source_graph_apply_original_clip(', 1)[1].split('\n}\n', 1)[0]
            assert '#ifdef SOURCE_GRAPH_COVERAGE_PREPASS_REQUIRED\n discard;' in clip
            assert ('if surface.alpha < ' in clip) == contract['source_keyword_alpha_test']
            if contract['source_keyword_alpha_test']:
                assert 'if surface.alpha <= ' not in clip
                assert 'surface.alpha_clip_threshold' in clip
            if stage == 'forward':
                assert 'out.color=source_graph_native_light_transport(p,surface);' in text
                assert 'p.material.emissive=vec4<f32>(surface.emission,1.0);' in text
                assert 'p.N=surface.normal_world;' in text
                assert 'p.material.perceptual_roughness=surface.perceptual_roughness;' in text
            else:
                assert 'coverage=surface.alpha;' in text
                assert 'out.normal=vec4<f32>(surface.normal_world*0.5+vec3<f32>(0.5),coverage);' in text
                assert 'in.unclipped_depth' in text and 'in.position.z' in text
        for entry in row['actual_compositions']:
            case = Path(row['forward']).parent
            out, err = case / (entry['permutation'] + '.stdout.log'), case / (entry['permutation'] + '.stderr.log')
            assert entry['actual_exit_code'] == 0
            assert sha(out) == entry['stdout_sha256'] and sha(err) == entry['stderr_sha256']
            stdout = out.read_text('utf-8-sig')
            assert 'PASS_REAL_BEVY0191_NAGA_OIL_COMPOSITION entrypoints=2' in stdout
            assert not err.read_bytes()
            if entry['shader_stage'] == 'forward':
                assert 'REAL_MODULE bevy_pbr::lighting' in stdout
                assert 'REAL_MODULE bevy_pbr::pbr_bindings' in stdout
            pins[str(out)], pins[str(err)] = sha(out), sha(err)
            count += 1
        rows.append(row)
    assert count == 46
    negative_root = ROOT / 'unsupported-stage-refusals'
    negative_root.mkdir()
    tests = {
        'skinned': (rows[0]['forward'], ['SKINNED'], 'source_graph_skinning_requires_source_qualification'),
        'morph': (rows[0]['forward'], ['MORPH_TARGETS'], 'source_graph_morphs_require_source_qualification'),
        'ordinary_uv_b': (rows[0]['forward'], ['VERTEX_UVS_B'], 'source_graph_second_uv_requires_source_qualification'),
        'motion': (rows[0]['prepass'], ['PREPASS_PIPELINE', 'MOTION_VECTOR_PREPASS'], 'source_graph_previous_camera_basis_and_displacement_require_source_qualification'),
        'deferred': (rows[0]['prepass'], ['PREPASS_PIPELINE', 'DEFERRED_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS'], 'source_graph_deferred_surface_mapping_requires_source_qualification'),
        'coverage_without_normal_attachment': (rows[0]['prepass'], ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'SOURCE_GRAPH_ALPHA_TO_COVERAGE'], 'source_graph_coverage_requires_actual_normal_attachment'),
    }
    negative = []
    validator = H / 'source-graph-real-lighting-composer-v2/validate_real_bevy_closure.exe'
    modules = H / 'source-graph-real-lighting-composer-v2/module-map.tsv'
    for name, (shader, defines, reason) in tests.items():
        out, err = negative_root / (name + '.stdout.log'), negative_root / (name + '.stderr.log')
        with out.open('xb') as stdout, err.open('xb') as stderr:
            code = subprocess.run([str(validator), str(modules), shader, '!VERTEX_COLORS', *defines],
                stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert code == 3 and reason in err.read_text('utf-8-sig'), 'Scoped refusal failed: ' + name
        pins[str(out)], pins[str(err)] = sha(out), sha(err)
        negative.append(dict(test=name, actual_exit_code=code, expected_reason=reason,
            stdout_sha256=sha(out), stderr_sha256=sha(err)))
    assert all(sha(path) == expected for path, expected in pins.items())
    evidence = dict(schema='hivemind.delta-native-lit-fragments-independent-reopen.v1',
        observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), native_lit_programs_reopened=6,
        actual_positive_compositions_reopened=count, scoped_negative_compositions=negative, rows=rows, input_pins=pins,
        source_alpha_cutout_material_count=sum(row['original_alpha_test'] for row in rows),
        source_alpha_to_mask_material_count=sum(row['original_alpha_to_mask'] for row in rows),
        native_lighting_and_prepass_fragments_present=True, pipeline_state_admission_complete=False,
        texture_storage_qualified=False, renderer_installed=False, gpu_rendered=False, full_import=False)
    target = ROOT / 'INDEPENDENT-REOPEN.v1.json'
    with target.open('x', encoding='utf-8') as stream:
        json.dump(evidence, stream, indent=2)
    print(json.dumps(dict(reopened=6, compositions=count, scoped_refusals=len(negative), receipt=str(target))))


if __name__ == '__main__':
    main()
