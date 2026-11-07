"""Resolve the previous mutable registry pin to its exact retained pre-transaction bytes."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'house-registry-epoch-0041-0048-v1'
assert not ROOT.exists()
old_proof = W / 'house-source-thumbnail-batch-0033-0040-delivery-reopen-v1/RESULT.v1.json'
registry = Path('F:/OpenRPG_AssetVault/metadata/asset-source-support-registry.v1.json')
snapshot = W / 'house-source-thumbnail-batch-0041-0048-index-v1/prior-registry.json'


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


proof = json.loads(old_proof.read_text(encoding='utf-8'))
expected = proof['input_pins'][str(registry)]
assert sha(snapshot) == expected and sha(registry) != expected
ROOT.mkdir()
(ROOT / 'RESULT.v1.json').write_text(json.dumps({'status': 'previous_library_registry_epoch_resolved_to_exact_snapshot',
    'finished_utc': datetime.now(timezone.utc).isoformat(), 'historical_proof': str(old_proof), 'historical_proof_sha256': sha(old_proof),
    'source_epoch': {str(registry): {'historical_sha256': expected, 'snapshot': str(snapshot)}},
    'current_registry_sha256': sha(registry), 'historical_receipts_modified': False, 'full_import': False}, indent=2), encoding='utf-8')
