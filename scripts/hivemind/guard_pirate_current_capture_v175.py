"""Admit only captured cards unaffected by unclassified private material changes."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'pirate-source-thumbnail-batch-0113-0120-v163'
O = W / 'pirate-source-thumbnail-batch-0113-0120-source-guard-v175'
pins = {}

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    pins[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not O.exists()
    run = read(CAP / 'RUN-RESULT.v1.json')
    prep = read(CAP / 'PREPARED.v1.json')
    batch = read(CAP / 'NATIVE-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['status'] == 'house_thumbnail_batch_refusal_retained'
    audit = read(W / 'pirate-private-import-change-audit-v173/RESULT.v1.json')
    assert audit['original_sources_unchanged'] and not audit['private_changes_classified']
    for path, digest in audit['input_pins'].items():
        assert sha(path) == digest, path
        pins[path] = digest
    changed = set()
    for row in audit['changes']:
        assert row['extension'] == '.mat'
        meta = Path(row['path']).with_suffix('.mat.meta')
        pins[str(meta)] = sha(meta)
        guid = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text('utf-8-sig'), re.M)
        assert guid
        changed.add(guid.group(1))
    historical = set()
    for version in range(1, 6):
        path = W / f'pirate-private-material-migrations-reopen-v{version}/RESULT.v1.json'
        if not path.exists(): continue
        for row in read(path).get('material_changes', []):
            if row.get('alpha_to_mask_keyword_moved_to_invalid'):
                meta = Path(row['path']).with_suffix('.mat.meta')
                pins[str(meta)] = sha(meta)
                guid = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text('utf-8-sig'), re.M)
                assert guid
                historical.add(guid.group(1))
    assert len(prep['cases']) == len(batch['cases']) == 8
    assert [c['id'] for c in batch['cases']] == [c['id'] for c in prep['cases']]
    refused = []; admitted = []
    for case in batch['cases']:
        if not case['captured']:
            refused.append(dict(id=case['id'], stage='native_capture', reason=case['error']))
            continue
        native = read(case['receipt'])
        assert native['id'] == case['id'] and native['source_object_unchanged'] and native['preview_scene_isolated']
        assert native['pipeline'] == 'UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset' and native['gpu'] != 'Null'
        used = {t['material'].split(':')[0] for t in native['textures']}
        for renderer in native['renderers']:
            for material in renderer.get('materials', []):
                used.add((material if isinstance(material, str) else material.get('identity', '')).split(':')[0])
        for image in native['images']:
            assert sha(image['path']) == image['sha256']
            pins[image['path']] = image['sha256']
        affected = used & (changed | historical)
        if affected:
            refused.append(dict(id=case['id'], stage='source_material_parity', material_guids=sorted(affected), reason='Private source shader defaults or historical alpha changes require dedicated qualification'))
        else: admitted.append(case['id'])
    pins[str(Path(__file__).resolve())] = sha(Path(__file__).resolve())
    result = dict(status='current_capture_source_pins_and_historical_alpha_dependencies_verified', lot='L08.IMAGE.PIRATE.07', source_inputs_unchanged=True, source_pin_verification_performed_by_capture_controller=False, source_pin_verification_performed_by_independent_audit=True, private_project_byte_unchanged=False, original_capture_refusal_retained=True, alpha_keyword_changes_not_used_by_admitted_cases=True, changed_private_materials_not_used_by_admitted_cases=True, cases_refused_source_parity=refused, admitted_ids=admitted, input_pins=pins, source_version_parity_qualified=False, full_import=False, finished_utc=datetime.now(timezone.utc).isoformat())
    O.mkdir(); (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(admitted=len(admitted), refused=len(refused), proof=str(O / 'RESULT.v1.json'))))

if __name__ == '__main__': main()
