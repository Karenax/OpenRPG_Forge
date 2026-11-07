"""Resolve source references conservatively; never borrow another variant silently."""
from pathlib import Path
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
BASE = W / 'collection-prefab-component-scope-v103'
O = W / 'collection-prefab-dependency-scope-v116'
PARENT = re.compile(r'^\s*m_SourcePrefab:\s*\{\s*fileID:\s*-?\d+\s*,\s*guid:\s*([0-9a-fA-F]{32})\s*,\s*type:\s*\d+\s*\}', re.M)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


assert not O.exists()
proof = read(BASE / 'RESULT.v1.json')
data_path = BASE / 'PREFAB_COMPONENTS.v1.json'
assert sha(data_path) == proof['input_pins'][str(data_path)]
prefabs = read(data_path)
assert len(prefabs) == 8736 and proof['refusal_count'] == 0
lineage_path = W / 'collection-archive-lineage-reconciliation-v1/RESULT.v1.json'
lineage = read(lineage_path)
index = defaultdict(lambda: defaultdict(list))
root_index = defaultdict(lambda: defaultdict(list))
root_for_pack = {row['pack_id']: row['root_pack_id'] for row in prefabs}
pins = {str(data_path): sha(data_path), str(BASE / 'RESULT.v1.json'): sha(BASE / 'RESULT.v1.json'),
        str(lineage_path): sha(lineage_path), str(Path(__file__)): sha(Path(__file__))}
for scope in lineage['manifests']:
    path = Path(scope['manifest'])
    assert sha(path) == scope['manifest_sha256'], str(path)
    pins[str(path)] = scope['manifest_sha256']
    for item in read(path)['files']:
        if item['kind'] != 'asset':
            continue
        record = dict(pack_id=scope['pack_id'], guid=item['guid'].lower(),
                      source_path=item['source_path'], declared_sha256=item['sha256'],
                      extension=Path(item['source_path']).suffix.lower(),
                      stored_path=str(path.parent / item['stored_path']))
        index[scope['pack_id']][record['guid']].append(record)
        root_index[root_for_pack[scope['pack_id']]][record['guid']].append(record)
by_id = {(row['pack_id'], row['guid'].lower()): row for row in prefabs}
assert len(by_id) == len(prefabs)
edges = defaultdict(list)
rows = []
total = Counter()
refusals = []
for prefab in prefabs:
    key = (prefab['pack_id'], prefab['guid'].lower())
    known = Counter()
    unresolved = []
    for guid in prefab['referenced_guids']:
        records = index[prefab['pack_id']].get(guid, [])
        if len(records) == 1:
            known[records[0]['extension']] += 1
            total['known_same_pack_reference_edges'] += 1
        else:
            alternatives = root_index[prefab['root_pack_id']].get(guid, [])
            unresolved.append(dict(guid=guid,
                reason='ambiguous_same_pack_guid' if len(records) > 1 else 'not_declared_in_current_pack_manifest',
                other_variant_candidates=alternatives, borrowed_other_variant=False))
            total['unresolved_reference_edges'] += 1
    parents = []
    if prefab['classification_flags']['nested_prefab']:
        path = Path(prefab['stored_path'])
        raw = path.read_bytes()
        assert hashlib.sha256(raw).hexdigest() == prefab['declared_sha256'], str(path)
        pins[str(path)] = prefab['declared_sha256']
        parent_guids = sorted(set(g.lower() for g in PARENT.findall(raw.decode('utf-8-sig'))))
        if not parent_guids:
            refusals.append(dict(pack_id=key[0], guid=key[1], reason='PrefabInstance lacks explicit source prefab GUID'))
        for guid in parent_guids:
            parent = by_id.get((key[0], guid))
            if parent is not None:
                edges[key].append((key[0], guid))
                parents.append(dict(guid=guid, state='same_pack_prefab_resolved',
                                    source_path=parent['source_path'], sha256=parent['declared_sha256']))
                total['resolved_nested_prefab_edges'] += 1
            elif len(index[key[0]].get(guid, [])) == 1 and index[key[0]][guid][0]['extension'] in {'.fbx', '.obj', '.blend'}:
                # Unity Model Prefab instances reference their model asset GUID.
                # Resolve the source container without pretending to resolve its hierarchy.
                model = index[key[0]][guid][0]
                model_path = Path(model['stored_path'])
                assert sha(model_path) == model['declared_sha256'], str(model_path)
                pins[str(model_path)] = model['declared_sha256']
                parents.append(dict(guid=guid, state='same_pack_model_prefab_container_resolved',
                                    source_path=model['source_path'], sha256=model['declared_sha256'],
                                    source_payload_rehashed=True, imported_model_hierarchy_verified=False,
                                    imported_file_ids_verified=False, rig_and_clips_verified=False))
                total['resolved_model_prefab_source_edges'] += 1
            else:
                parents.append(dict(guid=guid, state='source_prefab_not_resolved_in_same_pack',
                                    other_variant_candidates=root_index[prefab['root_pack_id']].get(guid, [])))
                total['unresolved_nested_prefab_edges'] += 1
                refusals.append(dict(pack_id=key[0], guid=key[1], source_prefab_guid=guid,
                                     reason='Nested prefab source absent from same-pack inventory'))
    rows.append(dict(pack_id=key[0], guid=key[1], root_pack_id=prefab['root_pack_id'],
                     source_path=prefab['source_path'], known_reference_types=dict(known),
                     unresolved_references=unresolved, source_prefab_dependencies=parents,
                     actual_unity_asset_database_resolution_executed=False,
                     conversion_verified=False, runtime_qualified=False))

