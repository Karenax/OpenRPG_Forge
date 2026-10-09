"""Independently reopen native surface interfaces and exact evaluator statements."""
from pathlib import Path
import datetime
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-native-surface-interfaces-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    receipt = ROOT / 'RESULT.v1.json'
    result = read(receipt)
    assert len(result['rows']) == result['native_surface_interfaces_composed'] == 6
    assert result['actual_positive_compositions'] == 24 and not result['failures']
    assert result['qualifier_must_never_be_installed'] and not result['renderer_installed']
    pins = dict(result['input_pins'])
    for path, expected in pins.items():
        assert sha(path) == expected
    pins[str(receipt)], pins[str(Path(__file__).resolve())] = sha(receipt), sha(__file__)
    topology = {row['material_guid']: row for row in read(W / 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json')['rows']}
    vertices = {row['material_guid']: row for row in read(W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json')['rows']}
    rows, identities = [], set()
    for row in result['rows']:
        guid = row['material_guid']
        assert guid not in identities
        identities.add(guid)
        assert sha(row['native']) == row['native_sha256']
        assert sha(row['qualifier']) == row['qualifier_sha256']
        text = Path(row['native']).read_text('utf-8-sig')
        qualifier = Path(row['qualifier']).read_text('utf-8-sig')
        assert qualifier.startswith(text) and '// Legality probe, never an installed material.' in qualifier
        for channel in (0, 1):
            assert text.count(f'out.source_uv{channel}=') == 2
            assert f'out.source_uv{channel}=vertex.source_uv{channel};' in text
            assert f'out.source_uv{channel}=frame.source_uv{channel};' in text
            assert f'packet.source_uv{channel}=in.source_uv{channel};' in text
            assert f'output.uv{channel}=packet.source_uv{channel};' in text
        assert '@location(9) source_uv1:vec4<f32>,' in text
        assert '@location(11) source_uv1:vec4<f32>,' in text
        assert '@location(12) source_uv1:vec4<f32>,' in text
        # Recompute closure independently, without importing the builder.
        ir = read(topology[guid]['output'])
        nodes = {node['id']: node for node in ir['nodes']}
        roots = [value for key, value in ir['terminals'].items() if key.startswith('SurfaceDescription.')]
        assert len(roots) == 10
        required = set()
        pending = [value['ref'] for value in roots if 'ref' in value]
        while pending:
            identity = pending.pop()
            if identity in required:
                continue
            required.add(identity)
            pending.extend(entry['value']['ref'] for entry in nodes[identity]['inputs'] if 'ref' in entry['value'])
        assert sorted(required, key=int) == row['surface_dependency_nodes']
        surface = text.split('fn evaluate_original_source_surface(', 1)[1].split('\n}\n', 1)[0]
        statements = dict(re.findall(r'^(let (?:n|pom)(\d+)=.*;)$', surface, re.M))
        selected = {identity: line for line, identity in statements.items()}
        assert set(selected) == required
        original = Path(vertices[guid]['shader']).read_text('utf-8-sig')
        whole = original.split('fn evaluate_original_source_material(', 1)[1].split('\n}\n', 1)[0]
        original_statements = {identity: line for line, identity in re.findall(r'^(let (?:n|pom)(\d+)=.*;)$', whole, re.M)}
        assert all(original_statements[identity] == line for identity, line in selected.items())
        assert 'evaluate_original_source_vertex(' not in surface
        assert 'packet.target_world_from_object=world_from_local;' in text
        stages = row['actual_compositions']
        assert {entry['permutation'] for entry in stages} == {'forward', 'depth', 'normal_depth', 'ortho_normal_depth'}
        for entry in stages:
            case = Path(row['native']).parent
            out, err = case / (entry['permutation'] + '.stdout.log'), case / (entry['permutation'] + '.stderr.log')
            assert entry['actual_exit_code'] == 0
            assert sha(out) == entry['stdout_sha256'] and sha(err) == entry['stderr_sha256']
            assert 'PASS_BEVY0191_NAGA_OIL_FULL_VERTEX_COMPOSITION' in out.read_text('utf-8-sig')
            assert not err.read_bytes()
            pins[str(out)], pins[str(err)] = sha(out), sha(err)
        pins[row['native']], pins[row['qualifier']] = row['native_sha256'], row['qualifier_sha256']
        rows.append(dict(material_guid=guid, surface_node_count=len(required), exact_source_statements_reopened=True,
                         original_uv0_uv1_stage_transport_reopened=True, actual_positive_compositions_reopened=4))
    assert all(sha(path) == expected for path, expected in pins.items())
    evidence = dict(schema='hivemind.delta-native-surface-interface-independent-reopen.v1',
        observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), native_surface_interfaces_reopened=6,
        actual_positive_compositions_reopened=24, rows=rows, input_pins=pins,
        fragment_lighting_adapter_present=False, qualifier_must_never_be_installed=True,
        renderer_installed=False, gpu_rendered=False, full_import=False)
    target = ROOT / 'INDEPENDENT-REOPEN.v1.json'
    with target.open('x', encoding='utf-8') as stream:
        json.dump(evidence, stream, indent=2)
    print(json.dumps(dict(reopened=6, compositions=24, receipt=str(target))))


if __name__ == '__main__':
    main()
