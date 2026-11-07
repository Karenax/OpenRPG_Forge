"""Measure declared LOD groups without inventing a uniform level for assemblies."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W/'pirate-source-lod-hierarchy-v959'
PROJECT = W/'unity-library-source-thumbnail-pirate-bench-v2/project'
pins = {}

def sha(path):
    with Path(path).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def read(path):
    pins[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def reference(block, key):
    match = re.search(r'^  '+re.escape(key)+r': \{fileID: (-?\d+)\}', block, re.M)
    assert match is not None, key
    return match[1]

assert not O.exists()
registry = read(V/'metadata/asset-source-support-registry.v1.json')
catalog_path = V/registry['path']
assert sha(catalog_path) == registry['sha256']
catalog = read(catalog_path)['assets']
missing = [c for c in catalog if c.get('pack_id') == 'hivemind.pirate-island' and not c.get('preview')]
assert len(missing) == 159
pool = {}
for card in missing:
    pool.setdefault(card['source_asset_path'], []).append(card)
prefabs, cards = [], []
for path, entries in sorted(pool.items()):
    source = V/entries[0]['source_file']
    digest = sha(source)
    assert all(c['source_sha256'] == digest for c in entries)
    pins[str(source)] = digest
    private = PROJECT/path
    assert sha(private) == digest
    pins[str(private)] = digest
    text = source.read_text('utf-8-sig')
    matches = list(re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$', text, re.M))
    blocks = {m[2]:(int(m[1]), text[m.end():matches[i+1].start() if i+1<len(matches) else len(text)]) for i,m in enumerate(matches)}
    objects, transforms, by_object = {}, {}, {}
    for file_id, (kind, block) in blocks.items():
        if kind == 1:
            name = re.search(r'^  m_Name: (.*)$', block, re.M)
            assert name
            objects[file_id] = name[1]
        if kind == 4:
            go = reference(block, 'm_GameObject')
            transforms[file_id] = dict(game_object=go, parent=reference(block, 'm_Father'))
            assert go not in by_object
            by_object[go] = file_id
    def object_path(go):
        ancestry, visited = [], set()
        transform = by_object[go]
        while transform != '0':
            assert transform not in visited and transform in transforms
            visited.add(transform)
            node = transforms[transform]
            ancestry.append(objects[node['game_object']])
            transform = node['parent']
        return '/'.join(reversed(ancestry))
    groups = []
    for file_id, (kind, block) in blocks.items():
        if kind != 205:
            continue
        go = reference(block, 'm_GameObject')
        lod_starts = list(re.finditer(r'^  - screenRelativeHeight: ([^\n]+)$', block, re.M))
        levels = []
        for level, start in enumerate(lod_starts):
            segment = block[start.end():lod_starts[level+1].start() if level+1<len(lod_starts) else len(block)]
            renderers = re.findall(r'^    - renderer: \{fileID: (-?\d+)\}', segment, re.M)
            assert all(r == '0' or r in blocks and blocks[r][0] in (23,137) for r in renderers)
            levels.append(dict(index=level, screen_relative_height=float(start[1]), renderer_file_ids=renderers,
                               empty_slots=renderers.count('0')))
        assert levels
        groups.append(dict(component_file_id=file_id, game_object_file_id=go, hierarchy_path=object_path(go),
                           declared_level_count=len(levels), levels=levels))
    prefabs.append(dict(source_path=path, source_sha256=digest, source_lod_groups=groups,
                        distinct_level_counts=sorted({len(g['levels']) for g in groups}),
                        declared_group_count=len(groups)))
    for card in entries:
        level = card.get('original_lod_level')
        absent = [g['component_file_id'] for g in groups if not isinstance(level,int) or level >= len(g['levels'])]
        cards.append(dict(id=card['id'], source_path=path, requested_lod=level,
                          groups_without_requested_lod=absent, source_group_count=len(groups),
                          uniform_requested_lod_declared=bool(groups) and not absent))
run = read(W/'pirate-upper-lod-images-v951/RUN-RESULT.v1.json')
by_id = {c['id']:c for c in cards}
for refusal in run['refusals']:
    row = by_id[refusal['id']]
    assert row['groups_without_requested_lod'] and row['source_group_count'] > 1
    assert 'Requested LOD absent in source group' in refusal['error']
    row['native_refusal_correlated'] = True
pins[str(Path(__file__).resolve())] = sha(Path(__file__))
assert all(sha(path) == digest for path,digest in pins.items())
O.mkdir()
result = dict(status='remaining_pirate_source_lod_hierarchies_measured', finished_utc=datetime.now(timezone.utc).isoformat(),
              catalog_revision=registry['revision'], source_cards_examined=159, prefabs_examined=len(prefabs),
              cards_with_uniform_declared_lod=sum(c['uniform_requested_lod_declared'] for c in cards),
              cards_without_uniform_declared_lod=sum(not c['uniform_requested_lod_declared'] for c in cards),
              prefabs=prefabs, cards=cards, native_refusals_correlated=2, input_pins=pins,
              sources_modified=False, registry_modified=False, full_import=False,
              scope='Serialized source hierarchy and references only. Mixed assemblies require per-group LOD preservation; no clamping, LOD0 replacement, removal, conversion or runtime qualification.')
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in {'prefabs','cards','input_pins'}}))
