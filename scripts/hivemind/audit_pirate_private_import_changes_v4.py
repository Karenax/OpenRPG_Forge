"""Inventory importer changes; do not qualify images or modify originals."""
from pathlib import Path
from datetime import datetime, timezone
import json, hashlib, difflib, collections, re
W=Path('F:/OpenRPG_ImportWork')
CAP=W/'pirate-source-thumbnail-batch-0073-0096-v1'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
SOURCE=W/'hivemind-run/pirate-island/source'
ROOT=W/'pirate-private-import-change-audit-v4'
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
assert not ROOT.exists()
prep=json.loads((CAP/'PREPARED.v1.json').read_text(encoding='utf-8-sig'))
ROOT.mkdir()
prior_path=CAP/'PRIVATE-EPOCH.v1.json'
prior={c['live_path']:c for c in json.loads(prior_path.read_text(encoding='utf-8'))['private_input_snapshots']}
changes=[]; pins={}; originals=0
for name, expected in prep['input_pins'].items():
    p=Path(name); actual=sha(p); pins[name]=actual
    if p.is_relative_to(SOURCE):
        assert actual==expected, 'ORIGINAL_CHANGED '+name
        originals+=1
    if actual!=expected:
        assert p.is_relative_to(PROJECT), 'NON_PRIVATE_CHANGED '+name
        row=dict(path=name,prior_sha256=expected,current_sha256=actual,extension=p.suffix)
        original=SOURCE/p.relative_to(PROJECT)
        if original.exists():
            row['purchased_source_path']=str(original)
            if sha(original)!=expected:
                c=prior[name];assert c['sha256']==expected
                snapshot=Path(c['snapshot']);assert sha(snapshot)==expected
                row['prior_exact_snapshot']=str(snapshot);pins[str(snapshot)]=expected
                original=snapshot
            row['original_path']=str(original)
            row['diff']=''.join(difflib.unified_diff(original.read_text(encoding='utf-8-sig').splitlines(True),p.read_text(encoding='utf-8-sig').splitlines(True)))
        changes.append(row)
assert originals>0
pins[str(prior_path)]=sha(prior_path)
result=dict(status='private_import_changes_inventoried_not_adjudicated',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L08.IMAGE.PIRATE.04',pins_checked=len(pins),original_source_files_rehashed=originals,original_sources_unchanged=True,changes=changes,changed_extension_counts=dict(collections.Counter(c['extension'] for c in changes)),input_pins=pins,visual_review_qualified=False,images_installed_in_vault=False,full_import=False)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('input_pins','changes')}))
print('CHANGED_FILES',len(changes))
for c in changes: print(c['path'])
