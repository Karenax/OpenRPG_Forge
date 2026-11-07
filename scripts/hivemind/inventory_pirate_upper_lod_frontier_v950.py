"""Route unillustrated upper source LODs without retrying blocked materials."""
from pathlib import Path
import datetime, hashlib, json, re

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W/'pirate-upper-lod-frontier-v950'
pins = {}

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def read(p):
    pins[str(p)] = sha(p)
    return json.loads(Path(p).read_text('utf-8-sig'))

assert not O.exists()
pointer = read(V/'metadata/asset-source-support-registry.v1.json')
catalog_path = V/pointer['path']
assert sha(catalog_path) == pointer['sha256']
catalog = read(catalog_path)['assets']
prior = read(W/'pirate-image-frontier-v815/RESULT.v1.json')
blocked = set(prior['blocked_material_guids'])
queue = read(W/'queue/pirate-island/prefabs.json')
pool = {}
for card in catalog:
    path = card.get('source_asset_path', '')
    if card.get('pack_id') == 'hivemind.pirate-island' and '/HDRP' in path and path.endswith('.prefab'):
        pool.setdefault(path, {})[card.get('original_lod_level')] = card
eligible, held = [], []
for path, rows in sorted(pool.items()):
    selected = [c for level, c in rows.items() if isinstance(level, int) and level >= 3 and not c.get('preview')]
    if not selected:
        continue
    source = Path(queue['source_root'])/path
    assert all(sha(source) == c['source_sha256'] for c in selected)
    pins[str(source)] = sha(source)
    text = source.read_text('utf-8-sig')
    materials = set()
    for match in re.finditer(r'^  m_Materials:\s*\n((?:  -[^\n]*\n)+)', text, re.M):
        materials.update(re.findall(r'guid: ([a-f0-9]{32})', match[1]))
    refs = re.findall(r'^\s+- renderer: \{fileID: (-?\d+)\}\s*$', text, re.M)
    renderers = set(re.findall(r'^--- !u!23 &(-?\d+)', text, re.M))
    reasons = []
    if not materials or not materials <= set(queue['materials']):
        reasons.append('unresolved_inline_material_references')
    if not refs or any(ref != '0' and ref not in renderers for ref in refs):
        reasons.append('unresolved_inline_lod_renderer_references')
    if 0 not in rows:
        reasons.append('no_source_lod0_control')
    affected = materials & blocked
    if affected:
        reasons.append('retained_material_refusal')
    row = dict(source_path=path, source_sha256=sha(source), cards=sorted(selected, key=lambda c:c['original_lod_level']),
               control=rows.get(0), declared_material_guids=sorted(materials), blocked_material_guids=sorted(affected),
               expected_empty_lod_slots=refs.count('0'), reasons=reasons)
    (held if reasons else eligible).append(row)
pins[str(Path(__file__).resolve())] = sha(Path(__file__))
assert all(sha(p) == h for p, h in pins.items())
O.mkdir()
result = dict(status='upper_lod_image_frontier_classified', finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              catalog_revision=pointer['revision'], eligible_groups=eligible, held_groups=held,
              eligible_cards=sum(len(r['cards']) for r in eligible), held_cards=sum(len(r['cards']) for r in held),
              input_pins=pins, blocked_material_guids=sorted(blocked), full_import=False,
              scope='Routing only; native capture, source guards and visual review remain required.')
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps(dict(eligible_cards=result['eligible_cards'], held_cards=result['held_cards'],
                     next_names=[Path(r['source_path']).stem for r in eligible[:8]])))
