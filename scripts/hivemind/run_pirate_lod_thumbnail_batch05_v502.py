"""Capture the next missing LOD images after rechecking private helper ownership."""
from pathlib import Path
import hashlib, json
parent = Path(__file__).with_name('run_pirate_lod_thumbnail_batch03_v368.py')
audit_path = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json')
audit = json.loads(audit_path.read_text('utf-8-sig'))
assert not audit['asset_dll_found']
for helper in audit['helpers']:
    for key in ['private_path', 'controlled_source']:
        with Path(helper[key]).open('rb') as stream:
            assert hashlib.file_digest(stream, 'sha256').hexdigest() == helper['sha256'], helper[key]
project = Path('F:/OpenRPG_ImportWork/unity-library-source-thumbnail-pirate-bench-v2/project')
assert {str(p) for p in (project/'Assets').rglob('*.cs')} == {h['private_path'] for h in audit['helpers']}
assert not any((project/'Assets').rglob('*.dll'))
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-v368', 'pirate-lod-thumbnail-batch05-v502').replace('L08.IMAGE.PIRATE.LOD.03', 'L08.IMAGE.PIRATE.LOD.05')
code = code.replace("for p in [Path(__file__),base", "for p in [parent,audit_path,Path(__file__),base")
exec(compile(code, str(parent) + '#next-reviewed-lod-image-lot', 'exec'), globals())
