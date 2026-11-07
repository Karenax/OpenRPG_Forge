"""Add an absent optional pin to existing placement constructors; preserve before bytes."""
from pathlib import Path
import hashlib, json, re
R=Path(__file__).resolve().parents[2]
O=Path('F:/OpenRPG_ImportWork/manual-physics-reference-change-v770')
assert not O.exists();O.mkdir()
pattern=re.compile(r'\bManualPlacement \{(\r?\n)([ \t]*)(?=(?:id|name|asset_id|asset_hash|transform|locked)\s*[:,])')
changes=[]
for folder in ['crates','apps']:
    for p in (R/folder).rglob('*.rs'):
        before=p.read_bytes();text=before.decode('utf-8')
        updated,count=pattern.subn(lambda m:'ManualPlacement {'+m[1]+m[2]+'physics: None,'+m[1]+m[2],text)
        if count:
            relative=p.relative_to(R);backup=O/'before'/relative
            backup.parent.mkdir(parents=True,exist_ok=True);backup.write_bytes(before)
            p.write_bytes(updated.encode('utf-8'))
            changes.append(dict(path=str(p),constructors=count,before_sha256=hashlib.sha256(before).hexdigest(),after_sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(O/'CONSTRUCTOR-CHANGES.v1.json').write_text(json.dumps(changes,indent=2),'utf-8')
print(json.dumps(dict(files=len(changes),constructors=sum(c['constructors'] for c in changes))))
