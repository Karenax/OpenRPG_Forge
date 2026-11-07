"""Inventory importer changes; do not qualify images or modify originals."""
from pathlib import Path
from datetime import datetime, timezone
import json, hashlib, difflib, collections, re
W=Path('F:/OpenRPG_ImportWork')
CAP=W/'pirate-source-thumbnail-batch-0025-0048-v1'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
SOURCE=W/'hivemind-run/pirate-island/source'
ROOT=W/'pirate-private-import-change-audit-v2'
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
assert not ROOT.exists()
prep=json.loads((CAP/'PREPARED.v1.json').read_text(encoding='utf-8-sig'))
ROOT.mkdir()
prior_path=W/'pirate-private-import-change-audit-v1/RESULT.v1.json'
prior={c['path']:c for c in json.loads(prior_path.read_text(encoding='utf-8'))['changes']}
def reconstruct(original, delta, expected):
    source=original.read_text(encoding='utf-8-sig').splitlines(); lines=delta.splitlines(); out=[]; pos=0; i=2
    while i<len(lines):
        match=re.match(r'@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@',lines[i]);assert match,lines[i]
        start=int(match.group(1))-1;assert start>=pos;out.extend(source[pos:start]);pos=start;i+=1
        while i<len(lines) and not lines[i].startswith('@@ '):
            line=lines[i];assert line and line[0] in ' +-';content=line[1:]
            if line[0] in ' -':assert source[pos]==content;pos+=1
            if line[0] in ' +':out.append(content)
            i+=1
    out.extend(source[pos:])
    for nl in ['\n','\r\n']:
        for ending in [True,False]:
            for bom in [b'',b'\xef\xbb\xbf']:
                raw=bom+(nl.join(out)+(nl if ending else '')).encode('utf-8')
                if hashlib.sha256(raw).hexdigest()==expected:return raw
    raise AssertionError('Cannot reconstruct exact pinned bytes')
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
                c=prior[name];assert c['current_sha256']==expected
                raw=reconstruct(original,c['diff'],expected)
                snapshot=ROOT/'prior-private'/p.relative_to(PROJECT);snapshot.parent.mkdir(parents=True,exist_ok=True);snapshot.write_bytes(raw)
                assert sha(snapshot)==expected
                row['prior_exact_snapshot']=str(snapshot);pins[str(snapshot)]=expected
                original=snapshot
            row['original_path']=str(original)
            row['diff']=''.join(difflib.unified_diff(original.read_text(encoding='utf-8-sig').splitlines(True),p.read_text(encoding='utf-8-sig').splitlines(True)))
        changes.append(row)
assert originals>0
pins[str(prior_path)]=sha(prior_path)
result=dict(status='private_import_changes_inventoried_not_adjudicated',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L08.IMAGE.PIRATE.02',pins_checked=len(pins),original_source_files_rehashed=originals,original_sources_unchanged=True,changes=changes,changed_extension_counts=dict(collections.Counter(c['extension'] for c in changes)),input_pins=pins,visual_review_qualified=False,images_installed_in_vault=False,full_import=False)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('input_pins','changes')}))
print('CHANGED_FILES',len(changes))
for c in changes: print(c['path'])
