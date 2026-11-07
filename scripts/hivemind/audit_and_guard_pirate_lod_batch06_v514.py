"""Recheck helper ownership and retain source/material guards for batch06."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
recipe = Path(__file__).with_name('audit_and_guard_pirate_lod_batch04_v493.py')
cap = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch06-v513')
audit_path = cap/'HELPER-OWNERSHIP.v1.json'
assert cap.exists() and not audit_path.exists()
previous = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json')
audit = json.loads(previous.read_text('utf-8-sig'))
assert not audit['asset_dll_found']
for helper in audit['helpers']:
    for key in ['private_path', 'controlled_source']:
        with Path(helper[key]).open('rb') as stream:
            assert hashlib.file_digest(stream, 'sha256').hexdigest() == helper['sha256'], helper[key]
project = Path('F:/OpenRPG_ImportWork/unity-library-source-thumbnail-pirate-bench-v2/project')
assert {str(p) for p in (project/'Assets').rglob('*.cs')} == {h['private_path'] for h in audit['helpers']}
assert not any((project/'Assets').rglob('*.dll'))
audit.update(checked_utc=datetime.now(timezone.utc).isoformat(), previous_audit=str(previous), batch='pirate-lod-thumbnail-batch06-v513')
audit_path.write_text(json.dumps(audit, indent=2), 'utf-8')
code = recipe.read_text('utf-8').replace('pirate-lod-thumbnail-batch04-v491', 'pirate-lod-thumbnail-batch06-v513').replace('pirate-lod-thumbnail-batch04-guard-v493', 'pirate-lod-thumbnail-batch06-guard-v514')
exec(compile(code, str(recipe)+'#lod06-source-guard', 'exec'), globals())
