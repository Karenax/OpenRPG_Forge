"""Capture the next sixteen missing original LOD thumbnails, preserving refusals."""
from pathlib import Path
import hashlib, json

parent = Path(__file__).with_name('run_pirate_lod_thumbnail_batch03_v368.py')
audit_path = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json')
audit = json.loads(audit_path.read_text('utf-8-sig'))
assert audit['status'] == 'private_helpers_match_repository_or_retained_bench_sources'
assert not audit['asset_dll_found']
for helper in audit['helpers']:
    for key in ['private_path', 'controlled_source']:
        with Path(helper[key]).open('rb') as stream:
            assert hashlib.file_digest(stream, 'sha256').hexdigest() == helper['sha256'], helper[key]
project = Path('F:/OpenRPG_ImportWork/unity-library-source-thumbnail-pirate-bench-v2/project')
assert {str(p) for p in (project/'Assets').rglob('*.cs')} == {h['private_path'] for h in audit['helpers']}
assert not any((project/'Assets').rglob('*.dll'))
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-v368', 'pirate-lod-thumbnail-batch06-v789').replace('L08.IMAGE.PIRATE.LOD.03', 'L08.IMAGE.PIRATE.LOD.06')
code = code.replace('for p in [Path(__file__),base', 'for p in [parent,audit_path,Path(__file__),base')
exec(compile(code, str(parent)+'#next-reviewed-lod-image-lot', 'exec'), globals())
# The parent returns after dispatching its one-shot controller. Preserve the
# checked ownership receipt for the later source/material guard.
output = Path('F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch06-v789')
receipt = output/'HELPER-OWNERSHIP.v1.json'
assert not receipt.exists()
receipt.write_bytes(audit_path.read_bytes())
