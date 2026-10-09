"""Reopen all six actual Bevy vertex programs and exercise scoped refusals."""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-vector-bevy-vertex-stages-v2'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    receipt = ROOT / 'RESULT.v1.json'
    result = json.loads(receipt.read_text('utf-8-sig'))
    assert result['native_vertex_programs_composed'] == len(result['rows']) == 6
    assert result['actual_positive_compositions'] == 24 and not result['failures']
    assert not result['fragment_adapter_complete'] and not result['renderer_installed']
    for path, expected in result['input_pins'].items():
        assert sha(path) == expected
    pins = dict(result['input_pins'])
    pins[str(receipt)] = sha(receipt)
    pins[str(Path(__file__).resolve())] = sha(__file__)
    rows, identities = [], set()
    for row in result['rows']:
        assert row['material_guid'] not in identities
        identities.add(row['material_guid'])
        assert sha(row['shader']) == row['sha256']
        source = Path(row['shader']).read_text('utf-8-sig')
        assert source.count('@location(12) source_uv1:vec4<f32>,') == 1
        assert 'packet.source_uv1=vertex.source_uv1;' in source
        assert 'output.uv1=packet.source_uv1;' in source
        assert 'output.source_world_to_object=source_graph_inverse_affine(world_m*packet.target_world_from_object*source_graph.target_object_from_source_object);' in source
        assert row['required_original_uv_channels'] == 2
        stages = row['actual_stage_compositions']
        assert {entry['permutation'] for entry in stages} == {'forward','depth_prepass','normal_prepass','ortho_normal_prepass'}
        for entry in stages:
            assert entry['actual_exit_code'] == 0
            case = Path(row['shader']).parent
            out, err = case / (entry['permutation']+'.stdout.log'), case / (entry['permutation']+'.stderr.log')
            assert 'PASS_BEVY0191_NAGA_OIL_FULL_VERTEX_COMPOSITION' in out.read_text('utf-8-sig')
            assert not err.read_bytes()
            pins[str(out)], pins[str(err)] = sha(out), sha(err)
        pins[row['shader']] = row['sha256']
        rows.append(row)
    old = W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4'
    validator, modules = old / 'validate_bevy_vertex.exe', old / 'module-list.txt'
    negative_root = ROOT / 'unsupported-stage-refusals'
    negative_root.mkdir(exist_ok=False)
    tests = {
        'skinned': (['SKINNED'], 'source_graph_skinning_requires_source_qualification'),
        'morph': (['MORPH_TARGETS'], 'source_graph_morphs_require_source_qualification'),
        'ordinary_uv_b': (['VERTEX_UVS_B'], 'source_graph_second_uv_requires_source_qualification'),
        'motion': (['PREPASS_PIPELINE','MOTION_VECTOR_PREPASS'], 'source_graph_previous_camera_basis_and_displacement_require_source_qualification'),
        'deferred': (['PREPASS_PIPELINE','DEFERRED_PREPASS','NORMAL_PREPASS_OR_DEFERRED_PREPASS'], 'source_graph_deferred_surface_mapping_requires_source_qualification'),
    }
    negative = []
    for name, (defines, reason) in tests.items():
        out, err = negative_root / (name+'.stdout.log'), negative_root / (name+'.stderr.log')
        with out.open('xb') as stdout, err.open('xb') as stderr:
            code = subprocess.run([str(validator), str(modules), rows[0]['shader'], '!VERTEX_COLORS', *defines],
                                  stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert code == 3 and reason in err.read_text('utf-8-sig'), 'Scoped refusal failed: '+name
        negative.append(dict(test=name, actual_exit_code=code, expected_exit_code=3, reason=reason,
                             stdout_sha256=sha(out), stderr_sha256=sha(err)))
    for path, expected in pins.items():
        assert sha(path) == expected
    evidence = dict(schema='hivemind.delta-vector-bevy-vertex-independent-reopen.v1',
                    observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                    native_vertex_programs_reopened=6, actual_positive_compositions_reopened=24,
                    scoped_negative_compositions=negative, rows=rows, input_pins=pins,
                    fragment_adapter_complete=False, renderer_installed=False,
                    transform_admission_complete=False, gpu_rendered=False, full_import=False)
    target = ROOT / 'INDEPENDENT-REOPEN.v1.json'
    with target.open('x', encoding='utf-8') as stream:
        json.dump(evidence, stream, indent=2)
    print(json.dumps(dict(reopened=6, positive=24, negative=len(negative), receipt=str(target))))


if __name__ == '__main__':
    main()
