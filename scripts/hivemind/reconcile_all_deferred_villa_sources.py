"""Reopen deferred scenes and compare their authored material/program identities.

This only establishes source identity coverage for the native defaults probe.
It does not establish effective Unity bindings or GPU/render equivalence.
"""
import datetime
import hashlib
import json
from pathlib import Path
import struct

WORK = Path('F:/OpenRPG_ImportWork')
DIAGNOSIS = WORK / 'villa-deferred-builtin-normal-diagnosis-v1/RESULT.v1.json'
FIRST = WORK / 'villa-builtin-normal-source-reconciliation-v1/RESULT.v1.json'
ROOT = WORK / 'villa-all-deferred-source-identity-reopen-v1'


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main():
    assert not ROOT.exists(), 'Preserve any prior receipt'
    pins = {str(p): sha(p) for p in (DIAGNOSIS, FIRST, Path(__file__))}
    first = json.loads(FIRST.read_text('utf-8-sig'))
    for path, expected in first['source_pins'].items():
        assert sha(path) == expected, 'Original authored source changed'
        pins[path] = expected
    material_paths = [p for p in first['source_pins'] if p.endswith('.mat')]
    program_paths = [p for p in first['source_pins'] if p.endswith('.shadergraph')]
    assert len(material_paths) == len(program_paths) == 1
    expected_material = pins[material_paths[0]]
    expected_program = pins[program_paths[0]]
    diagnosis = json.loads(DIAGNOSIS.read_text('utf-8-sig'))
    assert len(diagnosis['rows']) == 19
    rows = []
    for row in diagnosis['rows']:
        path = Path(row['glb'])
        before = sha(path)
        assert before == row['expected_original_glb_sha256'], 'Native GLB identity changed'
        with path.open('rb') as stream:
            header = stream.read(20)
            magic, version, total, size, chunk = struct.unpack('<4sIIII', header)
            assert magic == b'glTF' and version == 2 and chunk == 0x4E4F534A
            assert total == path.stat().st_size
            raw = stream.read(size)
            assert hashlib.sha256(raw).hexdigest() == row['header_sha256']
            document = json.loads(raw)
        contract = document['materials'][row['material_index']]['extensions']['OPENRPG_source_graph']
        same = (contract['source_material_sha256'] == expected_material
                and contract['source_program_sha256'] == expected_program)
        assert sha(path) == before, 'Scene changed during reopening'
        rows.append({'asset_id': row['asset_id'], 'material_index': row['material_index'],
                     'glb': str(path), 'full_glb_sha256': before,
                     'source_material_sha256': contract['source_material_sha256'],
                     'source_program_sha256': contract['source_program_sha256'],
                     'matches_first_authored_material_and_shader': same})
    assert len({(r['asset_id'], r['material_index']) for r in rows}) == 19
    for path, expected in pins.items():
        assert sha(path) == expected, 'Pinned input changed'
    matches = sum(r['matches_first_authored_material_and_shader'] for r in rows)
    result = {'schema': 'openrpg.deferred-source-identity-reopen.v1',
              'status': 'all_deferred_source_identities_reopened',
              'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'source_pins': pins, 'rows': rows, 'full_glbs_reopened': len(rows),
              'same_authored_material_and_program_as_first_probe': matches,
              'other_source_identities_requiring_separate_probes': len(rows) - matches,
              'native_effective_bindings_observed': False, 'gpu_render_qualified': False,
              'studio_verified': False, 'full_import': False}
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('rows', 'source_pins')}))


if __name__ == '__main__':
    main()
