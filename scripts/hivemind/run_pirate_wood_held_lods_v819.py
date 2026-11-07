"""Compare two captured but refused wood groups against retained stencil semantics."""
from pathlib import Path
import hashlib, json, sys

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
frontier_root = W/'pirate-wood-held-frontier-v819'
frontier_path = frontier_root/'RESULT.v1.json'
parent = Path(__file__).with_name('run_pirate_wood_lod_migration_v590.py')
ownership = W/'pirate-helper-ownership-recheck-v790/HELPER-OWNERSHIP.v1.json'
guid = 'abc00000000004592200770135964921'

def digest(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def load(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

if len(sys.argv) == 1:
    assert not frontier_root.exists()
    guard_path = W/'pirate-lod-thumbnail-next-guard-v793/RESULT.v1.json'
    guard = load(guard_path)
    prepared_path = W/'pirate-lod-thumbnail-batch-next-v791/PREPARED.v1.json'
    prepared = load(prepared_path)
    pointer = V/'metadata/asset-source-support-registry.v1.json'
    registry = load(pointer)
    catalog_path = V/registry['path']
    assert digest(catalog_path) == registry['sha256']
    catalog = {c['id']:c for c in load(catalog_path)['assets']}
    refused = {r['id'] for r in guard['cases_refused_source_parity'] if r['material_guids'] == [guid]}
    groups = {}
    for case in prepared['cases']:
        if case['id'] not in refused:
            continue
        card = catalog[case['source_card_id']]
        # LOD0 is an existing control, never an indexing candidate. Only the
        # missing LOD1/2 images are being considered for later admission.
        if case['requested_lod'] > 0:
            assert not card.get('preview'), 'Do not overwrite an admitted image'
        groups[card['source_asset_path']] = dict(source_path=card['source_asset_path'], source_sha256=card['source_sha256'], blocked_material_guids=[guid])
    assert len(groups) == 2 and len(refused) == 6
    pins = {str(p):digest(p) for p in [Path(__file__),parent,ownership,guard_path,prepared_path,pointer,catalog_path]}
    frontier_root.mkdir()
    frontier_path.write_text(json.dumps(dict(status='two_exact_wood_groups_routed_from_retained_refusals', catalog_revision=registry['revision'], held_groups=list(groups.values()), input_pins=pins, images_admitted=0, full_import=False), indent=2), 'utf-8')

frontier = load(frontier_path)
for p, h in frontier['input_pins'].items():
    assert digest(p) == h, p
code = parent.read_text('utf-8').replace('v590', 'v819').replace('V590', 'V819')
replacements = {
    "pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json": "pirate-helper-ownership-recheck-v790/HELPER-OWNERSHIP.v1.json",
    "pirate-image-frontier-v589/RESULT.v1.json": "pirate-wood-held-frontier-v819/RESULT.v1.json",
    "assert len(groups)==12": "assert len(groups)==2",
    "== 144": "== 24",
    "range(36)": "range(6)",
    "and a.get('source_asset_path')==path]": "and a.get('source_asset_path')==path and a.get('original_lod_level') in {0,1,2}]",
    "for p in [Path(__file__),parent,base": "for p in [Path(__file__),Path(__file__).with_name('run_pirate_wood_lod_migration_v590.py'),parent,base",
    "groups=12,lod_cases=36,captures=144,views=288": "groups=2,lod_cases=6,captures=24,views=48",
    "Twelve held wood prefab groups": "Two retained-refusal wood prefab groups",
}
for before, after in replacements.items():
    assert before in code, before
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#two-refused-wood-lod-groups', 'exec'), globals())
