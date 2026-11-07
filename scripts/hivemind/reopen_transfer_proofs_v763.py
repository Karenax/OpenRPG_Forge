"""Reopen retained transfer proofs; do not promote them to current payload validation."""
from pathlib import Path
import datetime, hashlib, json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault').resolve()
O=W/'historical-vault-transfer-proof-reopen-v763'
assert not O.exists()
def sha(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def read(path):return json.loads(Path(path).read_text('utf-8-sig'))
ledger_review=W/'vault-transfer-ledger-review-v1/RESULT.v1.json'
reopened=W/'vault-final-integrity-independent-reopen-v4/RESULT.v1.json'
transfer=read(ledger_review);prior=read(reopened)
checks={transfer['ledger']:transfer['ledger_sha256'],transfer['summary']:transfer['summary_sha256'],prior['source_receipt']:prior['source_receipt_sha256']}
observations=[]
for path,expected in checks.items():
    actual=sha(path)
    observations.append(dict(path=path,expected_sha256=expected,actual_sha256=actual,ok=actual==expected))
assert all(row['ok'] for row in observations)
receipt=read(prior['source_receipt'])
assert receipt['whole_current_vault_qualified'] and not receipt['full_import']
assert Path('E:/OpenRPG_AssetVault').resolve()==V
O.mkdir()
result=dict(status='historical_transfer_receipts_and_ledgers_reopened',observations=observations,
    reopened_receipt_utc=receipt['finished_utc'],historical_transferred_files=transfer['verified_records'],
    historical_transferred_bytes=transfer['verified_recorded_bytes'],historical_storage_files=receipt['physical_files'],
    e_vault_resolves_to=str(V),current_payloads_rehashed=False,current_entire_vault_qualified=False,
    studio_visual_verified=False,full_import_increment=0,utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
summary=f'''## Preuves du transfert historique réouvertes (v763)

- Journal et résumé du transfert E:→F: relus avec leurs SHA attendus : {transfer['verified_records']} fichiers et {transfer['verified_recorded_bytes']} octets, conformes aux preuves conservées. Receipt du contrôle global antérieur relu avec son SHA épinglé ; il décrit {receipt['physical_files']} fichiers physiques à sa date ({receipt['finished_utc']}). Aucun nouveau transfert ni suppression.
- Chemin E:/OpenRPG_AssetVault résolu vers F:/OpenRPG_AssetVault, jonction confirmée. Les preuves historiques restent utilisables ; leur réouverture ne vaut pas rehash des fichiers actuels ni qualification de la conversion ou du produit.
- Preuve actuelle : F:/OpenRPG_ImportWork/historical-vault-transfer-proof-reopen-v763/RESULT.v1.json. Audit SHA des modèles courants v761 et test natif du formulaire v757 en cours au dernier relevé ; observer leurs RESULT/PID avant modification ou relance. Objectif complet actif, incrément full-import 0.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    original=p.read_bytes();(O/name).write_bytes(original)
    p.write_text(summary+original.decode('utf-8-sig'),'utf-8')
print(json.dumps(result))
