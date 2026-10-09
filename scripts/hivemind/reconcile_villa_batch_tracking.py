"""Correct stale batch tracking from retained terminal receipts, without GPU claims."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
ROOT = W / 'villa-batch-tracking-reconciliation-v1'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Previous reconciliation preserved'
    path = T / 'SOUS_LOTS_VILLA.v1.json'
    original = path.read_bytes()
    batches = json.loads(original.decode('utf-8-sig'))
    pins = {str(Path(__file__).resolve()): sha(__file__)}
    evidence, seen = [], set()
    for batch in batches['batches']:
        if batch['id'] not in {f'L04.VILLA.{i:02}' for i in range(1,12)}:
            continue
        receipt = W / ('villa-native-source-frame-bound-' + batch['id'].lower().replace('.', '-') + '-v1/RUN-RESULT.v1.json')
        frame = read(receipt)
        pins[str(receipt)] = sha(receipt)
        assert frame['status'] == 'native_original_source_frames_bound_and_reopened'
        assert frame['batch_id'] == batch['id']
        ids = [row['asset_id'] for row in frame['rows']]
        assert len(ids) == len(set(ids)) == batch['expected_scenes']
        assert set(ids) == set(batch['asset_ids'])
        assert not seen.intersection(ids)
        seen.update(ids)
        assert batch['studio_runtime_qualified'] == 0
        before = dict(state=batch['state'], verified_derivatives=batch['verified_derivatives'],
                      source_frames_verified=batch.get('source_frames_verified', 0))
        batch.update(state='assemblage_et_reperes_source_verifies_rendu_et_studio_restants',
                     verified_derivatives=len(ids), source_frames_verified=len(ids),
                     source_frame_receipt=str(receipt), source_frame_receipt_sha256=pins[str(receipt)])
        evidence.append(dict(id=batch['id'], expected=len(ids), previous_tracking=before,
                             receipt=str(receipt), receipt_sha256=pins[str(receipt)]))
    assert len(evidence) == 11 and len(seen) == 207
    assert all(sha(p) == h for p,h in pins.items())
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    batches['tracking_reconciled_utc'] = now
    batches['tracking_reconciliation_scope'] = 'terminal_receipt_metadata_and_exact_asset_ids; no fresh derivative payload or GPU validation'
    ROOT.mkdir()
    (ROOT / 'PREVIOUS-SOUS_LOTS_VILLA.v1.json').write_bytes(original)
    result = dict(status='eleven_villa_batch_tracking_states_reconciled', batches=evidence,
                  expected_derivatives=207, historical_receipt_ids_reconciled=207,
                  derivative_payloads_reopened_now=False, new_assets_validated=0,
                  gpu_verified=False, studio_verified=False, full_import=False,
                  input_pins=pins, original_tracking_sha256=hashlib.sha256(original).hexdigest(), recorded_utc=now)
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    assert path.read_bytes() == original, 'Concurrent tracking change preserved'
    temporary = path.with_suffix('.json.reconcile.tmp')
    temporary.write_text(json.dumps(batches, indent=2, ensure_ascii=False), 'utf-8')
    os.replace(temporary, path)
    print(json.dumps(dict(status=result['status'], batches=11, historical_derivatives=207, new_assets_validated=0)))


if __name__ == '__main__':
    main()
