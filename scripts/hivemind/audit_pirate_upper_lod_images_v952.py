"""Apply the existing source/material guard to the bounded upper-LOD capture."""
from pathlib import Path
import hashlib, json

W = Path('F:/OpenRPG_ImportWork')
CAP = W/'pirate-upper-lod-images-v951'

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

ownership_path = CAP/'HELPER-OWNERSHIP.v1.json'
ownership = read(ownership_path)
project = W/'unity-library-source-thumbnail-pirate-bench-v2/project'
assert not ownership['asset_dll_found']
assert {str(p) for p in (project/'Assets').rglob('*.cs')} == {h['private_path'] for h in ownership['helpers']}
assert not any((project/'Assets').rglob('*.dll'))
for helper in ownership['helpers']:
    for key in ('private_path', 'controlled_source'):
        assert sha(helper[key]) == helper['sha256'], helper[key]
frontier_path = W/'pirate-upper-lod-frontier-v950/RESULT.v1.json'
frontier = read(frontier_path)
prep = read(CAP/'PREPARED.v1.json')
assert prep['catalog_revision'] == frontier['catalog_revision']
assert len(prep['candidate_ids']) == 24
parent = Path(__file__).with_name('audit_and_guard_pirate_lod_batch03_v369.py')
code = parent.read_text('utf-8')
code = code.replace('pirate-lod-thumbnail-batch03-v368', 'pirate-upper-lod-images-v951')
code = code.replace('pirate-lod-thumbnail-batch03-guard-v369', 'pirate-upper-lod-images-guard-v952')
assert "==len(prep['cases'])==24" in code
code = code.replace("==len(prep['cases'])==24", "==len(prep['cases'])==39")
anchor = "changes=[];blocked={'a441b81a4efaff04a85744086c47c402'}"
assert anchor in code
code = code.replace(anchor, anchor+"\nblocked.update(frontier['blocked_material_guids'])")
code = code.replace("pins[str(Path(__file__))]=sha(Path(__file__))",
                    "pins[str(Path(__file__))]=sha(Path(__file__))\n"
                    "for p in [parent,frontier_path,ownership_path]:pins[str(p)]=sha(p)")
exec(compile(code, str(parent)+'#upper-lods-preserved-source-guard', 'exec'), globals())
