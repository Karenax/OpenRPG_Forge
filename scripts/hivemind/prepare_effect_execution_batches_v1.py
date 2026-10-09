"""Partition retained effect sources without modifying the Vault or running pack code."""
import collections
import datetime
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
INPUT = W / 'hivemind-effect-source-ir-v1/RESULT.v1.json'
ROOT = W / 'effect-execution-batches-v1'


def sha(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Existing receipt must be preserved'
    report_hash = sha(INPUT)
    report = load(INPUT)
    assert report['parsed'] == 224 and report['errors'] == 0
    pins = {str(INPUT): report_hash, str(Path(__file__).resolve()): sha(__file__)}
    groups = collections.defaultdict(list)
    module_counts = collections.Counter()
    for row in report['rows']:
        for path, expected in [(row['source'], row['source_sha256']),
                               (row['meta'], row['meta_sha256']),
                               (row['ir_path'], row['ir_sha256'])]:
            assert sha(path) == expected, path
            pins[path] = expected
        manifest_path = row['source_manifest']
        pins.setdefault(manifest_path, sha(manifest_path))
        manifest = load(manifest_path)
        assert manifest['private_only'] is True
        ir = load(row['ir_path'])
        assert ir['id'] == row['id'] and ir['source_guid'] == row['guid']
        assert ir['license'] == manifest['license']
        assert ir['archive_sha256'] == row['archive_sha256']
        assert ir['runtime_converted'] is False
        systems = []
        renderers = []
        for doc in ir['documents']:
            data = doc['data']
            if doc['type'] == 'ParticleSystem':
                modules = {key: value['enabled'] for key, value in data.items()
                           if isinstance(value, dict) and 'enabled' in value}
                assert all(type(value) in (int, bool) and value in (0, 1, False, True)
                           for value in modules.values()), 'Non-boolean module flag'
                systems.append(dict(file_id=doc['file_id'], serialized_module_flags=modules))
                module_counts.update(key for key, value in modules.items() if value)
            if doc['type'] == 'ParticleSystemRenderer':
                renderers.append(dict(file_id=doc['file_id'],
                                      source_render_mode=data.get('m_RenderMode'),
                                      source_materials=data.get('m_Materials')))
        references = collections.Counter(ref['status'] for ref in ir['references'])
        entry = dict(id=row['id'], kind=row['kind'], guid=row['guid'],
                     source=row['source'], source_sha256=row['source_sha256'],
                     meta=row['meta'], meta_sha256=row['meta_sha256'],
                     source_manifest=manifest_path, archive_sha256=row['archive_sha256'],
                     license=ir['license'], private_only=True,
                     ir_path=row['ir_path'], ir_sha256=row['ir_sha256'],
                     component_counts=row['document_types'], particle_systems=systems,
                     particle_renderers=renderers, reference_status_counts=dict(references),
                     dependency_closure_verified=False, runtime_converted=False,
                     studio_verified=False)
        groups[(manifest_path, row['kind'])].append(entry)
    batches = []
    for (manifest_path, kind), entries in sorted(groups.items()):
        entries.sort(key=lambda value: value['id'])
        limit = 5 if kind == 'unresolved_effect_prefab' else 8
        pack = Path(manifest_path).parent.name
        for offset in range(0, len(entries), limit):
            batch_id = f'L06.{pack}.{kind}.{offset // limit + 1:03}'
            batches.append(dict(batch_id=batch_id, expected=len(entries[offset:offset + limit]),
                                stage='source_inventory_verified_conversion_pending',
                                entries=entries[offset:offset + limit],
                                remaining=['resolve exact dependencies and native component semantics',
                                           'convert complete effect behavior and rendering',
                                           'compare emission, trajectories, lifetime and transparency',
                                           'create asset-specific image and metadata',
                                           'verify Studio placement, save/reopen and runtime']))
    counts = collections.Counter(entry['kind'] for batch in batches for entry in batch['entries'])
    assert counts == {'unresolved_effect_prefab': 85, 'vfx_graph_source_file': 139}
    ids = [entry['id'] for batch in batches for entry in batch['entries']]
    assert len(ids) == len(set(ids)) == 224
    for path, expected in pins.items():
        assert sha(path) == expected, 'Input changed during inspection: ' + path
    result = dict(schema='openrpg.effect-execution-batches.v1',
                  checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  status='224_retained_effect_source_entries_partitioned_without_loss',
                  scope=report['scope'], full_collection_effect_coverage_verified=False,
                  entry_counts=dict(counts), batch_count=len(batches),
                  enabled_particle_module_occurrences=dict(module_counts),
                  input_pins=pins, batches=batches, source_inventory_verified=224,
                  new_assets_converted=0, runtime_verified=False, studio_verified=False,
                  vault_modified=False, full_import=False)
    ROOT.mkdir()
    with (ROOT / 'RESULT.v1.json').open('x', encoding='utf-8') as out:
        json.dump(result, out, ensure_ascii=False, allow_nan=False, indent=2)
    lines = ['# Sous-lots des effets Hivemind', '',
             '85 prefabs et 139 fichiers de graphes sources, sans fusion des IDs.',
             'Ce périmètre exclut les effets intégrés à des prefabs d’autres catégories :',
             'le recensement complet des effets de la collection reste requis.', '',
             'Sources, .meta et IR réouverts par SHA avant/après. Aucune conversion runtime validée.', '',
             '| Sous-lot | Entrées | État |', '|---|---:|---|']
    for batch in batches:
        lines.append(f"| {batch['batch_id']} | {batch['expected']} | Sources vérifiées ; conversion et recette restantes |")
    lines += ['', 'Chaque sous-lot conserve ses IDs exacts, composants, flags de modules,',
              'modes de rendu sources, licences et hashes dans RESULT.v1.json.',
              'Les graphes sont des dépendances sources ; ils ne comptent pas comme objets livrés.',
              'Les références indexées par chemin ne prouvent pas la résolution des sous-assets.']
    with (ROOT / 'SOUS_LOTS.md').open('x', encoding='utf-8') as out:
        out.write('\n'.join(lines) + '\n')
    print(json.dumps({key: result[key] for key in ['status', 'entry_counts', 'batch_count',
                                                 'source_inventory_verified', 'new_assets_converted']}))


if __name__ == '__main__':
    main()
