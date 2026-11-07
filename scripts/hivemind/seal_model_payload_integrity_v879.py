"""Seal the completed model audit; explicitly excludes sources/licences/render qualification."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

W = Path('F:/OpenRPG_ImportWork')
A = W/'current-model-payload-integrity-v761'
O = W/'model-payload-integrity-delivery-v879'
assert not O.exists()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

result = read(A/'RESULT.v1.json')
assert result['status'] == 'current_registered_model_sha256_verified'
assert result['passed'] == result['checked_unique_payloads'] == 13673 and result['failed'] == 0
assert result['model_cards'] == 14219 and not result['changed_metadata_inputs']
pins = read(A/'INPUT-PINS.v1.json')
for path,digest in pins.items():
    assert sha(path) == digest, path
inventory = read(A/'INVENTORY.v1.json')
expected = {(r['path'],r['sha256']):r['cards'] for r in inventory['files']}
assert len(expected) == 13673 and sum(len(c) for c in expected.values()) == 14219
seen = set();total_bytes = 0
with (A/'OBSERVATIONS.v1.jsonl').open('r',encoding='utf-8') as stream:
    for line in stream:
        row = json.loads(line)
        key = row['path'],row['expected_sha256']
        assert key in expected and key not in seen
        assert row['cards'] == expected[key]
        assert row['status'] == 'sha256_verified' and row['actual_sha256'] == key[1]
        assert Path(key[0]).resolve().is_relative_to(Path('F:/OpenRPG_AssetVault').resolve())
        seen.add(key);total_bytes += row['bytes']
assert seen == set(expected) and total_bytes == result['bytes_read']
for path in [Path(__file__),A/'RESULT.v1.json',A/'INVENTORY.v1.json',A/'OBSERVATIONS.v1.jsonl',A/'INPUT-PINS.v1.json']:
    pins[str(path)] = sha(path)
seal = dict(status='completed_model_payload_sha_audit_evidence_reopened_and_sealed',
            model_cards=14219,unique_payloads_verified=13673,failures=0,bytes_read=total_bytes,
            input_and_proof_pins=pins,audit_finished_utc=result['finished_utc'],
            scope='All current registered model payloads in F Vault at audit time; metadata unchanged at sealing',
            all_original_sources_and_licenses_rehashed=False,entire_vault_transfer_reverified=False,
            gltf_semantics_revalidated=False,actual_studio_gui_verified=False,full_import=False,
            complete_conversion_increment=0,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(seal,indent=2),'utf-8')
print(json.dumps({k:v for k,v in seal.items() if k != 'input_and_proof_pins'}))
