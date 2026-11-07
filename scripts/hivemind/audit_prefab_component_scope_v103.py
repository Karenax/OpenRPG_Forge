"""Read and hash source prefab components for lot selection; never execute them."""
from pathlib import Path
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
BASE = W / 'collection-source-type-scope-v102'
O = W / 'collection-prefab-component-scope-v103'
HEADER = re.compile(r'^--- !u!(-?\d+) &(-?\d+)(?: stripped)?\r?\n([A-Za-z0-9_]+):', re.M)
GUID = re.compile(r'\bguid:\s*([0-9a-fA-F]{32})\b')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


assert not O.exists()
proof = read(BASE / 'RESULT.v1.json')
for path, expected in proof['input_pins'].items():
    assert sha(Path(path)) == expected, path
source = read(BASE / 'SOURCE_OBJECT_CANDIDATES.v1.json')
prefabs = [row for row in source if row['extension'] == '.prefab']
assert len(prefabs) == 8736
graph_guids = defaultdict(set)
graph_rows = []
lineage = read(W / 'collection-archive-lineage-reconciliation-v1/RESULT.v1.json')
for scope in lineage['manifests']:
    path = Path(scope['manifest'])
    assert sha(path) == scope['manifest_sha256'], str(path)
    for item in read(path)['files']:
        if item['kind'] == 'asset' and Path(item['source_path']).suffix.lower() == '.vfx':
            graph_guids[scope['pack_id']].add(item['guid'])
            graph_rows.append(dict(pack_id=scope['pack_id'], guid=item['guid'],
                                   source_path=item['source_path'], declared_sha256=item['sha256'],
                                   source_payload_rehashed=False, graph_semantics_verified=False))
rows = []
all_components = Counter()
pack_counts = defaultdict(Counter)
source_pins = {}
refusals = []
for source_row in prefabs:
    path = Path(source_row['stored_path'])
    try:
        raw = path.read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        assert digest == source_row['declared_sha256'], 'Source prefab hash differs from manifest'
        source_pins[str(path)] = digest
        text = raw.decode('utf-8-sig', errors='strict')
        assert text.startswith('%YAML'), 'Source prefab is not Unity text YAML'
        components = Counter(header[2] for header in HEADER.findall(text))
        assert components, 'Unity object headers absent'
        refs = sorted(set(g.lower() for g in GUID.findall(text)))
        vfx_refs = sorted(set(refs) & graph_guids[source_row['pack_id']])
        flags = dict(
            particle_system='ParticleSystem' in components,
            visual_effect_component='VisualEffect' in components,
            referenced_vfx_graph=bool(vfx_refs),
            skinned_mesh='SkinnedMeshRenderer' in components,
            animation_component='Animator' in components or 'Animation' in components,
            custom_script_component='MonoBehaviour' in components,
            nested_prefab='PrefabInstance' in components,
            static_mesh_component='MeshRenderer' in components or 'MeshFilter' in components,
            lod_group='LODGroup' in components,
            collision_component=any(name.endswith('Collider') for name in components),
            terrain_component='Terrain' in components,
            audio_source='AudioSource' in components,
        )
        flags['effect_candidate'] = any(flags[key] for key in ['particle_system', 'visual_effect_component', 'referenced_vfx_graph'])
        flags['rig_or_animation_candidate'] = flags['skinned_mesh'] or flags['animation_component']
        all_components.update(components)
        pack_counts[source_row['pack_id']]['examined'] += 1
        for flag, present in flags.items():
            pack_counts[source_row['pack_id']][flag] += int(present)
        rows.append({**source_row, 'source_payload_rehashed': True,
                     'component_counts': dict(sorted(components.items())), 'classification_flags': flags,
                     'referenced_guids': refs, 'referenced_vfx_graph_guids': vfx_refs,
                     'nested_dependencies_resolved': False, 'conversion_verified': False})
    except Exception as error:
        refusals.append(dict(pack_id=source_row['pack_id'], guid=source_row['guid'],
                             stored_path=str(path), reason=repr(error)))
O.mkdir()
output = O / 'PREFAB_COMPONENTS.v1.json'
output.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(O / 'VFX_GRAPH_SOURCES.v1.json').write_text(json.dumps(graph_rows, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
counts = Counter()
for pack in pack_counts.values():
    counts.update(pack)
groups = defaultdict(list)
for row in rows:
    groups[(row['root_pack_id'], row['guid'])].append(row)
root_flag_counts = Counter()
for variants in groups.values():
    for flag in variants[0]['classification_flags']:
        if any(row['classification_flags'][flag] for row in variants):
            root_flag_counts[flag] += 1
pins = dict(proof['input_pins'])
pins.update(source_pins)
for path in [Path(__file__), BASE / 'RESULT.v1.json', output, O / 'VFX_GRAPH_SOURCES.v1.json']:
    pins[str(path)] = sha(path)
result = dict(status='source_prefab_components_hashed_and_classified_for_lot_selection',
              observed_utc=datetime.now(timezone.utc).isoformat(), lot='L00.INVENTAIRE.COMPOSANTS.01',
              expected_source_prefab_occurrences=len(prefabs), examined_source_prefab_occurrences=len(rows),
              refusal_count=len(refusals), refusals=refusals, occurrence_flag_counts=dict(counts),
              root_guid_candidate_groups=len(groups), root_guid_flag_counts=dict(root_flag_counts),
              vfx_graph_source_occurrences=len(graph_rows), vfx_graph_semantics_verified=False,
              component_names=dict(all_components), packs={pack: dict(value) for pack, value in sorted(pack_counts.items())},
              nested_dependencies_resolved=False, rig_mesh_and_clips_verified=False,
              logical_asset_denominator_reconciled=False, source_scripts_executed=False,
              automatically_merged_variants=0, full_import=False, new_fully_qualified_assets=0, input_pins=pins)
(O / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
md = ['# Prefabs source : composants et lots candidats', '',
      f"{len(rows)} / {len(prefabs)} prefabs réouverts, payloads SHA-256 contrôlés ; {len(refusals)} refus.", '',
      '| Critère observé | Occurrences avec variantes | Groupes archive racine/GUID |', '|---|---:|---:|']
for flag in sorted(root_flag_counts):
    md.append(f'| {flag} | {counts[flag]} | {root_flag_counts[flag]} |')
md += ['', 'Les catégories se recouvrent : un effet peut contenir un mesh, une animation ou un script.',
       'Les références aux prefabs imbriqués, meshes et clips restent à résoudre avant conversion.',
       'Les variantes et droits sont conservés ; aucun total logique final ni validation runtime déduit de ces nombres.', '',
       '[Détails par prefab](PREFAB_COMPONENTS.v1.json) · [Preuve](RESULT.v1.json)']
(O / 'PERIMETRE_COMPOSANTS.md').write_text('\n'.join(md) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items()
                  if key not in {'input_pins', 'packs', 'refusals', 'component_names'}}, ensure_ascii=False))