cycles = []
visiting = set()
visited = set()


def walk(key, trail):
    if key in visiting:
        cycles.append([list(entry) for entry in trail[trail.index(key):] + [key]])
        return
    if key in visited:
        return
    visiting.add(key)
    for child in edges[key]:
        walk(child, trail + [key])
    visiting.remove(key)
    visited.add(key)


for key in by_id:
    walk(key, [])

for row in rows:
    key = (row['pack_id'], row['guid'])
    reachable = set()
    pending = list(edges[key])
    while pending:
        child = pending.pop()
        if child in reachable:
            continue
        reachable.add(child)
        pending.extend(edges[child])
    inherited = [by_id[child] for child in reachable if child != key]
    flags = {flag: by_id[key]['classification_flags'][flag] or any(
        item['classification_flags'][flag] for item in inherited)
        for flag in by_id[key]['classification_flags']}
    row.update(transitive_same_pack_prefab_dependencies=len(inherited),
               flags_including_resolved_nested_prefabs=flags,
               external_or_other_variant_dependencies_still_unresolved=bool(row['unresolved_references']))

O.mkdir()
output = O / 'PREFAB_DEPENDENCIES.v1.json'
output.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
pins[str(output)] = sha(output)
effect_count = sum(row['flags_including_resolved_nested_prefabs']['effect_candidate'] for row in rows)
result = dict(status='same_pack_prefab_dependencies_resolved_with_external_refusals_retained',
              observed_utc=datetime.now(timezone.utc).isoformat(), lot='L00.INVENTAIRE.DEPENDANCES.01',
              examined_prefab_occurrences=len(rows), manifest_count=48,
              nested_source_payloads_rehashed=sum(row['classification_flags']['nested_prefab'] for row in prefabs),
              dependency_counts=dict(total), nested_refusal_count=len(refusals), nested_refusals=refusals,
              prefab_dependency_cycles=cycles, effect_candidates_including_known_nested_prefabs=effect_count,
              source_guid_references_are_not_logical_asset_count=True,
              unity_asset_database_resolution_executed=False,
              unknown_reference_payloads_not_assumed_missing_or_builtin=True,
              model_prefab_containers_are_not_missing_prefab_assets=True,
              imported_model_hierarchy_and_file_ids_verified=False,
              previous_false_missing_container_refusals_preserved=str(W / 'collection-prefab-dependency-scope-v115/RESULT.v1.json'),
              other_variants_borrowed=0, script_execution=False,
              logical_asset_denominator_reconciled=False, full_import=False,
              new_fully_qualified_assets=0, input_pins=pins)
(O / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
md = ['# Dépendances des prefabs source', '',
      f"{len(rows)} occurrences examinées ; 48 manifestes recontrôlés ; 128 prefabs imbriqués réouverts par SHA.", '',
      f"Parents imbriqués résolus dans le même pack : {total['resolved_nested_prefab_edges']} ; non résolus : {total['unresolved_nested_prefab_edges']}.",
      f"Refus imbriqués : {len(refusals)} ; cycles détectés : {len(cycles)}.", '',
      f"Références de ressources connues dans le même pack : {total['known_same_pack_reference_edges']} ; non résolues : {total['unresolved_reference_edges']}.",
      'Une référence non résolue peut viser un package Unity ou une autre variante. Sa charge utile n’est pas déclarée manquante sans résolution Unity.',
      'Aucune ressource d’une variante voisine n’est substituée automatiquement.',
      f"Conteneurs Model Prefab résolus par source modèle et SHA dans le même pack : {total['resolved_model_prefab_source_edges']}.",
      'Leur hiérarchie importée, fileIDs, rigs et clips restent à vérifier ; les neuf faux refus v115 sont conservés.', '',
      f"Candidats effets après propagation des composants des prefabs imbriqués résolus : {effect_count} occurrences.",
      'Ce classement ne vérifie ni la conversion ni les comportements de scripts, rigs et VFX.', '',
      '[Dépendances et refus par prefab](PREFAB_DEPENDENCIES.v1.json) · [Preuve](RESULT.v1.json)']
(O / 'DEPENDANCES_ET_REFUS.md').write_text('\n'.join(md) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items()
                  if key not in {'input_pins', 'nested_refusals', 'prefab_dependency_cycles'}}, ensure_ascii=False))
